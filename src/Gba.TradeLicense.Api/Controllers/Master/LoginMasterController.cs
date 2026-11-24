using System.Data;
using Dapper;
using Gba.TradeLicense.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

[ApiController]
[Gba.TradeLicense.Api.Services.StaffOnly]
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

    // Never send stored passwords (or hashes) to the browser; the edit form clears it anyway.
    private static object? WithoutPassword(object? row)
    {
        if (row is IDictionary<string, object> dict)
        {
            foreach (var key in dict.Keys.Where(k => k.Contains("password", StringComparison.OrdinalIgnoreCase)).ToList())
                dict.Remove(key);
        }
        return row;
    }

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
    [HttpGet]
    public async Task<IActionResult> GetAll(
     int pageNumber = 1,
     int pageSize = 10)
    {
        using var db = Db();

        using var multi = await db.QueryMultipleAsync(
            "usp_LoginMaster_CRUD",
            new
            {
                Action = "GETALL",
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            commandType: CommandType.StoredProcedure
        );

        // 1️⃣ Total Records
        var totalRecords = await multi.ReadFirstAsync<int>();

        // 2️⃣ Paged Data
        var users = (await multi.ReadAsync()).Select(u => WithoutPassword((object?)u)).ToList();

        return Ok(new
        {
            pageNumber,
            pageSize,
            totalRecords,
            totalPages = (int)Math.Ceiling((double)totalRecords / pageSize),
            data = users
        });
    }


    /* ================= UPDATE ================= */
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] LoginMasterDto dto)
    {
        using var db = Db();

        // The form says "leave blank to keep current", but the procedure always writes @password.
        // Blank -> pass the stored password through unchanged so the user is not locked out.
        var password = dto.Password;
        if (string.IsNullOrWhiteSpace(password))
        {
            password = await db.ExecuteScalarAsync<string?>(
                "SELECT ISNULL(password, '') FROM dbo.Login_Master WHERE loginID = @id", new { id });
            if (password == null)
                return NotFound(new { Message = "User not found." });
        }

        await db.ExecuteAsync(
            "usp_LoginMaster_CRUD",
            new
            {
                Action = "UPDATE",
                loginID = id,
                dto.Login,
                Password = password,
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

        return Ok(WithoutPassword((object?)data));
    }

    /* ================= SEARCH ================= */
    [HttpGet("search")]
    public async Task<IActionResult> Search(
      [FromQuery] string? q,
      [FromQuery] int pageNumber = 1,
      [FromQuery] int pageSize = 10
  )
    {
        using var db = Db();

        var parameters = new DynamicParameters();
        parameters.Add("@Action", "SEARCH");
        parameters.Add("@searchText", q);
        parameters.Add("@PageNumber", pageNumber);
        parameters.Add("@PageSize", pageSize);

        int totalRecords = 0;
        // Same rows as the list (GETALL): LoginMasterDto has no loginID, so edit / delete from
        // search results used id 0, and it carried the password back to the browser.
        var users = new List<object?>();

        using (var multi = await db.QueryMultipleAsync(
            "usp_LoginMaster_CRUD",
            parameters,
            commandType: CommandType.StoredProcedure
        ))
        {
            /* ========= FIRST RESULT SET → TOTAL COUNT ========= */
            totalRecords = await multi.ReadFirstAsync<int>();

            /* ========= SECOND RESULT SET → DATA ========= */
            users = (await multi.ReadAsync()).Select(u => WithoutPassword((object?)u)).ToList();
        }

        return Ok(new
        {
            totalRecords,
            pageNumber,
            pageSize,
            data = users
        });
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
