using Gba.TradeLicense.Application.Models;

namespace Gba.TradeLicense.Application.Abstractions;

public interface ITradeApplicationService
{
    Task<ApplicationCreateResult> CreateAsync(Guid traderUserId, ApplicationCreateRequest request, CancellationToken ct);
    Task<ApplicationStatusResult> GetStatusAsync(string applicationNo, Guid userId, CancellationToken ct);
    Task<ApplicationSubmitResult> SubmitAsync(string applicationNo, Guid traderUserId, CancellationToken ct);
}
