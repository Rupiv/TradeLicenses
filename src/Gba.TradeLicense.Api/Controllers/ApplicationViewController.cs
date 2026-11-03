using System.Data;
using System.Security.Claims;
using Dapper;
using Gba.TradeLicense.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;

namespace Gba.TradeLicense.Api.Controllers
{
    /// <summary>
    /// Trader portal "View Application": everything about one application (the
    /// Licence_Application_Temp id the trader's list uses) in a single read-only call, plus
    /// "complete submission" for applications that were paid but never submitted (e.g. the
    /// gateway redirect failed).
    ///
    /// Life of an application:
    ///   draft   : Licence_Application_Temp.tradeLicenceID -> Licence_Master_Temp
    ///   submit  : usp_TradeLicence_CRUD FINAL_SUBMIT copies it to Licence_Master under a NEW id
    ///             and re-points Licence_Application_Temp.tradeLicenceID to that id;
    ///             usp_LicenceApplication_CRUD FINAL_SUBMIT creates Licence_Application and moves
    ///             documents + geo to it. From then on the status lives in Licence_Application.
    /// </summary>
    [ApiController]
    [Route("api/licence-application-view")]
    public class ApplicationViewController : ControllerBase
    {
        private readonly string _connStr;
        private readonly GbaLocationService _gba;
        private readonly ApplicationSmsNotifier _smsNotifier;

        public ApplicationViewController(IConfiguration config, GbaLocationService gba, ApplicationSmsNotifier smsNotifier)
        {
            _connStr = config.GetConnectionString("Default")
                ?? throw new InvalidOperationException("Connection string not found");
            _gba = gba;
            _smsNotifier = smsNotifier;
        }

        private static string Normalize(string? s) =>
            new string((s ?? string.Empty).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

        // Traders may only open their own applications; staff roles may open any.
        private bool IsTrader()
        {
            var role = Normalize(User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("designation")?.Value);
            return role is "trader" or "tradeowner";
        }

        private long? CallerId() =>
            long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("loginID")?.Value, out var id)
                ? id : null;

        // Legacy bulk-migrated applications were saved with loginID = -1 (never linked to any real
        // trader account). The trader dashboard already surfaces these by matching the caller's
        // registered mobile number (see TradeLicenceController.GetUserApplications) - this view must
        // accept the same match, or an application the dashboard lists would 403 when opened.
        private async Task<bool> CanViewAsync(IDbConnection db, long ownerLoginId, long tradeLicenceId, bool isFinal)
        {
            if (ownerLoginId == CallerId()) return true;
            if (ownerLoginId != -1) return false;

            var callerMobile = User.FindFirst("mobile")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(callerMobile)) return false;

            var table = isFinal ? "dbo.Licence_Master" : "dbo.Licence_Master_Temp";
            var mobile = await db.ExecuteScalarAsync<string?>(
                $"SELECT mobileNumber FROM {table} WHERE tradeLicenceID = @tradeLicenceId",
                new { tradeLicenceId });

            return !string.IsNullOrWhiteSpace(mobile) && mobile.Trim() == callerMobile;
        }

        private sealed record TempState(long LoginID, long TradeLicenceID, int StatusID, string? ReceiptNumber);

        private static Task<TempState?> GetTempAsync(IDbConnection db, long licenceApplicationId) =>
            db.QueryFirstOrDefaultAsync<TempState>(
                @"SELECT CAST(loginID AS BIGINT) AS LoginID, CAST(tradeLicenceID AS BIGINT) AS TradeLicenceID,
                         licenceApplicationStatusID AS StatusID, receiptNumber AS ReceiptNumber
                  FROM dbo.Licence_Application_Temp WHERE licenceApplicationID = @licenceApplicationId",
                new { licenceApplicationId });

        /// <summary>Latest Licence_Application of this temp application, or null while not submitted.</summary>
        private static Task<dynamic?> GetFinalAsync(IDbConnection db, long licenceApplicationId) =>
            db.QueryFirstOrDefaultAsync(
                @"SELECT TOP 1 la.licenceApplicationID, la.applicationNumber, la.applicationSubmitDate,
                         la.acknowledgementNumber, la.licenceApplicationStatusID, la.tradeLicenceID,
                         COALESCE(mp.licenceProcessName, las.licenceApplicationStatusName) AS statusName
                  FROM dbo.Licence_Application_Temp t
                  JOIN dbo.Licence_Application la ON la.tradeLicenceID = t.tradeLicenceID
                  LEFT JOIN dbo.Master_LicenceProcess mp ON mp.licenceProcessID = la.licenceApplicationStatusID
                  LEFT JOIN dbo.Licence_ApplicationStatus las ON las.licenceApplicationStatusID = la.licenceApplicationStatusID
                  WHERE t.licenceApplicationID = @licenceApplicationId
                  ORDER BY la.licenceApplicationID DESC",
                new { licenceApplicationId });

        private static async Task<bool> HasSuccessfulPaymentAsync(IDbConnection db, long licenceApplicationId) =>
            await db.ExecuteScalarAsync<int>(
                @"SELECT COUNT(1) FROM dbo.Payment_Audit_Log
                  WHERE LicenceApplicationId = @licenceApplicationId AND PaymentStage = 'SUCCESS'
                    AND ISNULL(IsActive, 1) = 1",
                new { licenceApplicationId }) > 0;

        // =====================================================================
        // VIEW BY SUBMITTED ID (officer screens: inspection, zone approver, senior approver, admin)
        // Same response as GET {draftId}. Online applications -> their draft (has the online
        // payments); older applications without a draft -> built from the submitted records.
        // Read-only.
        // =====================================================================
        [HttpGet("submitted/{licenceApplicationId:long}")]
        public async Task<IActionResult> GetSubmitted(long licenceApplicationId)
        {
            using var db = new SqlConnection(_connStr);

            var la = await db.QueryFirstOrDefaultAsync<(long LoginID, long TradeLicenceID)?>(
                @"SELECT CAST(loginID AS BIGINT), CAST(tradeLicenceID AS BIGINT)
                  FROM dbo.Licence_Application WHERE licenceApplicationID = @licenceApplicationId",
                new { licenceApplicationId });
            if (la == null)
                return NotFound(new { Message = "Application not found." });
            if (IsTrader() && !await CanViewAsync(db, la.Value.LoginID, la.Value.TradeLicenceID, isFinal: true))
                return Forbid();

            // Draft of an online application: same licence record and same applicant
            var draftId = await db.ExecuteScalarAsync<long?>(
                @"SELECT TOP 1 t.licenceApplicationID
                  FROM dbo.Licence_Application_Temp t
                  WHERE t.tradeLicenceID = @tradeLicenceId AND t.loginID = @loginId
                  ORDER BY t.licenceApplicationID DESC",
                new { tradeLicenceId = la.Value.TradeLicenceID, loginId = la.Value.LoginID });

            if (draftId is long d)
            {
                var finalOfDraft = await GetFinalAsync(db, d);
                if (finalOfDraft != null && Convert.ToInt64(finalOfDraft.licenceApplicationID) == licenceApplicationId)
                    return await Get(d);
            }

            return Ok(await BuildSubmittedOnlyViewAsync(db, licenceApplicationId));
        }

        /// <summary>Full view for an application that has no online draft (older applications).</summary>
        private static async Task<object> BuildSubmittedOnlyViewAsync(IDbConnection db, long finalId)
        {
            var application = await db.QueryFirstOrDefaultAsync(
                @"SELECT la.licenceApplicationID, la.applicationNumber, la.applicationSubmitDate, la.applicationEntryDate,
                         la.licenceFromDate, la.licenceToDate, la.licenceApplicationStatusID,
                         COALESCE(mp.licenceProcessName, las.licenceApplicationStatusName) AS licenceApplicationStatusName,
                         la.tradeTypeID, la.bescomRRNumber, la.GSTNumber, la.PANNumber, la.licenseType,
                         la.receiptNumber, la.receiptDate, la.tradeLicenceID, la.mohID, la.loginID
                  FROM dbo.Licence_Application la
                  LEFT JOIN dbo.Master_LicenceProcess mp ON mp.licenceProcessID = la.licenceApplicationStatusID
                  LEFT JOIN dbo.Licence_ApplicationStatus las ON las.licenceApplicationStatusID = la.licenceApplicationStatusID
                  WHERE la.licenceApplicationID = @finalId",
                new { finalId });

            var final = await db.QueryFirstOrDefaultAsync(
                @"SELECT la.licenceApplicationID, la.applicationNumber, la.applicationSubmitDate,
                         la.acknowledgementNumber, la.licenceApplicationStatusID, la.tradeLicenceID,
                         COALESCE(mp.licenceProcessName, las.licenceApplicationStatusName) AS statusName
                  FROM dbo.Licence_Application la
                  LEFT JOIN dbo.Master_LicenceProcess mp ON mp.licenceProcessID = la.licenceApplicationStatusID
                  LEFT JOIN dbo.Licence_ApplicationStatus las ON las.licenceApplicationStatusID = la.licenceApplicationStatusID
                  WHERE la.licenceApplicationID = @finalId",
                new { finalId });

            long tradeLicenceId = Convert.ToInt64(application!.tradeLicenceID);

            var master = await db.QueryFirstOrDefaultAsync(
                @"SELECT m.applicantName, m.tradeName, m.mobileNumber, m.emailID, m.landLineNumber,
                         m.doorNumber, m.address1, m.address2, m.address3, m.pincode, m.street,
                         m.PIDNumber, m.khathaNumber, m.surveyNumber, m.wardID, m.mohID,
                         m.licenceNumber, m.newlicenceNumber,
                         bw.wardName, bw.wardCode AS wardNo, bz.zoneName, mm.mohName,
                         tzc.zonalClassificationName
                  FROM dbo.Licence_Master m
                  LEFT JOIN dbo.Master_BBMPWard bw ON bw.wardID = m.wardID
                  LEFT JOIN dbo.Master_BBMPZone bz ON bz.zoneID = bw.zoneID
                  LEFT JOIN dbo.Master_MOH mm ON mm.mohID = m.mohID
                  LEFT JOIN dbo.Trade_ZonalClassification tzc ON tzc.zonalClassificationID = m.zonalClassificationID
                  WHERE m.tradeLicenceID = @tradeLicenceId",
                new { tradeLicenceId });

            var tradeTypeName = await db.ExecuteScalarAsync<string?>(
                "SELECT TOP 1 tradeTypeName FROM dbo.Trade_Type WHERE tradeTypeID = @id",
                new { id = (int?)application.tradeTypeID });

            // GBA ward: chosen (CONFIRMED) or mapped from the legacy ward (DERIVED)
            dynamic? gba = null;
            try
            {
                gba = await db.QueryFirstOrDefaultAsync(
                    @"SELECT TOP 1 c.corporationID, c.corporationName, c.corporationNativeName,
                             z.gbaZoneID, z.zoneName, w.gbaWardID, w.wardNo, w.wardName, w.wardNativeName,
                             w.assemblyName, v.locationStatus AS source
                      FROM dbo.vw_LicenceCorporation v
                      JOIN dbo.Master_GBAWard w ON w.gbaWardID = COALESCE(v.gbaWardID, v.suggestedGbaWardID)
                      JOIN dbo.Master_GBAZone z ON z.gbaZoneID = w.gbaZoneID
                      JOIN dbo.Master_Corporation c ON c.corporationID = w.corporationID
                      WHERE v.tradeLicenceID = @tradeLicenceId",
                    new { tradeLicenceId });
            }
            catch (SqlException ex) when (ex.Number == 208) { }

            var trades = (await db.QueryAsync<dynamic>(
                @"SELECT ltd.licenceTradeDetailsID, ltd.licenceApplicationID, ltd.tradeSubID,
                         sub.tradeSubName, tmc.tradeMinorID, tmc.tradeMinorName, tjc.tradeMajorID, tjc.tradeMajorName, ltd.tradeFee
                  FROM dbo.Licence_TradeDetails ltd
                  OUTER APPLY (SELECT TOP 1 tradeSubName, tradeMinorID FROM dbo.Trade_SubCategory s WHERE s.tradeSubID = ltd.tradeSubID) sub
                  LEFT JOIN dbo.Trade_MinorCategory tmc ON tmc.tradeMinorID = sub.tradeMinorID
                  LEFT JOIN dbo.Trade_MajorCategory tjc ON tjc.tradeMajorID = tmc.tradeMajorID
                  WHERE ltd.licenceApplicationID = @finalId
                  ORDER BY ltd.licenceTradeDetailsID",
                new { finalId })).ToList();

            var documents = (await db.QueryAsync<dynamic>(
                @"SELECT lad.ApplicationDocumentID, lad.LicenceApplicationID, lad.DocumentID, md.documentName,
                         lad.FileName, lad.FilePath, lad.FileExtension, lad.FileSizeKB, lad.IsActive, lad.EntryDate
                  FROM dbo.Licence_Application_Document lad
                  LEFT JOIN dbo.Master_Document md ON md.documentID = lad.DocumentID
                  WHERE lad.LicenceApplicationID = @finalId AND ISNULL(lad.IsActive, 1) = 1
                  ORDER BY lad.ApplicationDocumentID",
                new { finalId })).ToList();

            var geo = await db.QueryFirstOrDefaultAsync(
                @"SELECT TOP 1 GeoID, LicenceApplicationID, Latitude, Longitude, RoadID, RoadWidthMtrs,
                         RoadCategoryCode, RoadCategory, IsConfirmed, EntryDate
                  FROM dbo.Licence_Application_GeoLocation
                  WHERE LicenceApplicationID = @finalId
                  ORDER BY EntryDate DESC, GeoID DESC",
                new { finalId });

            IEnumerable<dynamic> timeline = Array.Empty<dynamic>();
            try
            {
                timeline = await db.QueryAsync(
                    "usp_Licence_SubmitAction",
                    new { Mode = "GET", LicenceApplicationID = finalId },
                    commandType: CommandType.StoredProcedure);
            }
            catch (SqlException) { }

            // Payments recorded by the old system (counter / bank) for this application
            var legacyPayments = (await db.QueryAsync<(long Id, decimal? Total, decimal? Paid, string? Status, DateTime? Entry, DateTime? Received)>(
                @"SELECT licencePaymentID, totalPayable, amountPaid, bankApproveStatus, entryDate, AmtReceivedDate
                  FROM dbo.Licence_Payments WHERE licenceApplicationID = @finalId ORDER BY licencePaymentID",
                new { finalId })).ToList();

            string? receipt = application.receiptNumber;
            var payments = legacyPayments.Select(p => new
            {
                txnId = !string.IsNullOrWhiteSpace(receipt) ? $"Receipt {receipt}" : $"Payment #{p.Id}",
                amount = p.Paid ?? 0m,
                status = string.Equals(p.Status, "A", StringComparison.OrdinalIgnoreCase) && (p.Paid ?? 0m) > 0
                         ? "SUCCESS" : (p.Status ?? "PENDING"),
                paidOn = p.Received ?? p.Entry,
                bankRefNo = (string?)null,
                gatewayTxnId = (string?)null,
                mode = (string?)null,
                message = (string?)null
            }).ToList();

            var paidTotal = payments.Where(p => p.status == "SUCCESS").Sum(p => p.amount);
            var totalTradeFee = trades.Sum(t => (decimal)(t.tradeFee ?? 0m));

            return new
            {
                application,
                master,
                final,
                timeline,
                extra = new
                {
                    zoneName = master?.zoneName,
                    wardNo = master?.wardNo,
                    zonalClassificationName = master?.zonalClassificationName,
                    tradeTypeName,
                    landLineNumber = master?.landLineNumber,
                    licenceNumber = master?.licenceNumber,
                    newlicenceNumber = master?.newlicenceNumber
                },
                gba,
                trades,
                documents,
                geo,
                payments,
                awaitingSubmission = false,
                fees = new
                {
                    totalTradeFee,
                    paymentStatus = paidTotal > 0 ? "Paid" : payments.Count > 0 ? "Not completed" : "Not paid",
                    amountPaid = paidTotal > 0 ? paidTotal : (decimal?)null
                }
            };
        }

        // =====================================================================
        // VIEW
        // =====================================================================
        [HttpGet("{licenceApplicationId:long}")]
        public async Task<IActionResult> Get(long licenceApplicationId)
        {
            using var db = new SqlConnection(_connStr);

            var temp = await GetTempAsync(db, licenceApplicationId);
            if (temp == null)
            {
                // No draft row at all - legacy/migrated applications can live only in the final
                // tables (never had a Licence_Application_Temp row). Same place GetSubmitted looks.
                var fa = await db.QueryFirstOrDefaultAsync<(long LoginID, long TradeLicenceID)?>(
                    @"SELECT CAST(loginID AS BIGINT), CAST(tradeLicenceID AS BIGINT)
                      FROM dbo.Licence_Application WHERE licenceApplicationID = @licenceApplicationId",
                    new { licenceApplicationId });
                if (fa == null)
                    return NotFound(new { Message = "Application not found." });
                if (IsTrader() && !await CanViewAsync(db, fa.Value.LoginID, fa.Value.TradeLicenceID, isFinal: true))
                    return Forbid();

                return Ok(await BuildSubmittedOnlyViewAsync(db, licenceApplicationId));
            }
            if (IsTrader() && !await CanViewAsync(db, temp.LoginID, temp.TradeLicenceID, isFinal: false))
                return Forbid();

            // 1) Existing full-details procedure (header, trades, documents, geo)
            using var multi = await db.QueryMultipleAsync(
                "usp_LicenceApplicationTemp_GetFullDetails",
                new { licenceApplicationID = licenceApplicationId },
                commandType: CommandType.StoredProcedure);

            var application = (await multi.ReadAsync<dynamic>()).FirstOrDefault();
            var trades = (await multi.ReadAsync<dynamic>()).ToList();
            var documents = (await multi.ReadAsync<dynamic>()).ToList();
            var geo = (await multi.ReadAsync<dynamic>()).FirstOrDefault();

            if (application == null)
                return NotFound(new { Message = "Application not found." });

            var final = await GetFinalAsync(db, licenceApplicationId);
            var submitted = final != null;

            // 2) Applicant / address / ward / MOH. The procedure reads Licence_Master_Temp, which no
            //    longer matches after submission (tradeLicenceID now points to Licence_Master).
            var master = await db.QueryFirstOrDefaultAsync(
                $@"SELECT m.applicantName, m.tradeName, m.mobileNumber, m.emailID, m.landLineNumber,
                          m.doorNumber, m.address1, m.address2, m.address3, m.pincode, m.street,
                          m.PIDNumber, m.khathaNumber, m.surveyNumber, m.wardID, m.mohID,
                          m.licenceNumber, m.newlicenceNumber,
                          bw.wardName, bw.wardCode AS wardNo, bz.zoneName, mm.mohName,
                          tzc.zonalClassificationName
                   FROM {(submitted ? "dbo.Licence_Master" : "dbo.Licence_Master_Temp")} m
                   LEFT JOIN dbo.Master_BBMPWard bw ON bw.wardID = m.wardID
                   LEFT JOIN dbo.Master_BBMPZone bz ON bz.zoneID = bw.zoneID
                   LEFT JOIN dbo.Master_MOH mm ON mm.mohID = m.mohID
                   LEFT JOIN dbo.Trade_ZonalClassification tzc ON tzc.zonalClassificationID = m.zonalClassificationID
                   WHERE m.tradeLicenceID = @tradeLicenceId",
                new { tradeLicenceId = temp.TradeLicenceID });

            var tradeTypeName = await db.ExecuteScalarAsync<string?>(
                @"SELECT TOP 1 t.tradeTypeName FROM dbo.Licence_Application_Temp lat
                  JOIN dbo.Trade_Type t ON t.tradeTypeID = lat.tradeTypeID
                  WHERE lat.licenceApplicationID = @licenceApplicationId",
                new { licenceApplicationId });

            // 3) GBA corporation / zone / ward (draft: by temp id; submitted: promoted to the new id)
            dynamic? gba = null;
            try
            {
                gba = await db.QueryFirstOrDefaultAsync(
                    $@"SELECT TOP 1 c.corporationID, c.corporationName, c.corporationNativeName,
                             z.gbaZoneID, z.zoneName, w.gbaWardID, w.wardNo, w.wardName, w.wardNativeName,
                             w.assemblyName, loc.source
                      FROM dbo.Licence_GBALocation loc
                      JOIN dbo.Master_GBAWard w ON w.gbaWardID = loc.gbaWardID
                      JOIN dbo.Master_GBAZone z ON z.gbaZoneID = w.gbaZoneID
                      JOIN dbo.Master_Corporation c ON c.corporationID = loc.corporationID
                      WHERE {(submitted ? "loc.tradeLicenceID" : "loc.tempTradeLicenceID")} = @tradeLicenceId",
                    new { tradeLicenceId = temp.TradeLicenceID });
            }
            catch (SqlException ex) when (ex.Number == 208) { }

            // 4) Submitted: status history, and trades / documents / geo moved to the final id
            IEnumerable<dynamic> timeline = Array.Empty<dynamic>();
            if (submitted)
            {
                long finalId = Convert.ToInt64(final!.licenceApplicationID);

                var finalTrades = (await db.QueryAsync<dynamic>(
                    @"SELECT ltd.licenceTradeDetailsID, ltd.licenceApplicationID, ltd.tradeSubID,
                             sub.tradeSubName, tmc.tradeMinorID, tmc.tradeMinorName, tjc.tradeMajorID, tjc.tradeMajorName, ltd.tradeFee
                      FROM dbo.Licence_TradeDetails ltd
                      OUTER APPLY (SELECT TOP 1 tradeSubName, tradeMinorID FROM dbo.Trade_SubCategory s WHERE s.tradeSubID = ltd.tradeSubID) sub
                      LEFT JOIN dbo.Trade_MinorCategory tmc ON tmc.tradeMinorID = sub.tradeMinorID
                      LEFT JOIN dbo.Trade_MajorCategory tjc ON tjc.tradeMajorID = tmc.tradeMajorID
                      WHERE ltd.licenceApplicationID = @finalId
                      ORDER BY ltd.licenceTradeDetailsID",
                    new { finalId })).ToList();
                if (finalTrades.Count > 0)
                    trades = finalTrades;

                documents.AddRange(await db.QueryAsync<dynamic>(
                    @"SELECT lad.ApplicationDocumentID, lad.LicenceApplicationID, lad.DocumentID, md.documentName,
                             lad.FileName, lad.FilePath, lad.FileExtension, lad.FileSizeKB, lad.IsActive, lad.EntryDate
                      FROM dbo.Licence_Application_Document lad
                      LEFT JOIN dbo.Master_Document md ON md.documentID = lad.DocumentID
                      WHERE lad.LicenceApplicationID = @finalId
                      ORDER BY lad.ApplicationDocumentID",
                    new { finalId }));

                geo ??= await db.QueryFirstOrDefaultAsync(
                    @"SELECT TOP 1 GeoID, LicenceApplicationID, Latitude, Longitude, RoadID, RoadWidthMtrs,
                             RoadCategoryCode, RoadCategory, IsConfirmed, EntryDate
                      FROM dbo.Licence_Application_GeoLocation
                      WHERE LicenceApplicationID = @finalId
                      ORDER BY EntryDate DESC, GeoID DESC",
                    new { finalId });

                try
                {
                    timeline = await db.QueryAsync(
                        "usp_Licence_SubmitAction",
                        new { Mode = "GET", LicenceApplicationID = finalId },
                        commandType: CommandType.StoredProcedure);
                }
                catch (SqlException) { /* history is optional on this page */ }
            }

            // Trade_SubCategory has duplicate ids, which repeats rows from the procedure
            trades = trades.GroupBy(t => (long)t.licenceTradeDetailsID).Select(g => g.First()).ToList();

            // 5) Payments (gateway audit log: one entry per transaction, latest stage wins)
            var auditRows = await db.QueryAsync<(string TxnId, decimal Amount, string Stage, string GatewayStatus, string? Response, DateTime CreatedOn)>(
                @"SELECT TxnId, Amount, PaymentStage, GatewayStatus, ResponsePayload, CreatedOn
                  FROM dbo.Payment_Audit_Log
                  WHERE LicenceApplicationId = @licenceApplicationId AND ISNULL(IsActive, 1) = 1
                  ORDER BY CreatedOn",
                new { licenceApplicationId });

            var payments = auditRows
                .GroupBy(r => r.TxnId)
                .Select(g =>
                {
                    var last = g.OrderBy(r => r.CreatedOn).Last();
                    JObject? resp = null;
                    try { if (!string.IsNullOrWhiteSpace(last.Response)) resp = JObject.Parse(last.Response); } catch { }
                    return new
                    {
                        txnId = g.Key,
                        amount = last.Amount,
                        status = last.Stage,                // INITIATED / SUCCESS / FAILURE ...
                        paidOn = last.CreatedOn,
                        initiatedOn = g.Min(r => r.CreatedOn),
                        bankRefNo = resp?["bank_ref_num"]?.ToString(),
                        gatewayTxnId = resp?["easepayid"]?.ToString(),
                        mode = resp?["mode"]?.ToString(),
                        message = resp?["error_Message"]?.ToString()
                    };
                })
                .OrderByDescending(p => p.initiatedOn)
                .ToList();

            var totalTradeFee = trades.Sum(t => (decimal)(t.tradeFee ?? 0m));
            var paid = payments.FirstOrDefault(p => string.Equals(p.status, "SUCCESS", StringComparison.OrdinalIgnoreCase));

            return Ok(new
            {
                application,
                master,
                final,
                timeline,
                extra = new
                {
                    zoneName = master?.zoneName,
                    wardNo = master?.wardNo,
                    zonalClassificationName = master?.zonalClassificationName,
                    tradeTypeName,
                    landLineNumber = master?.landLineNumber,
                    licenceNumber = master?.licenceNumber,
                    newlicenceNumber = master?.newlicenceNumber
                },
                gba,
                trades,
                documents = documents
                    .Where(d => d.IsActive == null || d.IsActive is true || Equals(d.IsActive, "Y") || Equals(d.IsActive, 1))
                    .GroupBy(d => (long)d.ApplicationDocumentID).Select(g => g.First())
                    .ToList(),
                geo,
                payments,
                // Paid (receipt + gateway SUCCESS) but never reached the officers -> can be completed
                awaitingSubmission = !submitted && temp.ReceiptNumber != null && paid != null,
                fees = new
                {
                    totalTradeFee,
                    paymentStatus = paid != null ? "Paid" : payments.Count > 0 ? "Not completed" : "Not paid",
                    amountPaid = paid?.amount
                }
            });
        }

        // =====================================================================
        // COMPLETE SUBMISSION (paid, but the success page never ran)
        // Runs the same steps as the payment-success page; safe to call again.
        // =====================================================================
        [HttpPost("{licenceApplicationId:long}/complete-submission")]
        public async Task<IActionResult> CompleteSubmission(long licenceApplicationId)
        {
            using var db = new SqlConnection(_connStr);

            var temp = await GetTempAsync(db, licenceApplicationId);
            if (temp == null)
                return NotFound(new { Message = "Application not found." });
            if (IsTrader() && temp.LoginID != CallerId())
                return Forbid();

            if (await GetFinalAsync(db, licenceApplicationId) is { } already)
                return Ok(new { Submitted = true, AlreadySubmitted = true, ApplicationNumber = (string?)already.applicationNumber });

            if (temp.ReceiptNumber == null || !await HasSuccessfulPaymentAsync(db, licenceApplicationId))
                return BadRequest(new { Message = "Payment for this application is not completed yet." });

            // Step 1: copy the licence to Licence_Master (skipped if a previous attempt already did it)
            var inMaster = await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM dbo.Licence_Master WHERE tradeLicenceID = @id", new { id = temp.TradeLicenceID }) > 0;
            var inTemp = await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM dbo.Licence_Master_Temp WHERE tradeLicenceID = @id", new { id = temp.TradeLicenceID }) > 0;

            if (inMaster && inTemp)
                return Conflict(new { Message = "This application needs a manual check (licence id is ambiguous). Please contact support." });

            if (!inMaster)
            {
                if (!inTemp)
                    return BadRequest(new { Message = "Application details not found. Please contact support." });

                try
                {
                    var copied = await db.QueryFirstOrDefaultAsync<dynamic>(
                        "usp_TradeLicence_CRUD",
                        new { Action = "FINAL_SUBMIT", tradeLicenceID = temp.TradeLicenceID },
                        commandType: CommandType.StoredProcedure);

                    if (copied?.tradeLicenceID != null)
                        await _gba.PromoteDraftAsync(temp.TradeLicenceID, Convert.ToInt64(copied.tradeLicenceID));
                }
                catch (SqlException ex)
                {
                    return BadRequest(new { Message = "Could not submit the licence details: " + ex.Message });
                }
            }

            // Step 2: create the application for the officers (status APPLIED)
            try
            {
                var result = await db.QueryFirstOrDefaultAsync<dynamic>(
                    "usp_LicenceApplication_CRUD",
                    new { Action = "FINAL_SUBMIT", licenceApplicationID = licenceApplicationId },
                    commandType: CommandType.StoredProcedure);

                var final = await GetFinalAsync(db, licenceApplicationId);
                if (final == null)
                    return BadRequest(new { Message = "Submission did not complete. Please try again." });

                string? appNo = final.applicationNumber;
                if (result != null && (result.Submitted == true || Equals(result.Submitted, 1)))
                    await _smsNotifier.ApplicationReceivedAsync(licenceApplicationId, appNo);

                return Ok(new { Submitted = true, ApplicationNumber = appNo });
            }
            catch (SqlException ex)
            {
                return BadRequest(new { Message = "Could not submit the application: " + ex.Message });
            }
        }
    }
}
