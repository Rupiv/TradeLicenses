using Microsoft.AspNetCore.Mvc;

namespace Gba.TradeLicense.Api.Controllers.Master
{
    using System.Data;
    using Dapper;
    using Gba.TradeLicense.Domain.Entities;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Data.SqlClient;

    [ApiController]
    [Route("api/financial-years")]
    public class FinancialYearController : ControllerBase
    {
        private readonly IConfiguration _config;

        public FinancialYearController(IConfiguration config)
        {
            _config = config;
        }

        private IDbConnection CreateConnection()
            => new SqlConnection(_config.GetConnectionString("Default"));

        /* ================= CREATE ================= */
        [HttpPost]
        public async Task<IActionResult> Create(FinancialYearDto dto)
        {
            using var db = CreateConnection();

            var id = await db.ExecuteScalarAsync<int>(
                "usp_Master_FinancialYear_CRUD",
                new
                {
                    Action = "INSERT",
                    dto.FinancialYear,
                    dto.BlockPeriodID
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(new { FinanicalYearID = id });
        }

        /* ================= UPDATE ================= */
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, FinancialYearDto dto)
        {
            using var db = CreateConnection();

            await db.ExecuteAsync(
                "usp_Master_FinancialYear_CRUD",
                new
                {
                    Action = "UPDATE",
                    finanicalYearID = id,
                    dto.FinancialYear,
                    dto.BlockPeriodID
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
                "usp_Master_FinancialYear_CRUD",
                new
                {
                    Action = "DELETE",
                    finanicalYearID = id
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

            var result = await db.QueryFirstOrDefaultAsync<FinancialYearDto>(
                "usp_Master_FinancialYear_CRUD",
                new
                {
                    Action = "GET_BY_ID",
                    finanicalYearID = id
                },
                commandType: CommandType.StoredProcedure
            );

            if (result == null)
                return NotFound();

            return Ok(result);
        }

        /* ================= SEARCH / LIST ================= */
        [HttpGet]
        public async Task<IActionResult> Search([FromQuery] string? q)
        {
            using var db = CreateConnection();

            var list = await db.QueryAsync<FinancialYearDto>(
                "usp_Master_FinancialYear_CRUD",
                new
                {
                    Action = "SEARCH",
                    SearchText = q
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(list);
        }
    }

}
