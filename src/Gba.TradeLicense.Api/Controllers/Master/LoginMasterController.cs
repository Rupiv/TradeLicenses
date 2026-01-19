using System.Data;
using Dapper;
using Gba.TradeLicense.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

[ApiController]
[Route("api/login-master")]
public class LoginMasterController : ControllerBase
{
    private readonly IConfiguration _config;

    public LoginMasterController(IConfiguration config)
    {
        _config = config;
    }

    private IDbConnection Db()
        => new SqlConnection(_config.GetConnectionString("Default"));

    /* ================= CREATE ================= */
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] LoginMasterDto dto)
    {
        using var db = Db();

        var id = await db.ExecuteScalarAsync<int>(
            "usp_LoginMaster_CRUD",
            new
            {
                Action = "INSERT",
                dto.Login,
                dto.Password,
                dto.OfficeDetailsID,
                dto.UserDesignationID,
                dto.SakalaDO_Code,
                dto.MobileNo
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { loginID = id });
    }

    /* ================= UPDATE ================= */
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] LoginMasterDto dto)
    {
        using var db = Db();

        await db.ExecuteAsync(
            "usp_LoginMaster_CRUD",
            new
            {
                Action = "UPDATE",
                loginID = id,
                dto.Login,
                dto.Password,
                dto.OfficeDetailsID,
                dto.UserDesignationID,
                dto.SakalaDO_Code,
                dto.MobileNo,
                dto.UpdatedBy
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
            "usp_LoginMaster_CRUD",
            new
            {
                Action = "GET_BY_ID",
                loginID = id
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
            "usp_LoginMaster_CRUD",
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
    public async Task<IActionResult> Delete(int id, [FromQuery] int updatedBy)
    {
        using var db = Db();

        await db.ExecuteAsync(
            "usp_LoginMaster_CRUD",
            new
            {
                Action = "DELETE",
                loginID = id,
                updatedby = updatedBy
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { Deleted = true });
    }
}
