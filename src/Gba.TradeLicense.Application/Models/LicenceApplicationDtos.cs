namespace Gba.TradeLicense.Application.Models;

// -------- INSERT / UPDATE (DRAFT) --------
public sealed record LicenceApplicationUpsertDto
(
    long? LicenceApplicationID,
    string? NewApplicationNumber,
    int FinanicalYearID,
    int TradeTypeID,
    string? BescomRRNumber,
    string? TINNumber,
    string? VATNumber,
    DateTime? LicenceFromDate,
    DateTime? LicenceToDate,
    int LicenceApplicationStatusID,
    int CurrentStatus,
    long TradeLicenceID,
    int MohID,
    int LoginID,
    int EntryOriginLoginID,
    int? InspectingOfficerID,
    string? LicenseType,
    int ApplicantRepersenting,
    string? JathaStatus,
    bool DocsSubmitted,
    string? ChallanNo,
    int NoOfYearsApplied
);
public class MasterDocumentDto
{
    public int DocumentID { get; set; }
    public string DocumentName { get; set; } = string.Empty;
}

// -------- SEARCH --------
public sealed record LicenceApplicationSearchDto
(
    string? SearchText
);

// -------- RESPONSE --------
public sealed class LicenceApplicationResponseDto
{
    public long LicenceApplicationID { get; set; }
    public string? ApplicationNumber { get; set; }
    public int TradeTypeID { get; set; }
    public int LicenceApplicationStatusID { get; set; }
    public string Source { get; set; } = "";
}
