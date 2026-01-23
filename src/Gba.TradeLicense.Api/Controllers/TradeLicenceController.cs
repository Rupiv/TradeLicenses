using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
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

    public TradeLicenceController(IConfiguration config)
    {
        _config = config;
    }

    private IDbConnection Db()
        => new SqlConnection(_config.GetConnectionString("Default"));

    [HttpPost]
    public async Task<IActionResult> CreateDraft([FromBody] TradeLicenceDto dto)
    {
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

        return Ok(new { tradeLicenceID = id });
    }



    [HttpGet("user/{userId}/applications")]
    public async Task<IActionResult> GetUserApplications(int userId)
    {
        using var con = Db();

        var data = await con.QueryAsync<dynamic>(
            "sp_GetFullTradeLicenceApplication_ByUserID",
            new { UserID = userId },
            commandType: CommandType.StoredProcedure
        );

        if (!data.Any())
            return NotFound("No applications found");

        var firstRow = data.First();

        var user = new UserApplicationResponse
        {
            UserId = userId,
            FullName = firstRow.FullName,
            MobileNumber = firstRow.UserMobile,
            EmailId = firstRow.UserEmail
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
                    FilePath = doc.FilePath
                });
            }

            user.Applications.Add(application);
        }

        return Ok(user);
    }



    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateDraft(long id, [FromBody] TradeLicenceDto dto)
    {
        using var db = Db();

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

        return Ok(new { Updated = true });
    }


    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(long id)
    {
        using var db = Db();

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

        await db.ExecuteAsync(
            "usp_TradeLicence_CRUD",
            new
            {
                Action = "FINAL_SUBMIT",
                tradeLicenceID = id
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { Submitted = true });
    }
}

