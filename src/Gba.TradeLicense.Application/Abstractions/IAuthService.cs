using Gba.TradeLicense.Application.Models;

public interface IAuthService
{
    // Login with credentials + server-captured metadata
    Task<LoginResult> LoginAsync(
        string usernameOrPhone,
        string password,
        string ipAddress,
        string browser,
        CancellationToken ct
    );

    // Send OTP
    Task<OtpSendResult> SendOtpAsync(
        OtpSendRequest request,
        CancellationToken ct
    );
    Task<LoginResult> LoginUserByMobileAsync(
      string mobileNumber,
      CancellationToken ct
  );

    // Verify OTP
    Task<OtpVerifyResult> VerifyOtpAsync(
        OtpVerifyRequest request,
        CancellationToken ct
    );
}
