using System.Data;
using Dapper;
using Gba.TradeLicense.Application.Models;
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

