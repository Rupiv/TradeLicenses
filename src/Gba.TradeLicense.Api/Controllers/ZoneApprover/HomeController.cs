using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Gba.TradeLicense.Domain.Entities;

namespace Gba.TradeLicense.Api.Controllers.ZoneApprover
{
    [ApiController]
    [Route("api/trade-licence/zone-approver")]
    public class TradeLicenceZoneApproverController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly Gba.TradeLicense.Api.Services.GbaQueueService _gbaQueue;

        // Zone approver queue: INSPECTED, OBJECTION, REJECTED (same as sp_GetTradeLicenceApplications_ZoneApprover)
        private static readonly int[] QueueStatuses = { 7, 5, 4 };

        public TradeLicenceZoneApproverController(IConfiguration configuration,
            Gba.TradeLicense.Api.Services.GbaQueueService gbaQueue)
        {
            _configuration = configuration;
            _gbaQueue = gbaQueue;
        }

        private IDbConnection Db()
        {
            return new SqlConnection(
                _configuration.GetConnectionString("Default"));
        }

        // ======================================================
        // ZONE APPROVER – APPLICATION LIST
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
                    Role = "ZoneApprover",
                    Scope = "GBA",
                    VisibleStatuses = new[] { "INSPECTED", "OBJECTION", "REJECTED" },
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

            var applications =
                await con.QueryAsync<TradeLicenceApproverApplicationDto>(
                    "sp_GetTradeLicenceApplications_ZoneApprover",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

            var totalCount = parameters.Get<int>("@TotalCount");

            return Ok(new
            {
                Role = "ZoneApprover",
                VisibleStatuses = new[] { "INSPECTED", "OBJECTION", "REJECTED" },
                LoginID = loginId,
                TotalRecords = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Data = applications
            });
        }


        // ======================================================
        // ZONE APPROVER – DASHBOARD COUNTS
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
                    Role = "ZoneApprover",
                    Scope = "GBA",
                    Dashboard = new
                    {
                        TotalInspected = n(7),
                        TotalObjection = n(5),
                        TotalRejected = n(4),
                        GrandTotal = n(7) + n(5) + n(4)
                    }
                });
            }

            using var con = Db();

            var parameters = new DynamicParameters();

            parameters.Add("@Action", "DASHBOARD");
            parameters.Add("@LoginID", loginId);
            parameters.Add("@MohID", mohId);
            parameters.Add("@WardID", wardId);

            // ✅ ADD THIS (IMPORTANT)
            parameters.Add("@TotalCount",
                dbType: DbType.Int32,
                direction: ParameterDirection.Output);

            var dashboard =
                await con.QueryFirstOrDefaultAsync(
                    "sp_GetTradeLicenceApplications_ZoneApprover",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

            return Ok(new
            {
                Role = "ZoneApprover",
                Dashboard = dashboard
            });
        }

    }
}
