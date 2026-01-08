namespace Gba.TradeLicense.Application.Models;

public sealed class ApplicationStatusCount
{
    public int LicenceApplicationStatusID { get; set; }

    public string LicenceApplicationStatusName { get; set; } = string.Empty;

    public long TotalApplications { get; set; }
}
