using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Gba.TradeLicense.Api.Services
{
    /// <summary>
    /// Who created a draft licence record (Licence_Master_Temp.tradeLicenceID), recorded from the
    /// JWT at creation time in dbo.Licence_Master_Temp_Owner (Database/GBA_LicenceOwner_Migration.sql).
    /// Needed for records no application points to yet: before this, any trader could read or edit
    /// such a record (D6). Until the migration has run the table is missing and the API keeps its
    /// previous behaviour. No owner is ever guessed for existing records.
    /// </summary>
    public static class LicenceOwnerStore
    {
        private const int InvalidObjectName = 208;

        /// <summary>Records the creator; false when the table does not exist yet.</summary>
        public static async Task<bool> RecordAsync(IDbConnection db, long tradeLicenceId, long ownerLoginId)
        {
            try
            {
                await db.ExecuteAsync(
                    @"IF NOT EXISTS (SELECT 1 FROM dbo.Licence_Master_Temp_Owner WHERE tradeLicenceID = @tradeLicenceId)
                          INSERT INTO dbo.Licence_Master_Temp_Owner (tradeLicenceID, ownerLoginID) VALUES (@tradeLicenceId, @ownerLoginId);",
                    new { tradeLicenceId, ownerLoginId });
                return true;
            }
            catch (SqlException ex) when (ex.Number == InvalidObjectName)
            {
                return false;
            }
        }

        /// <summary>
        /// Owner of an unlinked draft record: (tableExists, ownerLoginId). ownerLoginId is null when
        /// the record was created before ownership was recorded.
        /// </summary>
        public static async Task<(bool TableExists, long? Owner)> GetOwnerAsync(IDbConnection db, long tradeLicenceId)
        {
            try
            {
                var owner = await db.ExecuteScalarAsync<long?>(
                    "SELECT ownerLoginID FROM dbo.Licence_Master_Temp_Owner WHERE tradeLicenceID = @tradeLicenceId",
                    new { tradeLicenceId });
                return (true, owner);
            }
            catch (SqlException ex) when (ex.Number == InvalidObjectName)
            {
                return (false, null);
            }
        }
    }
}
