using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Gba.TradeLicense.Api.Services
{
    /// <summary>
    /// Officer queues (inspector / zone approver / senior approver) for officers that have a
    /// GBA Corporation / Zone / Ward assignment (Users &amp; Roles -> Login_GBAJurisdiction).
    ///
    /// Officers WITHOUT an assignment keep the existing Office -> MOH zone routing (the stored
    /// procedures); nothing changes for them.
    ///
    /// The GBA ward of an application = the ward the applicant chose (CONFIRMED) or, for older
    /// applications, the GBA ward mapped from its legacy ward (DERIVED) - see vw_LicenceCorporation.
    /// Rows keep the column names the screens already use: mohID / mohName = GBA zone,
    /// wardID / wardName = GBA ward. Read-only.
    /// </summary>
    public class GbaQueueService
    {
        private readonly string _connStr;

        public GbaQueueService(IConfiguration config)
        {
            _connStr = config.GetConnectionString("Default")
                ?? throw new InvalidOperationException("Connection string not found");
        }

        private IDbConnection Db() => new SqlConnection(_connStr);

        /// <summary>True when the officer has an active GBA assignment (false if the GBA tables are not there).</summary>
        public async Task<bool> HasAssignmentAsync(int loginId)
        {
            using var db = Db();
            try
            {
                return await db.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM dbo.Login_GBAJurisdiction WHERE loginID = @loginId AND isActive = 1",
                    new { loginId }) > 0;
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                return false;
            }
        }

        // Applications of the requested statuses whose GBA ward is inside the officer's assignment.
        // Same rule as vw_LicenceCorporation (applicant's chosen ward, else the primary mapped ward
        // of the legacy ward) but driven from the officer's own wards, so it stays fast even for
        // statuses with hundreds of thousands of rows (APPROVED).
        private const string ScopedCte = @"
            WITH allowed AS (
                SELECT DISTINCT gw.gbaWardID, gw.gbaZoneID, gw.corporationID, gw.wardNo, gw.wardName
                FROM dbo.Login_GBAJurisdiction j
                JOIN dbo.Master_GBAWard gw
                  ON gw.corporationID = j.corporationID
                 AND (j.gbaZoneID IS NULL OR gw.gbaZoneID = j.gbaZoneID)
                 AND (j.gbaWardID IS NULL OR gw.gbaWardID = j.gbaWardID)
                WHERE j.loginID = @loginId AND j.isActive = 1
                  AND (@zoneId IS NULL OR gw.gbaZoneID = @zoneId)
                  AND (@wardId IS NULL OR gw.gbaWardID = @wardId)
            ), pm AS (
                SELECT legacyWardID, gbaWardID FROM (
                    SELECT mp.legacyWardID, mp.gbaWardID,
                           ROW_NUMBER() OVER (PARTITION BY mp.legacyWardID ORDER BY mp.isPrimary DESC, mp.matchScore DESC) AS rn
                    FROM dbo.Master_LegacyGBAWardMapping mp) x
                WHERE rn = 1
            ), located AS (
                -- new applications: GBA ward chosen by the applicant
                SELECT la.licenceApplicationID, a.gbaWardID
                FROM dbo.Licence_GBALocation loc
                JOIN allowed a ON a.gbaWardID = loc.gbaWardID
                JOIN dbo.Licence_Application la ON la.tradeLicenceID = loc.tradeLicenceID
                WHERE la.licenceApplicationStatusID IN @statuses
                UNION ALL
                -- older applications: GBA ward mapped from the legacy ward (when none was chosen)
                SELECT la.licenceApplicationID, pm.gbaWardID
                FROM pm
                JOIN allowed a ON a.gbaWardID = pm.gbaWardID
                JOIN dbo.Licence_Master lm ON lm.wardID = pm.legacyWardID
                JOIN dbo.Licence_Application la ON la.tradeLicenceID = lm.tradeLicenceID
                WHERE la.licenceApplicationStatusID IN @statuses
                  AND NOT EXISTS (SELECT 1 FROM dbo.Licence_GBALocation x WHERE x.tradeLicenceID = lm.tradeLicenceID)
            ), scoped AS (
                SELECT la.licenceApplicationID, la.applicationNumber, la.applicationSubmitDate, la.ChangesDate,
                       la.licenceApplicationStatusID, lm.tradeLicenceID, lm.applicantName, lm.tradeName,
                       lm.mobileNumber, lm.emailID, a.gbaZoneID, gz.zoneName, a.gbaWardID, a.wardNo,
                       a.wardName, a.corporationID
                FROM located l
                JOIN allowed a ON a.gbaWardID = l.gbaWardID
                JOIN dbo.Master_GBAZone gz ON gz.gbaZoneID = a.gbaZoneID
                JOIN dbo.Licence_Application la ON la.licenceApplicationID = l.licenceApplicationID
                JOIN dbo.Licence_Master lm ON lm.tradeLicenceID = la.tradeLicenceID
                WHERE (@licenceApplicationId IS NULL OR la.licenceApplicationID = @licenceApplicationId)
                  AND (@search IS NULL
                       OR la.applicationNumber LIKE '%' + @search + '%'
                       OR lm.applicantName LIKE '%' + @search + '%'
                       OR lm.tradeName LIKE '%' + @search + '%'
                       OR lm.mobileNumber LIKE '%' + @search + '%')
            )";

        /// <summary>One page of the officer's queue + total count (dynamic rows, as the zone / senior procedures return).</summary>
        public Task<(int Total, IEnumerable<dynamic> Rows)> GetApplicationsAsync(
            int loginId, int[] statuses, int? zoneId, int? wardId,
            long? licenceApplicationId, string? search, int pageNumber, int pageSize)
            => GetApplicationsAsync<dynamic>(loginId, statuses, zoneId, wardId, licenceApplicationId, search, pageNumber, pageSize);

        /// <summary>One page of the officer's queue + total count, mapped to <typeparamref name="T"/>.</summary>
        public async Task<(int Total, IEnumerable<T> Rows)> GetApplicationsAsync<T>(
            int loginId, int[] statuses, int? zoneId, int? wardId,
            long? licenceApplicationId, string? search, int pageNumber, int pageSize)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 500);
            search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

            var args = new
            {
                loginId, statuses, zoneId, wardId, licenceApplicationId, search,
                offset = (pageNumber - 1) * pageSize, pageSize
            };

            using var db = Db();
            using var multi = await db.QueryMultipleAsync(
                ScopedCte + " SELECT COUNT(1) FROM scoped; " +
                ScopedCte + @"
                SELECT s.licenceApplicationID, s.applicationNumber, s.applicationSubmitDate, s.ChangesDate,
                       s.licenceApplicationStatusID,
                       ISNULL(mp.licenceProcessName, CONCAT('STATUS-', s.licenceApplicationStatusID)) AS licenceApplicationStatusName,
                       s.tradeLicenceID, s.applicantName, s.tradeName, s.mobileNumber, s.emailID,
                       s.gbaZoneID AS mohID, s.zoneName AS mohName,
                       s.gbaWardID AS wardID, CONCAT(s.wardNo, ' - ', s.wardName) AS wardName,
                       s.corporationID,
                       CAST(geo.Latitude AS DECIMAL(18,6)) AS Latitude,
                       CAST(geo.Longitude AS DECIMAL(18,6)) AS Longitude,
                       CAST(geo.RoadID AS NVARCHAR(200)) AS RoadID,
                       CAST(geo.RoadWidthMtrs AS INT) AS RoadWidthMtrs,
                       CAST(geo.RoadCategoryCode AS NVARCHAR(100)) AS RoadCategoryCode,
                       CAST(geo.RoadCategory AS NVARCHAR(200)) AS RoadCategory,
                       CAST(ISNULL(geo.IsConfirmed, 0) AS BIT) AS IsConfirmed,
                       geo.EntryDate
                FROM scoped s
                LEFT JOIN dbo.Master_LicenceProcess mp ON mp.licenceProcessID = s.licenceApplicationStatusID
                OUTER APPLY (SELECT TOP 1 g.Latitude, g.Longitude, g.RoadID, g.RoadWidthMtrs, g.RoadCategoryCode,
                                    g.RoadCategory, g.IsConfirmed, g.EntryDate
                             FROM dbo.Licence_Application_GeoLocation g
                             WHERE g.LicenceApplicationID = s.licenceApplicationID
                             ORDER BY g.EntryDate DESC, g.GeoID DESC) geo
                ORDER BY ISNULL(s.ChangesDate, s.applicationSubmitDate) DESC, s.licenceApplicationID DESC
                OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;",
                args);

            var total = await multi.ReadFirstAsync<int>();
            var rows = (await multi.ReadAsync<T>()).ToList();
            return (total, rows);
        }

        /// <summary>Count per status (key = licenceApplicationStatusID) for the dashboard tiles.</summary>
        public async Task<Dictionary<int, int>> GetStatusCountsAsync(
            int loginId, int[] statuses, int? zoneId, int? wardId)
        {
            using var db = Db();
            var rows = await db.QueryAsync<(int Status, int Count)>(
                ScopedCte + @" SELECT licenceApplicationStatusID, COUNT(1) FROM scoped GROUP BY licenceApplicationStatusID;",
                new { loginId, statuses, zoneId, wardId, licenceApplicationId = (long?)null, search = (string?)null });
            return rows.ToDictionary(r => r.Status, r => r.Count);
        }

        /// <summary>The officer's GBA zones and wards, for the Zone / Ward filters.</summary>
        public async Task<(IEnumerable<dynamic> Zones, IEnumerable<dynamic> Wards)> GetLookupAsync(int loginId)
        {
            using var db = Db();
            using var multi = await db.QueryMultipleAsync(@"
                WITH w AS (
                    SELECT DISTINCT gw.gbaWardID, gw.gbaZoneID, gw.wardNo, gw.wardName
                    FROM dbo.Login_GBAJurisdiction j
                    JOIN dbo.Master_GBAWard gw
                      ON gw.corporationID = j.corporationID
                     AND (j.gbaZoneID IS NULL OR gw.gbaZoneID = j.gbaZoneID)
                     AND (j.gbaWardID IS NULL OR gw.gbaWardID = j.gbaWardID)
                    WHERE j.loginID = @loginId AND j.isActive = 1 AND gw.isActive = 1
                )
                SELECT DISTINCT z.gbaZoneID AS ZoneID, z.zoneName AS ZoneName
                FROM w JOIN dbo.Master_GBAZone z ON z.gbaZoneID = w.gbaZoneID
                ORDER BY ZoneName;

                WITH w AS (
                    SELECT DISTINCT gw.gbaWardID, gw.gbaZoneID, gw.wardNo, gw.wardName
                    FROM dbo.Login_GBAJurisdiction j
                    JOIN dbo.Master_GBAWard gw
                      ON gw.corporationID = j.corporationID
                     AND (j.gbaZoneID IS NULL OR gw.gbaZoneID = j.gbaZoneID)
                     AND (j.gbaWardID IS NULL OR gw.gbaWardID = j.gbaWardID)
                    WHERE j.loginID = @loginId AND j.isActive = 1 AND gw.isActive = 1
                )
                SELECT gbaWardID AS WardID, CONCAT(wardNo, ' - ', wardName) AS WardName, gbaZoneID AS ZoneID
                FROM w ORDER BY wardNo;",
                new { loginId });

            var zones = (await multi.ReadAsync()).ToList();
            var wards = (await multi.ReadAsync()).ToList();
            return (zones, wards);
        }
    }
}
