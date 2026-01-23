using Dapper;
using Gba.TradeLicense.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Security.Claims;

[ApiController]
[Route("api/licence-workflow")]
[Authorize]   // JWT protected
public class LicenceWorkflowController : ControllerBase
{
    private readonly IConfiguration _config;

    public LicenceWorkflowController(IConfiguration config)
    {
        _config = config;
    }

    private IDbConnection Db()
        => new SqlConnection(_config.GetConnectionString("DefaultConnection"));
    [HttpPost("action")]
    public async Task<IActionResult> WorkflowAction(
    [FromBody] LicenceWorkflowRequest request)
    {
        int loginId = int.Parse(User.FindFirstValue("loginID"));
        string role = User.FindFirstValue(ClaimTypes.Role);
        // role: INSPECTOR / SENIOR / ADMIN

        using var db = Db();

        try
        {
            var result = await db.QuerySingleAsync<dynamic>(
                "usp_LicenceApplication_Workflow_Action",
                new
                {
                    licenceApplicationID = request.LicenceApplicationID,
                    licenceProcessID = request.LicenceProcessID,
                    remarks = request.Remarks,
                    ActionReasonIds = request.ActionReasonIds,
                    loginID = loginId,
                    role = role
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(result);
        }
        catch (SqlException ex)
        {
            return BadRequest(new
            {
                Success = 0,
                Message = ex.Message
            });
        }
    }
    [HttpGet("{licenceApplicationId}/timeline")]
    public async Task<IActionResult> GetWorkflowTimeline(long licenceApplicationId)
    {
        using var db = Db();

        var data = await db.QueryAsync(
            @"SELECT 
                s.licenceApplicationStatusName AS Status,
                d.Remarks,
                d.entryDate
              FROM Licence_ApplicationStatusDetails d
              INNER JOIN Licence_ApplicationStatus s
                ON d.licenceApplicationStatusID = s.licenceApplicationStatusID
              WHERE d.licenceApplicationID = @id
              ORDER BY d.entryDate",
            new { id = licenceApplicationId }
        );

        return Ok(data);
    }
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingApplications()
    {
        string role = User.FindFirstValue(ClaimTypes.Role);
        int loginId = int.Parse(User.FindFirstValue("loginID"));

        int statusId = role switch
        {
            "INSPECTOR" => 2, // APPLIED
            "SENIOR" => 5,    // FORWARD
            _ => 0
        };

        using var db = Db();

        var data = await db.QueryAsync(
            @"SELECT *
              FROM Licence_Application
              WHERE licenceApplicationStatusID = @status",
            new { status = statusId }
        );

        return Ok(data);
    }
}
