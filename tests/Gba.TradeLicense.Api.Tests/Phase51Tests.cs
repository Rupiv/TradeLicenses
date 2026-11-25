using System.Net;
using Gba.TradeLicense.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Gba.TradeLicense.Api.Tests;

public class RecaptchaVerifierTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public HttpRequestMessage? Last;
        public string? LastBody;
        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Last = request;
            LastBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
            return _respond(request);
        }
    }

    private sealed class Factory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _h;
        public Factory(HttpMessageHandler h) => _h = h;
        public HttpClient CreateClient(string name) => new(_h, disposeHandler: false);
    }

    private sealed class Env : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static (RecaptchaVerifier Verifier, FakeHandler Handler) Create(string json, string env = "Production",
        string? secret = "server-secret", bool bypass = false, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(status) { Content = new StringContent(json) });
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReCaptcha:SecretKey"] = secret,
            ["ReCaptcha:BypassValidation"] = bypass ? "true" : "false"
        }).Build();
        return (new RecaptchaVerifier(new Factory(handler), cfg, new Env { EnvironmentName = env },
            NullLogger<RecaptchaVerifier>.Instance), handler);
    }

    [Fact]
    public async Task ValidToken_IsAccepted_AndSecretGoesInThePostBody()
    {
        var (v, h) = Create("{\"success\":true,\"hostname\":\"gbatrade.karnataka.gov.in\"}");
        var r = await v.VerifyAsync("token-from-widget", "1.2.3.4");
        Assert.True(r.IsValid);
        Assert.Equal(HttpMethod.Post, h.Last!.Method);
        Assert.DoesNotContain("server-secret", h.Last.RequestUri!.ToString());
        Assert.Contains("secret=server-secret", h.LastBody);
        Assert.Contains("response=token-from-widget", h.LastBody);
    }

    [Fact]
    public async Task LiteralTestToken_IsNotAcceptedInProduction()
    {
        var (v, _) = Create("{\"success\":false,\"error-codes\":[\"invalid-input-response\"]}");
        var r = await v.VerifyAsync("test", null);
        Assert.False(r.IsValid);
        Assert.Equal(CaptchaOutcome.Invalid, r.Outcome);
    }

    [Fact]
    public async Task BypassFlag_IsIgnoredOutsideDevelopment()
    {
        var (v, h) = Create("{\"success\":false}", env: "Production", bypass: true);
        Assert.False((await v.VerifyAsync("anything", null)).IsValid);
        Assert.NotNull(h.Last); // provider was asked
    }

    [Fact]
    public async Task BypassFlag_WorksInDevelopmentOnly()
    {
        var (v, h) = Create("{\"success\":false}", env: "Development", bypass: true);
        Assert.True((await v.VerifyAsync(null, null)).IsValid);
        Assert.Null(h.Last);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MissingToken_IsRejectedWithoutCallingTheProvider(string? token)
    {
        var (v, h) = Create("{\"success\":true}");
        var r = await v.VerifyAsync(token, null);
        Assert.Equal(CaptchaOutcome.Missing, r.Outcome);
        Assert.Null(h.Last);
    }

    [Fact]
    public async Task ExpiredOrReusedToken_IsReportedAsExpired()
    {
        var (v, _) = Create("{\"success\":false,\"error-codes\":[\"timeout-or-duplicate\"]}");
        var r = await v.VerifyAsync("old", null);
        Assert.Equal(CaptchaOutcome.Expired, r.Outcome);
        Assert.Contains("expired", r.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingSecret_FailsClosed()
    {
        var (v, h) = Create("{\"success\":true}", secret: null);
        var r = await v.VerifyAsync("token", null);
        Assert.Equal(CaptchaOutcome.Unavailable, r.Outcome);
        Assert.Null(h.Last);
    }

    [Fact]
    public async Task ProviderError_FailsClosed()
    {
        var (v, _) = Create("oops", status: HttpStatusCode.InternalServerError);
        Assert.Equal(CaptchaOutcome.Unavailable, (await v.VerifyAsync("token", null)).Outcome);
    }

    [Fact]
    public void WrongSecret_IsUnavailable_NotBlamedOnTheUser()
    {
        Assert.Equal(CaptchaOutcome.Unavailable,
            RecaptchaVerifier.Interpret("{\"success\":false,\"error-codes\":[\"invalid-input-secret\"]}", null).Outcome);
    }

    [Fact]
    public void Hostname_IsCheckedWhenConfigured()
    {
        const string ok = "{\"success\":true,\"hostname\":\"gbatrade.karnataka.gov.in\"}";
        const string other = "{\"success\":true,\"hostname\":\"evil.example\"}";
        Assert.True(RecaptchaVerifier.Interpret(ok, "gbatrade.karnataka.gov.in").IsValid);
        Assert.False(RecaptchaVerifier.Interpret(other, "gbatrade.karnataka.gov.in").IsValid);
        Assert.True(RecaptchaVerifier.Interpret(other, null).IsValid);
    }

    [Fact]
    public void Messages_NeverMentionConfigurationOrProvider()
    {
        foreach (CaptchaOutcome o in Enum.GetValues(typeof(CaptchaOutcome)))
        {
            var m = new CaptchaResult(o).Message;
            Assert.DoesNotContain("secret", m, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("google", m, StringComparison.OrdinalIgnoreCase);
        }
    }
}

public class SafePathTests
{
    [Theory]
    [InlineData(@"C:\inetpub\vhosts\pickitover.com\httpdocs\api\Uploads\LicenceDocuments\2026\10\13\a.pdf", "/Uploads/LicenceDocuments/2026/10/13/a.pdf")]
    [InlineData(@"D:/GBA/TradeLicenses/src/Gba.TradeLicense.Api/Uploads/LicenceDocuments/x.png", "/Uploads/LicenceDocuments/x.png")]
    [InlineData("/uploads/licence-certificates/1/a.pdf", "/uploads/licence-certificates/1/a.pdf")]
    [InlineData(@"E:\secret\folder\a.pdf", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ToPublic_NeverReturnsDriveLettersOrServerFolders(string? stored, string? expected)
    {
        var p = SafePaths.ToPublic(stored);
        Assert.Equal(expected, p);
        if (p != null)
        {
            Assert.DoesNotContain(":", p);
            Assert.DoesNotContain("\\", p);
            Assert.DoesNotContain("inetpub", p);
        }
    }

    [Fact]
    public void SanitizeRows_RewritesFilePathOfDapperStyleRows()
    {
        IDictionary<string, object> row = new Dictionary<string, object>
        {
            ["FilePath"] = @"C:\inetpub\api\Uploads\LicenceDocuments\a.pdf",
            ["FileName"] = "a.pdf"
        };
        SafePaths.SanitizeRows(new object?[] { row });
        Assert.Equal("/Uploads/LicenceDocuments/a.pdf", row["FilePath"]);
        Assert.Equal("a.pdf", row["FileName"]);
    }
}

public class LogScrubTests
{
    [Fact]
    public void Scrub_RemovesJwtConnectionPasswordKeysAndOtp()
    {
        var text = "token eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.abcdefghijk; Server=x;Password=Pa55word;" +
                   " -----BEGIN PRIVATE KEY-----\nMIIabc\n-----END PRIVATE KEY----- OTP IS 123456 at 10:00";
        var s = SensitiveDataEnricher.Scrub(text);
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiJ9", s);
        Assert.DoesNotContain("Pa55word", s);
        Assert.DoesNotContain("MIIabc", s);
        Assert.DoesNotContain("123456", s);
        Assert.Contains("10:00", s); // ordinary text stays readable
    }

    [Fact]
    public void Enricher_MasksPropertiesWithSensitiveNames()
    {
        var evt = new Serilog.Events.LogEvent(DateTimeOffset.Now, Serilog.Events.LogEventLevel.Information, null,
            new Serilog.Parsing.MessageTemplateParser().Parse("x"),
            new[]
            {
                new Serilog.Events.LogEventProperty("Password", new Serilog.Events.ScalarValue("p")),
                new Serilog.Events.LogEventProperty("OtpToken", new Serilog.Events.ScalarValue("abc")),
                new Serilog.Events.LogEventProperty("TxnId", new Serilog.Events.ScalarValue("GBA-TL-1"))
            });
        new SensitiveDataEnricher().Enrich(evt, null!);
        Assert.Equal("\"***\"", evt.Properties["Password"].ToString());
        Assert.Equal("\"***\"", evt.Properties["OtpToken"].ToString());
        Assert.Equal("\"GBA-TL-1\"", evt.Properties["TxnId"].ToString());
    }
}
