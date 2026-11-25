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

    // Traders: only their own drafts. Officers: their GBA jurisdiction. Admin: all.
    // (trade details belong to drafts: Licence_Application_Temp ids only)
    private Task<bool> MayAccess(IDbConnection db, long licenceApplicationId)
        => Gba.TradeLicense.Api.Services.CallerAccess.MayAccessApplicationAsync(db, User, licenceApplicationId,
               Gba.TradeLicense.Api.Services.ApplicationIdKind.Draft);


    // ================= INSERT TEMP =================
    [HttpPost("temp")]
    [HttpPost("draft")]
    public async Task<IActionResult> InsertTemp(
     [FromBody] LicenceTradeDetailsUpsertDto dto,
     CancellationToken ct)
    {
        using var db = CreateConnection();
        if (!await MayAccess(db, dto.LicenceApplicationID))
            return Forbid();

        var id = await db.ExecuteScalarAsync<long>(
            "usp_LicenceTradeDetails_CRUD",
            new
            {
                Action = "INSERT_TEMP",
                dto.TradeSubID,
                dto.TradeFee,
                TempLicenceApplicationID = dto.LicenceApplicationID
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { LicenceTradeDetailsID = id });
    }

    // The client data-centre firewall blocks any URL with a "/temp/<more>" path; the "draft/..."
    // routes are the same endpoints under a name it accepts (the old "temp/..." routes still work).
    [HttpGet("temp/full-details/{licenceApplicationID:long}")]
    [HttpGet("draft/full-details/{licenceApplicationID:long}")]
    public async Task<IActionResult> GetTempFullDetails(long licenceApplicationID, CancellationToken ct)
    {
        using var db = CreateConnection();
        if (!await MayAccess(db, licenceApplicationID))
            return Forbid();

        try
        {
            // Read before the multi-result reader below occupies the connection
            var powerDetails = await Gba.TradeLicense.Api.Services.PowerHpStore.GetAsync(db, licenceApplicationID);

            using var multi = await db.QueryMultipleAsync(
                "usp_LicenceApplicationTemp_GetFullDetails",
                new { licenceApplicationID },
                commandType: CommandType.StoredProcedure
            );

            var application = (await multi.ReadAsync<dynamic>()).FirstOrDefault();
            var tradeDetails = (await multi.ReadAsync<dynamic>()).ToList();
            var documents = (await multi.ReadAsync<dynamic>()).ToList();
            var geoLocations = (await multi.ReadAsync<dynamic>()).ToList();

            if (application == null)
            {
                return NotFound(new
                {
                    Message = "Temp licence application not found.",
                    LicenceApplicationID = licenceApplicationID
                });
            }

            return Ok(new
            {
                LicenceApplicationID = licenceApplicationID,
                Application = application,
                TradeDetails = tradeDetails,
                Documents = documents,
                GeoLocations = geoLocations,
                // Declared HP (null for drafts saved before HP was stored)
                PowerDetails = powerDetails
            });
        }
        catch (SqlException ex)
        {
            return BadRequest(new
            {
                Message = Gba.TradeLicense.Api.Services.CallerAccess.SafeDbMessage(ex, "Application details could not be loaded."),
                LicenceApplicationID = licenceApplicationID
            });
        }
    }
    // ================= UPDATE TEMP =================
    [HttpPut("temp/{id:long}")]
    [HttpPut("draft/{id:long}")]
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
    [HttpDelete("draft/{id:long}")]
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
    [HttpGet("draft/by-application/{licenceApplicationID:long}")]
    public async Task<IActionResult> GetByApplicationId(long licenceApplicationID)
    {
        using var db = CreateConnection();
        if (!await MayAccess(db, licenceApplicationID))
            return Forbid();

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
    // ================= FINAL SUBMIT =================
    [HttpPost("final-submit/{licenceApplicationID:long}")]
    public async Task<IActionResult> FinalSubmit(long licenceApplicationID)
    {
        using var db = CreateConnection();
        if (!await MayAccess(db, licenceApplicationID))
            return Forbid();

        await db.ExecuteAsync(
            "usp_LicenceTradeDetails_CRUD",
            new
            {
                Action = "FINAL_SUBMIT",
                LicenceApplicationID = licenceApplicationID
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { TradeDetailsSubmitted = true });
    }

}
