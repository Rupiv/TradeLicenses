using Microsoft.AspNetCore.Mvc;

namespace Gba.TradeLicense.Api.Controllers.Master
{
    using System.Data;
    using Dapper;
    using Gba.TradeLicense.Application.Models;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Data.SqlClient;

    [ApiController]
    [Route("api/master-documents")]
    public class MasterDocumentController : ControllerBase
    {
        private readonly IConfiguration _config;

        public MasterDocumentController(IConfiguration config)
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

            var list = await db.QueryAsync<MasterDocumentDto>(
                "usp_MasterDocument_CRUD",
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

            var doc = await db.QueryFirstOrDefaultAsync<MasterDocumentDto>(
                "usp_MasterDocument_CRUD",
                new { Action = "GET_BY_ID", DocumentID = id },
                commandType: CommandType.StoredProcedure
            );

            if (doc == null)
                return NotFound();

            return Ok(doc);
        }

        // ================= INSERT =================
        [HttpPost]
        public async Task<IActionResult> Insert([FromBody] MasterDocumentDto dto)
        {
            using var db = CreateConnection();

            var id = await db.ExecuteScalarAsync<int>(
                "usp_MasterDocument_CRUD",
                new
                {
                    Action = "INSERT",
                    dto.DocumentName
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(new { DocumentID = id });
        }

        // ================= UPDATE =================
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] MasterDocumentDto dto)
        {
            using var db = CreateConnection();

            await db.ExecuteAsync(
                "usp_MasterDocument_CRUD",
                new
                {
                    Action = "UPDATE",
                    DocumentID = id,
                    dto.DocumentName
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
                "usp_MasterDocument_CRUD",
                new
                {
                    Action = "DELETE",
                    DocumentID = id
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(new { Deleted = true });
        }
    }

}
