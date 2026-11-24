using System.Data;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Gba.TradeLicense.Api.Services
{
    /// <summary>
    /// Who is calling, and may they touch this application? Same rules as
    /// ApplicationViewController: traders (TRADE_OWNER) only their own applications - including
    /// legacy bulk-migrated ones saved with loginID = -1 that match their registered mobile number -
    /// while staff roles are unchanged.
    /// </summary>
    /// <summary>Which table an application id refers to (draft and submitted ids can overlap).</summary>
    public enum ApplicationIdKind { Any = 0, Draft = 1, Submitted = 2 }

    public static class CallerAccess
    {
        private static string Normalize(string? s) =>
            new string((s ?? string.Empty).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

        public static bool IsTrader(ClaimsPrincipal user)
        {
            var role = Normalize(user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("designation")?.Value);
            return role is "trader" or "tradeowner";
        }

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
