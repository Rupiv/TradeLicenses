using Gba.TradeLicense.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _service;

    public DashboardController(IDashboardService service)
    {
        _service = service;
    }

    [HttpGet("application-status-count/{loginID}")]
    public async Task<IActionResult> GetStatusCount(
        int loginID,
        CancellationToken ct)
    {
        var result = await _service.GetStatusCountAsync(loginID, ct);
        return Ok(result);
    }
}
