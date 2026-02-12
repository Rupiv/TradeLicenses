using System.Data;
using Dapper;
using Gba.TradeLicense.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
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
            => new SqlConnection(_config.GetConnectionString("Default"));

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
        [HttpPost("submit-action")]
        public async Task<IActionResult> SubmitAction([FromBody] LicenceActionRequest request)
        {
            if (request == null)
                return BadRequest("Invalid request.");

            using var db = Db();

            var result = await db.QueryFirstOrDefaultAsync<dynamic>(
                "usp_Licence_SubmitAction",
                new
                {
                    Mode = "SUBMIT",
                    request.LicenceApplicationID,
                    request.LoginID,
                    request.LicenceProcessID,
                    request.CurrentStatus,
                    request.Remarks,
                    request.ActionReasonIds
                },
                commandType: CommandType.StoredProcedure
            );

            if (result != null && result.Success == 1)
                return Ok(result);

            return BadRequest(result);
        }

        [HttpGet("application/{licenceApplicationID}/timeline")]
        public async Task<IActionResult> GetApplicationTimeline(int licenceApplicationID)
        {
            using var db = Db();

            var result = await db.QueryAsync<ApplicationTimelineDto>(
                "usp_Licence_SubmitAction",
                new
                {
                    Mode = "GET",
                    LicenceApplicationID = licenceApplicationID
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(new
            {
                Success = true,
                LicenceApplicationID = licenceApplicationID,
                Timeline = result
            });
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
