using System.Data;
using Gba.TradeLicense.Application.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;

[ApiController]
[Route("api/master/trade-sub")]
public class TradeSubController : ControllerBase
{
    private readonly IConfiguration _config;

    public TradeSubController(IConfiguration config)
    {
        _config = config;
    }

    private IDbConnection Db() =>
        new SqlConnection(_config.GetConnectionString("Default"));

    // 🔹 GET BY MINOR (for cascading dropdown)
    [HttpGet("by-minor/{tradeMinorID}")]
    public async Task<IActionResult> GetByMinor(int tradeMinorID)
    {
        using var db = Db();

        var data = await db.QueryAsync(
            "usp_TradeSub_CRUD",
            new
            {
                Action = "GET_BY_MINOR",
                tradeMinorID
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(data);
    }

    // 🔹 INSERT
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] TradeSubDto dto)
    {
        using var db = Db();

        var id = await db.ExecuteScalarAsync<int>(
            "usp_TradeSub_CRUD",
            new
            {
                Action = "INSERT",
                dto.TradeSubID,
                dto.TradeSubCode,
                dto.TradeSubName,
                dto.TradeSubNativeName,
                dto.BlockPeriodID
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { Success = true, NewID = id });
    }

    // 🔹 UPDATE
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] TradeSubDto dto)
    {
        using var db = Db();

        await db.ExecuteAsync(
            "usp_TradeSub_CRUD",
            new
            {
                Action = "UPDATE",
                tradeSubID = id,
                dto.TradeSubCode,
                dto.TradeSubName,
                dto.TradeSubNativeName,
                dto.BlockPeriodID
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
            "usp_TradeSub_CRUD",
            new
            {
                Action = "DELETE",
                tradeSubID = id
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { Success = true });
    }
}
