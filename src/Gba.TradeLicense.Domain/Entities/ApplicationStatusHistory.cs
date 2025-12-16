namespace Gba.TradeLicense.Domain.Entities;

public sealed class ApplicationStatusHistory : BaseEntity
{
    public Guid TradeApplicationId { get; set; }
    public TradeApplication TradeApplication { get; set; } = default!;
    public string Status { get; set; } = "";
    public string Comment { get; set; } = "";
    public Guid ByUserId { get; set; }
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
