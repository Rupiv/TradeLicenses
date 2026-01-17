using System.Data;
using Gba.TradeLicense.Application.Abstractions;
using Gba.TradeLicense.Application.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }
    [HttpPost("register")]
    public async Task<IActionResult> Register(
         [FromBody] RegisterUserDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName)
            || string.IsNullOrWhiteSpace(dto.MobileNumber))
            return BadRequest("Invalid request");

        using var db = Db();

        try
        {
            var userId = await db.ExecuteScalarAsync<int>(
                "usp_UserAuth_CRUD",
                new
                {
                    Action = "REGISTER",
                    dto.FullName,
                    dto.MobileNumber,
                    dto.EmailID
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(new
            {
                UserID = userId,
                Message = "User registered successfully"
            });
        }
        catch (SqlException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /* ======================================================
       LOGIN (AFTER OTP VERIFIED)
    ====================================================== */
    [HttpPost("login-USER")]
    public async Task<IActionResult> Login_USER(
        [FromBody] LoginDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.MobileNumber))
            return BadRequest("Mobile number is required");

        using var db = Db();

        var user = await db.QueryFirstOrDefaultAsync(
            "usp_UserAuth_CRUD",
            new
            {
                Action = "LOGIN",
                dto.MobileNumber
            },
            commandType: CommandType.StoredProcedure
        );

        if (user == null)
            return Unauthorized("User not found");

        return Ok(user);
    }

// ----------------- Login -----------------
[HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.UsernameOrPhone) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { Error = "Username/Phone and Password are required." });
        }

        // ✅ Capture IP & Browser SERVER-SIDE (trusted)
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "UNKNOWN";
        var browser = Request.Headers["User-Agent"].ToString();
        var designation = User.FindFirst("designation")?.Value;

        // Call service with credentials + server metadata
        var result = await _authService.LoginAsync(
            request.UsernameOrPhone,
            request.Password,
            ipAddress,
            browser,
            ct
        );

        if (!result.Success && !result.OtpRequired)
            return Unauthorized(new { result.Error });

        return Ok(new
        {
            result.Success,
            result.AccessToken,
            result.OtpRequired,
            result.Error
        });
    }

    // ----------------- Send OTP -----------------
    [HttpPost("send-otp")]
    public async Task<IActionResult> SendOtp([FromBody] OtpSendRequest request, CancellationToken ct)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.Phone) ||
            string.IsNullOrWhiteSpace(request.Purpose))
        {
            return BadRequest(new { Error = "Phone and Purpose are required." });
        }

        var result = await _authService.SendOtpAsync(request, ct);

        return Ok(new
        {
            result.Success,
            result.Message
        });
    }

    // ----------------- Verify OTP -----------------
    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] OtpVerifyRequest request, CancellationToken ct)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.Phone) ||
            string.IsNullOrWhiteSpace(request.Otp))
        {
            return BadRequest(new { Error = "Phone and OTP are required." });
        }

        var result = await _authService.VerifyOtpAsync(request, ct);

        if (!result.Success)
            return Unauthorized(new { result.Error });

        return Ok(new
        {
            result.Success,
            result.AccessToken,
            result.Error
        });
    }
}
