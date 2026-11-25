using System.Data;
using Dapper;
using Gba.TradeLicense.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Gba.TradeLicense.Api.Controllers.Licence
{
    [ApiController]
    [Route("api/licence/certificate")]
    public class LicenceCertificateController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public LicenceCertificateController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("approved/{applicationNumber}")]
        public async Task<IActionResult> GetApprovedLicenceCertificate(string applicationNumber)
        {
            var result = new List<ApprovedLicenceCertificateDto>();

            try
            {
                using SqlConnection con = new SqlConnection(
                    _configuration.GetConnectionString("Default"));

                using SqlCommand cmd = new SqlCommand(
                    "sp_GetApprovedLicenceCertificate_FullTrade", con);

                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@applicationNumber", applicationNumber);

                await con.OpenAsync();

                // Traders: only the licence of their own application. Officers: only inside their
                // GBA jurisdiction. Admin: all.
                if (!Gba.TradeLicense.Api.Services.CallerAccess.IsAdmin(User))
                {
                    var ids = await con.QueryAsync<long>(
                        "SELECT CAST(licenceApplicationID AS BIGINT) FROM dbo.Licence_Application WHERE applicationNumber = @applicationNumber",
                        new { applicationNumber });
                    var owns = false;
                    foreach (var id in ids)
                    {
                        if (await Gba.TradeLicense.Api.Services.CallerAccess.MayAccessApplicationAsync(
                                con, User, id, Gba.TradeLicense.Api.Services.ApplicationIdKind.Submitted))
                        {
                            owns = true;
                            break;
                        }
                    }
                    if (!owns)
                        return NotFound(new { message = "No approved licence found for this application number" });
                }

                // Online applications: give them a licence number before the procedure reads it
                await EnsureOnlineLicenceNumber(con, applicationNumber);
                // Old-system applications approved here: issue the receipt the old approval step did
                await EnsureLegacyReceipt(con, applicationNumber);

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();

                if (!reader.HasRows)
                {
                    return NotFound(new
                    {
                        message = "No approved licence found for this application number"
                    });
                }

                while (await reader.ReadAsync())
                {
                    result.Add(new ApprovedLicenceCertificateDto
                    {
                        LicenceApplicationID =
                            reader.GetInt64(reader.GetOrdinal("licenceApplicationID")),

                        ApplicationNumber =
                            reader["applicationNumber"]?.ToString(),

                        FinancialYear =
                            reader["financialYear"]?.ToString(),

                        LicenceNumber =
                            reader["LicenceNumber"]?.ToString(),

                        ApplicantName =
                            reader["applicantName"]?.ToString(),

                        TradeName =
                            reader["tradeName"]?.ToString(),

                        TradeAddress =
                            reader["TradeAddress"]?.ToString(),

                        TradeMajorName =
                            reader["tradeMajorName"]?.ToString(),

                        TradeMinorName =
                            reader["tradeMinorName"]?.ToString(),

                        TradeSubName =
                            reader["tradeSubName"]?.ToString(),

                        LicenceFromDate =
                            reader["licenceFromDate"] == DBNull.Value
                                ? null
                                : Convert.ToDateTime(reader["licenceFromDate"]),

                        LicenceToDate =
                            reader["licenceToDate"] == DBNull.Value
                                ? null
                                : Convert.ToDateTime(reader["licenceToDate"]),

                        ReceiptNumber =
                            reader["receiptNumber"]?.ToString(),

                        ReceiptDate =
                            reader["receiptDate"] == DBNull.Value
                                ? null
                                : Convert.ToDateTime(reader["receiptDate"]),

                        TradeFee =
                            reader["tradeFee"] == DBNull.Value
                                ? 0
                                : Convert.ToDecimal(reader["tradeFee"]),
                        WardID =
                        reader["wardID"] == DBNull.Value
                            ? null
                            : Convert.ToInt32(reader["wardID"]),

                        WardName =
                        reader["wardName"]?.ToString(),

                        ApplicationStatus =
                            reader["ApplicationStatus"]?.ToString()
                    });
                }

                await reader.CloseAsync();
                await AddOnlinePaymentDetails(con, result);
                try
                {
                    await AddGbaLocation(con, result);
                }
                catch (SqlException ex) when (ex.Number == 208)
                {
                    // GBA tables not created yet (migration not run): certificate prints as before
                }

                return Ok(result);
            }
            catch (SqlException ex)
            {
                // Handles RAISERROR from stored procedure
                return BadRequest(new
                {
                    message = Gba.TradeLicense.Api.Services.CallerAccess.SafeDbMessage(ex, "The request could not be processed.")
                });
            }
        }

        /*
         * ONLINE (portal) APPLICATIONS
         * Identified by their draft row (same tradeLicenceID + loginID) carrying a portal receipt
         * "GBA/TL/<year>/<draft id>". Legacy licences never match, so they print exactly as before.
         *
         * Why: FINAL_SUBMIT does not copy receiptNumber / receiptDate to Licence_Application, and
         * generatenewlicencenumber appends LA.receiptNumber -> NULL -> no licence number at all.
         */
        private const string OnlineDraftJoin =
            @"JOIN dbo.Licence_Application_Temp T
                ON T.tradeLicenceID = LA.tradeLicenceID
               AND T.loginID = LA.loginID
               AND T.receiptNumber LIKE 'GBA/TL/%'";

        // Same layout as generatenewlicencenumber (zone, constituency, ward, trade major/minor/sub)
        // but ending in "G" + the draft id instead of the legacy numeric receipt, so it can never
        // collide with a legacy licence number. Written once: only when the number is still empty.
        private static Task EnsureOnlineLicenceNumber(SqlConnection con, string applicationNumber) =>
            con.ExecuteAsync(
                $@"UPDATE LM
                   SET newlicenceNumber = g.licenceNo
                   FROM dbo.Licence_Application LA
                   JOIN dbo.Licence_Master LM ON LM.tradeLicenceID = LA.tradeLicenceID
                   {OnlineDraftJoin}
                   CROSS APPLY (
                       SELECT MIN(x.tradeSubID) AS tradeSubID
                       FROM (SELECT tradeSubID FROM dbo.Licence_PowerDetails WHERE licenceApplicationID = LA.licenceApplicationID
                             UNION ALL
                             SELECT tradeSubID FROM dbo.Licence_TradeDetails WHERE licenceApplicationID = LA.licenceApplicationID) x
                   ) firstSub
                   JOIN dbo.Master_BBMPWard MW ON MW.wardID = LM.wardID
                   JOIN dbo.Master_BBMPZone MZ ON MZ.zoneID = MW.zoneID
                   CROSS APPLY (SELECT TOP 1 s.tradeMinorID FROM dbo.Trade_SubCategory s WHERE s.tradeSubID = firstSub.tradeSubID) subCat
                   JOIN dbo.Trade_MinorCategory TMN ON TMN.tradeMinorID = subCat.tradeMinorID
                   JOIN dbo.Trade_MajorCategory TM ON TM.tradeMajorID = TMN.tradeMajorID
                   CROSS APPLY (
                       SELECT SUBSTRING(UPPER(MZ.zoneName), 1, 2)
                            + RIGHT('0' + CONVERT(VARCHAR(2), MW.constituencyID), 2)
                            + RIGHT('00' + CONVERT(VARCHAR(3), LM.wardID), 3)
                            + CONVERT(VARCHAR(1), TM.tradeMajorID)
                            + RIGHT('0' + CONVERT(VARCHAR(2), TMN.tradeMinorID), 2)
                            + RIGHT('000' + CONVERT(VARCHAR(3), firstSub.tradeSubID), 3)
                            + 'G' + CONVERT(VARCHAR(20), T.licenceApplicationID) AS licenceNo
                   ) g
                   WHERE LA.applicationNumber = @applicationNumber
                     AND LA.licenceApplicationStatusID = 3
                     AND LA.isActive = 'Y'
                     AND (LM.newlicenceNumber IS NULL OR LM.newlicenceNumber = '')
                     AND g.licenceNo IS NOT NULL",
                new { applicationNumber });

        /*
         * OLD-SYSTEM (non-portal) APPLICATIONS approved through the new workflow never ran the old
         * approval step usp_Tradelicenseno, which issues the receipt number (Licence_ReceiptSequenceNumber)
         * and the licence number. Without a receipt the licence number is NULL.
         * Run it ONLY when: approved + active, no receipt number yet, no licence number yet, payment
         * confirmed by the bank, and not a portal application. (usp_Tradelicenseno tests the receipt
         * security code, not the receipt number, so it is never called when a receipt already exists.)
         */
        private static async Task EnsureLegacyReceipt(SqlConnection con, string applicationNumber)
        {
            var needsReceipt = await con.ExecuteScalarAsync<int>(
                $@"SELECT COUNT(1)
                   FROM dbo.Licence_Application LA
                   JOIN dbo.Licence_Master LM ON LM.tradeLicenceID = LA.tradeLicenceID
                   WHERE LA.applicationNumber = @applicationNumber
                     AND LA.licenceApplicationStatusID = 3
                     AND LA.isActive = 'Y'
                     AND (LA.receiptNumber IS NULL OR LTRIM(RTRIM(LA.receiptNumber)) = '')
                     AND (LM.newlicenceNumber IS NULL OR LM.newlicenceNumber = '')
                     AND EXISTS (SELECT 1 FROM dbo.Licence_Payments LP
                                 WHERE LP.licenceApplicationID = LA.licenceApplicationID
                                   AND LP.amountPaid > 0 AND LP.bankApproveStatus = 'A')
                     AND NOT EXISTS (SELECT 1 FROM dbo.Licence_Application_Temp T
                                     WHERE T.tradeLicenceID = LA.tradeLicenceID AND T.loginID = LA.loginID
                                       AND T.receiptNumber LIKE 'GBA/TL/%')",
                new { applicationNumber });

            if (needsReceipt == 1)
            {
                await con.ExecuteAsync(
                    "dbo.usp_Tradelicenseno",
                    new { applicationNumber },
                    commandType: CommandType.StoredProcedure);
            }
        }

        // Receipt, amount actually paid, payment mode, financial year and validity for online applications
        private static async Task AddOnlinePaymentDetails(SqlConnection con, List<ApprovedLicenceCertificateDto> certificates)
        {
            if (certificates.Count == 0)
                return;

            var rows = await con.QueryAsync<(long LicenceApplicationID, string ReceiptNumber, DateTime? ReceiptDate,
                                             int? NoOfYearsApplied, decimal? Amount, string? Response)>(
                $@"SELECT LA.licenceApplicationID, T.receiptNumber, T.receiptDate, LA.NoOfYearsApplied,
                          pay.Amount, pay.ResponsePayload
                   FROM dbo.Licence_Application LA
                   {OnlineDraftJoin}
                   OUTER APPLY (
                       SELECT TOP 1 p.Amount, p.ResponsePayload
                       FROM dbo.Payment_Audit_Log p
                       WHERE p.LicenceApplicationId = T.licenceApplicationID AND p.PaymentStage = 'SUCCESS'
                         AND ISNULL(p.IsActive, 1) = 1
                       ORDER BY p.CreatedOn DESC
                   ) pay
                   WHERE LA.licenceApplicationID IN @ids",
                new { ids = certificates.Select(c => c.LicenceApplicationID).Distinct().ToArray() });

            var byApplication = rows.GroupBy(r => r.LicenceApplicationID).ToDictionary(g => g.Key, g => g.First());
            foreach (var cert in certificates)
            {
                if (!byApplication.TryGetValue(cert.LicenceApplicationID, out var r))
                    continue;

                if (string.IsNullOrWhiteSpace(cert.ReceiptNumber))
                    cert.ReceiptNumber = r.ReceiptNumber;
                if (cert.ReceiptDate == null || cert.ReceiptDate.Value.Year < 2000)
                    cert.ReceiptDate = r.ReceiptDate;

                cert.AmountPaid = r.Amount;
                try
                {
                    if (!string.IsNullOrWhiteSpace(r.Response))
                        cert.PaymentMode = Newtonsoft.Json.Linq.JObject.Parse(r.Response)["mode"]?.ToString();
                }
                catch { /* payment mode is optional */ }

                // Financial year (April–March) of the licence date; licence valid till 31 March of
                // the last year applied for. (The procedure prints the newest master year instead,
                // and licenceToDate holds the proposed commencement date.)
                var start = cert.LicenceFromDate ?? cert.ReceiptDate ?? DateTime.Today;
                int fyStart = start.Month >= 4 ? start.Year : start.Year - 1;
                int years = Math.Max(1, r.NoOfYearsApplied ?? 1);
                cert.FinancialYear = $"{fyStart}-{fyStart + 1}";
                cert.ValidUpto = new DateTime(fyStart + years, 3, 31);
            }
        }

        // Corporation + GBA ward for licences whose location was confirmed (CONFIRMED in vw_LicenceCorporation).
        // Older licences are left as they are, so their certificate prints exactly as before.
        private static async Task AddGbaLocation(SqlConnection con, List<ApprovedLicenceCertificateDto> certificates)
        {
            if (certificates.Count == 0)
                return;

            var rows = await con.QueryAsync<(long LicenceApplicationID, string CorporationName, string CorporationNativeName, int WardNo, string WardName)>(
                @"SELECT la.licenceApplicationID, c.corporationName, c.corporationNativeName, gw.wardNo, gw.wardName
                  FROM dbo.Licence_Application la
                  JOIN dbo.vw_LicenceCorporation v ON v.tradeLicenceID = la.tradeLicenceID AND v.locationStatus = 'CONFIRMED'
                  JOIN dbo.Master_Corporation c ON c.corporationID = v.corporationID
                  JOIN dbo.Master_GBAWard gw ON gw.gbaWardID = v.gbaWardID
                  WHERE la.licenceApplicationID IN @ids",
                new { ids = certificates.Select(c => c.LicenceApplicationID).Distinct().ToArray() });

            var byApplication = rows.ToDictionary(r => r.LicenceApplicationID);
            foreach (var cert in certificates)
            {
                if (!byApplication.TryGetValue(cert.LicenceApplicationID, out var loc))
                    continue;
                cert.CorporationName = loc.CorporationName;
                cert.CorporationNativeName = loc.CorporationNativeName;
                cert.GbaWardName = $"{loc.WardNo} - {loc.WardName}";
            }
        }
    }
}
