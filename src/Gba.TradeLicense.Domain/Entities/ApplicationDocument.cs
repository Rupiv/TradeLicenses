namespace Gba.TradeLicense.Domain.Entities;

public sealed class ApplicationDocument : BaseEntity
{
    public Guid TradeApplicationId { get; set; }
    public TradeApplication TradeApplication { get; set; } = default!;
    public string DocType { get; set; } = "";
    public string FileName { get; set; } = "";
    public string StoragePath { get; set; } = ""; // local path, blob key, etc.
}
