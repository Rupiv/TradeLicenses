using Microsoft.AspNetCore.Mvc;

namespace Gba.TradeLicense.Api.Controllers.Master
{
    using System.Data;
    using Dapper;
    using Gba.TradeLicense.Domain.Entities;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Data.SqlClient;

    [ApiController]
    [Route("api/moh")]
    public class MohController : ControllerBase
    {
        private readonly IConfiguration _config;

        public MohController(IConfiguration config)
        {
            _config = config;
        }

        private IDbConnection CreateConnection()
            => new SqlConnection(_config.GetConnectionString("Default"));

        // ================= GET ALL =================
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            using var db = CreateConnection();

            var list = await db.QueryAsync<MohDto>(
                "usp_MOH_CRUD",
                new { Action = "GET_ALL" },
                commandType: CommandType.StoredProcedure
            );

            return Ok(list);
        }

        // ================= GET BY ID =================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            using var db = CreateConnection();

            var moh = await db.QueryFirstOrDefaultAsync<MohDto>(
                "usp_MOH_CRUD",
                new { Action = "GET_BY_ID", mohcd = id },
                commandType: CommandType.StoredProcedure
            );

            if (moh == null)
                return NotFound();

            return Ok(moh);
        }

        // ================= SEARCH =================
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string? q)
        {
            using var db = CreateConnection();

            var list = await db.QueryAsync<MohDto>(
                "usp_MOH_CRUD",
                new
                {
                    Action = "SEARCH",
                    SearchText = q
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(list);
        }

        // ================= INSERT =================
        [HttpPost]
        public async Task<IActionResult> Insert([FromBody] MohDto dto)
        {
            using var db = CreateConnection();

            await db.ExecuteAsync(
                "usp_MOH_CRUD",
                new
                {
                    Action = "INSERT",
                    dto.MohCd,
                    dto.MohName,
                    dto.MohNameK,
                    dto.MohShortName,
                    dto.MajZoneCd,
                    dto.Address
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(new { Inserted = true });
        }

        // ================= UPDATE =================
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] MohDto dto)
        {
            using var db = CreateConnection();

            await db.ExecuteAsync(
                "usp_MOH_CRUD",
                new
                {
                    Action = "UPDATE",
                    mohcd = id,
                    dto.MohName,
                    dto.MohNameK,
                    dto.MohShortName,
                    dto.MajZoneCd,
                    dto.Address
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(new { Updated = true });
        }

        // ================= DELETE =================
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            using var db = CreateConnection();

            await db.ExecuteAsync(
                "usp_MOH_CRUD",
                new
                {
                    Action = "DELETE",
                    mohcd = id
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(new { Deleted = true });
        }
    }

}
