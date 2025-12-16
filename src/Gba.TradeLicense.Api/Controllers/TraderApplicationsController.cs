using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Gba.TradeLicense.Application.Abstractions;
using Gba.TradeLicense.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gba.TradeLicense.Api.Controllers;

[ApiController]
[Route("api/trader/applications")]
[Authorize(Policy = "TraderOnly")]
public sealed class TraderApplicationsController : ControllerBase
{
    private readonly ITradeApplicationService _apps;
    public TraderApplicationsController(ITradeApplicationService apps) => _apps = apps;

    private Guid CurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ApplicationCreateRequest req, CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var res = await _apps.CreateAsync(userId, req, ct);
        if (!res.Success) return BadRequest(new { error = res.Error });

        return Ok(new { applicationNo = res.ApplicationNo });
    }

    [HttpPost("{applicationNo}/submit")]
    public async Task<IActionResult> Submit([FromRoute] string applicationNo, CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var res = await _apps.SubmitAsync(applicationNo, userId, ct);
        if (!res.Success) return BadRequest(new { error = res.Error });

        return Ok(new { status = res.Status });
    }

    [HttpGet("{applicationNo}/status")]
    public async Task<IActionResult> Status([FromRoute] string applicationNo, CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var res = await _apps.GetStatusAsync(applicationNo, userId, ct);
        if (!res.Success) return NotFound(new { error = res.Error });

        return Ok(new { applicationNo = res.ApplicationNo, status = res.Status, history = res.History });
    }
}
