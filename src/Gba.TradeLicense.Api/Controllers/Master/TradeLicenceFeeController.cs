using Microsoft.AspNetCore.Mvc;

namespace Gba.TradeLicense.Api.Controllers.Master
{
    using System.Data;
    using Dapper;
    using Gba.TradeLicense.Domain.Entities;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Data.SqlClient;

    [ApiController]
    [Route("api/trade-licence-fees")]
    public class TradeLicenceFeeController : ControllerBase
    {
        private readonly IConfiguration _config;

        public TradeLicenceFeeController(IConfiguration config)
        {
            _config = config;
        }

        private IDbConnection CreateConnection()
            => new SqlConnection(_config.GetConnectionString("Default"));

        /* ================= CREATE ================= */
        [HttpPost]
        public async Task<IActionResult> Create(TradeLicenceFeeDto dto)
        {
            using var db = CreateConnection();

            var id = await db.ExecuteScalarAsync<int>(
                "usp_Trade_LicenceFee_CRUD",
                new
                {
                    Action = "INSERT",
                    dto.TradeSubID,
                    dto.TradeLicenceFee,
                    dto.TradeApproveAuth,
                    dto.IsActive,
                    dto.BlockPeriodID,
                    dto.Remarks
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(new { TradeFeeID = id });
        }

        /* ================= UPDATE ================= */
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, TradeLicenceFeeDto dto)
        {
            using var db = CreateConnection();

            await db.ExecuteAsync(
                "usp_Trade_LicenceFee_CRUD",
                new
                {
                    Action = "UPDATE",
                    tradeFeeID = id,
                    dto.TradeSubID,
                    dto.TradeLicenceFee,
                    dto.TradeApproveAuth,
                    dto.IsActive,
                    dto.BlockPeriodID,
                    dto.Remarks
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(new { Updated = true });
        }

        /* ================= DELETE ================= */
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            using var db = CreateConnection();

            await db.ExecuteAsync(
                "usp_Trade_LicenceFee_CRUD",
                new
                {
                    Action = "DELETE",
                    tradeFeeID = id
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(new { Deleted = true });
        }

        /* ================= GET BY ID ================= */
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            using var db = CreateConnection();

            var result = await db.QueryFirstOrDefaultAsync<TradeLicenceFeeDto>(
                "usp_Trade_LicenceFee_CRUD",
                new
                {
                    Action = "GET_BY_ID",
                    tradeFeeID = id
                },
                commandType: CommandType.StoredProcedure
            );

            if (result == null)
                return NotFound();

            return Ok(result);
        }
        [HttpGet("by-trade-sub/{tradeSubID:int}")]
        public async Task<IActionResult> GetFeeByTradeSubID(int tradeSubID)
        {
            using var db = CreateConnection();

            var fee = await db.QueryFirstOrDefaultAsync<TradeLicenceFeeDto>(
                "usp_GetTradeLicenceFee_ByTradeSubID_Only",
                new { tradeSubID },
                commandType: CommandType.StoredProcedure
            );

            if (fee == null)
                return NotFound(new { Message = "Fee not configured for this Trade Sub ID" });

            return Ok(fee);
        }
        /* ================= LIST / FILTER ================= */
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] int? tradeSubID)
        {
            using var db = CreateConnection();

            var list = await db.QueryAsync<TradeLicenceFeeDto>(
                "usp_Trade_LicenceFee_CRUD",
                new
                {
                    Action = "SEARCH",
                    tradeSubID
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(list);
        }
    }

}
