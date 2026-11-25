using Gba.TradeLicense.Application.Abstractions;
using Gba.TradeLicense.Infrastructure.Persistence;
using Gba.TradeLicense.Infrastructure.Security;
using Gba.TradeLicense.Infrastructure.Services;
using Gba.TradeLicense.Infrastructure.Sms;
using Gba.TradeLicense.Infrastructure.Sms.esms_client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.RateLimiting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// --------------------------------------------------
// SECRETS
// --------------------------------------------------
// Secrets are NOT kept in appsettings.json (it is in source control). Supply them per environment:
//   - IIS / server: environment variables, e.g. ConnectionStrings__Default, Jwt__Key, Otp__Secret,
//     ReCaptcha__SecretKey, Sms__Password, Sms__SecureKey, PaymentRedirect__PrivateKeyPath ...
//     (web.config <environmentVariables> on the server only, or the machine / app-pool environment)
//   - local development: `dotnet user-secrets set "Jwt:Key" "..."` (UserSecretsId in the .csproj)
// The app refuses to start without the ones it cannot work without; values are never logged.
static void RequireSetting(IConfiguration cfg, string key, int minLength = 1)
{
    var value = cfg[key];
    if (string.IsNullOrWhiteSpace(value) || value.Length < minLength)
        throw new InvalidOperationException(
            $"Configuration '{key}' is missing{(minLength > 1 ? $" or shorter than {minLength} characters" : "")}. " +
            "Set it as an environment variable (use '__' for ':') or a user secret - see Program.cs.");
}
RequireSetting(builder.Configuration, "ConnectionStrings:Default");
RequireSetting(builder.Configuration, "Jwt:Key", 32);
RequireSetting(builder.Configuration, "Jwt:Issuer");
RequireSetting(builder.Configuration, "Jwt:Audience");

// --------------------------------------------------
// LOGGING (structured JSON files + console; sensitive values scrubbed)
// --------------------------------------------------
// Files: Logging:File:Path (default <app>\logs\api-<date>.json), one file per day, 30 days kept,
// max 50 MB per file. Request bodies, headers and query strings are never logged; framework request
// logging is reduced to warnings (its "Request starting" lines contain full URLs with query strings).
builder.Host.UseSerilog((ctx, services, cfg) =>
{
    var path = ctx.Configuration["Logging:File:Path"];
    if (string.IsNullOrWhiteSpace(path))
        path = Path.Combine(ctx.HostingEnvironment.ContentRootPath, "logs", "api-.json");

    cfg.MinimumLevel.Information()
       .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
       .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
       .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
       .Enrich.FromLogContext()
       .Enrich.With(new Gba.TradeLicense.Api.Services.SensitiveDataEnricher())
       .WriteTo.Console()
       .WriteTo.File(new CompactJsonFormatter(), path,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: ctx.Configuration.GetValue<int?>("Logging:File:RetainedDays") ?? 30,
            fileSizeLimitBytes: 50L * 1024 * 1024,
            rollOnFileSizeLimit: true,
            shared: true);
});

// --------------------------------------------------
// CORS
// --------------------------------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.WithOrigins(
                "http://localhost:4200",
                "https://pickitover.com"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// --------------------------------------------------
// CONTROLLERS (🔥 GLOBAL AUTH ENABLED)
// --------------------------------------------------
builder.Services.AddControllers(options =>
{
    options.Filters.Add(new AuthorizeFilter()); // 🔥 ALL APIs PROTECTED
});

builder.Services.AddEndpointsApiExplorer();

// --------------------------------------------------
// DB
// --------------------------------------------------
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default"));
});

// --------------------------------------------------
// HTTP CLIENT
// --------------------------------------------------
builder.Services.AddHttpClient("KgisClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
});

// --------------------------------------------------
// SERVICES
// --------------------------------------------------
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ITradeApplicationService, TradeApplicationService>();
builder.Services.AddSingleton<KarnatakaSmsService>();
builder.Services.AddScoped<ISmsService, SmsService>();
builder.Services.AddScoped<Gba.TradeLicense.Api.Services.ApplicationSmsNotifier>();
builder.Services.AddScoped<Gba.TradeLicense.Api.Services.GbaLocationService>();
builder.Services.AddScoped<Gba.TradeLicense.Api.Services.GbaQueueService>();
builder.Services.AddScoped<SMSHttpPostClient>();
builder.Services.AddSingleton<BbmpBoundaryService>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<Gba.TradeLicense.Api.Services.OtpGuard>();
builder.Services.AddHttpClient(Gba.TradeLicense.Api.Services.RecaptchaVerifier.HttpClientName,
    c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddScoped<Gba.TradeLicense.Api.Services.RecaptchaVerifier>();

// Health: /health/live (process up) and /health/ready (database reachable). Status text only.
builder.Services.AddHealthChecks()
    .AddCheck<Gba.TradeLicense.Api.Services.DatabaseHealthCheck>("database", tags: new[] { "ready" });

// --------------------------------------------------
// RATE LIMITING (anonymous login / OTP / password-reset endpoints)
// --------------------------------------------------
// Per client IP, on top of the per-mobile-number limits in OtpGuard. Generous on purpose:
// several traders can share one public IP (offices, mobile carriers).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsync(
            "{\"message\":\"Too many requests. Please try again after some time.\"}", ct);
    };
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        var path = ctx.Request.Path.Value ?? string.Empty;
        var isAuth = path.Contains("/api/auth/", StringComparison.OrdinalIgnoreCase)
                     || path.Contains("/api/sms/otp/", StringComparison.OrdinalIgnoreCase);
        if (!isAuth)
            return RateLimitPartition.GetNoLimiter("other");

        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter("auth:" + ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
});

// --------------------------------------------------
// JWT AUTHENTICATION
// --------------------------------------------------
var jwtSection = builder.Configuration.GetSection("Jwt");
var key = jwtSection["Key"] ?? throw new Exception("JWT Key missing");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = true;
    options.SaveToken = true;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtSection["Issuer"],

        ValidateAudience = true,
        ValidAudience = jwtSection["Audience"],

        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(key)),

        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        RequireSignedTokens = true,
        RequireExpirationTime = true
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
            {
                context.Response.Headers.Add("Token-Expired", "true");
            }
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            context.HandleResponse();

            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";

            return context.Response.WriteAsync(
                "{\"error\":\"Unauthorized or Session expired\"}");
        }
    };
});

// --------------------------------------------------
// AUTHORIZATION
// --------------------------------------------------
builder.Services.AddAuthorization();

// --------------------------------------------------
// 🔐 SWAGGER WITH JWT SUPPORT (FIXED)
// --------------------------------------------------
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "GBA Trade License API",
        Version = "v1"
    });

    // 🔐 JWT AUTH IN SWAGGER
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {your token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// --------------------------------------------------
// BUILD
// --------------------------------------------------
var app = builder.Build();

app.UseMiddleware<Gba.TradeLicense.Api.Services.CorrelationIdMiddleware>();

// One line per request (also for requests rejected by authentication / rate limiting):
// method, path WITHOUT query string, status, duration, correlation id
app.UseSerilogRequestLogging(o =>
{
    o.MessageTemplate = "HTTP {RequestMethod} {RequestPath} -> {StatusCode} in {Elapsed:0} ms";
});

// Payment-redirect key pair (see CryptoHelper): source is logged, key material never is.
var paymentKeySource = CryptoHelper.Configure(
    app.Configuration["PaymentRedirect:PublicKeyPem"], app.Configuration["PaymentRedirect:PrivateKeyPem"],
    app.Configuration["PaymentRedirect:PublicKeyPath"], app.Configuration["PaymentRedirect:PrivateKeyPath"]);
app.Logger.LogInformation("Payment redirect keys: {Source}", paymentKeySource);

// --------------------------------------------------
// GLOBAL ERROR HANDLING
// --------------------------------------------------
// Show the actual exception while debugging locally. The production
// exception handler remains intentionally generic so no internal details
// are exposed to callers.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    // No exception details to callers; the exception is logged by the middleware with the
    // correlation id the caller receives.
    app.UseExceptionHandler(errorApp => errorApp.Run(async ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new
        {
            message = "Something went wrong. Please try again.",
            correlationId = ctx.TraceIdentifier
        });
    }));
}

// --------------------------------------------------
// SECURITY HEADERS
// --------------------------------------------------
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;

        headers["X-XSS-Protection"] = "1; mode=block";
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "SAMEORIGIN";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";

        headers["Content-Security-Policy"] =
            "default-src 'self' https: data: blob:;" +
            "script-src 'self' 'unsafe-inline' 'unsafe-eval' https:;" +
            "style-src 'self' 'unsafe-inline' https:;" +
            "img-src 'self' data: https:;" +
            "connect-src 'self' https:;" +
            "font-src 'self' https: data:;" +
            "frame-ancestors 'self';";

        headers["Permissions-Policy"] =
            "geolocation=(), microphone=(), camera=()";

        // No caching of API responses (same as live)
        headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
        headers["Pragma"] = "no-cache";
        headers["Expires"] = "0";
        headers["Surrogate-Control"] = "no-store";

        return Task.CompletedTask;
    });

    await next();
});

// --------------------------------------------------
// HTTPS
// --------------------------------------------------
app.UseHsts();
app.UseHttpsRedirection();

// --------------------------------------------------
// SWAGGER: development only (or Swagger:Enabled = true on a non-public test server)
// --------------------------------------------------
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        // Relative path: works locally (/swagger) and on IIS under the /api application (/api/swagger)
        c.SwaggerEndpoint("v1/swagger.json", "GBA Trade License API V1");
        c.RoutePrefix = "swagger";
    });
}

// --------------------------------------------------
// PIPELINE
// --------------------------------------------------
// The client data-centre firewall blocks every PUT / DELETE ("Unauthorized Activity Has Been
// Detected" HTML page). The frontend therefore sends them as POST with the header
// X-HTTP-Method-Override: PUT / DELETE; this restores the real method before routing, so the
// same endpoints and the same [Authorize] checks apply. Direct PUT / DELETE keep working too.
app.UseHttpMethodOverride();

app.UseRouting();
app.UseCors("CorsPolicy");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// --------------------------------------------------
// LOGGING
// --------------------------------------------------

// --------------------------------------------------
// ENDPOINTS
// --------------------------------------------------
app.MapControllers();

// Anonymous health endpoints (outside MVC, so the global [Authorize] filter does not apply).
// Only "Healthy" / "Unhealthy" is returned - no versions, paths, server names or exception text.
static Task WriteHealthStatus(HttpContext ctx, Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport report)
{
    ctx.Response.ContentType = "text/plain";
    return ctx.Response.WriteAsync(report.Status.ToString());
}
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false, // process only
    ResponseWriter = WriteHealthStatus
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = c => c.Tags.Contains("ready"),
    ResponseWriter = WriteHealthStatus
});

app.Run();