namespace Gba.TradeLicense.Application.Models;

public sealed record ApplicationCreateRequest(
    string TradeType,
    string BusinessName,
    string AddressLine1,
    string WardNo,
    decimal? ConnectedLoadKw
);

public sealed record ApplicationCreateResult(bool Success, string? ApplicationNo, string? Error);

public sealed record ApplicationStatusResult(bool Success, string? ApplicationNo, string? Status, object? History, string? Error);

public sealed record ApplicationSubmitResult(bool Success, string? Status, string? Error);
