using System.Data;
using System.Threading.Tasks;
using Dapper;
using Gba.TradeLicense.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

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
    [HttpGet("api/get-by-id")]
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
    /* ================= GET BY ID ================= */
    [HttpGet("api/getall")]
    public async Task<IActionResult> GetAll()
    {
        using var db = Db();

        var list = await db.QueryAsync<OfficeDetailsDto>(
            "usp_MasterOfficeDetails_CRUD",
            new { Action = "GETALL" },
            commandType: CommandType.StoredProcedure
        );

        return Ok(list);
    }
    [HttpGet("api/get-all-user-designation")]
    public async Task<IActionResult> GetAllUserDesig()
    {
        using var db = Db();

        var list = await db.QueryAsync<userd>(
            "usp_MasterOfficeDetails_CRUD",
            new { Action = "GETALLUSERD" },
            commandType: CommandType.StoredProcedure
        );

        return Ok(list);
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
