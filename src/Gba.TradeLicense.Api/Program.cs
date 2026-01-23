using System;
using System.Net.Http.Headers;
using System.Text;
using Gba.TradeLicense.Application.Abstractions;
using Gba.TradeLicense.Infrastructure.Persistence;
using Gba.TradeLicense.Infrastructure.Security;
using Gba.TradeLicense.Infrastructure.Services;
using Gba.TradeLicense.Infrastructure.Sms;
using Gba.TradeLicense.Infrastructure.Sms.esms_client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// --------------------------------------------------
// CORS (SINGLE POLICY)
// --------------------------------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// --------------------------------------------------
// SERVICES
// --------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default"));
});

// HttpClient (KGIS)
builder.Services.AddHttpClient("KgisClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
});

// Application Services
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ITradeApplicationService, TradeApplicationService>();
builder.Services.AddSingleton<KarnatakaSmsService>();
builder.Services.AddScoped<ISmsService, SmsService>();
builder.Services.AddScoped<SMSHttpPostClient>();

// --------------------------------------------------
// JWT AUTHENTICATION
// --------------------------------------------------
var jwtSection = builder.Configuration.GetSection("Jwt");
var issuer = jwtSection["Issuer"];
var audience = jwtSection["Audience"];
var key = jwtSection["Key"]
          ?? throw new InvalidOperationException("Jwt:Key is missing");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,

            ValidateAudience = true,
            ValidAudience = audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(key)
            ),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

// Authorization Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TraderOnly", p => p.RequireRole("Trader"));
    options.AddPolicy("ApproverOnly", p => p.RequireRole("Approver"));
    options.AddPolicy("SeniorApproverOnly", p => p.RequireRole("SeniorApprover"));
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.AddPolicy("ApproverOrSenior", p =>
        p.RequireRole("Approver", "SeniorApprover"));
});

// --------------------------------------------------
// SWAGGER
// --------------------------------------------------
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "GBA Trade License API",
        Version = "v1"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter token as: Bearer {your JWT token}"
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
            Array.Empty<string>()
        }
    });
});

// --------------------------------------------------
// APP PIPELINE
// --------------------------------------------------
var app = builder.Build();

// Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "GBA Trade License API v1");
    c.DisplayRequestDuration();
});

// HTTPS (ONLY ONCE)
app.UseHttpsRedirection();

// Routing
app.UseRouting();

// CORS (MUST be before auth)
app.UseCors("AngularPolicy");

// Auth
app.UseAuthentication();
app.UseAuthorization();

// Audit / performance logging
app.Use(async (context, next) =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    await next();
    sw.Stop();

    Console.WriteLine(
        $"{context.Request.Method} {context.Request.Path} - {sw.ElapsedMilliseconds} ms"
    );
});

// Controllers
app.MapControllers();

app.Run();
