using System.Text.Json;

namespace Gba.TradeLicense.Api.Services
{
    public enum CaptchaOutcome { Valid, Missing, Expired, Invalid, Unavailable }

    public sealed record CaptchaResult(CaptchaOutcome Outcome)
    {
        public bool IsValid => Outcome == CaptchaOutcome.Valid;

        /// <summary>Message for the user (never contains provider or configuration details).</summary>
        public string Message => Outcome switch
        {
            CaptchaOutcome.Valid => string.Empty,
            CaptchaOutcome.Missing => "Please complete the CAPTCHA check.",
            CaptchaOutcome.Expired => "The CAPTCHA check expired. Please try again.",
            CaptchaOutcome.Invalid => "CAPTCHA verification failed. Please try again.",
            _ => "CAPTCHA could not be verified right now. Please try again in a moment."
        };
    }

    /// <summary>
    /// Server-side Google reCAPTCHA check (siteverify). Fails closed: a missing secret, an
    /// unreachable provider or an unreadable answer is a failed check. The only bypass is
    /// ReCaptcha:BypassValidation = true, and it is honoured in the Development environment only.
    /// The secret comes from configuration (environment) and is sent in the POST body.
    /// </summary>
    public sealed class RecaptchaVerifier
    {
        public const string HttpClientName = "Recaptcha";
        private const string VerifyUrl = "https://www.google.com/recaptcha/api/siteverify";

        private readonly IHttpClientFactory _http;
        private readonly IConfiguration _config;
        private readonly IHostEnvironment _env;
        private readonly ILogger<RecaptchaVerifier> _logger;

        public RecaptchaVerifier(IHttpClientFactory http, IConfiguration config, IHostEnvironment env,
            ILogger<RecaptchaVerifier> logger)
        {
            _http = http; _config = config; _env = env; _logger = logger;
        }

        public async Task<CaptchaResult> VerifyAsync(string? token, string? remoteIp, CancellationToken ct = default)
        {
            if (_env.IsDevelopment() && _config.GetValue<bool>("ReCaptcha:BypassValidation"))
                return new CaptchaResult(CaptchaOutcome.Valid);

            if (string.IsNullOrWhiteSpace(token))
                return new CaptchaResult(CaptchaOutcome.Missing);

            var secret = _config["ReCaptcha:SecretKey"];
            if (string.IsNullOrWhiteSpace(secret))
            {
                _logger.LogError("ReCaptcha:SecretKey is not configured; CAPTCHA checks fail until it is set");
                return new CaptchaResult(CaptchaOutcome.Unavailable);
            }

            var form = new Dictionary<string, string> { ["secret"] = secret, ["response"] = token.Trim() };
            if (!string.IsNullOrWhiteSpace(remoteIp)) form["remoteip"] = remoteIp;

            string body;
            try
            {
                var client = _http.CreateClient(HttpClientName);
                using var response = await client.PostAsync(VerifyUrl, new FormUrlEncodedContent(form), ct);
                body = await response.Content.ReadAsStringAsync(ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("reCAPTCHA provider answered HTTP {Status}", (int)response.StatusCode);
                    return new CaptchaResult(CaptchaOutcome.Unavailable);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning("reCAPTCHA provider unreachable: {Error}", ex.GetType().Name);
                return new CaptchaResult(CaptchaOutcome.Unavailable);
            }

            return Interpret(body, _config["ReCaptcha:AllowedHostnames"], _logger);
        }

        /// <summary>Reads a siteverify answer. Public for unit tests.</summary>
        public static CaptchaResult Interpret(string body, string? allowedHostnames, ILogger? logger = null)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                var success = root.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True;

                var errors = root.TryGetProperty("error-codes", out var e) && e.ValueKind == JsonValueKind.Array
                    ? e.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList()
                    : new List<string>();

                if (!success)
                {
                    if (errors.Contains("timeout-or-duplicate")) return new CaptchaResult(CaptchaOutcome.Expired);
                    if (errors.Contains("missing-input-response")) return new CaptchaResult(CaptchaOutcome.Missing);
                    if (errors.Contains("missing-input-secret") || errors.Contains("invalid-input-secret"))
                    {
                        logger?.LogError("reCAPTCHA rejected the configured secret ({Codes})", string.Join(",", errors));
                        return new CaptchaResult(CaptchaOutcome.Unavailable);
                    }
                    return new CaptchaResult(CaptchaOutcome.Invalid);
                }

                // Optional: token must have been issued for one of our host names
                if (!string.IsNullOrWhiteSpace(allowedHostnames))
                {
                    var allowed = allowedHostnames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var host = root.TryGetProperty("hostname", out var h) ? h.GetString() : null;
                    if (host == null || !allowed.Contains(host, StringComparer.OrdinalIgnoreCase))
                        return new CaptchaResult(CaptchaOutcome.Invalid);
                }

                return new CaptchaResult(CaptchaOutcome.Valid);
            }
            catch (JsonException)
            {
                return new CaptchaResult(CaptchaOutcome.Unavailable);
            }
        }
    }
}
