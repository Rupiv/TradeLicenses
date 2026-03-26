using System.Data;
using Gba.TradeLicense.Application.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;

[ApiController]
[Route("api/master/trade-major")]
public class TradeMajorController : ControllerBase
{
    private readonly IConfiguration _config;

    public TradeMajorController(IConfiguration config)
    {
        _config = config;
    }

    private IDbConnection Db()
        => new SqlConnection(_config.GetConnectionString("Default"));

    /* =========================================
       GET ALL
    ========================================= */
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        using var db = Db();

        var data = await db.QueryAsync<TradeMajorDto>(
            "usp_TradeMajor_CRUD",
            new { Action = "GET" },
            commandType: CommandType.StoredProcedure);

        return Ok(data);
    }

    /* =========================================
       INSERT
       tradeMajorCode will be auto-generated
    ========================================= */
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] TradeMajorDto dto)
    {
        using var db = Db();

        var result = await db.QueryFirstAsync<dynamic>(
            "usp_TradeMajor_CRUD",
            new
            {
                Action = "INSERT",
                tradeMajorName = dto.TradeMajorName,
                tradeMajorNativeName = dto.TradeMajorNativeName
            },
            commandType: CommandType.StoredProcedure);

        return Ok(result);
    }

    /* =========================================
       UPDATE
       tradeMajorCode not editable
    ========================================= */
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] TradeMajorDto dto)
    {
        using var db = Db();

        var result = await db.QueryFirstAsync<dynamic>(
            "usp_TradeMajor_CRUD",
            new
            {
                Action = "UPDATE",
                tradeMajorID = id,
                tradeMajorName = dto.TradeMajorName,
                tradeMajorNativeName = dto.TradeMajorNativeName
            },
            commandType: CommandType.StoredProcedure);

        return Ok(result);
    }

    /* =========================================
       DELETE (SOFT DELETE)
    ========================================= */
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        using var db = Db();

        var result = await db.QueryFirstAsync<dynamic>(
            "usp_TradeMajor_CRUD",
            new
            {
                Action = "DELETE",
                tradeMajorID = id
            },
            commandType: CommandType.StoredProcedure);

        return Ok(result);
    }
}