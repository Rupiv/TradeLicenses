using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Gba.TradeLicense.Infrastructure.Security;

public sealed class JwtTokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config)
    {
        _config = config;
    }

    public string CreateAccessToken(
        int loginID,
        string loginName,
        string mobileNo,
        string designation
    )
    {
        var jwt = _config.GetSection("Jwt");

        var issuer = jwt["Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer missing");
        var audience = jwt["Audience"] ?? throw new InvalidOperationException("Jwt:Audience missing");
        var key = jwt["Key"] ?? throw new InvalidOperationException("Jwt:Key missing");

        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(key));

        var creds = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256);

        // 🔥 STRICT SESSION TIME (DEFAULT 15 MIN)
        var minutes = int.TryParse(jwt["AccessTokenMinutes"], out var m) ? m : 15;

        var now = DateTime.UtcNow;

        var claims = new List<Claim>
        {
            // ===== EXISTING CLAIMS =====
            new(JwtRegisteredClaimNames.Sub, loginID.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, loginName ?? string.Empty),

            new("loginID", loginID.ToString()),
            new("login", loginName ?? string.Empty),
            new("mobile", mobileNo ?? string.Empty),
            new("designation", designation ?? string.Empty),

            // ===== STANDARD CLAIMS =====
            new(ClaimTypes.NameIdentifier, loginID.ToString()),
            new(ClaimTypes.Name, loginName ?? string.Empty),
            new(ClaimTypes.Role, designation ?? string.Empty),

            // 🔐 SECURITY CLAIMS (IMPORTANT)
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // unique token id
            new(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(now).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(minutes), // ⏱ SESSION TIMEOUT
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}