using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;
using Gba.TradeLicense.Domain.Entities;

namespace Gba.TradeLicense.Api.Controllers.Master
{
    [ApiController]
    [Route("api/mlc")]
    public class MLCController : ControllerBase
    {
        private readonly string _connStr;

        public MLCController(IConfiguration config)
        {
            _connStr = config.GetConnectionString("Default")
                ?? throw new InvalidOperationException("Connection string not found");
        }

        private IDbConnection CreateConnection()
            => new SqlConnection(_connStr);

        // ================= GET ALL =================
        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            using var db = CreateConnection();

            var data = await db.QueryAsync<MLC>(
                "sp_MLC_CRUD",
                new { Action = "GET" },
                commandType: CommandType.StoredProcedure
            );

            return Ok(data);
        }

        // ================= GET BY ID =================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            using var db = CreateConnection();

            var data = await db.QueryFirstOrDefaultAsync<MLC>(
                "sp_MLC_CRUD",
                new
                {
                    Action = "GETBYID",
                    mlccd = id
                },
                commandType: CommandType.StoredProcedure
            );

            if (data == null)
                return NotFound("MLC record not found");

            return Ok(data);
        }

        // ================= INSERT =================
        [HttpPost]
        public async Task<IActionResult> Insert([FromBody] MLC model, CancellationToken ct)
        {
            using var db = CreateConnection();

            var result = await db.QuerySingleAsync<string>(
                "sp_MLC_CRUD",
                new
                {
                    Action = "INSERT",
                    mlcname = model.mlcname
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(result);
        }

        // ================= UPDATE =================
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] MLC model, CancellationToken ct)
        {
            using var db = CreateConnection();

            var result = await db.QuerySingleAsync<string>(
                "sp_MLC_CRUD",
                new
                {
                    Action = "UPDATE",
                    mlccd = model.mlccd,
                    mlcname = model.mlcname
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(result);
        }

        // ================= DELETE =================
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            using var db = CreateConnection();

            var result = await db.QuerySingleAsync<string>(
                "sp_MLC_CRUD",
                new
                {
                    Action = "DELETE",
                    mlccd = id
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(result);
        }
    }
}
