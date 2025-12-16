using Gba.TradeLicense.Application.Abstractions;
using Gba.TradeLicense.Application.Models;
using Gba.TradeLicense.Domain.Entities;
using Gba.TradeLicense.Infrastructure.Persistence;
using Gba.TradeLicense.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Gba.TradeLicense.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly JwtTokenService _jwt;
    private readonly IConfiguration _config;
    private readonly PasswordHasher<User> _hasher = new();

    public AuthService(AppDbContext db, JwtTokenService jwt, IConfiguration config)
    {
        _db = db;
        _jwt = jwt;
        _config = config;
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var u = await _db.Users
    .Include(x => x.UserRoles)
        .ThenInclude(ur => ur.Role)
    .FirstOrDefaultAsync(x =>
        x.Phone == request.UsernameOrPhone ||
        x.Email == request.UsernameOrPhone, ct);


        if (u is null || !u.IsActive)
            return new(false, null, "Invalid credentials.", false);

        var verify = _hasher.VerifyHashedPassword(u, u.PasswordHash, request.Password);
        if (verify == PasswordVerificationResult.Failed)
            return new(false, null, "Invalid credentials.", false);

        // If trader, enforce OTP (you can change this rule later)
        var roles = u.UserRoles.Select(r => r.Role.Name).ToList();
        var otpRequired = roles.Contains("Trader", StringComparer.OrdinalIgnoreCase);

        if (otpRequired)
            return new(true, null, null, true);

        var token = _jwt.CreateAccessToken(u.Id, u.Email, u.Phone, roles);
        return new(true, token, null, false);
    }

    public async Task<OtpSendResult> SendOtpAsync(OtpSendRequest request, CancellationToken ct)
    {
        var phone = request.Phone.Trim();
        if (string.IsNullOrWhiteSpace(phone))
            return new(false, "Phone is required.");

        var otp = Random.Shared.Next(100000, 999999).ToString();
        var secret = _config["Otp:Secret"] ?? _config.GetSection("Jwt")["Key"] ?? "dev-secret";
        var hash = OtpHasher.Hash(phone, request.Purpose, otp, secret);

        // lock out after too many OTP sends? keep minimal
        var entity = new OtpCode
        {
            Phone = phone,
            Purpose = request.Purpose,
            OtpHash = hash,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
        };

        _db.OtpCodes.Add(entity);
        await _db.SaveChangesAsync(ct);

        // TODO: integrate SMS gateway here
        // For now, we return a generic message (never return OTP in production).
        // During development, you can log it on server.
        return new(true, "OTP sent (if phone exists).");
    }

    public async Task<OtpVerifyResult> VerifyOtpAsync(OtpVerifyRequest request, CancellationToken ct)
    {
        var phone = request.Phone.Trim();
        var otp = request.Otp.Trim();

        var latest = await _db.OtpCodes
            .Where(x => x.Phone == phone && x.Purpose == request.Purpose)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (latest is null) return new(false, null, "OTP not found.");
        if (latest.IsLocked) return new(false, null, "OTP locked.");
        if (latest.VerifiedAtUtc is not null) return new(false, null, "OTP already used.");
        if (DateTime.UtcNow > latest.ExpiresAtUtc) return new(false, null, "OTP expired.");

        // attempt tracking
        latest.AttemptCount += 1;
        if (latest.AttemptCount > 5)
        {
            latest.IsLocked = true;
            await _db.SaveChangesAsync(ct);
            return new(false, null, "Too many attempts. OTP locked.");
        }

        var secret = _config["Otp:Secret"] ?? _config.GetSection("Jwt")["Key"] ?? "dev-secret";
        var hash = OtpHasher.Hash(phone, request.Purpose, otp, secret);
        if (!string.Equals(hash, latest.OtpHash, StringComparison.OrdinalIgnoreCase))
        {
            await _db.SaveChangesAsync(ct);
            return new(false, null, "Invalid OTP.");
        }

        latest.VerifiedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var u = await _db.Users
            .Include(x => x.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(x => x.Phone == phone, ct);

        if (u is null || !u.IsActive)
            return new(false, null, "User not found or inactive.");

        var roles = u.UserRoles.Select(r => r.Role.Name);
        var token = _jwt.CreateAccessToken(u.Id, u.Email, u.Phone, roles);

        return new(true, token, null);
    }
}
