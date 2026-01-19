namespace Gba.TradeLicense.Domain.Entities;

public sealed class Inspection : BaseEntity
{
    public Guid TradeApplicationId { get; set; }
    public TradeApplication TradeApplication { get; set; } = default!;
    public Guid InspectorUserId { get; set; }
    public string ChecklistJson { get; set; } = "{}";
    public string Notes { get; set; } = "";
    public string PhotosJson { get; set; } = "[]";
    public string Result { get; set; } = "Pending"; // Pending, Pass, Fail
    public DateTime? CompletedAtUtc { get; set; }
}


