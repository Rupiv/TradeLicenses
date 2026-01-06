using System.Data;
using Dapper;
using Gba.TradeLicense.Application.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

[ApiController]
[Route("api/trade-type")]
public class TradeTypeController : ControllerBase
{
    private readonly IConfiguration _config;

    public TradeTypeController(IConfiguration config)
    {
        _config = config;
    }

    private IDbConnection CreateConnection()
        => new SqlConnection(_config.GetConnectionString("Default"));

    /* ================= GET ================= */
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        using var db = CreateConnection();

        var result = await db.QueryAsync<TradeTypeDto>(
            "usp_TradeType_CRUD",
            new { Action = "SELECT" },
            commandType: CommandType.StoredProcedure
        );

        return Ok(result);
    }
        [HttpGet("sub/{tradeMinorID}")]
    public async Task<IActionResult> GetSubTrades(int tradeMinorID)
    {
        using var db = CreateConnection();

        var data = await db.QueryAsync<TradeSubDto>(
            "usp_TradeSub_ByMinor",
            new { tradeMinorID },
            commandType: CommandType.StoredProcedure
        );

        return Ok(data);
    }

    [HttpGet("major")]
    public async Task<IActionResult> GetMajorTrades()
    {
        using var db = CreateConnection();

        var data = await db.QueryAsync<TradeMajorDto>(
            "usp_TradeMajor_GetAll",
            commandType: CommandType.StoredProcedure
        );

        return Ok(data);
    }

    [HttpGet("minor/{tradeMajorID}")]
    public async Task<IActionResult> GetMinorTrades(int tradeMajorID)
    {
        using var db = CreateConnection();

        var data = await db.QueryAsync<TradeMinorDto>(
            "usp_TradeMinor_ByMajor",
            new { tradeMajorID },
            commandType: CommandType.StoredProcedure
        );

        return Ok(data);
    }

    /* ================= POST ================= */
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TradeTypeDto dto)
    {
        using var db = CreateConnection();

        var id = await db.ExecuteScalarAsync<int>(
            "usp_TradeType_CRUD",
            new
            {
                Action = "INSERT",
                dto.TradeTypeCode,
                dto.TradeTypeName,
                dto.IsActive
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { tradeTypeID = id });
    }

    /* ================= PUT ================= */
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] TradeTypeDto dto)
    {
        using var db = CreateConnection();

        var rows = await db.ExecuteScalarAsync<int>(
            "usp_TradeType_CRUD",
            new
            {
                Action = "UPDATE",
                tradeTypeID = id,
                dto.TradeTypeCode,
                dto.TradeTypeName,
                dto.IsActive
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { RowsAffected = rows });
    }

    /* ================= DELETE ================= */
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        using var db = CreateConnection();

        var rows = await db.ExecuteScalarAsync<int>(
            "usp_TradeType_CRUD",
            new
            {
                Action = "DELETE",
                tradeTypeID = id
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { RowsAffected = rows });
    }
}
