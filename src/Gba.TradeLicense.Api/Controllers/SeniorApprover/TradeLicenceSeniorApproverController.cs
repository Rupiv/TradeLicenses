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
        // SENIOR APPROVER – APPLICATION LIST
        // ======================================================
        [HttpGet("applications")]
        public async Task<IActionResult> GetApplications(
            int loginId,
            int? mohId,
            int? wardId,
            int? licenceApplicationId,
            string? applicationNumber,
            int pageNumber = 1,
            int pageSize = 10)
        {
            using var con = Db();

            var parameters = new DynamicParameters();
            parameters.Add("@Action", "LIST");
            parameters.Add("@LoginID", loginId);
            parameters.Add("@MohID", mohId);
            parameters.Add("@WardID", wardId);
            parameters.Add("@LicenceApplicationID", licenceApplicationId);
            parameters.Add("@ApplicationNumber", applicationNumber);
            parameters.Add("@PageNumber", pageNumber);
            parameters.Add("@PageSize", pageSize);
            parameters.Add("@TotalCount",
                dbType: DbType.Int32,
                direction: ParameterDirection.Output);

            var data = await con.QueryAsync(
                "sp_GetTradeLicenceApplications_SeniorApprover",
                parameters,
                commandType: CommandType.StoredProcedure
            );

            return Ok(new
            {
                Role = "SeniorApprover",
                VisibleStatuses = new[]
                {
                    "OBJECTION",
                    "FORWARDED",
                    "APPROVED",
                    "REJECTED"
                },
                LoginID = loginId,
                TotalRecords = parameters.Get<int>("@TotalCount"),
                PageNumber = pageNumber,
                PageSize = pageSize,
                Data = data
            });
        }


        // ======================================================
        // SENIOR APPROVER – DASHBOARD COUNT
        // ======================================================
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard(
            int loginId,
            int? mohId,
            int? wardId)
        {
            using var con = Db();

            var parameters = new DynamicParameters();
            parameters.Add("@Action", "DASHBOARD");
            parameters.Add("@LoginID", loginId);
            parameters.Add("@MohID", mohId);
            parameters.Add("@WardID", wardId);
            parameters.Add("@TotalCount",
                dbType: DbType.Int32,
                direction: ParameterDirection.Output);

            var dashboard = await con.QueryFirstAsync(
                "sp_GetTradeLicenceApplications_SeniorApprover",
                parameters,
                commandType: CommandType.StoredProcedure
            );

            return Ok(new
            {
                Role = "SeniorApprover",
                LoginID = loginId,
                Dashboard = dashboard
            });
        }
    }
}
