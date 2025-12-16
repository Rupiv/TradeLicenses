using Gba.TradeLicense.Application.Abstractions;
using Gba.TradeLicense.Application.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Gba.TradeLicense.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private readonly IConfiguration _config;

    // in-memory OTP store for testing
    private static readonly Dictionary<string, DateTime> _otpExpiry = new();

    public AuthService(IConfiguration config)
    {
        _config = config;
    }

    public Task<LoginResult> LoginAsync(LoginRequest req, CancellationToken ct)
    {
        // 🔴 TEST USERS ONLY

        // Trader → direct login
        if (req.UsernameOrPhone == "9999999999" && req.Password == "123456")
        {
            return Task.FromResult(
                new LoginResult(
                    Success: true,
                    AccessToken: GenerateJwt("trader@gba.gov", "Trader"),
                    Error: null,
                    OtpRequired: false
                )
            );
        }

        // Approver → OTP required
        if (req.UsernameOrPhone == "9999999998" && req.Password == "123456")
        {
            return Task.FromResult(
                new LoginResult(
                    Success: true,
                    AccessToken: null,
                    Error: null,
                    OtpRequired: true
                )
            );
        }

        return Task.FromResult(
            new LoginResult(
                Success: false,
                AccessToken: null,
                Error: "Invalid username or password",
                OtpRequired: false
            )
        );
    }

    public Task<OtpSendResult> SendOtpAsync(OtpSendRequest req, CancellationToken ct)
    {
        // mock OTP = 123456 valid for 10 minutes
        _otpExpiry[req.Phone] = DateTime.UtcNow.AddMinutes(10);

        return Task.FromResult(
            new OtpSendResult(
                Success: true,
                Message: $"OTP sent for {req.Purpose} (use 123456 for testing)"
            )
        );
    }

    public Task<OtpVerifyResult> VerifyOtpAsync(OtpVerifyRequest req, CancellationToken ct)
    {
        if (req.Otp != "123456")
        {
            return Task.FromResult(
                new OtpVerifyResult(
                    Success: false,
                    AccessToken: null,
                    Error: "Invalid OTP"
                )
            );
        }

        if (!_otpExpiry.TryGetValue(req.Phone, out var expiry) || expiry < DateTime.UtcNow)
        {
            return Task.FromResult(
                new OtpVerifyResult(
                    Success: false,
                    AccessToken: null,
                    Error: "OTP expired"
                )
            );
        }

        return Task.FromResult(
            new OtpVerifyResult(
                Success: true,
                AccessToken: GenerateJwt("approver@gba.gov", "Approver"),
                Error: null
            )
        );
    }

    // ---------------- JWT ----------------

    private string GenerateJwt(string email, string role)
    {
        var jwt = _config.GetSection("Jwt");

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Role, role)
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwt["Key"]!)
        );

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                int.Parse(jwt["AccessTokenMinutes"]!)
            ),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
