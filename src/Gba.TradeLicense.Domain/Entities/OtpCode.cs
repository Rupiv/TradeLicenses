namespace Gba.TradeLicense.Domain.Entities;

public sealed class OtpCode : BaseEntity
{
    public string Phone { get; set; } = "";
    public string Purpose { get; set; } = "login";
    public string OtpHash { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public int AttemptCount { get; set; } = 0;
    public bool IsLocked { get; set; } = false;

}
