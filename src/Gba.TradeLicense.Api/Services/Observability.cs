using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;

namespace Gba.TradeLicense.Api.Services
{
    /// <summary>
    /// One id per request (X-Correlation-ID from the caller if well-formed, else a new one), returned
    /// in the response header and attached to every log line written while the request runs.
    /// </summary>
    public sealed class CorrelationIdMiddleware
    {
        public const string Header = "X-Correlation-ID";
        private static readonly Regex Valid = new("^[A-Za-z0-9-]{8,64}$", RegexOptions.Compiled);
        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            var incoming = context.Request.Headers[Header].ToString();
            var id = Valid.IsMatch(incoming) ? incoming : Guid.NewGuid().ToString("N");
            context.TraceIdentifier = id;
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[Header] = id;
                return Task.CompletedTask;
            });

            using (LogContext.PushProperty("CorrelationId", id))
            {
                await _next(context);
            }
        }
    }

    /// <summary>
    /// Last line of defence for the log files: properties whose NAME suggests a secret are replaced,
    /// and text values are scrubbed of JWTs, connection-string passwords, PEM keys and OTP texts.
    /// (The code itself does not log request bodies, headers or query strings.)
    /// </summary>
    public sealed class SensitiveDataEnricher : ILogEventEnricher
    {
        private static readonly Regex SensitiveName = new(
            "(password|passwd|pwd|secret|salt|token|otp|hash|authorization|connectionstring|privatekey|apikey|cookie)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly (Regex Pattern, string Replacement)[] TextRules =
        {
            (new Regex(@"eyJ[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]{5,}", RegexOptions.Compiled), "***JWT***"),
            (new Regex(@"(?i)(password|pwd)\s*=\s*[^;""\s]+", RegexOptions.Compiled), "$1=***"),
            (new Regex(@"-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]*?-----END [A-Z ]*PRIVATE KEY-----", RegexOptions.Compiled), "***PRIVATE KEY***"),
            (new Regex(@"(?i)\bOTP\b(\s*(is|:)?\s*)\d{4,8}", RegexOptions.Compiled), "OTP$1******"),
        };

        public static string Scrub(string text)
        {
            foreach (var (pattern, replacement) in TextRules)
                text = pattern.Replace(text, replacement);
            return text;
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            foreach (var (name, value) in logEvent.Properties.ToList())
            {
                if (SensitiveName.IsMatch(name))
                    logEvent.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue("***")));
                else if (value is ScalarValue { Value: string s })
                {
                    var scrubbed = Scrub(s);
                    if (!ReferenceEquals(scrubbed, s) && scrubbed != s)
                        logEvent.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(scrubbed)));
                }
            }
        }
    }

    /// <summary>Readiness: can the API open a connection and run SELECT 1? No details are returned.</summary>
    public sealed class DatabaseHealthCheck : IHealthCheck
    {
        private readonly string? _connectionString;
        public DatabaseHealthCheck(IConfiguration config) => _connectionString = config.GetConnectionString("Default");

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(5));
                await using var con = new SqlConnection(_connectionString);
                await con.OpenAsync(cts.Token);
                await using var cmd = new SqlCommand("SELECT 1", con);
                await cmd.ExecuteScalarAsync(cts.Token);
                return HealthCheckResult.Healthy();
            }
            catch (Exception)
            {
                // the reason goes to the log via the health check publisher, never to the caller
                return HealthCheckResult.Unhealthy("database unavailable");
            }
        }
    }
}
