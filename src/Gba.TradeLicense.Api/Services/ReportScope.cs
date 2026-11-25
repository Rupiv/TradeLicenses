using System.Data;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;

namespace Gba.TradeLicense.Api.Services
{
    /// <summary>
    /// Corporation-level scope of the admin applications list / dashboard
    /// (sp_GetTradeLicenceApplications_Admin, source: Database/GBA_AdminSearch_Fix.sql). That
    /// procedure can filter by GBA corporation (@CorporationID via vw_LicenceCorporation) but not by
    /// GBA zone or ward (its @ZoneID / @MohID / @WardID are the legacy BBMP ones), so:
    ///   - admin: unchanged (any corporation, or all);
    ///   - officer with whole-corporation assignment(s): only those corporations, forced server-side;
    ///   - officer with only zone / ward assignments: denied (the list cannot be limited to them);
    ///   - officer without an active assignment: denied (default deny).
    /// </summary>
    public static class ReportScope
    {
        public sealed record Assignment(int CorporationID, int? GbaZoneID, int? GbaWardID);

        public sealed record OfficerScope(bool IsAdmin, IReadOnlyList<Assignment> Assignments)
        {
            public IReadOnlyList<int> WholeCorporations =>
                Assignments.Where(a => a.GbaZoneID == null && a.GbaWardID == null)
                           .Select(a => a.CorporationID).Distinct().OrderBy(x => x).ToList();
        }

        public static async Task<OfficerScope> GetAsync(IDbConnection db, ClaimsPrincipal user)
        {
            if (CallerAccess.IsAdmin(user))
                return new OfficerScope(true, Array.Empty<Assignment>());

            var callerId = CallerAccess.CallerId(user);
            if (callerId == null)
                return new OfficerScope(false, Array.Empty<Assignment>());

            try
            {
                var rows = await db.QueryAsync<Assignment>(
                    @"SELECT corporationID AS CorporationID, gbaZoneID AS GbaZoneID, gbaWardID AS GbaWardID
                      FROM dbo.Login_GBAJurisdiction
                      WHERE loginID = @loginId AND isActive = 1",
                    new { loginId = callerId.Value });
                return new OfficerScope(false, rows.ToList());
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                return new OfficerScope(false, Array.Empty<Assignment>()); // no GBA tables: no assignment
            }
        }

        /// <summary>
        /// Corporation to pass to the procedure, or the reason to refuse. A corporation id sent by the
        /// client is honoured only when it is inside the caller's scope.
        /// </summary>
        public static (int? CorporationId, string? DenyReason) ResolveCorporation(OfficerScope scope, int? requested)
        {
            if (scope.IsAdmin)
                return (requested, null);

            if (scope.Assignments.Count == 0)
                return (null, "No GBA corporation is assigned to your login. Please contact the administrator.");

            var whole = scope.WholeCorporations;
            if (whole.Count == 0)
                return (null, "This list covers whole corporations; your login is assigned to a zone or ward only.");

            if (requested is int r)
                return whole.Contains(r)
                    ? (r, null)
                    : (null, "You are not assigned to this corporation.");

            return whole.Count == 1
                ? (whole[0], null)
                : (null, "Please choose one of your assigned corporations.");
        }
    }

    /// <summary>
    /// Admin only (role "admin"). Used for administrator functions the UI offers only under the
    /// admin-guarded /admin routes: user management, officer jurisdictions, master-data changes,
    /// payment administration, and the three reports until they can be limited to a jurisdiction.
    /// WritesOnly = true leaves GET open to the roles that already had it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class AdminOnlyAttribute : Attribute, IAuthorizationFilter
    {
        public bool WritesOnly { get; set; }
        public string Message { get; set; } = "This function is available to administrators only.";

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            if (user?.Identity?.IsAuthenticated != true)
                return; // global [Authorize] answers 401
            var method = context.HttpContext.Request.Method;
            if (WritesOnly && (HttpMethods.IsGet(method) || HttpMethods.IsHead(method)))
                return;
            if (!CallerAccess.IsAdmin(user))
                context.Result = new ObjectResult(new { message = Message })
                { StatusCode = StatusCodes.Status403Forbidden };
        }
    }
}
