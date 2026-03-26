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

    private IDbConnection Db()
        => new SqlConnection(_config.GetConnectionString("Default"));

    //---------------------------------------------
    // GET ALL
    //---------------------------------------------
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        using var db = Db();

        var data = await db.QueryAsync<TradeMinorDto>(
            "usp_TradeMinor_CRUD",
            new { Action = "GET" },
            commandType: CommandType.StoredProcedure
        );

        return Ok(data);
    }

    //---------------------------------------------
    // GET BY MAJOR (dropdown cascading)
    //---------------------------------------------
    [HttpGet("by-major/{tradeMajorID}")]
    public async Task<IActionResult> GetByMajor(int tradeMajorID)
    {
        using var db = Db();

        var data = await db.QueryAsync<TradeMinorDto>(
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

    //---------------------------------------------
    // INSERT
    //---------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] TradeMinorDto dto)
    {
        try
        {
            using var db = Db();

            var result = await db.QueryFirstAsync<dynamic>(
                "usp_TradeMinor_CRUD",
                new
                {
                    Action = "INSERT",
                    tradeMajorID = dto.TradeMajorID,
                    tradeMinorName = dto.TradeMinorName,
                    tradeMinorNativeName = dto.TradeMinorNativeName
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

    //---------------------------------------------
    // UPDATE
    //---------------------------------------------
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] TradeMinorDto dto)
    {
        try
        {
            using var db = Db();

            var result = await db.QueryFirstAsync<dynamic>(
                "usp_TradeMinor_CRUD",
                new
                {
                    Action = "UPDATE",
                    tradeMinorID = id,
                    tradeMinorName = dto.TradeMinorName,
                    tradeMinorNativeName = dto.TradeMinorNativeName
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

    //---------------------------------------------
    // DELETE
    //---------------------------------------------
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        using var db = Db();

        var result = await db.QueryFirstAsync<dynamic>(
            "usp_TradeMinor_CRUD",
            new
            {
                Action = "DELETE",
                tradeMinorID = id
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(result);
    }
}