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


        // ======================================================
        // SENIOR APPROVER – LOOKUP (ZONE + WARD)
        // ======================================================
        [HttpGet("lookup")]
        public async Task<IActionResult> GetLookup(int loginId)
        {
            // GBA-assigned officer: only the assigned GBA zone(s) and ward(s)
            if (await _gbaQueue.HasAssignmentAsync(loginId))
            {
                var (gbaZones, gbaWards) = await _gbaQueue.GetLookupAsync(loginId);
                return Ok(new
                {
                    Role = "SeniorApprover",
                    Mode = "LOOKUP",
                    Scope = "GBA",
                    LoginID = loginId,
                    Zones = gbaZones,
                    Wards = gbaWards
                });
            }

            // Non-GBA officer: same zone the LIST/DASHBOARD queries scope to (Login_Master ->
            // Master_OfficeDetails -> moh). sp_GetTradeLicenceApplications_SeniorApprover has no
            // LOOKUP action, so this is resolved directly instead of touching that procedure.
            using var con = Db();

            var zoneCode = await con.QueryFirstOrDefaultAsync<int?>(
                @"SELECT TOP 1 m.majzonecd
                  FROM Login_Master lm
                  INNER JOIN Master_OfficeDetails od ON lm.officeDetailsID = od.officeDetailsID
                  INNER JOIN moh m ON LTRIM(RTRIM(od.officeName)) = LTRIM(RTRIM(m.mohname))
                  WHERE lm.loginID = @loginId",
                new { loginId });

            if (zoneCode is null)
            {
                return Ok(new
                {
                    Role = "SeniorApprover",
                    Mode = "LOOKUP",
                    LoginID = loginId,
                    Zones = Array.Empty<object>(),
                    Wards = Array.Empty<object>()
                });
            }

            var zones = await con.QueryAsync(
                "SELECT z.zoneID AS ZoneID, z.zoneName AS ZoneName FROM Master_BBMPZone z WHERE z.zoneID = @zoneCode",
                new { zoneCode });
            var wards = await con.QueryAsync(
                "SELECT w.wardID AS WardID, w.wardName AS WardName FROM Master_BBMPWard w WHERE w.zoneID = @zoneCode ORDER BY w.wardName",
                new { zoneCode });

            return Ok(new
            {
                Role = "SeniorApprover",
                Mode = "LOOKUP",
                LoginID = loginId,
                Zones = zones,
                Wards = wards
            });
        }
    }
}
