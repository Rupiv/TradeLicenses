using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Gba.TradeLicense.Api.Services
{
    /// <summary>
    /// Power HP / generator HP declared on the New Licence form, stored against the draft
    /// application (Licence_Application_Temp id) in dbo.Licence_Application_PowerHP
    /// (Database/GBA_PowerHP_Migration.sql). Until that script has run, reads return null and
    /// writes are skipped, i.e. the API behaves as before (payment amount checked against the
    /// minimum without the power fee).
    /// </summary>
    public static class PowerHpStore
    {
        public sealed record PowerHp(decimal PowerHP, decimal GeneratorHP);

        private const int InvalidObjectName = 208;

        public static async Task<PowerHp?> GetAsync(IDbConnection db, long draftLicenceApplicationId, IDbTransaction? tx = null)
        {
            try
            {
                return await db.QueryFirstOrDefaultAsync<PowerHp>(
                    @"SELECT PowerHP, GeneratorHP FROM dbo.Licence_Application_PowerHP
                      WHERE LicenceApplicationID = @id",
                    new { id = draftLicenceApplicationId }, tx);
            }
            catch (SqlException ex) when (ex.Number == InvalidObjectName)
            {
                return null;
            }
        }

        /// <summary>Saves both values when the client sent them; a missing value keeps the stored one (or 0).</summary>
        public static async Task<bool> SaveAsync(IDbConnection db, long draftLicenceApplicationId,
            decimal? powerHp, decimal? generatorHp, long? updatedBy, IDbTransaction? tx = null)
        {
            if (powerHp == null && generatorHp == null)
                return false;

            try
            {
                await db.ExecuteAsync(
                    @"MERGE dbo.Licence_Application_PowerHP WITH (HOLDLOCK) AS t
                      USING (SELECT @id AS LicenceApplicationID) AS s
                         ON t.LicenceApplicationID = s.LicenceApplicationID
                      WHEN MATCHED THEN
                          UPDATE SET PowerHP = COALESCE(@powerHp, t.PowerHP),
                                     GeneratorHP = COALESCE(@generatorHp, t.GeneratorHP),
                                     UpdatedBy = @updatedBy, UpdatedOn = SYSUTCDATETIME()
                      WHEN NOT MATCHED THEN
                          INSERT (LicenceApplicationID, PowerHP, GeneratorHP, UpdatedBy, UpdatedOn)
                          VALUES (@id, COALESCE(@powerHp, 0), COALESCE(@generatorHp, 0), @updatedBy, SYSUTCDATETIME());",
                    new { id = draftLicenceApplicationId, powerHp, generatorHp, updatedBy }, tx);
                return true;
            }
            catch (SqlException ex) when (ex.Number == InvalidObjectName)
            {
                return false;
            }
        }
    }
}
