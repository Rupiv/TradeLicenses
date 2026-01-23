using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System.Threading;
using Gba.TradeLicense.Domain.Entities;

[ApiController]
[Route("api/licence-trade-details")]
public class LicenceTradeDetailsController : ControllerBase
{
    private readonly IConfiguration _config;

    public LicenceTradeDetailsController(IConfiguration config)
    {
        _config = config;
    }

    private IDbConnection CreateConnection()
        => new SqlConnection(_config.GetConnectionString("Default"));

    // ================= INSERT TEMP =================
    [HttpPost("temp")]
    public async Task<IActionResult> InsertTemp(
        [FromBody] LicenceTradeDetailsUpsertDto dto,
        CancellationToken ct)
    {
        using var db = CreateConnection();

        var id = await db.ExecuteScalarAsync<long>(
            "usp_LicenceTradeDetails_CRUD",
            new
            {
                Action = "INSERT_TEMP",
                dto.TradeSubID,
                dto.TradeFee,
                dto.LicenceApplicationID
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { LicenceTradeDetailsID = id });
    }

    // ================= UPDATE TEMP =================
    [HttpPut("temp/{id:long}")]
    public async Task<IActionResult> UpdateTemp(
        long id,
        [FromBody] LicenceTradeDetailsUpsertDto dto,
        CancellationToken ct)
    {
        using var db = CreateConnection();

        await db.ExecuteAsync(
            "usp_LicenceTradeDetails_CRUD",
            new
            {
                Action = "UPDATE_TEMP",
                LicenceTradeDetailsID = id,
                dto.TradeSubID,
                dto.TradeFee
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { Updated = true });
    }

    // ================= DELETE TEMP =================
    [HttpDelete("temp/{id:long}")]
    public async Task<IActionResult> DeleteTemp(long id, CancellationToken ct)
    {
        using var db = CreateConnection();

        await db.ExecuteAsync(
            "usp_LicenceTradeDetails_CRUD",
            new
            {
                Action = "DELETE_TEMP",
                LicenceTradeDetailsID = id
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { Deleted = true });
    }

    // ================= GET TEMP BY APPLICATION ID =================
    [HttpGet("temp/by-application/{licenceApplicationID:long}")]
    public async Task<IActionResult> GetByApplicationId(long licenceApplicationID)
    {
        using var db = CreateConnection();

        var list = await db.QueryAsync(
            "usp_LicenceTradeDetails_CRUD",
            new
            {
                Action = "GET_BY_APP_ID",
                LicenceApplicationID = licenceApplicationID
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(list);
    }
}
