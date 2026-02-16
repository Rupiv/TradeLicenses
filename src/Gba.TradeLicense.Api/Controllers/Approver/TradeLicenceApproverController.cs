using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Gba.TradeLicense.Domain.Entities;

namespace Gba.TradeLicense.Api.Controllers.Approver
{
    [ApiController]
    [Route("api/trade-licence/approver")]
    public class TradeLicenceApproverController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public TradeLicenceApproverController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private IDbConnection Db()
        {
            return new SqlConnection(
                _configuration.GetConnectionString("Default"));
        }

        // ======================================================
        // APPROVER – APPLICATION LIST (PAGED + SEARCH)
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

            var applications =
                await con.QueryAsync<TradeLicenceApproverApplicationDto>(
                    "sp_GetTradeLicenceApplications_Approver",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

            var totalCount = parameters.Get<int>("@TotalCount");

            return Ok(new
            {
                Role = "Approver",
                Mode = "LIST",
                LoginID = loginId,
                TotalRecords = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Data = applications
            });
        }

        // ======================================================
        // APPROVER – DASHBOARD COUNTS
        // ======================================================
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard(
      int loginId,
      int? mohId,
      int? wardId,
      string? applicationNumber)
        {
            using var con = Db();

            var parameters = new DynamicParameters();
            parameters.Add("@Action", "DASHBOARD");
            parameters.Add("@LoginID", loginId);
            parameters.Add("@MohID", mohId);
            parameters.Add("@WardID", wardId);
            parameters.Add("@LicenceApplicationID", null);
            parameters.Add("@ApplicationNumber", applicationNumber);
            parameters.Add("@PageNumber", 1);
            parameters.Add("@PageSize", 10);
            parameters.Add("@TotalCount",
                dbType: DbType.Int32,
                direction: ParameterDirection.Output);

            var dashboard =
                await con.QueryFirstOrDefaultAsync<ApproverDashboardDto>(
                    "sp_GetTradeLicenceApplications_Approver",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

            return Ok(new
            {
                Role = "Approver",
                Mode = "DASHBOARD",
                LoginID = loginId,
                Data = dashboard
            });
        }


        // ======================================================
        // APPROVER – LOOKUP (ZONE + WARD)
        // ======================================================
        [HttpGet("lookup")]
        public async Task<IActionResult> GetLookup(int loginId)
        {
            using var con = Db();

            var parameters = new DynamicParameters();
            parameters.Add("@Action", "LOOKUP");
            parameters.Add("@LoginID", loginId);
            parameters.Add("@MohID", null);
            parameters.Add("@WardID", null);
            parameters.Add("@LicenceApplicationID", null);
            parameters.Add("@ApplicationNumber", null);
            parameters.Add("@PageNumber", 1);
            parameters.Add("@PageSize", 10);
            parameters.Add("@TotalCount",
                dbType: DbType.Int32,
                direction: ParameterDirection.Output);

            using var multi =
                await con.QueryMultipleAsync(
                    "sp_GetTradeLicenceApplications_Approver",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

            var zones = await multi.ReadAsync<dynamic>();
            var wards = await multi.ReadAsync<dynamic>();

            return Ok(new
            {
                Role = "Approver",
                Mode = "LOOKUP",
                LoginID = loginId,
                Zones = zones,
                Wards = wards
            });
        }
    }
}
