using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Gba.TradeLicense.Api.Services
{
    /// <summary>
    /// GBA (5 corporations / 369 wards) location of a licence.
    /// Tables come from Database/GBA_Corporation_Ward_Migration.sql.
    ///
    /// A GBA ward carries the legacy ward + MOH it overlaps; those keep going into the
    /// existing NOT NULL Licence_Master(_Temp).wardID / mohID columns, so approver routing
    /// (MOH -> old zone), reports and FKs keep working unchanged.
    /// </summary>
    public class GbaLocationService
    {
        public const string SourceNewApplication = "NEW_APPLICATION";
        public const string SourceRenewal = "RENEWAL";

        private readonly string _connStr;

        public GbaLocationService(IConfiguration config)
        {
            _connStr = config.GetConnectionString("Default")
                ?? throw new InvalidOperationException("Connection string not found");
        }

        private IDbConnection Db() => new SqlConnection(_connStr);

        public record GbaWard(
            int GbaWardID, int CorporationID, int GbaZoneID, int WardNo,
            string WardName, int? LegacyWardID, int? MohID, bool IsActive);

        public async Task<GbaWard?> GetWardAsync(int gbaWardId)
        {
            using var db = Db();
            return await db.QueryFirstOrDefaultAsync<GbaWard>(
                @"SELECT gbaWardID, corporationID, gbaZoneID, wardNo, wardName, legacyWardID, mohID, isActive
                  FROM dbo.Master_GBAWard WHERE gbaWardID = @gbaWardId",
                new { gbaWardId });
        }

        /// <summary>
        /// Validates a GBA ward chosen by an applicant. Returns an error message, or null when usable.
        /// </summary>
        public static string? Validate(GbaWard? ward)
        {
            if (ward == null || !ward.IsActive)
                return "Selected ward is not valid.";
            if (ward.LegacyWardID == null || ward.MohID == null)
                return $"Ward '{ward.WardName}' is not yet mapped for applications. Please contact the GBA office.";
            return null;
        }

        /// <summary>Insert or update the GBA location of a draft (Licence_Master_Temp) application.</summary>
        public async Task SaveDraftLocationAsync(long tempTradeLicenceId, GbaWard ward, int? loginId)
        {
            using var db = Db();
            await db.ExecuteAsync(
                @"UPDATE dbo.Licence_GBALocation
                     SET corporationID = @CorporationID, gbaWardID = @GbaWardID,
                         updatedBy = @loginId, updatedDate = GETDATE()
                   WHERE tempTradeLicenceID = @tempTradeLicenceId;
                  IF @@ROWCOUNT = 0
                      INSERT INTO dbo.Licence_GBALocation (tempTradeLicenceID, corporationID, gbaWardID, source, updatedBy)
                      VALUES (@tempTradeLicenceId, @CorporationID, @GbaWardID, @source, @loginId);",
                new { tempTradeLicenceId, ward.CorporationID, ward.GbaWardID, loginId, source = SourceNewApplication });
        }

        /// <summary>After FINAL_SUBMIT: link the draft's location to the new Licence_Master id.</summary>
        public async Task PromoteDraftAsync(long tempTradeLicenceId, long tradeLicenceId)
        {
            using var db = Db();
            await db.ExecuteAsync(
                @"UPDATE dbo.Licence_GBALocation
                     SET tradeLicenceID = @tradeLicenceId, updatedDate = GETDATE()
                   WHERE tempTradeLicenceID = @tempTradeLicenceId
                     AND (tradeLicenceID IS NULL OR tradeLicenceID = @tradeLicenceId)
                     AND NOT EXISTS (SELECT 1 FROM dbo.Licence_GBALocation x
                                     WHERE x.tradeLicenceID = @tradeLicenceId AND x.tempTradeLicenceID <> @tempTradeLicenceId);",
                new { tempTradeLicenceId, tradeLicenceId });
        }

        /// <summary>
        /// Corporation that receives the payment for a draft application (Licence_Application_Temp id).
        /// Uses the ward the applicant chose; drafts started before GBA go-live fall back to the
        /// corporation mapped from their legacy ward. Null when it cannot be determined.
        /// </summary>
        public async Task<int?> GetCorporationForDraftApplicationAsync(long licenceApplicationId)
        {
            using var db = Db();
            return await db.ExecuteScalarAsync<int?>(
                @"SELECT TOP 1 COALESCE(loc.corporationID, mapped.corporationID)
                  FROM dbo.Licence_Application_Temp lat
                  LEFT JOIN dbo.Licence_GBALocation loc ON loc.tempTradeLicenceID = lat.tradeLicenceID
                  LEFT JOIN dbo.Licence_Master_Temp lmt ON lmt.tradeLicenceID = lat.tradeLicenceID
                  OUTER APPLY (
                      SELECT TOP 1 gw.corporationID
                      FROM dbo.Master_LegacyGBAWardMapping mp
                      JOIN dbo.Master_GBAWard gw ON gw.gbaWardID = mp.gbaWardID
                      WHERE mp.legacyWardID = lmt.wardID
                      ORDER BY mp.isPrimary DESC, mp.matchScore DESC
                  ) mapped
                  WHERE lat.licenceApplicationID = @licenceApplicationId",
                new { licenceApplicationId });
        }
    }
}
