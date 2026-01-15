using System.Data;
using Gba.TradeLicense.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;

namespace Gba.TradeLicense.Api.Controllers.Master
{
    [ApiController]
    [Route("api/bbmp-zones")]
    public class BBMPZoneController : ControllerBase
    {
        private readonly string _connStr;

        public BBMPZoneController(IConfiguration config)
        {
            _connStr = config.GetConnectionString("Default")
                ?? throw new InvalidOperationException("Connection string not found");
        }

        private IDbConnection CreateConnection()
            => new SqlConnection(_connStr);

        // ================= GET ALL ZONES =================
        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            using var db = CreateConnection();

            var data = await db.QueryAsync<BBMPZoneModel>(
                "sp_Master_BBMPZone_CRUD",
                new { Action = "GET" },
                commandType: CommandType.StoredProcedure
            );

            return Ok(data);
        }

        // ================= GET ZONE BY ID =================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            using var db = CreateConnection();

            var data = await db.QueryFirstOrDefaultAsync<BBMPZoneModel>(
                "sp_Master_BBMPZone_CRUD",
                new { Action = "GETBYID", zoneID = id },
                commandType: CommandType.StoredProcedure
            );

            if (data == null)
                return NotFound("Zone not found");

            return Ok(data);
        }

        // ================= INSERT =================
        [HttpPost]
        public async Task<IActionResult> Insert([FromBody] BBMPZoneModel model, CancellationToken ct)
        {
            using var db = CreateConnection();

            var result = await db.QuerySingleAsync<string>(
                "sp_Master_BBMPZone_CRUD",
                new
                {
                    Action = "INSERT",
                    model.zoneCode,
                    model.zoneCodeOld,
                    model.zoneName,
                    model.zoneNativeName
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(result);
        }

        // ================= UPDATE =================
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] BBMPZoneModel model, CancellationToken ct)
        {
            using var db = CreateConnection();

            var result = await db.QuerySingleAsync<string>(
                "sp_Master_BBMPZone_CRUD",
                new
                {
                    Action = "UPDATE",
                    model.zoneID,
                    model.zoneCode,
                    model.zoneCodeOld,
                    model.zoneName,
                    model.zoneNativeName
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
                "sp_Master_BBMPZone_CRUD",
                new { Action = "DELETE", zoneID = id },
                commandType: CommandType.StoredProcedure
            );

            return Ok(result);
        }

        // ================= GET WARDS BY ZONE =================
        [HttpGet("{zoneId:int}/wards")]
        public async Task<IActionResult> GetWardsByZone(int zoneId, CancellationToken ct)
        {
            using var db = CreateConnection();

            var wards = await db.QueryAsync<BBMPWardDropdownModel>(
                "sp_Master_BBMPZone_CRUD",
                new { Action = "GETWARDSBYZONE", zoneID = zoneId },
                commandType: CommandType.StoredProcedure
            );

            return Ok(wards);
        }
    }
}
