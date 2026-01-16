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
public class ActionReasonDto
{
    public int ActionReasonId { get; set; }
    public string ActionReasonName { get; set; } = string.Empty;
}
public class LicenceCurrentStatusDto
{
    public int LicenceCurrentStatusID { get; set; }
    public string StatusDecription { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
public class FinancialYearDto
{
    public int FinanicalYearID { get; set; }
    public string FinancialYear { get; set; } = string.Empty;
    public int BlockPeriodID { get; set; }
}
