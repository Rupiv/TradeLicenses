using Dapper;
using Gba.TradeLicense.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Net;
using System.Text.RegularExpressions;

namespace Gba.TradeLicense.Api.Controllers.Approver
{
    [ApiController]
    [Gba.TradeLicense.Api.Services.StaffOnly]
    [Route("api/trade-licence/approver")]
    public class TradeLicenceApproverController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly Gba.TradeLicense.Api.Services.GbaQueueService _gbaQueue;

        // Inspector queue: APPLIED, REJECTED, OBJECTION (same as sp_GetTradeLicenceApplications_Approver)
        private static readonly int[] QueueStatuses = { 2, 4, 5 };

        public TradeLicenceApproverController(IConfiguration configuration,
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
        // APPROVER – APPLICATION LIST (PAGED + SEARCH)
        // ======================================================
        [HttpPost("applications")]
        public async Task<IActionResult> GetApplications(
    [FromBody] ApproverApplicationRequest request)
        {
            // Officers read their own queue only (admin workflow pages keep the requested one)
            request.LoginId = Gba.TradeLicense.Api.Services.CallerAccess.QueueLoginId(User, request.LoginId);
            // ✅ Step 1: Model validation
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // ✅ Step 2: Extra manual validation (defense in depth)
            if (!string.IsNullOrEmpty(request.ApplicationNumber) &&
                !Regex.IsMatch(request.ApplicationNumber, @"^[a-zA-Z0-9\-\/]+$"))
            {
                return BadRequest("Invalid Application Number");
            }

            // Officer with a GBA Corporation/Zone/Ward assignment: queue follows that assignment
            // (mohId / wardId filters = GBA zone / GBA ward). Others: unchanged Office/MOH routing.
            if (await _gbaQueue.HasAssignmentAsync(request.LoginId))
            {
                var (total, rows) = await _gbaQueue.GetApplicationsAsync<TradeLicenceApproverApplicationDto>(
                    request.LoginId, QueueStatuses, request.MohId, request.WardId,
                    request.LicenceApplicationId, request.ApplicationNumber, request.PageNumber, request.PageSize);
                foreach (var x in rows) x.ApplicationNumber = WebUtility.HtmlEncode(x.ApplicationNumber);
                return Ok(new
                {
                    Role = "Approver",
                    Mode = "LIST",
                    Scope = "GBA",
                    TotalRecords = total,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize,
                    Data = rows
                });
            }

            using var con = Db();

            var parameters = new DynamicParameters();
            parameters.Add("@Action", "LIST");
            parameters.Add("@LoginID", request.LoginId);
            parameters.Add("@MohID", request.MohId);
            parameters.Add("@WardID", request.WardId);
            parameters.Add("@LicenceApplicationID", request.LicenceApplicationId);
            parameters.Add("@ApplicationNumber", request.ApplicationNumber);
            parameters.Add("@PageNumber", request.PageNumber);
            parameters.Add("@PageSize", request.PageSize);

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

            // ✅ Step 3: Encode output (VERY IMPORTANT for XSS)
            var safeData = applications.Select(x =>
            {
                x.ApplicationNumber = WebUtility.HtmlEncode(x.ApplicationNumber);
                return x;
            });

            return Ok(new
            {
                Role = "Approver",
                Mode = "LIST",
                TotalRecords = totalCount,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                Data = safeData
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
            // Officers read their own queue only (admin workflow pages keep the requested one)
            loginId = Gba.TradeLicense.Api.Services.CallerAccess.QueueLoginId(User, loginId);
            if (await _gbaQueue.HasAssignmentAsync(loginId))
            {
                var c = await _gbaQueue.GetStatusCountsAsync(loginId, QueueStatuses, mohId, wardId);
                int n(int s) => c.TryGetValue(s, out var v) ? v : 0;
                return Ok(new
                {
                    Role = "Approver",
                    Mode = "DASHBOARD",
                    Scope = "GBA",
                    LoginID = loginId,
                    Data = new ApproverDashboardDto
                    {
                        TotalApplied = n(2),
                        TotalObjection = n(5),
                        TotalRejected = n(4),
                        GrandTotal = n(2) + n(4) + n(5)
                    }
                });
            }

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
            // Officers read their own queue only (admin workflow pages keep the requested one)
            loginId = Gba.TradeLicense.Api.Services.CallerAccess.QueueLoginId(User, loginId);
            // GBA-assigned officer: only the assigned GBA zone(s) and ward(s)
            if (await _gbaQueue.HasAssignmentAsync(loginId))
            {
                var (gbaZones, gbaWards) = await _gbaQueue.GetLookupAsync(loginId);
                return Ok(new
                {
                    Role = "Approver",
                    Mode = "LOOKUP",
                    Scope = "GBA",
                    LoginID = loginId,
                    Zones = gbaZones,
                    Wards = gbaWards
                });
            }

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
