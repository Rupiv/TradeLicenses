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
        private readonly Gba.TradeLicense.Api.Services.GbaQueueService _gbaQueue;

        // Senior approver queue: OBJECTION, FORWARD, APPROVED, REJECTED (same as sp_GetTradeLicenceApplications_SeniorApprover)
        private static readonly int[] QueueStatuses = { 5, 6, 3, 4 };

        public TradeLicenceSeniorApproverController(IConfiguration config,
            Gba.TradeLicense.Api.Services.GbaQueueService gbaQueue)
        {
            _config = config;
            _gbaQueue = gbaQueue;
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
            // GBA-assigned officer: queue follows the Corporation/Zone/Ward assignment
            if (await _gbaQueue.HasAssignmentAsync(loginId))
            {
                var (total, rows) = await _gbaQueue.GetApplicationsAsync(
                    loginId, QueueStatuses, mohId, wardId, licenceApplicationId, applicationNumber, pageNumber, pageSize);
                return Ok(new
                {
                    Role = "SeniorApprover",
                    Scope = "GBA",
                    VisibleStatuses = new[] { "OBJECTION", "FORWARDED", "APPROVED", "REJECTED" },
                    LoginID = loginId,
                    TotalRecords = total,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    Data = rows
                });
            }

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
            if (await _gbaQueue.HasAssignmentAsync(loginId))
            {
                var c = await _gbaQueue.GetStatusCountsAsync(loginId, QueueStatuses, mohId, wardId);
                int n(int s) => c.TryGetValue(s, out var v) ? v : 0;
                return Ok(new
                {
                    Role = "SeniorApprover",
                    Scope = "GBA",
                    LoginID = loginId,
                    Dashboard = new
                    {
                        TotalObjection = n(5),
                        TotalForwarded = n(6),
                        TotalApproved = n(3),
                        TotalRejected = n(4),
                        GrandTotal = n(5) + n(6) + n(3) + n(4)
                    }
                });
            }

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
