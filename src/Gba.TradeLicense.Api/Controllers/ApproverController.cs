using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gba.TradeLicense.Api.Controllers;

[ApiController]
[Route("api/approver")]
[Authorize(Policy = "ApproverOrSenior")]
public sealed class ApproverController : ControllerBase
{
    [HttpGet("dashboard")]
    public IActionResult Dashboard()
        => Ok(new { pending = 0, dueForInspection = 0, note = "Wire this to DB queries." });

    [HttpPost("applications/{applicationNo}/approve")]
    public IActionResult Approve(string applicationNo)
        => Ok(new { applicationNo, status = "Approved (stub)" });

    [HttpPost("applications/{applicationNo}/reject")]
    public IActionResult Reject(string applicationNo)
        => Ok(new { applicationNo, status = "Rejected (stub)" });
}
