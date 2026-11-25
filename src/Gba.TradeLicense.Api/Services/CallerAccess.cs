using System.Data;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Gba.TradeLicense.Api.Services
{
    /// <summary>Which table an application id refers to (draft and submitted ids can overlap).</summary>
    public enum ApplicationIdKind { Any = 0, Draft = 1, Submitted = 2 }

    /// <summary>
    /// Who is calling, and may they touch this application?
    ///   - Traders (TRADE_OWNER): only their own applications, including legacy bulk-migrated ones
    ///     saved with loginID = -1 that match their registered mobile number (same rule as
    ///     ApplicationViewController).
    ///   - Officers with a GBA assignment (Login_GBAJurisdiction): only applications whose GBA ward
    ///     is inside that assignment (same ward rule as GbaQueueService, i.e. their queues).
    ///   - Officers without a GBA assignment keep the legacy Office -> MOH routing done inside the
    ///     stored procedures; it is not re-checked here.
    ///   - Admin: everything.
    /// </summary>

    public static class CallerAccess
    {
        private static string Normalize(string? s) =>
            new string((s ?? string.Empty).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

        public static bool IsTrader(ClaimsPrincipal user)
        {
            var role = Normalize(user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("designation")?.Value);
            return role is "trader" or "tradeowner";
        }

        public static bool IsAdmin(ClaimsPrincipal user) =>
            Normalize(user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("designation")?.Value) == "admin";

        /// <summary>
        /// The officer whose queue / dashboard is read. Officers always get their own (a loginId in
        /// the URL or body is ignored); admin keeps the requested one (admin workflow pages).
        /// </summary>
        public static int QueueLoginId(ClaimsPrincipal user, int requestedLoginId)
        {
            if (IsAdmin(user)) return requestedLoginId;
            var id = CallerId(user);
            return id is long v && v > 0 && v <= int.MaxValue ? (int)v : 0;
        }

        /// <summary>Trader ownership or officer jurisdiction, as described on the class.</summary>
        public static async Task<bool> MayAccessApplicationAsync(IDbConnection db, ClaimsPrincipal user,
            long licenceApplicationId, ApplicationIdKind kind = ApplicationIdKind.Any)
        {
            if (IsTrader(user))
                return await OwnsApplicationAsync(db, user, licenceApplicationId, kind);
            return await OfficerMayAccessAsync(db, user, licenceApplicationId, kind);
        }

        public static async Task<bool> OfficerMayAccessAsync(IDbConnection db, ClaimsPrincipal user,
            long licenceApplicationId, ApplicationIdKind kind = ApplicationIdKind.Any)
        {
            if (IsTrader(user)) return false;
            if (IsAdmin(user)) return true;
            var callerId = CallerId(user);
            if (callerId == null) return false;

            try
            {
                return await db.ExecuteScalarAsync<int>(OfficerJurisdictionSql,
                    new { loginId = callerId.Value, id = licenceApplicationId, kind = (int)kind }) == 1;
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 208)
            {
                return true; // GBA tables not created on this database: legacy routing only (as GbaQueueService)
            }
        }

        // 1 when the officer has no active GBA assignment (legacy routing) or the application's GBA
        // ward is inside it. Ward of an application = the ward the applicant chose
        // (Licence_GBALocation) or, for older licences, the primary GBA ward mapped from the legacy
        // ward - the same rule GbaQueueService uses for the officer queues.
        private const string OfficerJurisdictionSql = @"
            IF NOT EXISTS (SELECT 1 FROM dbo.Login_GBAJurisdiction WHERE loginID = @loginId AND isActive = 1)
                SELECT 1;
            ELSE
            BEGIN
                ;WITH allowed AS (
                    SELECT DISTINCT gw.gbaWardID
                    FROM dbo.Login_GBAJurisdiction j
                    JOIN dbo.Master_GBAWard gw
                      ON gw.corporationID = j.corporationID
                     AND (j.gbaZoneID IS NULL OR gw.gbaZoneID = j.gbaZoneID)
                     AND (j.gbaWardID IS NULL OR gw.gbaWardID = j.gbaWardID)
                    WHERE j.loginID = @loginId AND j.isActive = 1
                ), pm AS (
                    SELECT legacyWardID, gbaWardID FROM (
                        SELECT mp.legacyWardID, mp.gbaWardID,
                               ROW_NUMBER() OVER (PARTITION BY mp.legacyWardID ORDER BY mp.isPrimary DESC, mp.matchScore DESC) AS rn
                        FROM dbo.Master_LegacyGBAWardMapping mp) x
                    WHERE rn = 1
                ), licences AS (
                    SELECT la.tradeLicenceID FROM dbo.Licence_Application la
                    WHERE @kind <> 1 AND la.licenceApplicationID = @id
                    UNION
                    SELECT la.tradeLicenceID FROM dbo.Licence_Application_Temp t
                    JOIN dbo.Licence_Application la ON la.tradeLicenceID = t.tradeLicenceID
                    WHERE @kind <> 2 AND t.licenceApplicationID = @id
                ), wards AS (
                    SELECT loc.gbaWardID FROM licences l
                    JOIN dbo.Licence_GBALocation loc ON loc.tradeLicenceID = l.tradeLicenceID
                    UNION ALL
                    SELECT pm.gbaWardID FROM licences l
                    JOIN dbo.Licence_Master lm ON lm.tradeLicenceID = l.tradeLicenceID
                    JOIN pm ON pm.legacyWardID = lm.wardID
                    WHERE NOT EXISTS (SELECT 1 FROM dbo.Licence_GBALocation x WHERE x.tradeLicenceID = l.tradeLicenceID)
                    UNION ALL
                    -- draft not submitted yet: the ward chosen on the draft
                    SELECT loc.gbaWardID FROM dbo.Licence_Application_Temp t
                    JOIN dbo.Licence_GBALocation loc ON loc.tempTradeLicenceID = t.tradeLicenceID
                    WHERE @kind <> 2 AND t.licenceApplicationID = @id
                )
                SELECT CASE WHEN EXISTS (SELECT 1 FROM wards w JOIN allowed a ON a.gbaWardID = w.gbaWardID)
                            THEN 1 ELSE 0 END;
            END";

        public static long? CallerId(ClaimsPrincipal user) =>
            long.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("loginID")?.Value, out var id)
                ? id : null;

        private static string CallerMobile(ClaimsPrincipal user) =>
            user.FindFirst("mobile")?.Value?.Trim() ?? string.Empty;

        /// <summary>
        /// True when the trader owns the application. The id may be a draft id
        /// (Licence_Application_Temp) or a submitted id (Licence_Application): documents, geo and
        /// certificates move from one to the other at final submission.
        /// </summary>
        public static async Task<bool> OwnsApplicationAsync(IDbConnection db, ClaimsPrincipal user, long licenceApplicationId,
            ApplicationIdKind kind = ApplicationIdKind.Any)
        {
            var callerId = CallerId(user);
            if (callerId == null) return false;

            return await db.ExecuteScalarAsync<int>(
                @"SELECT CASE WHEN (@kind <> 2 AND EXISTS (
                        SELECT 1
                        FROM dbo.Licence_Application_Temp t
                        LEFT JOIN dbo.Licence_Master_Temp mt ON mt.tradeLicenceID = t.tradeLicenceID
                        LEFT JOIN dbo.Licence_Master m ON m.tradeLicenceID = t.tradeLicenceID
                        WHERE t.licenceApplicationID = @id
                          AND (t.loginID = @callerId
                               OR (t.loginID = -1 AND @mobile <> ''
                                   AND (LTRIM(RTRIM(mt.mobileNumber)) = @mobile OR LTRIM(RTRIM(m.mobileNumber)) = @mobile)))))
                     OR (@kind <> 1 AND EXISTS (
                        SELECT 1
                        FROM dbo.Licence_Application la
                        LEFT JOIN dbo.Licence_Master m ON m.tradeLicenceID = la.tradeLicenceID
                        WHERE la.licenceApplicationID = @id
                          AND (la.loginID = @callerId
                               OR (la.loginID = -1 AND @mobile <> '' AND LTRIM(RTRIM(m.mobileNumber)) = @mobile))))
                  THEN 1 ELSE 0 END",
                new { id = licenceApplicationId, callerId = callerId.Value, mobile = CallerMobile(user), kind = (int)kind }) == 1;
        }

        /// <summary>
        /// May the caller work on this licence record (tradeLicenceID)?
        ///   traders: their own record, or a brand-new one no application points to yet (the form
        ///            creates the record first and links it right after);
        ///   officers: a record linked to an application inside their GBA jurisdiction (unlinked
        ///            records are drafts in progress and are not officer business);
        ///   admin: all.
        /// </summary>
        public static async Task<bool> MayAccessLicenceAsync(IDbConnection db, ClaimsPrincipal user, long tradeLicenceId)
        {
            if (IsAdmin(user)) return true;

            if (IsTrader(user))
            {
                if (await OwnsTradeLicenceAsync(db, user, tradeLicenceId)) return true;
                var linked = await db.ExecuteScalarAsync<int>(
                    @"SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.Licence_Application_Temp WHERE tradeLicenceID = @id)
                                  OR EXISTS (SELECT 1 FROM dbo.Licence_Application WHERE tradeLicenceID = @id)
                             THEN 1 ELSE 0 END",
                    new { id = tradeLicenceId });
                if (linked == 1) return false;

                // Unlinked record (created, not yet attached to an application). Once the owner table
                // exists (GBA_LicenceOwner_Migration.sql) only its recorded creator may use it; records
                // created before that have no owner and are not opened to anyone. Without the table:
                // previous behaviour.
                var (tableExists, owner) = await LicenceOwnerStore.GetOwnerAsync(db, tradeLicenceId);
                if (!tableExists) return true;
                return owner != null && owner == CallerId(user);
            }

            var applications = (await db.QueryAsync<long>(
                @"SELECT CAST(licenceApplicationID AS BIGINT) FROM dbo.Licence_Application WHERE tradeLicenceID = @id",
                new { id = tradeLicenceId })).ToList();
            foreach (var appId in applications)
                if (await OfficerMayAccessAsync(db, user, appId, ApplicationIdKind.Submitted))
                    return true;

            var drafts = (await db.QueryAsync<long>(
                @"SELECT CAST(licenceApplicationID AS BIGINT) FROM dbo.Licence_Application_Temp WHERE tradeLicenceID = @id",
                new { id = tradeLicenceId })).ToList();
            foreach (var draftId in drafts)
                if (await OfficerMayAccessAsync(db, user, draftId, ApplicationIdKind.Draft))
                    return true;

            return false;
        }

        /// <summary>
        /// Raw SQL Server error text must not reach the browser. Messages a procedure raises on
        /// purpose (RAISERROR / THROW with a user error number >= 50000, e.g. "Inspection not
        /// completed") are business messages and are kept; anything else becomes the fallback.
        /// </summary>
        public static string SafeDbMessage(Exception ex, string fallback) =>
            ex is Microsoft.Data.SqlClient.SqlException sql && sql.Number >= 50000 ? sql.Message : fallback;

        /// <summary>True when the trader owns the draft / submitted licence record (tradeLicenceID).</summary>
        public static async Task<bool> OwnsTradeLicenceAsync(IDbConnection db, ClaimsPrincipal user, long tradeLicenceId)
        {
            var callerId = CallerId(user);
            if (callerId == null) return false;

            return await db.ExecuteScalarAsync<int>(
                @"SELECT CASE WHEN EXISTS (
                        SELECT 1
                        FROM dbo.Licence_Application_Temp t
                        LEFT JOIN dbo.Licence_Master_Temp mt ON mt.tradeLicenceID = t.tradeLicenceID
                        LEFT JOIN dbo.Licence_Master m ON m.tradeLicenceID = t.tradeLicenceID
                        WHERE t.tradeLicenceID = @id
                          AND (t.loginID = @callerId
                               OR (t.loginID = -1 AND @mobile <> ''
                                   AND (LTRIM(RTRIM(mt.mobileNumber)) = @mobile OR LTRIM(RTRIM(m.mobileNumber)) = @mobile))))
                     OR EXISTS (
                        SELECT 1
                        FROM dbo.Licence_Application la
                        LEFT JOIN dbo.Licence_Master m ON m.tradeLicenceID = la.tradeLicenceID
                        WHERE la.tradeLicenceID = @id
                          AND (la.loginID = @callerId
                               OR (la.loginID = -1 AND @mobile <> '' AND LTRIM(RTRIM(m.mobileNumber)) = @mobile)))
                  THEN 1 ELSE 0 END",
                new { id = tradeLicenceId, callerId = callerId.Value, mobile = CallerMobile(user) }) == 1;
        }

        /// <summary>
        /// The gateway-verified success for this draft application. Written only by
        /// PaymentController.PaymentSuccess after the Easebuzz response hash (and the transaction
        /// status API) confirmed it - never from anything the browser sends.
        /// </summary>
        public static async Task<bool> HasVerifiedPaymentAsync(IDbConnection db, long draftLicenceApplicationId) =>
            await db.ExecuteScalarAsync<int>(
                @"SELECT COUNT(1) FROM dbo.Payment_Audit_Log
                  WHERE LicenceApplicationId = @id AND PaymentStage = 'SUCCESS'
                    AND ISNULL(IsActive, 1) = 1",
                new { id = draftLicenceApplicationId }) > 0;
    }

    /// <summary>
    /// Officer / admin endpoints: a trader token (TRADE_OWNER) gets 403. Staff roles keep exactly
    /// the access they had. WritesOnly = true leaves GET open to traders (master lookups the
    /// New Licence form needs) and blocks only POST / PUT / DELETE.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class StaffOnlyAttribute : Attribute, IAuthorizationFilter
    {
        public bool WritesOnly { get; set; }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            if (user?.Identity?.IsAuthenticated != true)
                return; // the global [Authorize] filter already answers 401

            var method = context.HttpContext.Request.Method;
            if (WritesOnly && (HttpMethods.IsGet(method) || HttpMethods.IsHead(method)))
                return;

            if (CallerAccess.IsTrader(user))
                context.Result = new ForbidResult();
        }
    }
}
