using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Gba.TradeLicense.Domain.Entities;

namespace Gba.TradeLicense.Api.Controllers.ZoneApprover
{
    [ApiController]
    [Gba.TradeLicense.Api.Services.StaffOnly]
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


        // ======================================================
        // ZONE APPROVER – LOOKUP (ZONE + WARD)
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
                    Role = "ZoneApprover",
                    Mode = "LOOKUP",
                    Scope = "GBA",
                    LoginID = loginId,
                    Zones = gbaZones,
                    Wards = gbaWards
                });
            }

            // Non-GBA officer: same zone the LIST/DASHBOARD queries scope to (Login_Master ->
            // Master_OfficeDetails -> moh). sp_GetTradeLicenceApplications_ZoneApprover has no
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
                    Role = "ZoneApprover",
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
                Role = "ZoneApprover",
                Mode = "LOOKUP",
                LoginID = loginId,
                Zones = zones,
                Wards = wards
            });
        }

    }
}
