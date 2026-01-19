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

                BescomRRNumber = dto.BescomRRNumber ?? "",
                TINNumber = dto.TINNumber ?? "",   // NOT NULL column
                VATNumber = dto.VATNumber ?? "",

                dto.LicenceFromDate,
                dto.LicenceToDate,

                dto.TradeLicenceID,
                dto.MohID,                     // ✅ REQUIRED (MISSING EARLIER)

                dto.LoginID,
                dto.EntryOriginLoginID,
                dto.InspectingOfficerID,

                LicenseType = dto.LicenseType ?? "",
                dto.ApplicantRepersenting,
                JathaStatus = dto.JathaStatus ?? "",
                dto.DocsSubmitted,
                ChallanNo = dto.ChallanNo ?? "",
                dto.NoOfYearsApplied
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { LicenceApplicationID = id });
    }
    [HttpGet("by-login/{loginId:int}")]
    public async Task<IActionResult> GetByLogin(
       int loginId,
       [FromQuery] int pageNumber = 1,
       [FromQuery] int pageSize = 10)
    {
        using var db = CreateConnection();

        using var multi = await db.QueryMultipleAsync(
            "usp_LicenceApplication_GetByLogin_Paged",
            new
            {
                LoginID = loginId,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            commandType: CommandType.StoredProcedure
        );

        var totalRecords = await multi.ReadFirstAsync<int>();
        var data = (await multi.ReadAsync()).ToList();

        return Ok(new
        {
            TotalRecords = totalRecords,
            PageNumber = pageNumber,
            PageSize = pageSize,
            Data = data
        });
    }
    [HttpGet("paged")]
    public async Task<IActionResult> GetAllApplicationsPaged(
    [FromQuery] int pageNumber = 1,
    [FromQuery] int pageSize = 10)
    {
        using var db = CreateConnection();

        using var multi = await db.QueryMultipleAsync(
            "usp_LicenceApplication_GetAll_Paged",
            new
            {
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            commandType: CommandType.StoredProcedure
        );

        var totalRecords = await multi.ReadFirstAsync<int>();
        var applications = await multi.ReadAsync();

        return Ok(new
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = totalRecords,
            TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
            Data = applications
        });
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

                BescomRRNumber = dto.BescomRRNumber ?? "",
                TINNumber = dto.TINNumber ?? "",
                VATNumber = dto.VATNumber ?? "",

                dto.LicenceFromDate,
                dto.LicenceToDate,

                dto.InspectingOfficerID,
                LicenseType = dto.LicenseType ?? "",
                dto.ApplicantRepersenting,
                JathaStatus = dto.JathaStatus ?? "",
                dto.DocsSubmitted,
                ChallanNo = dto.ChallanNo ?? "",
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
