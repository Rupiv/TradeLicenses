using System.Data;
using Gba.TradeLicense.Application.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;

[ApiController]
[Route("api/master/trade-minor")]
public class TradeMinorController : ControllerBase
{
    private readonly IConfiguration _config;

    public TradeMinorController(IConfiguration config)
    {
        _config = config;
    }

    private IDbConnection Db() =>
        new SqlConnection(_config.GetConnectionString("Default"));

    // 🔹 GET ALL (optional – admin screens)
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        using var db = Db();

        var data = await db.QueryAsync(
            "usp_TradeMinor_CRUD",
            new { Action = "GET" },
            commandType: CommandType.StoredProcedure
        );

        return Ok(data);
    }

    // 🔹 GET BY MAJOR (for cascading dropdown)
    [HttpGet("by-major/{tradeMajorID}")]
    public async Task<IActionResult> GetByMajor(int tradeMajorID)
    {
        using var db = Db();

        var data = await db.QueryAsync(
            "usp_TradeMinor_CRUD",
            new
            {
                Action = "GET_BY_MAJOR",
                tradeMajorID
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(data);
    }

    // 🔹 INSERT
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] TradeMinorDto dto)
    {
        using var db = Db();

        var id = await db.ExecuteScalarAsync<int>(
            "usp_TradeMinor_CRUD",
            new
            {
                Action = "INSERT",
                dto.TradeMinorID,
                dto.TradeMinorCode,
                dto.TradeMinorName,
                dto.TradeMinorNativeName
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { Success = true, NewID = id });
    }

    // 🔹 UPDATE
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] TradeMinorDto dto)
    {
        using var db = Db();

        await db.ExecuteAsync(
            "usp_TradeMinor_CRUD",
            new
            {
                Action = "UPDATE",
                tradeMinorID = id,
                dto.TradeMinorCode,
                dto.TradeMinorName,
                dto.TradeMinorNativeName
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { Success = true });
    }

    // 🔹 DELETE (SOFT)
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        using var db = Db();

        await db.ExecuteAsync(
            "usp_TradeMinor_CRUD",
            new
            {
                Action = "DELETE",
                tradeMinorID = id
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { Success = true });
    }
}
