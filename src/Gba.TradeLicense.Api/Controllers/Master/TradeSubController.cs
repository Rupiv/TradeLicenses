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

    private IDbConnection Db()
        => new SqlConnection(_config.GetConnectionString("Default"));

    //-----------------------------------------
    // GET BY MINOR
    //-----------------------------------------
    [HttpGet("by-minor/{tradeMinorID}")]
    public async Task<IActionResult> GetByMinor(int tradeMinorID)
    {
        using var db = Db();

        var data = await db.QueryAsync<TradeSubDto>(
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

    //-----------------------------------------
    // INSERT
    //-----------------------------------------
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] TradeSubDto dto)
    {
        try
        {
            using var db = Db();

            var result = await db.QueryFirstAsync<dynamic>(
                "usp_TradeSub_CRUD",
                new
                {
                    Action = "INSERT",
                    tradeMinorID = dto.TradeMinorID,
                    tradeSubName = dto.TradeSubName,
                    tradeSubNativeName = dto.TradeSubNativeName,
                    blockPeriodID = dto.BlockPeriodID
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(result);
        }
        catch (SqlException ex)
        {
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    //-----------------------------------------
    // UPDATE
    //-----------------------------------------
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] TradeSubDto dto)
    {
        try
        {
            using var db = Db();

            var result = await db.QueryFirstAsync<dynamic>(
                "usp_TradeSub_CRUD",
                new
                {
                    Action = "UPDATE",
                    tradeSubID = id,
                    tradeSubName = dto.TradeSubName,
                    tradeSubNativeName = dto.TradeSubNativeName,
                    blockPeriodID = dto.BlockPeriodID
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(result);
        }
        catch (SqlException ex)
        {
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    //-----------------------------------------
    // DELETE
    //-----------------------------------------
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        using var db = Db();

        var result = await db.QueryFirstAsync<dynamic>(
            "usp_TradeSub_CRUD",
            new
            {
                Action = "DELETE",
                tradeSubID = id
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(result);
    }
}