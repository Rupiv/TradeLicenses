using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;   

namespace Gba.TradeLicense.Api.Controllers.Master
{
    [ApiController]
    [Route("api/assembly-constituencies")]
    public class AssemblyConstituencyController : ControllerBase
    {
        private readonly string _connStr;

        public AssemblyConstituencyController(IConfiguration config)
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

            var data = await db.QueryAsync<AssemblyConstituencyModel>(
                "sp_Master_AssemblyConstituency_CRUD",
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

            var data = await db.QueryFirstOrDefaultAsync<AssemblyConstituencyModel>(
                "sp_Master_AssemblyConstituency_CRUD",
                new { Action = "GETBYID", constituencyID = id },
                commandType: CommandType.StoredProcedure
            );

            if (data == null)
                return NotFound("Constituency not found");

            return Ok(data);
        }

        // ================= INSERT =================
        [HttpPost]
        public async Task<IActionResult> Insert(
            [FromBody] AssemblyConstituencyModel model,
            CancellationToken ct)
        {
            using var db = CreateConnection();

            var result = await db.QuerySingleAsync<string>(
                "sp_Master_AssemblyConstituency_CRUD",
                new
                {
                    Action = "INSERT",
                    model.constituencyCode,
                    model.constituencyName,
                    model.constituencyNativeName,
                    model.zoneID
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(result);
        }

        // ================= UPDATE =================
        [HttpPut]
        public async Task<IActionResult> Update(
            [FromBody] AssemblyConstituencyModel model,
            CancellationToken ct)
        {
            using var db = CreateConnection();

            var result = await db.QuerySingleAsync<string>(
                "sp_Master_AssemblyConstituency_CRUD",
                new
                {
                    Action = "UPDATE",
                    model.constituencyID,
                    model.constituencyCode,
                    model.constituencyName,
                    model.constituencyNativeName,
                    model.zoneID
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
                "sp_Master_AssemblyConstituency_CRUD",
                new { Action = "DELETE", constituencyID = id },
                commandType: CommandType.StoredProcedure
            );

            return Ok(result);
        }

        // ================= GET BY ZONE (Dropdown) =================
        [HttpGet("by-zone/{zoneId:int}")]
        public async Task<IActionResult> GetByZone(int zoneId, CancellationToken ct)
        {
            using var db = CreateConnection();

            var data = await db.QueryAsync(
                "sp_Master_AssemblyConstituency_CRUD",
                new { Action = "GETBYZONE", zoneID = zoneId },
                commandType: CommandType.StoredProcedure
            );

            return Ok(data);
        }
    }
    }
