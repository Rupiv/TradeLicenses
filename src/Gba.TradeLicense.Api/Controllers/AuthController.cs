using Dapper;
using Gba.TradeLicense.Application.Abstractions;
using Gba.TradeLicense.Application.Models;
using Gba.TradeLicense.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
[AllowAnonymous]
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ISmsService _sms;
    private readonly IConfiguration _config;

    public AuthController(IAuthService authService, ISmsService sms, IConfiguration config)
    {
        _authService = authService;
        _sms = sms;
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
    [HttpPost("forgot-password/send-otp")]
    public async Task<IActionResult> SendOtp([FromBody] ForgotPasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Login))
            return BadRequest("Login is required");

        using var db = Db();

        var user = await db.QueryFirstOrDefaultAsync<ForgotPasswordUser>(
            "USP_ADMIN_FORGOT_PASSWORD",
            new
            {
                Action = "GET_MOBILE",
                Login = req.Login
            },
            commandType: CommandType.StoredProcedure);

        if (user == null)
            return BadRequest("User not found");

        string mobileNo = user.MobileNo;

        string? otp = await db.ExecuteScalarAsync<string>(
            "USP_OTP_PROCESS",
            new
            {
                Action = "GENERATE",
                MobileNo = mobileNo
            },
            commandType: CommandType.StoredProcedure);

        if (string.IsNullOrEmpty(otp))
            return BadRequest("OTP generation failed");

        string date = DateTime.Now.ToString("dd-MM-yyyy");
        string time = DateTime.Now.ToString("HH:mm");

        await _sms.SendAsync(
            "OTP_PAYMENT",
            mobileNo,
            otp,
            date,
            time);

        string maskedMobile = mobileNo.Length > 4
            ? new string('X', mobileNo.Length - 4) + mobileNo[^4..]
            : mobileNo;

        return Ok(new
        {
            Message = "OTP sent successfully",
            MobileNo = maskedMobile
        });
    }

    /* ==========================================================
       VERIFY OTP
    ========================================================== */

    [HttpPost("forgot-password/verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] OtpVerifyRequestDto req)
    {
        if (string.IsNullOrWhiteSpace(req.MobileNo) ||
            string.IsNullOrWhiteSpace(req.Otp))
            return BadRequest("Invalid request");

        using var db = Db();

        int isValid = await db.ExecuteScalarAsync<int>(
            "USP_OTP_PROCESS",
            new
            {
                Action = "VERIFY",
                MobileNo = req.MobileNo,
                OTP = req.Otp
            },
            commandType: CommandType.StoredProcedure);

        return Ok(new
        {
            IsValid = isValid == 1
        });
    }

    /* ==========================================================
       RESET PASSWORD
    ========================================================== */

    [HttpPost("forgot-password/reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Login) ||
            string.IsNullOrWhiteSpace(req.NewPassword))
            return BadRequest("Invalid request");

        using var db = Db();

        int result = await db.ExecuteScalarAsync<int>(
            "USP_ADMIN_FORGOT_PASSWORD",
            new
            {
                Action = "RESET_PASSWORD",
                Login = req.Login,
                NewPassword = req.NewPassword
            },
            commandType: CommandType.StoredProcedure);

        if (result != 1)
            return BadRequest("Password reset failed");

        return Ok(new
        {
            Message = "Password reset successfully"
        });
    }
}
