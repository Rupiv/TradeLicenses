namespace Gba.TradeLicense.Domain.Entities;

public sealed class TradeApplication : BaseEntity
{
    public string ApplicationNo { get; set; } = "";
    public Guid TraderUserId { get; set; }
    public User TraderUser { get; set; } = default!;

    public string TradeType { get; set; } = "";
    public string BusinessName { get; set; } = "";
    public string AddressLine1 { get; set; } = "";
    public string WardNo { get; set; } = "";
    public decimal? RoadWidthMeters { get; set; }
    public decimal? ConnectedLoadKw { get; set; }

    public string Status { get; set; } = "Draft"; // Draft, Submitted, UnderReview, Inspection, Approved, Rejected
    public ICollection<ApplicationStatusHistory> StatusHistory { get; set; } = new List<ApplicationStatusHistory>();
    public ICollection<ApplicationDocument> Documents { get; set; } = new List<ApplicationDocument>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<Inspection> Inspections { get; set; } = new List<Inspection>();
}
