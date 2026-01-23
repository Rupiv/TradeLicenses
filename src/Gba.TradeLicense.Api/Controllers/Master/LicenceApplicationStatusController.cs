using System.Data;
using Gba.TradeLicense.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;

namespace Gba.TradeLicense.Api.Controllers.Master
{
    [ApiController]
    [Route("api/master/licence-status")]
    public class LicenceApplicationStatusController : ControllerBase
    {
        private readonly IConfiguration _config;

        public LicenceApplicationStatusController(IConfiguration config)
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
                "usp_Licence_ApplicationStatus_CRUD",
                new { Action = "GET_ALL" },
                commandType: CommandType.StoredProcedure
            );
            return Ok(data);
        }

        [HttpPost]
        public async Task<IActionResult> Create(LicenceStatusDto dto)
        {
            using var db = Db();
            var id = await db.QuerySingleAsync<int>(
                "usp_Licence_ApplicationStatus_CRUD",
                new
                {
                    Action = "INSERT",
                    licenceApplicationStatusName = dto.LicenceApplicationStatusName
                },
                commandType: CommandType.StoredProcedure
            );
            return Ok(new { licenceApplicationStatusID = id });
        }

        [HttpPut]
        public async Task<IActionResult> Update(LicenceStatusDto dto)
        {
            using var db = Db();
            await db.ExecuteAsync(
                "usp_Licence_ApplicationStatus_CRUD",
                new
                {
                    Action = "UPDATE",
                    licenceApplicationStatusID = dto.LicenceApplicationStatusID,
                    licenceApplicationStatusName = dto.LicenceApplicationStatusName
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
                "usp_Licence_ApplicationStatus_CRUD",
                new
                {
                    Action = "DELETE",
                    licenceApplicationStatusID = id
                },
                commandType: CommandType.StoredProcedure
            );
            return Ok(new { Deleted = true });
        }
    }

}
