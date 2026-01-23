using System.Data;
using Gba.TradeLicense.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;
namespace Gba.TradeLicense.Api.Controllers.Master
{
    [ApiController]
    [Route("api/master/licence-process")]
    public class LicenceProcessController : ControllerBase
    {
        private readonly IConfiguration _config;

        public LicenceProcessController(IConfiguration config)
        {
            _config = config;
        }

        private IDbConnection Db()
            => new SqlConnection(_config.GetConnectionString("DefaultConnection"));

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            using var db = Db();
            var data = await db.QueryAsync(
                "usp_Master_LicenceProcess_CRUD",
                new { Action = "GET_ALL" },
                commandType: CommandType.StoredProcedure
            );
            return Ok(data);
        }

        [HttpPost]
        public async Task<IActionResult> Create(LicenceProcessDto dto)
        {
            using var db = Db();
            var id = await db.QuerySingleAsync<int>(
                "usp_Master_LicenceProcess_CRUD",
                new
                {
                    Action = "INSERT",
                    licenceProcessName = dto.LicenceProcessName,
                    isActive = dto.IsActive
                },
                commandType: CommandType.StoredProcedure
            );
            return Ok(new { licenceProcessID = id });
        }

        [HttpPut]
        public async Task<IActionResult> Update(LicenceProcessDto dto)
        {
            using var db = Db();
            await db.ExecuteAsync(
                "usp_Master_LicenceProcess_CRUD",
                new
                {
                    Action = "UPDATE",
                    licenceProcessID = dto.LicenceProcessID,
                    licenceProcessName = dto.LicenceProcessName,
                    isActive = dto.IsActive
                },
                commandType: CommandType.StoredProcedure
            );
            return Ok(new { Updated = true });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            using var db = Db();
            await db.ExecuteAsync(
                "usp_Master_LicenceProcess_CRUD",
                new
                {
                    Action = "DELETE",
                    licenceProcessID = id
                },
                commandType: CommandType.StoredProcedure
            );
            return Ok(new { Deleted = true });
        }
    }

}
