using System.Security.Claims;
using Gba.TradeLicense.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;
using static Gba.TradeLicense.Api.Services.ReportScope;

namespace Gba.TradeLicense.Api.Tests;

public class ReportScopeTests
{
    private static OfficerScope Officer(params Assignment[] a) => new(false, a);
    private static readonly OfficerScope Admin = new(true, Array.Empty<Assignment>());

    [Fact]
    public void Admin_KeepsExistingAccess_AllOrRequestedCorporation()
    {
        Assert.Equal((null, null), ResolveCorporation(Admin, null));
        Assert.Equal((3, null), ResolveCorporation(Admin, 3));
    }

    [Fact]
    public void AssignedOfficer_IsForcedToTheirCorporation()
    {
        var (corp, deny) = ResolveCorporation(Officer(new Assignment(2, null, null)), null);
        Assert.Null(deny);
        Assert.Equal(2, corp);
    }

    [Fact]
    public void AssignedOfficer_CanRequestTheirOwnCorporation()
    {
        Assert.Equal((2, null), ResolveCorporation(Officer(new Assignment(2, null, null)), 2));
    }

    [Fact]
    public void CrossCorporationRequest_IsDenied()
    {
        var (corp, deny) = ResolveCorporation(Officer(new Assignment(2, null, null)), 4);
        Assert.Null(corp);
        Assert.NotNull(deny);
    }

    [Fact]
    public void ZoneOrWardLevelOfficer_IsDenied_ForTheCorporationWideList()
    {
        Assert.NotNull(ResolveCorporation(Officer(new Assignment(2, 11, null)), null).DenyReason);
        Assert.NotNull(ResolveCorporation(Officer(new Assignment(2, 11, 105)), 2).DenyReason); // cross-ward / override attempt
    }

    [Fact]
    public void UnassignedOfficer_IsDenied_EvenWhenNamingACorporation()
    {
        Assert.NotNull(ResolveCorporation(Officer(), null).DenyReason);
        Assert.NotNull(ResolveCorporation(Officer(), 1).DenyReason);
    }

    [Fact]
    public void OfficerWithSeveralCorporations_MustPickOneOfThem()
    {
        var scope = Officer(new Assignment(1, null, null), new Assignment(3, null, null));
        Assert.NotNull(ResolveCorporation(scope, null).DenyReason);
        Assert.Equal((3, null), ResolveCorporation(scope, 3));
        Assert.NotNull(ResolveCorporation(scope, 2).DenyReason);
    }

    [Fact]
    public void WardRowsInAnotherCorporation_DoNotWidenTheScope()
    {
        var scope = Officer(new Assignment(1, null, null), new Assignment(5, 50, 500));
        Assert.Equal((1, null), ResolveCorporation(scope, null));
        Assert.NotNull(ResolveCorporation(scope, 5).DenyReason);
    }
}

public class ReportFilterTests
{
    private static AuthorizationFilterContext Context(string? role)
    {
        var identity = role == null
            ? new ClaimsIdentity()
            : new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.NameIdentifier, "7") }, "test");
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        return new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>());
    }

    private static int? Status(IActionResult? r) => r switch
    {
        ObjectResult o => o.StatusCode,
        ForbidResult => 403,
        null => null,
        _ => -1
    };

    [Theory]
    [InlineData("Admin", null)]                 // admin: allowed
    [InlineData("Approver", 403)]               // officers: denied until the reports can be scoped
    [InlineData("SeniorApprover", 403)]
    [InlineData("Zone Approver", 403)]
    [InlineData("TRADE_OWNER", 403)]
    public void Reports_AreAdminOnly(string role, int? expected)
    {
        var ctx = Context(role);
        new AdminOnlyAttribute().OnAuthorization(ctx);
        Assert.Equal(expected, Status(ctx.Result));
    }

    [Fact]
    public void Reports_Anonymous_IsLeftToTheGlobalAuthorizeFilter()
    {
        var ctx = Context(null);
        new AdminOnlyAttribute().OnAuthorization(ctx);
        Assert.Null(ctx.Result);
    }

    [Theory]
    [InlineData("TRADE_OWNER", "GET", 403)]
    [InlineData("TRADE_OWNER", "POST", 403)]
    [InlineData("Approver", "GET", null)]
    [InlineData("Admin", "POST", null)]
    public void StaffOnly_BlocksTraders(string role, string method, int? expected)
    {
        var ctx = Context(role);
        ctx.HttpContext.Request.Method = method;
        new StaffOnlyAttribute().OnAuthorization(ctx);
        Assert.Equal(expected, Status(ctx.Result));
    }
}

public class AdminFunctionTests
{
    private static AuthorizationFilterContext Context(string role, string method)
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.NameIdentifier, "7") }, "test");
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        http.Request.Method = method;
        return new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>());
    }

    [Theory]
    // user management, jurisdiction changes, payment administration: admin only
    [InlineData("Admin", "POST", null)]
    [InlineData("Approver", "POST", 403)]
    [InlineData("SeniorApprover", "PUT", 403)]
    [InlineData("Zone Approver", "DELETE", 403)]
    [InlineData("Approver", "GET", 403)]
    public void AdminOnly_AllMethods(string role, string method, int? expected)
    {
        var ctx = Context(role, method);
        new AdminOnlyAttribute().OnAuthorization(ctx);
        Assert.Equal(expected, (ctx.Result as ObjectResult)?.StatusCode);
    }

    [Theory]
    // master data: officers keep reading (the forms need it), only admin changes it
    [InlineData("Approver", "GET", null)]
    [InlineData("Approver", "POST", 403)]
    [InlineData("Approver", "PUT", 403)]
    [InlineData("Approver", "DELETE", 403)]
    [InlineData("Admin", "PUT", null)]
    public void AdminOnly_WritesOnly(string role, string method, int? expected)
    {
        var ctx = Context(role, method);
        new AdminOnlyAttribute { WritesOnly = true }.OnAuthorization(ctx);
        Assert.Equal(expected, (ctx.Result as ObjectResult)?.StatusCode);
    }

    [Fact]
    public void AdminOnly_UsesTheConfiguredMessage()
    {
        var ctx = Context("Approver", "GET");
        new AdminOnlyAttribute { Message = "Reports: administrators only" }.OnAuthorization(ctx);
        Assert.Contains("Reports: administrators only", System.Text.Json.JsonSerializer.Serialize((ctx.Result as ObjectResult)!.Value));
    }

    [Fact]
    public void SafeDbMessage_HidesInternalErrors()
    {
        Assert.Equal("fallback", CallerAccess.SafeDbMessage(new InvalidOperationException("Invalid object name 'dbo.X'"), "fallback"));
        Assert.Equal("fallback", CallerAccess.SafeDbMessage(new Exception("Login failed for user 'sa'"), "fallback"));
    }
}
