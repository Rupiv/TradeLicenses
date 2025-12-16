namespace Gba.TradeLicense.Application.Models;

public sealed record LoginRequest(string UsernameOrPhone, string Password);
public sealed record LoginResult(bool Success, string? AccessToken, string? Error, bool OtpRequired);

public sealed record OtpSendRequest(string Phone, string Purpose = "login");
public sealed record OtpSendResult(bool Success, string Message);

public sealed record OtpVerifyRequest(string Phone, string Otp, string Purpose = "login");
public sealed record OtpVerifyResult(bool Success, string? AccessToken, string? Error);
