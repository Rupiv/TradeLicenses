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
        string designation   // existing parameter – unchanged
    )
    {
        var jwt = _config.GetSection("Jwt");

        var issuer = jwt["Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer missing");
        var audience = jwt["Audience"] ?? throw new InvalidOperationException("Jwt:Audience missing");
        var key = jwt["Key"] ?? throw new InvalidOperationException("Jwt:Key missing");

        var claims = new List<Claim>
    {
        // ===== EXISTING CLAIMS (UNCHANGED) =====
        new(JwtRegisteredClaimNames.Sub, loginID.ToString()),
        new(JwtRegisteredClaimNames.UniqueName, loginName ?? string.Empty),

        new("loginID", loginID.ToString()),
        new("login", loginName ?? string.Empty),
        new("mobile", mobileNo ?? string.Empty),
        new("designation", designation ?? string.Empty),

        // ===== STANDARD .NET CLAIMS (ADDED – SAFE) =====
        new(ClaimTypes.NameIdentifier, loginID.ToString()),
        new(ClaimTypes.Name, loginName ?? string.Empty),

        // 🔐 IMPORTANT: enables [Authorize(Roles = "...")]
        new(ClaimTypes.Role, designation ?? string.Empty)
    };

        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(key));

        var creds = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256);

        var minutes = int.TryParse(jwt["AccessTokenMinutes"], out var m) ? m : 30;

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(minutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

}
