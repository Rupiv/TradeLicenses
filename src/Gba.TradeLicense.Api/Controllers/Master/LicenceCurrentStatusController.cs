using Microsoft.AspNetCore.Mvc;

namespace Gba.TradeLicense.Api.Controllers.Master
{
    using System.Data;
    using Dapper;
    using Gba.TradeLicense.Domain.Entities;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Data.SqlClient;

    [ApiController]
    [Route("api/licence-current-status")]
    public class LicenceCurrentStatusController : ControllerBase
    {
        private readonly IConfiguration _config;

        public LicenceCurrentStatusController(IConfiguration config)
        {
            _config = config;
        }

        private IDbConnection CreateConnection()
            => new SqlConnection(_config.GetConnectionString("Default"));

        /* ================= CREATE ================= */
        [HttpPost]
        public async Task<IActionResult> Create(LicenceCurrentStatusDto dto)
        {
            using var db = CreateConnection();

            var id = await db.ExecuteScalarAsync<int>(
                "usp_Licence_CurrentStatus_CRUD",
                new
                {
                    Action = "INSERT",
                    dto.StatusDecription,
                    dto.IsActive
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(new { LicenceCurrentStatusID = id });
        }

        /* ================= UPDATE ================= */
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, LicenceCurrentStatusDto dto)
        {
            using var db = CreateConnection();

            await db.ExecuteAsync(
                "usp_Licence_CurrentStatus_CRUD",
                new
                {
                    Action = "UPDATE",
                    licenceCurrentStatusID = id,
                    dto.StatusDecription,
                    dto.IsActive
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(new { Updated = true });
        }

        /* ================= DELETE (SOFT) ================= */
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            using var db = CreateConnection();

            await db.ExecuteAsync(
                "usp_Licence_CurrentStatus_CRUD",
                new
                {
                    Action = "DELETE",
                    licenceCurrentStatusID = id
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

            var result = await db.QueryFirstOrDefaultAsync<LicenceCurrentStatusDto>(
                "usp_Licence_CurrentStatus_CRUD",
                new
                {
                    Action = "GET_BY_ID",
                    licenceCurrentStatusID = id
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

            var list = await db.QueryAsync<LicenceCurrentStatusDto>(
                "usp_Licence_CurrentStatus_CRUD",
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
