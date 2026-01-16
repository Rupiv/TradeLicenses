using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Gba.TradeLicense.Application.Models;

[ApiController]
[Route("api/licence-application")]
public class LicenceApplicationController : ControllerBase
{
    private readonly IConfiguration _config;

    public LicenceApplicationController(IConfiguration config)
    {
        _config = config;
    }

    private IDbConnection CreateConnection()
        => new SqlConnection(_config.GetConnectionString("Default"));

    // ================= INSERT DRAFT =================
    [HttpPost("draft")]
    public async Task<IActionResult> InsertDraft(
        [FromBody] LicenceApplicationUpsertDto dto,
        CancellationToken ct)
    {
        using var db = CreateConnection();

        var id = await db.ExecuteScalarAsync<long>(
            "usp_LicenceApplication_CRUD",
            new
            {
                Action = "INSERT",
                dto.FinanicalYearID,
                dto.TradeTypeID,
                dto.BescomRRNumber,
                dto.TINNumber,
                dto.VATNumber,
                dto.LicenceFromDate,
                dto.LicenceToDate,
                dto.TradeLicenceID,          // from Licence_Master
                dto.LoginID,
                dto.EntryOriginLoginID,
                dto.InspectingOfficerID,
                dto.LicenseType,
                dto.ApplicantRepersenting,
                dto.JathaStatus,
                dto.DocsSubmitted,
                dto.ChallanNo,
                dto.NoOfYearsApplied
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { LicenceApplicationID = id });
    }

    // ================= UPDATE DRAFT =================
    [HttpPut("draft/{id:long}")]
    public async Task<IActionResult> UpdateDraft(
        long id,
        [FromBody] LicenceApplicationUpsertDto dto,
        CancellationToken ct)
    {
        using var db = CreateConnection();

        await db.ExecuteAsync(
            "usp_LicenceApplication_CRUD",
            new
            {
                Action = "UPDATE",
                LicenceApplicationID = id,
                dto.TradeTypeID,
                dto.BescomRRNumber,
                dto.TINNumber,
                dto.VATNumber,
                dto.LicenceFromDate,
                dto.LicenceToDate,
                dto.InspectingOfficerID,
                dto.LicenseType,
                dto.ApplicantRepersenting,
                dto.JathaStatus,
                dto.DocsSubmitted,
                dto.ChallanNo,
                dto.NoOfYearsApplied
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { Updated = true });
    }

    // ================= GET BY ID =================
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        using var db = CreateConnection();

        var result = await db.QueryFirstOrDefaultAsync<dynamic>(
            "usp_LicenceApplication_CRUD",
            new
            {
                Action = "GET_BY_ID",
                LicenceApplicationID = id
            },
            commandType: CommandType.StoredProcedure
        );

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    // ================= SEARCH =================
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? q,
        CancellationToken ct)
    {
        using var db = CreateConnection();

        var list = await db.QueryAsync<dynamic>(
            "usp_LicenceApplication_CRUD",
            new
            {
                Action = "SEARCH",
                SearchText = q
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(list);
    }

    // ================= DELETE DRAFT =================
    [HttpDelete("draft/{id:long}")]
    public async Task<IActionResult> DeleteDraft(long id, CancellationToken ct)
    {
        using var db = CreateConnection();

        await db.ExecuteAsync(
            "usp_LicenceApplication_CRUD",
            new
            {
                Action = "DELETE_TEMP",
                LicenceApplicationID = id
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { Deleted = true });
    }

    // ================= FINAL SUBMIT =================
    [HttpPost("submit/{id:long}")]
    public async Task<IActionResult> FinalSubmit(long id, CancellationToken ct)
    {
        using var db = CreateConnection();

        // SP now RETURNS ApplicationNumber
        var result = await db.QuerySingleAsync<dynamic>(
            "usp_LicenceApplication_CRUD",
            new
            {
                Action = "FINAL_SUBMIT",
                LicenceApplicationID = id
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new
        {
            Submitted = true,
            ApplicationNumber = result.ApplicationNumber
        });
    }
}
