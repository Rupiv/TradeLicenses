using Microsoft.AspNetCore.Mvc;

namespace Gba.TradeLicense.Api.Controllers.Master
{
    using System.Data;
    using Dapper;
    using Gba.TradeLicense.Domain.Entities;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Data.SqlClient;

    [ApiController]
    [Route("api/action-reasons")]
    public class ActionReasonsController : ControllerBase
    {
        private readonly IConfiguration _config;

        public ActionReasonsController(IConfiguration config)
        {
            _config = config;
        }

        private IDbConnection CreateConnection()
            => new SqlConnection(_config.GetConnectionString("Default"));

        /* ================= CREATE ================= */
        [HttpPost]
        public async Task<IActionResult> Create(ActionReasonDto dto)
        {
            using var db = CreateConnection();

            var id = await db.ExecuteScalarAsync<int>(
                "usp_ActionReasons_CRUD",
                new
                {
                    Action = "INSERT",
                    dto.ActionReasonName
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(new { ActionReasonId = id });
        }

        /* ================= UPDATE ================= */
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, ActionReasonDto dto)
        {
            using var db = CreateConnection();

            await db.ExecuteAsync(
                "usp_ActionReasons_CRUD",
                new
                {
                    Action = "UPDATE",
                    ActionReasonId = id,
                    dto.ActionReasonName
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
                "usp_ActionReasons_CRUD",
                new
                {
                    Action = "DELETE",
                    ActionReasonId = id
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

            var result = await db.QueryFirstOrDefaultAsync<ActionReasonDto>(
                "usp_ActionReasons_CRUD",
                new
                {
                    Action = "GET_BY_ID",
                    ActionReasonId = id
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

            var list = await db.QueryAsync<ActionReasonDto>(
                "usp_ActionReasons_CRUD",
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
