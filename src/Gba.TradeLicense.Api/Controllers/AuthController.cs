using System.Data;
using Gba.TradeLicense.Application.Abstractions;
using Gba.TradeLicense.Application.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IConfiguration _config;

    public AuthController(IAuthService authService, IConfiguration config)
    {
        _authService = authService;
        _config = config;
        
    }
    private IDbConnection Db()
             => new SqlConnection(_config.GetConnectionString("Default"));
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterUserDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName) ||
            string.IsNullOrWhiteSpace(dto.MobileNumber))
        {
            return BadRequest(new
            {
                Message = "Full name and mobile number are required"
            });
        }

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
            // Business validation errors from SP
            if (ex.Message.Contains("Mobile number already registered"))
            {
                return Conflict(new { Message = "Mobile number already registered" });
            }

            if (ex.Message.Contains("Email ID already registered"))
            {
                return Conflict(new { Message = "Email ID already registered" });
            }

            // Unknown SQL error
            return StatusCode(500, new
            {
                Message = "An error occurred while registering user"
            });
        }
    }

    /* ======================================================
       LOGIN (AFTER OTP VERIFIED)
    ====================================================== */
    [HttpPost("login-user")]
    public async Task<IActionResult> Login_USER(
        [FromBody] LoginDto dto,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.MobileNumber))
        {
            return BadRequest(new
            {
                Message = "Mobile number is required"
            });
        }

        var result = await _authService.LoginUserByMobileAsync(
            dto.MobileNumber,
            ct
        );

        if (!result.Success)
            return Unauthorized(new { result.Error });

        return Ok(new
        {
            result.Success,
            result.AccessToken
        });
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
