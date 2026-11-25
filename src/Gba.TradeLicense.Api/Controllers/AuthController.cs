using Dapper;
using Gba.TradeLicense.Application.Abstractions;
using Gba.TradeLicense.Application.Models;
using Gba.TradeLicense.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Data;
using System.Net.Http;
using System.Text.Json;
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
    private readonly IMemoryCache _cache;
    private readonly Gba.TradeLicense.Api.Services.OtpGuard _otpGuard;

    public AuthController(IAuthService authService, ISmsService sms, IConfiguration config, IMemoryCache cache,
        Gba.TradeLicense.Api.Services.OtpGuard otpGuard)
    {
        _authService = authService;
        _sms = sms;
        _config = config;
        _cache = cache;
        _otpGuard = otpGuard;
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

        if (!await IsCaptchaValid(dto.CaptchaToken))
        {
            return BadRequest(new
            {
                Message = "Invalid CAPTCHA"
            });
        }

        // 🔐 The OTP must have been verified on the server for THIS mobile number
        if (!Gba.TradeLicense.Api.Services.OtpLoginTokens.TryConsume(_cache, dto.OtpToken, dto.MobileNumber))
        {
            return Unauthorized(new
            {
                Error = "OTP verification required. Please verify the OTP again."
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

    // ----------------- Google reCAPTCHA check -----------------
    private async Task<bool> IsCaptchaValid(string? token)
    {
        // Set ReCaptcha:BypassValidation = true only for local/dev
        if (_config.GetValue<bool>("ReCaptcha:BypassValidation"))
            return true;

        if (string.IsNullOrEmpty(token))
            return false;

        // Same as live build: frontend sends "test" when captcha is disabled
        if (token == "test")
            return true;

        try
        {
            var secret = _config["ReCaptcha:SecretKey"];
            using var client = new HttpClient();
            // secret + token in the POST body (form-encoded), not in a URL that proxies / logs can record
            var response = await client.PostAsync(
                "https://www.google.com/recaptcha/api/siteverify",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["secret"] = secret ?? string.Empty,
                    ["response"] = token
                }));
            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("success", out var success) && success.GetBoolean();
        }
        catch
        {
            return false;
        }
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

        if (!await IsCaptchaValid(request.CaptchaToken))
        {
            return BadRequest(new { Error = "Invalid CAPTCHA" });
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
            return BadRequest(new { Message = "Username is required" });

        using var db = Db();

        var user = await GetForgotPasswordUser(db, req.Login);

        if (user == null || string.IsNullOrWhiteSpace(user.MobileNo))
            return BadRequest(new { Message = "User not found" });

        string mobileNo = user.MobileNo;

        // If the user typed a mobile number, it must be the one registered for this username
        if (!string.IsNullOrWhiteSpace(req.MobileNo) && LastTenDigits(req.MobileNo) != LastTenDigits(mobileNo))
            return BadRequest(new { Message = "Mobile number does not match the registered mobile for this username" });

        // cooldown + per-number / per-IP limits
        var limited = _otpGuard.TryReserveSend(mobileNo, HttpContext.Connection.RemoteIpAddress?.ToString());
        if (limited != null)
            return StatusCode(StatusCodes.Status429TooManyRequests, new { Message = limited });

        string? otp = await db.ExecuteScalarAsync<string>(
            "USP_OTP_PROCESS",
            new
            {
                Action = "GENERATE",
                MobileNo = mobileNo
            },
            commandType: CommandType.StoredProcedure);

        if (string.IsNullOrEmpty(otp))
            return StatusCode(500, new { Message = "OTP could not be generated" });

        string date = DateTime.Now.ToString("dd-MM-yyyy");
        string time = DateTime.Now.ToString("HH:mm");

        // Order must match the DLT template:
        // "OTP IS {#var#} at {#var#} on {#var#} for Trade License Registration. ... {#var#} City Corporation Trade License."
        string gatewayResponse = await _sms.SendAsync(
            "OTP_PAYMENT",
            mobileNo,
            otp,
            time,
            date,
            _config["Sms:CorporationName"] ?? "BBMP");

        // Same check as SmsController: the gateway returns "402,MsgID = ..." when the message is accepted
        if (gatewayResponse == null || !gatewayResponse.TrimStart().StartsWith("402"))
            return StatusCode(502, new { Message = "Failed to send OTP. Please try again." });

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
    public async Task<IActionResult> VerifyOtp([FromBody] ForgotPasswordVerifyRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Login) ||
            string.IsNullOrWhiteSpace(req.Otp))
            return BadRequest(new { Message = "Username and OTP are required" });

        using var db = Db();

        // OTP was sent to the registered mobile, so verify against that, never a client-supplied number
        var user = await GetForgotPasswordUser(db, req.Login);

        if (user == null || string.IsNullOrWhiteSpace(user.MobileNo))
            return BadRequest(new { Message = "User not found" });

        // same protection as sms/otp/verify
        var verifyLock = _otpGuard.VerifyLock(user.MobileNo);
        if (!await verifyLock.WaitAsync(TimeSpan.FromSeconds(10)))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { Message = "Please try again." });

        int isValid;
        try
        {
            if (_otpGuard.IsVerifyLocked(user.MobileNo))
                return StatusCode(StatusCodes.Status429TooManyRequests,
                    new { IsValid = false, Message = "Too many wrong OTP attempts. Please request a new OTP after some time." });

            isValid = _otpGuard.WasAlreadyUsed(user.MobileNo, req.Otp)
                ? 0
                : await db.ExecuteScalarAsync<int>(
                    "USP_OTP_PROCESS",
                    new
                    {
                        Action = "VERIFY",
                        MobileNo = user.MobileNo,
                        OTP = req.Otp
                    },
                    commandType: CommandType.StoredProcedure);

            if (isValid == 1) _otpGuard.RecordSuccess(user.MobileNo, req.Otp);
            else _otpGuard.RecordFailure(user.MobileNo);
        }
        finally
        {
            verifyLock.Release();
        }

        if (isValid != 1)
            return BadRequest(new { IsValid = false, Message = "Invalid or expired OTP" });

        // One-time token proving the OTP was verified; required by reset-password
        string resetToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _cache.Set(ResetTokenCacheKey(resetToken), req.Login.Trim(), TimeSpan.FromMinutes(10));

        return Ok(new
        {
            IsValid = true,
            ResetToken = resetToken
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
            return BadRequest(new { Message = "Username and new password are required" });

        if (string.IsNullOrWhiteSpace(req.ResetToken) ||
            !_cache.TryGetValue(ResetTokenCacheKey(req.ResetToken), out string? tokenLogin) ||
            !string.Equals(tokenLogin, req.Login.Trim(), StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { Message = "OTP verification expired. Please verify OTP again." });

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
            return BadRequest(new { Message = "Password reset failed" });

        _cache.Remove(ResetTokenCacheKey(req.ResetToken));

        return Ok(new
        {
            Message = "Password reset successfully"
        });
    }

    private static Task<ForgotPasswordUser?> GetForgotPasswordUser(IDbConnection db, string login)
        => db.QueryFirstOrDefaultAsync<ForgotPasswordUser>(
            "USP_ADMIN_FORGOT_PASSWORD",
            new
            {
                Action = "GET_MOBILE",
                Login = login
            },
            commandType: CommandType.StoredProcedure);

    private static string LastTenDigits(string mobile)
    {
        var digits = new string(mobile.Where(char.IsDigit).ToArray());
        return digits.Length > 10 ? digits[^10..] : digits;
    }

    private static string ResetTokenCacheKey(string token) => "pwd-reset:" + token;
}
