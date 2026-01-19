using System.Data;
using Dapper;
using Gba.TradeLicense.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

[ApiController]
[Route("api/office-details")]
public class OfficeDetailsController : ControllerBase
{
    private readonly IConfiguration _config;

    public OfficeDetailsController(IConfiguration config)
    {
        _config = config;
    }

    private IDbConnection Db()
        => new SqlConnection(_config.GetConnectionString("Default"));

    /* ================= CREATE ================= */
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] OfficeDetailsDto dto)
    {
        using var db = Db();

        var id = await db.ExecuteScalarAsync<int>(
            "usp_MasterOfficeDetails_CRUD",
            new
            {
                Action = "INSERT",
                dto.OfficeName,
                dto.OfficeID,
                dto.OfficeTypeID,
                dto.BranchAddress,
                dto.ContactPerson,
                dto.EmailID,
                dto.ContactNO
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { officeDetailsID = id });
    }

    /* ================= UPDATE ================= */
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] OfficeDetailsDto dto)
    {
        using var db = Db();

        await db.ExecuteAsync(
            "usp_MasterOfficeDetails_CRUD",
            new
            {
                Action = "UPDATE",
                officeDetailsID = id,
                dto.OfficeName,
                dto.OfficeID,
                dto.OfficeTypeID,
                dto.BranchAddress,
                dto.ContactPerson,
                dto.EmailID,
                dto.ContactNO
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { Updated = true });
    }

    /* ================= GET BY ID ================= */
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        using var db = Db();

        var data = await db.QueryFirstOrDefaultAsync(
            "usp_MasterOfficeDetails_CRUD",
            new
            {
                Action = "GET_BY_ID",
                officeDetailsID = id
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(data);
    }

    /* ================= SEARCH ================= */
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q)
    {
        using var db = Db();

        var data = await db.QueryAsync(
            "usp_MasterOfficeDetails_CRUD",
            new
            {
                Action = "SEARCH",
                searchText = q
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(data);
    }

    /* ================= DELETE ================= */
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        using var db = Db();

        await db.ExecuteAsync(
            "usp_MasterOfficeDetails_CRUD",
            new
            {
                Action = "DELETE",
                officeDetailsID = id
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { Deleted = true });
    }
}
