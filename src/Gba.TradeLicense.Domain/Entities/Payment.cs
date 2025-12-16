namespace Gba.TradeLicense.Domain.Entities;

public sealed class Payment : BaseEntity
{
    public Guid TradeApplicationId { get; set; }
    public TradeApplication TradeApplication { get; set; } = default!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public string Status { get; set; } = "Pending"; // Pending, Success, Failed
    public string? TransactionRef { get; set; }
    public DateTime? PaidAtUtc { get; set; }
}
