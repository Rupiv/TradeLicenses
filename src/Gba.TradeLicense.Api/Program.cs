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
    app.UseExceptionHandler("/error");
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
// SWAGGER (🔥 ENABLE IN ALL ENV)
// --------------------------------------------------
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    // Relative path: works locally (/swagger) and on IIS under the /api application (/api/swagger)
    c.SwaggerEndpoint("v1/swagger.json", "GBA Trade License API V1");
    c.RoutePrefix = "swagger";
});

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
app.Use(async (context, next) =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    await next();
    sw.Stop();

    Console.WriteLine($"{context.Request.Method} {context.Request.Path} - {sw.ElapsedMilliseconds} ms");
});

// --------------------------------------------------
// ENDPOINTS
// --------------------------------------------------
app.MapControllers();

app.Run();