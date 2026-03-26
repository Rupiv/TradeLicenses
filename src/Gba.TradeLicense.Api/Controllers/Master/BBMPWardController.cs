using System.Data;
using Gba.TradeLicense.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;

namespace Gba.TradeLicense.Api.Controllers.Master
{
    [ApiController]
    [Route("api/bbmp-wards")]
    public class BBMPWardController : ControllerBase
    {
        private readonly string _connStr;

        public BBMPWardController(IConfiguration config)
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

            var data = await db.QueryAsync<BBMPWardModel>(
                "sp_Master_BBMPWard_CRUD",
                new { Action = "GET" },
                commandType: CommandType.StoredProcedure
            );

            return Ok(data);
        }

        // ================= GET BY CONSTITUENCY =================
        [HttpGet("by-constituency/{constituencyId:int}")]
        public async Task<IActionResult> GetWardsByConstituency(
            int constituencyId,
            CancellationToken ct)
        {
            using var db = CreateConnection();

            var wards = await db.QueryAsync<BBMPWardModel>(
                "sp_Master_BBMPWard_CRUD",
                new
                {
                    Action = "GETBYCONSTITUENCY",
                    constituencyID = constituencyId
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(wards);
        }

        // ================= GET BY ID =================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            using var db = CreateConnection();

            var data = await db.QueryFirstOrDefaultAsync<BBMPWardModel>(
                "sp_Master_BBMPWard_CRUD",
                new
                {
                    Action = "GETBYID",
                    wardID = id
                },
                commandType: CommandType.StoredProcedure
            );

            if (data == null)
                return NotFound(new { message = "Ward not found" });

            return Ok(data);
        }

        // ================= INSERT =================
        [HttpPost]
        public async Task<IActionResult> Insert([FromBody] BBMPWardModel model, CancellationToken ct)
        {
            try
            {
                using var db = CreateConnection();

                var result = await db.QueryFirstOrDefaultAsync<dynamic>(
                    "sp_Master_BBMPWard_CRUD",
                    new
                    {
                        Action = "INSERT",
                        model.wardCode,
                        model.wardName,
                        model.wardNativeName,
                        model.zoneID,
                        model.constituencyID
                    },
                    commandType: CommandType.StoredProcedure
                );

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Insert failed",
                    detail = ex.Message
                });
            }
        }

        // ================= UPDATE =================
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] BBMPWardModel model, CancellationToken ct)
        {
            try
            {
                using var db = CreateConnection();

                var result = await db.QueryFirstOrDefaultAsync<dynamic>(
                    "sp_Master_BBMPWard_CRUD",
                    new
                    {
                        Action = "UPDATE",
                        model.wardID,
                        model.wardCode,
                        model.wardName,
                        model.wardNativeName,
                        model.zoneID,
                        model.constituencyID
                    },
                    commandType: CommandType.StoredProcedure
                );

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Update failed",
                    detail = ex.Message
                });
            }
        }

        // ================= DELETE =================
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            try
            {
                using var db = CreateConnection();

                var result = await db.QueryFirstOrDefaultAsync<dynamic>(
                    "sp_Master_BBMPWard_CRUD",
                    new
                    {
                        Action = "DELETE",
                        wardID = id
                    },
                    commandType: CommandType.StoredProcedure
                );

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Delete failed",
                    detail = ex.Message
                });
            }
        }
    }
}