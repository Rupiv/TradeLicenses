using Gba.TradeLicense.Application.Models;

namespace Gba.TradeLicense.Application.Abstractions;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<OtpSendResult> SendOtpAsync(OtpSendRequest request, CancellationToken ct);
    Task<OtpVerifyResult> VerifyOtpAsync(OtpVerifyRequest request, CancellationToken ct);

   
}
