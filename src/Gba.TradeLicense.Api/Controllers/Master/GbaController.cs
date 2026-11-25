using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Gba.TradeLicense.Api.Controllers.Master
{
    /// <summary>
    /// GBA masters: Corporation -> Zone -> Ward (369 wards), plus the corporation/ward of a
    /// licence and the candidate GBA wards for an existing (legacy-ward) licence.
    /// </summary>
    [ApiController]
    [Gba.TradeLicense.Api.Services.StaffOnly(WritesOnly = true)]
    [Route("api/gba")]
    public class GbaController : ControllerBase
    {
        private readonly string _connStr;

        public GbaController(IConfiguration config)
        {
            _connStr = config.GetConnectionString("Default")
                ?? throw new InvalidOperationException("Connection string not found");
        }

        private IDbConnection CreateConnection()
            => new SqlConnection(_connStr);

        // ================= CORPORATIONS =================
        [HttpGet("corporations")]
        public async Task<IActionResult> GetCorporations()
        {
            using var db = CreateConnection();

            var data = await db.QueryAsync(
                @"SELECT corporationID, corporationName, corporationNativeName
                  FROM dbo.Master_Corporation
                  WHERE isActive = 1
                  ORDER BY corporationName");

            return Ok(data);
        }

        // ================= ZONES OF A CORPORATION =================
        [HttpGet("corporations/{corporationId:int}/zones")]
        public async Task<IActionResult> GetZones(int corporationId)
        {
            using var db = CreateConnection();

            var data = await db.QueryAsync(
                @"SELECT gbaZoneID, corporationID, zoneName, zoneNativeName
                  FROM dbo.Master_GBAZone
                  WHERE corporationID = @corporationId AND isActive = 1
                  ORDER BY zoneName",
                new { corporationId });

            return Ok(data);
        }

        // ================= WARDS (by zone, or whole corporation) =================
        [HttpGet("wards")]
        public async Task<IActionResult> GetWards([FromQuery] int? corporationId, [FromQuery] int? zoneId)
        {
            if (corporationId == null && zoneId == null)
                return BadRequest(new { Message = "corporationId or zoneId is required" });

            using var db = CreateConnection();

            var data = await db.QueryAsync(
                @"SELECT w.gbaWardID, w.corporationID, w.gbaZoneID, w.wardNo, w.wardName, w.wardNativeName,
                         w.assemblyNo, w.assemblyName,
                         w.legacyWardID, w.mohID,   -- application draft still needs a valid mohID
                         CAST(CASE WHEN w.legacyWardID IS NOT NULL AND w.mohID IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS isAvailable
                  FROM dbo.Master_GBAWard w
                  WHERE w.isActive = 1
                    AND (@corporationId IS NULL OR w.corporationID = @corporationId)
                    AND (@zoneId IS NULL OR w.gbaZoneID = @zoneId)
                  ORDER BY w.wardNo",
                new { corporationId, zoneId });

            return Ok(data);
        }

        // ================= ONE WARD (with its corporation / zone) =================
        [HttpGet("wards/{gbaWardId:int}")]
        public async Task<IActionResult> GetWard(int gbaWardId)
        {
            using var db = CreateConnection();

            var data = await db.QueryFirstOrDefaultAsync(
                @"SELECT w.gbaWardID, w.wardNo, w.wardName, w.wardNativeName, w.assemblyNo, w.assemblyName,
                         z.gbaZoneID, z.zoneName, c.corporationID, c.corporationName, c.corporationNativeName
                  FROM dbo.Master_GBAWard w
                  JOIN dbo.Master_GBAZone z ON z.gbaZoneID = w.gbaZoneID
                  JOIN dbo.Master_Corporation c ON c.corporationID = w.corporationID
                  WHERE w.gbaWardID = @gbaWardId",
                new { gbaWardId });

            return data == null ? NotFound(new { Message = "Ward not found" }) : Ok(data);
        }

        // ================= LOCATION OF A DRAFT (new application being filled) =================
        [HttpGet("draft-location/{tempTradeLicenceId:long}")]
        public async Task<IActionResult> GetDraftLocation(long tempTradeLicenceId)
        {
            using var db = CreateConnection();

            var data = await db.QueryFirstOrDefaultAsync(
                @"SELECT loc.gbaWardID, w.gbaZoneID, loc.corporationID
                  FROM dbo.Licence_GBALocation loc
                  JOIN dbo.Master_GBAWard w ON w.gbaWardID = loc.gbaWardID
                  WHERE loc.tempTradeLicenceID = @tempTradeLicenceId",
                new { tempTradeLicenceId });

            return data == null ? NotFound(new { Message = "No GBA ward saved for this draft" }) : Ok(data);
        }

        // ================= CORPORATION / WARD OF AN EXISTING LICENCE =================
        // locationStatus: CONFIRMED (chosen by applicant), DERIVED (from legacy ward), UNMAPPED
        [HttpGet("licence/{tradeLicenceId:long}")]
        public async Task<IActionResult> GetLicenceLocation(long tradeLicenceId)
        {
            using var db = CreateConnection();

            var location = await db.QueryFirstOrDefaultAsync(
                @"SELECT v.tradeLicenceID, v.legacyWardID, lw.wardName AS legacyWardName,
                         v.corporationID, c.corporationName, v.gbaWardID, v.suggestedGbaWardID, v.locationStatus
                  FROM dbo.vw_LicenceCorporation v
                  LEFT JOIN dbo.Master_BBMPWard lw ON lw.wardID = v.legacyWardID
                  LEFT JOIN dbo.Master_Corporation c ON c.corporationID = v.corporationID
                  WHERE v.tradeLicenceID = @tradeLicenceId",
                new { tradeLicenceId });

            if (location == null)
                return NotFound(new { Message = "Licence not found" });

            // GBA wards this licence's legacy ward may now fall in (shortlist for the applicant)
            var candidates = await db.QueryAsync(
                @"SELECT gw.gbaWardID, gw.wardNo, gw.wardName, gw.wardNativeName,
                         z.gbaZoneID, z.zoneName, c.corporationID, c.corporationName, mp.isPrimary
                  FROM dbo.Licence_Master lm
                  JOIN dbo.Master_LegacyGBAWardMapping mp ON mp.legacyWardID = lm.wardID
                  JOIN dbo.Master_GBAWard gw ON gw.gbaWardID = mp.gbaWardID AND gw.isActive = 1
                  JOIN dbo.Master_GBAZone z ON z.gbaZoneID = gw.gbaZoneID
                  JOIN dbo.Master_Corporation c ON c.corporationID = gw.corporationID
                  WHERE lm.tradeLicenceID = @tradeLicenceId
                  ORDER BY mp.isPrimary DESC, mp.matchScore DESC, gw.wardName",
                new { tradeLicenceId });

            return Ok(new { location, candidates });
        }

        // ================= USER JURISDICTION (Corporation / Zone / Wards of an officer) =================
        // Stored in Login_GBAJurisdiction only; Login_Master and the Office/MOH routing are untouched.
        private const string JurisdictionSelect =
            @"SELECT j.loginID, j.corporationID, c.corporationName, j.gbaZoneID, z.zoneName,
                     j.gbaWardID, w.wardNo, w.wardName
              FROM dbo.Login_GBAJurisdiction j
              JOIN dbo.Master_Corporation c ON c.corporationID = j.corporationID
              LEFT JOIN dbo.Master_GBAZone z ON z.gbaZoneID = j.gbaZoneID
              LEFT JOIN dbo.Master_GBAWard w ON w.gbaWardID = j.gbaWardID
              WHERE j.isActive = 1";

        // Assignments of several users at once (for the user list)
        [Gba.TradeLicense.Api.Services.StaffOnly]
        [Gba.TradeLicense.Api.Services.AdminOnly]
        [HttpGet("user-jurisdiction")]
        public async Task<IActionResult> GetUserJurisdictions([FromQuery] string? loginIds)
        {
            var ids = (loginIds ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out var v) ? v : 0)
                .Where(v => v > 0)
                .Distinct()
                .Take(200)
                .ToArray();
            if (ids.Length == 0) return Ok(Array.Empty<object>());

            using var db = CreateConnection();
            try
            {
                return Ok(await db.QueryAsync(
                    JurisdictionSelect + " AND j.loginID IN @ids ORDER BY j.loginID, c.corporationName, w.wardNo",
                    new { ids }));
            }
            catch (SqlException ex) when (ex.Number == 208) // GBA_UserJurisdiction.sql not run yet
            {
                return Ok(Array.Empty<object>());
            }
        }

        [Gba.TradeLicense.Api.Services.StaffOnly]
        [Gba.TradeLicense.Api.Services.AdminOnly]
        [HttpGet("user-jurisdiction/{loginId:int}")]
        public async Task<IActionResult> GetUserJurisdiction(int loginId)
        {
            using var db = CreateConnection();
            try
            {
                return Ok(await db.QueryAsync(
                    JurisdictionSelect + " AND j.loginID = @loginId ORDER BY w.wardNo",
                    new { loginId }));
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                return Ok(Array.Empty<object>());
            }
        }

        public class UserJurisdictionRequest
        {
            public int? CorporationID { get; set; }   // null / 0 = remove the GBA assignment
            public int? GbaZoneID { get; set; }       // optional: whole zone
            public List<int>? GbaWardIDs { get; set; } // optional: specific wards
        }

        // Replaces the user's active assignment (old rows are deactivated, never deleted)
        [Gba.TradeLicense.Api.Services.AdminOnly]
        [HttpPut("user-jurisdiction/{loginId:int}")]
        public async Task<IActionResult> SaveUserJurisdiction(int loginId, [FromBody] UserJurisdictionRequest req)
        {
            if (loginId <= 0) return BadRequest(new { Message = "Invalid user." });

            int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var actor);
            var corporationId = req?.CorporationID ?? 0;
            var zoneId = req?.GbaZoneID is > 0 ? req.GbaZoneID : null;
            var wardIds = (req?.GbaWardIDs ?? new List<int>()).Where(w => w > 0).Distinct().ToList();

            using var con = new SqlConnection(_connStr);
            await con.OpenAsync();

            try
            {
                if (corporationId > 0)
                {
                    var corpOk = await con.ExecuteScalarAsync<int>(
                        "SELECT COUNT(1) FROM dbo.Master_Corporation WHERE corporationID = @corporationId AND isActive = 1",
                        new { corporationId });
                    if (corpOk == 0) return BadRequest(new { Message = "Selected corporation is not valid." });

                    if (zoneId != null)
                    {
                        var zoneOk = await con.ExecuteScalarAsync<int>(
                            "SELECT COUNT(1) FROM dbo.Master_GBAZone WHERE gbaZoneID = @zoneId AND corporationID = @corporationId AND isActive = 1",
                            new { zoneId, corporationId });
                        if (zoneOk == 0) return BadRequest(new { Message = "Selected zone does not belong to the corporation." });
                    }
                }

                // Each ward is stored with its own zone (from the ward master)
                var wards = wardIds.Count == 0
                    ? new List<(int WardId, int ZoneId)>()
                    : (await con.QueryAsync<(int WardId, int ZoneId)>(
                        @"SELECT gbaWardID, gbaZoneID FROM dbo.Master_GBAWard
                          WHERE gbaWardID IN @wardIds AND corporationID = @corporationId AND isActive = 1
                            AND (@zoneId IS NULL OR gbaZoneID = @zoneId)",
                        new { wardIds, corporationId, zoneId })).ToList();
                if (corporationId > 0 && wards.Count != wardIds.Count)
                    return BadRequest(new { Message = "Some selected wards do not belong to the selected corporation / zone." });

                var userOk = await con.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM dbo.Login_Master WHERE loginID = @loginId", new { loginId });
                if (userOk == 0) return NotFound(new { Message = "User not found." });

                using var tx = con.BeginTransaction();

                await con.ExecuteAsync(
                    @"UPDATE dbo.Login_GBAJurisdiction
                      SET isActive = 0, updatedDate = GETDATE(), updatedBy = @actor
                      WHERE loginID = @loginId AND isActive = 1",
                    new { loginId, actor }, tx);

                if (corporationId > 0)
                {
                    const string insert =
                        @"INSERT INTO dbo.Login_GBAJurisdiction (loginID, corporationID, gbaZoneID, gbaWardID, entryBy)
                          VALUES (@loginId, @corporationId, @zoneId, @wardId, @actor)";

                    if (wards.Count == 0)
                    {
                        await con.ExecuteAsync(insert,
                            new { loginId, corporationId, zoneId, wardId = (int?)null, actor }, tx);
                    }
                    else
                    {
                        await con.ExecuteAsync(insert,
                            wards.Select(w => new { loginId, corporationId, zoneId = (int?)w.ZoneId, wardId = (int?)w.WardId, actor }),
                            tx);
                    }
                }

                tx.Commit();
                return Ok(new { Saved = true });
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                return StatusCode(503, new { Message = "GBA user jurisdiction table is not created yet (run Database/GBA_UserJurisdiction.sql)." });
            }
        }
    }
}
