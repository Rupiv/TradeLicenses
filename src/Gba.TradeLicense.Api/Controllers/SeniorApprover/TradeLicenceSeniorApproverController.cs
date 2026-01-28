using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace TradeLicence.API.Controllers
{
    [ApiController]
    [Route("api/trade-licence/senior-approver")]
    public class TradeLicenceSeniorApproverController : ControllerBase
    {
        private readonly IConfiguration _config;

        public TradeLicenceSeniorApproverController(IConfiguration config)
        {
            _config = config;
        }

        private IDbConnection Db()
        {
            return new SqlConnection(
                _config.GetConnectionString("Default"));
        }

        // ======================================================
        // SENIOR APPROVER – INSPECTED APPLICATIONS (ZONE WISE)
        // ======================================================
        [HttpGet("applications")]
        public async Task<IActionResult> GetApplications(
            int loginId,                      // senior approver loginID
            int? mohId,
            int? wardId,
            int? licenceApplicationId,
            string? applicationNumber,
            int pageNumber = 1,
            int pageSize = 10
        )
        {
            using var con = Db();

            var parameters = new DynamicParameters();
            parameters.Add("@LoginID", loginId);
            parameters.Add("@MohID", mohId);
            parameters.Add("@WardID", wardId);
            parameters.Add("@LicenceApplicationID", licenceApplicationId);
            parameters.Add("@ApplicationNumber", applicationNumber);
            parameters.Add("@PageNumber", pageNumber);
            parameters.Add("@PageSize", pageSize);
            parameters.Add("@TotalCount", dbType: DbType.Int32, direction: ParameterDirection.Output);

            var data = await con.QueryAsync(
                "sp_GetTradeLicenceApplications_SeniorApprover",
                parameters,
                commandType: CommandType.StoredProcedure
            );

            return Ok(new
            {
                Role = "SeniorApprover",
                Status = "INSPECTED",
                LoginID = loginId,
                TotalRecords = parameters.Get<int>("@TotalCount"),
                PageNumber = pageNumber,
                PageSize = pageSize,
                Data = data
            });
        }
    }
}
