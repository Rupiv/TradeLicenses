using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Gba.TradeLicense.Api.Services;
using Gba.TradeLicense.Application.Models;
using Gba.TradeLicense.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

[ApiController]
[Route("api/trade-licence")]
public class TradeLicenceController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly GbaLocationService _gba;

    public TradeLicenceController(IConfiguration config, GbaLocationService gba)
    {
        _config = config;
        _gba = gba;
    }

    private IDbConnection Db()
        => new SqlConnection(_config.GetConnectionString("Default"));

    // Traders: own records (or a brand-new unlinked one); officers: their GBA jurisdiction; admin: all.
    private async Task<bool> TraderMayNotAccessLicence(IDbConnection db, long tradeLicenceId)
        => !await CallerAccess.MayAccessLicenceAsync(db, User, tradeLicenceId);

    private bool TraderActsForSomeoneElse(long userId)
        => CallerAccess.IsTrader(User) && CallerAccess.CallerId(User) != userId;

    private int? LoginId()
        => int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                        ?? User.FindFirst("sub")?.Value, out var id) ? id : null;

    /// <summary>
    /// Resolves the applicant's GBA ward and fills the legacy mohID / wardID it maps to,
    /// so the existing NOT NULL columns, approver routing and reports keep working.
    /// </summary>
    private async Task<(GbaLocationService.GbaWard? Ward, string? Error)> ApplyGbaWard(TradeLicenceDto dto)
    {
        if (dto.gbaWardID == null)
            return (null, null);   // legacy client (old MOH/ward picker) - unchanged behaviour

        var ward = await _gba.GetWardAsync(dto.gbaWardID.Value);
        var error = GbaLocationService.Validate(ward);
        if (error != null)
            return (null, error);

        dto.wardID = ward!.LegacyWardID;
        dto.mohID = ward.MohID;
        return (ward, null);
    }

    [HttpPost]
    public async Task<IActionResult> CreateDraft([FromBody] TradeLicenceDto dto)
    {
        var (gbaWard, gbaError) = await ApplyGbaWard(dto);
        if (gbaError != null)
            return BadRequest(new { Message = gbaError });

        using var db = Db();

        var id = await db.ExecuteScalarAsync<long>(
            "usp_TradeLicence_CRUD",
            new
            {
                Action = "INSERT",
                dto.applicantName,
                dto.doorNumber,
                dto.address1,
                dto.address2,
                dto.address3,
                dto.pincode,
                dto.landLineNumber,
                dto.mobileNumber,
                dto.emailID,
                dto.tradeName,
                dto.zonalClassificationID,
                dto.mohID,
                dto.wardID,
                dto.PropertyID,
                dto.PIDNumber,
                dto.khathaNumber,
                dto.surveyNumber,
                dto.street,
                dto.GISNumber,
                dto.licenceNumber,
                dto.licenceCommencementDate,
                dto.licenceStatusID,
                dto.oldapplicationNumber,
                dto.newlicenceNumber
            },
            commandType: CommandType.StoredProcedure
        );

        if (gbaWard != null)
            await _gba.SaveDraftLocationAsync(id, gbaWard, LoginId());

        // Owner of the new record = the signed-in user (from the token, never from the request)
        if (CallerAccess.CallerId(User) is long creator)
            await LicenceOwnerStore.RecordAsync(db, id, creator);

        return Ok(new { tradeLicenceID = id });
    }



    [HttpGet("user/{userId}/applications")]
    public async Task<IActionResult> GetUserApplications(int userId)
    {
        if (TraderActsForSomeoneElse(userId))
            return Forbid();

        using var con = Db();

        var data = await con.QueryAsync<dynamic>(
            "sp_GetFullTradeLicenceApplication_ByUserID",
            new { UserID = userId },
            commandType: CommandType.StoredProcedure
        );

        // Fetch the account directly instead of relying on the SP's result (which only returns a
        // row when the user already has at least one linked application) - a brand new trader with
        // zero applications must still get a 200 with an empty list, not a 404.
        var me = await con.QueryFirstOrDefaultAsync<(int UserID, string FullName, string MobileNumber, string EmailID)>(
            "SELECT UserID, FullName, MobileNumber, EmailID FROM dbo.User_Login WHERE UserID = @userId",
            new { userId });

        if (me.UserID == 0)
            return NotFound("User not found");

        var user = new UserApplicationResponse
        {
            UserId = me.UserID,
            FullName = me.FullName,
            MobileNumber = me.MobileNumber,
            EmailId = me.EmailID
        };

        var groupedApplications = data
            .GroupBy(x => Convert.ToInt32(x.licenceApplicationID));

        foreach (var appGroup in groupedApplications)
        {
            var first = appGroup.First();

            var application = new ApplicationDto
            {
                LicenceApplicationID = Convert.ToInt32(first.licenceApplicationID),
                ApplicationNumber = first.applicationNumber,
                ApplicationSubmitDate = first.applicationSubmitDate,
                LicenceFromDate = first.licenceFromDate,
                LicenceToDate = first.licenceToDate,
                ApplicationStatus = first.licenceApplicationStatusName,
                CurrentStatus = first.CurrentStatusDescription,
                TradeLicenceID = Convert.ToInt32(first.tradeLicenceID),
                ApplicantName = first.applicantName,
                TradeName = first.tradeName,
                GeoLocation = first.Latitude != null ? new GeoLocationDtos
                {
                    Latitude = Convert.ToDecimal(first.Latitude),
                    Longitude = Convert.ToDecimal(first.Longitude),
                    RoadWidthMtrs = Convert.ToInt32(first.RoadWidthMtrs),
                    RoadCategory = first.RoadCategory
                } : null
            };

            foreach (var doc in appGroup.Where(x => x.ApplicationDocumentID != null))
            {
                application.Documents.Add(new DocumentDto
                {
                    DocumentID = Convert.ToInt32(doc.DocumentID),
                    DocumentName = doc.documentName,
                    FileName = doc.FileName,
                    FilePath = SafePaths.ToPublic(doc.FilePath)
                });
            }

            user.Applications.Add(application);
        }

        // Legacy bulk-migrated applications were saved with loginID = -1 (never linked to any real
        // trader account) instead of this user's loginID. Surface them by matching the applicant's
        // mobile number against this user's registered mobile number, so a trader who signs up with
        // their old number can still see their historical applications. Read-only: nothing here is
        // written back, so a false match (an old number since reassigned) only affects this view.
        if (!string.IsNullOrWhiteSpace(me.MobileNumber))
        {
            var existingIds = user.Applications.Select(a => a.LicenceApplicationID).ToHashSet();

            var orphaned = await con.QueryAsync<dynamic>(
                @"SELECT lat.licenceApplicationID, lat.applicationNumber, lat.applicationSubmitDate,
                         lat.licenceFromDate, lat.licenceToDate, las.licenceApplicationStatusName,
                         lcs.statusDecription AS CurrentStatusDescription,
                         lmt.tradeLicenceID, lmt.applicantName, lmt.tradeName
                  FROM dbo.Licence_Application_Temp lat
                  INNER JOIN dbo.Licence_Master_Temp lmt ON lmt.tradeLicenceID = lat.tradeLicenceID
                  LEFT JOIN dbo.Licence_ApplicationStatus las ON las.licenceApplicationStatusID = lat.licenceApplicationStatusID
                  LEFT JOIN dbo.Licence_CurrentStatus lcs ON lcs.licenceCurrentStatusID = lat.currentStatus
                  WHERE lat.loginID = -1 AND lmt.mobileNumber = @mobile",
                new { mobile = me.MobileNumber });

            foreach (var row in orphaned)
            {
                int id = Convert.ToInt32(row.licenceApplicationID);
                if (!existingIds.Add(id)) continue;

                user.Applications.Add(new ApplicationDto
                {
                    LicenceApplicationID = id,
                    ApplicationNumber = row.applicationNumber,
                    ApplicationSubmitDate = row.applicationSubmitDate,
                    LicenceFromDate = row.licenceFromDate,
                    LicenceToDate = row.licenceToDate,
                    ApplicationStatus = row.licenceApplicationStatusName,
                    CurrentStatus = row.CurrentStatusDescription,
                    TradeLicenceID = Convert.ToInt32(row.tradeLicenceID),
                    ApplicantName = row.applicantName,
                    TradeName = row.tradeName
                });
            }

            // Some legacy applications never had a Licence_Application_Temp row at all - they exist
            // only in the final tables and are unlinked there too (loginID = -1). Same mobile match.
            var orphanedFinal = await con.QueryAsync<dynamic>(
                @"SELECT la.licenceApplicationID, la.applicationNumber, la.applicationSubmitDate,
                         la.licenceFromDate, la.licenceToDate,
                         COALESCE(mp.licenceProcessName, las.licenceApplicationStatusName) AS StatusName,
                         lcs.statusDecription AS CurrentStatusDescription,
                         m.tradeLicenceID, m.applicantName, m.tradeName
                  FROM dbo.Licence_Application la
                  LEFT JOIN dbo.Licence_Master m ON m.tradeLicenceID = la.tradeLicenceID
                  LEFT JOIN dbo.Master_LicenceProcess mp ON mp.licenceProcessID = la.licenceApplicationStatusID
                  LEFT JOIN dbo.Licence_ApplicationStatus las ON las.licenceApplicationStatusID = la.licenceApplicationStatusID
                  LEFT JOIN dbo.Licence_CurrentStatus lcs ON lcs.licenceCurrentStatusID = la.currentStatus
                  WHERE la.loginID = -1 AND m.mobileNumber = @mobile",
                new { mobile = me.MobileNumber });

            foreach (var row in orphanedFinal)
            {
                int id = Convert.ToInt32(row.licenceApplicationID);
                if (!existingIds.Add(id)) continue;

                user.Applications.Add(new ApplicationDto
                {
                    LicenceApplicationID = id,
                    ApplicationNumber = row.applicationNumber,
                    ApplicationSubmitDate = row.applicationSubmitDate,
                    LicenceFromDate = row.licenceFromDate,
                    LicenceToDate = row.licenceToDate,
                    ApplicationStatus = row.StatusName,
                    CurrentStatus = row.CurrentStatusDescription,
                    TradeLicenceID = row.tradeLicenceID != null ? Convert.ToInt32(row.tradeLicenceID) : 0,
                    ApplicantName = row.applicantName,
                    TradeName = row.tradeName
                });
            }
        }

        if (user.Applications.Count == 0)
            return Ok(user);

        // Submitted applications move on in Licence_Application (same tradeLicenceID) while the
        // temp row stays APPLIED -> show the real, current status and application number.
        // (After submission tradeLicenceID points to Licence_Master, so names come from there.)
        var allIds = user.Applications.Select(a => (long)a.LicenceApplicationID).ToList();
        var finals = (await con.QueryAsync<(long TempId, string? AppNo, string? Status, string? ApplicantName, string? TradeName, int? TradeLicenceId)>(
            @"SELECT t.licenceApplicationID, f.applicationNumber, f.statusName, m.applicantName, m.tradeName, m.tradeLicenceID
              FROM dbo.Licence_Application_Temp t
              CROSS APPLY (
                  SELECT TOP 1 la.applicationNumber,
                         COALESCE(mp.licenceProcessName, las.licenceApplicationStatusName) AS statusName
                  FROM dbo.Licence_Application la
                  LEFT JOIN dbo.Master_LicenceProcess mp ON mp.licenceProcessID = la.licenceApplicationStatusID
                  LEFT JOIN dbo.Licence_ApplicationStatus las ON las.licenceApplicationStatusID = la.licenceApplicationStatusID
                  WHERE la.tradeLicenceID = t.tradeLicenceID
                  ORDER BY la.licenceApplicationID DESC
              ) f
              LEFT JOIN dbo.Licence_Master m ON m.tradeLicenceID = t.tradeLicenceID
              WHERE t.licenceApplicationID IN @allIds",
            new { allIds }))
            .ToDictionary(x => x.TempId);

        // Paid (receipt + gateway SUCCESS) but no Licence_Application yet -> "Complete Submission"
        var awaiting = (await con.QueryAsync<long>(
            @"SELECT t.licenceApplicationID
              FROM dbo.Licence_Application_Temp t
              WHERE t.loginID = @userId AND t.receiptNumber IS NOT NULL
                AND NOT EXISTS (SELECT 1 FROM dbo.Licence_Application la WHERE la.tradeLicenceID = t.tradeLicenceID)
                AND EXISTS (SELECT 1 FROM dbo.Payment_Audit_Log p
                            WHERE p.LicenceApplicationId = t.licenceApplicationID AND p.PaymentStage = 'SUCCESS'
                              AND ISNULL(p.IsActive, 1) = 1)",
            new { userId })).ToHashSet();

        foreach (var app in user.Applications)
        {
            app.AwaitingSubmission = awaiting.Contains(app.LicenceApplicationID);
            if (!finals.TryGetValue(app.LicenceApplicationID, out var f)) continue;
            if (!string.IsNullOrWhiteSpace(f.Status)) app.ApplicationStatus = f.Status.Trim();
            if (!string.IsNullOrWhiteSpace(f.AppNo)) app.ApplicationNumber = f.AppNo;
            if (!string.IsNullOrWhiteSpace(f.ApplicantName)) app.ApplicantName = f.ApplicantName;
            if (!string.IsNullOrWhiteSpace(f.TradeName)) app.TradeName = f.TradeName;
            if (app.TradeLicenceID == 0 && f.TradeLicenceId is int tl) app.TradeLicenceID = tl;
        }

        return Ok(user);
    }



    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateDraft(long id, [FromBody] TradeLicenceDto dto)
    {
        var (gbaWard, gbaError) = await ApplyGbaWard(dto);
        if (gbaError != null)
            return BadRequest(new { Message = gbaError });

        using var db = Db();
        if (await TraderMayNotAccessLicence(db, id))
            return Forbid();

        await db.ExecuteAsync(
            "usp_TradeLicence_CRUD",
            new
            {
                Action = "UPDATE",
                tradeLicenceID = id,
                dto.applicantName,
                dto.doorNumber,
                dto.address1,
                dto.address2,
                dto.address3,
                dto.pincode,
                dto.landLineNumber,
                dto.mobileNumber,
                dto.emailID,
                dto.tradeName,
                dto.zonalClassificationID,
                dto.mohID,
                dto.wardID,
                dto.PropertyID,
                dto.PIDNumber,
                dto.khathaNumber,
                dto.surveyNumber,
                dto.street,
                dto.GISNumber,
                dto.licenceNumber,
                dto.licenceCommencementDate,
                dto.licenceStatusID,
                dto.oldapplicationNumber,
                dto.newlicenceNumber
            },
            commandType: CommandType.StoredProcedure
        );

        if (gbaWard != null)
            await _gba.SaveDraftLocationAsync(id, gbaWard, LoginId());

        return Ok(new { Updated = true });
    }


    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(long id)
    {
        using var db = Db();
        if (await TraderMayNotAccessLicence(db, id))
            return Forbid();

        var data = await db.QueryAsync(
            "usp_TradeLicence_CRUD",
            new
            {
                Action = "GET_BY_ID",
                tradeLicenceID = id
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(data);
    }


    [HttpGet("search")]
    [StaffOnly]
    public async Task<IActionResult> Search([FromQuery] string? text)
    {
        using var db = Db();

        var data = await db.QueryAsync(
            "usp_TradeLicence_CRUD",
            new
            {
                Action = "SEARCH",
                searchText = text
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(data);
    }


    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDraft(long id)
    {
        using var db = Db();
        if (await TraderMayNotAccessLicence(db, id))
            return Forbid();

        await db.ExecuteAsync(
            "usp_TradeLicence_CRUD",
            new
            {
                Action = "DELETE_TEMP",
                tradeLicenceID = id
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { Deleted = true });
    }
    [HttpGet("user/{userId}/profile")]
    public async Task<IActionResult> GetUserWithLicence(int userId)
    {
        if (TraderActsForSomeoneElse(userId))
            return Forbid();

        using var db = Db();

        var data = await db.QueryAsync(
            "usp_GetUserWithLicenceDetails",
            new { UserID = userId },
            commandType: CommandType.StoredProcedure
        );

        if (!data.Any())
            return NotFound(new { Message = "User not found" });

        return Ok(data);
    }


    [HttpPost("{id}/submit")]
    public async Task<IActionResult> FinalSubmit(long id)
    {
        using var db = Db();
        if (await TraderMayNotAccessLicence(db, id))
            return Forbid();

        // Payment-before-submission: the draft application of this licence must have a
        // gateway-verified payment (logged by payment/payment-success) before it is submitted.
        var paid = await db.ExecuteScalarAsync<int>(
            @"SELECT COUNT(1)
              FROM dbo.Licence_Application_Temp t
              JOIN dbo.Payment_Audit_Log p ON p.LicenceApplicationId = t.licenceApplicationID
              WHERE t.tradeLicenceID = @id AND p.PaymentStage = 'SUCCESS' AND ISNULL(p.IsActive, 1) = 1",
            new { id });
        if (paid == 0)
            return BadRequest(new { Submitted = false, Error = "Payment for this application is not completed yet." });

        try
        {
            var result = await db.QueryFirstOrDefaultAsync<dynamic>(
                "usp_TradeLicence_CRUD",
                new
                {
                    Action = "FINAL_SUBMIT",
                    tradeLicenceID = id
                },
                commandType: CommandType.StoredProcedure
            );

            // Draft moved to Licence_Master under a new id: carry its GBA ward across
            if (result?.tradeLicenceID != null)
                await _gba.PromoteDraftAsync(id, Convert.ToInt64(result.tradeLicenceID));

            return Ok(new
            {
                Submitted = result?.Submitted ?? false,
                TradeLicenceID = result?.tradeLicenceID
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                Submitted = false,
                Error = CallerAccess.SafeDbMessage(ex, "The licence details could not be submitted. Please try again.")
            });
        }
    }

}

