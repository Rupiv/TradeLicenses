using System.Data;
using Dapper;
using Gba.TradeLicense.Application.Abstractions;

using Gba.TradeLicense.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;

namespace Gba.TradeLicense.Api.Controllers
{
    [ApiController]
    [Route("api/sms")]
    public class SmsController : ControllerBase
    {
        private readonly ISmsService _sms;
        private readonly IConfiguration _config;
        private readonly ILogger<SmsController> _logger;
        private readonly IMemoryCache _cache;
        private readonly Gba.TradeLicense.Api.Services.OtpGuard _otpGuard;

        public SmsController(ISmsService sms, IConfiguration config, ILogger<SmsController> logger, IMemoryCache cache,
            Gba.TradeLicense.Api.Services.OtpGuard otpGuard)
        {
            _sms = sms;
            _config = config;
            _logger = logger;
            _cache = cache;
            _otpGuard = otpGuard;
        }

        private IDbConnection CreateConnection()
            => new SqlConnection(_config.GetConnectionString("Default"));

        /* ==========================================================
           APPLICATION RECEIVED
        ========================================================== */
        [Gba.TradeLicense.Api.Services.AdminOnly]
        [HttpPost("application-received")]
        public async Task<IActionResult> ApplicationReceived(
            [FromBody] ApplicationSmsRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.ApplicationNo)
                || string.IsNullOrWhiteSpace(req.MobileNo))
                return BadRequest("Invalid request");

            await _sms.SendAsync(
                "APP_RECEIVED",
                req.MobileNo,
                req.ApplicationNo);

            return Ok(new { Message = "SMS sent successfully" });
        }

        /* ==========================================================
           SEND OTP
        ========================================================== */
        // Used by login / registration / new application before the user has a JWT
        [AllowAnonymous]
        [HttpPost("otp/send")]
        public async Task<IActionResult> SendOtp(
            [FromBody] OtpRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.MobileNo))
                return BadRequest("Mobile number is required");

            // cooldown + per-number / per-IP limits (before an OTP is generated or sent)
            var limited = _otpGuard.TryReserveSend(req.MobileNo, HttpContext.Connection.RemoteIpAddress?.ToString());
            if (limited != null)
                return StatusCode(StatusCodes.Status429TooManyRequests, new { Message = limited });

            using var db = CreateConnection();

            string? otp = await db.ExecuteScalarAsync<string>(
                "USP_OTP_PROCESS",
                new
                {
                    Action = "GENERATE",
                    MobileNo = req.MobileNo
                },
                commandType: CommandType.StoredProcedure);

            if (string.IsNullOrWhiteSpace(otp))
                return StatusCode(500, new { Message = "OTP could not be generated" });

            string date = DateTime.Now.ToString("dd-MM-yyyy");
            string time = DateTime.Now.ToString("HH:mm");

            // Order must match the DLT template:
            // "OTP IS {#var#} at {#var#} on {#var#} for Trade License Registration. ... {#var#} City Corporation Trade License."
            string gatewayResponse = await _sms.SendAsync(
                "OTP_PAYMENT",
                req.MobileNo,
                otp,
                time,
                date,
                "BBMP");

            _logger.LogInformation("SMS gateway response for {MobileNo}: {Response}",
                req.MobileNo, gatewayResponse);

            // Karnataka (CDAC MSDG) gateway returns "402,MsgID = ..." when the message is accepted;
            // any other text is an error (credentials, sender ID, template, credits, ...).
            if (gatewayResponse == null || !gatewayResponse.TrimStart().StartsWith("402"))
                return StatusCode(502, new { Message = "Failed to send OTP" }); // gateway text stays in the log

            return Ok(new { Message = "OTP sent successfully" });
        }

        /* ==========================================================
           VERIFY OTP
        ========================================================== */
        [AllowAnonymous]
        [HttpPost("otp/verify")]
        public async Task<IActionResult> VerifyOtp(
            [FromBody] OtpVerifyRequestDto req)
        {
            if (string.IsNullOrWhiteSpace(req.MobileNo)
                || string.IsNullOrWhiteSpace(req.Otp))
                return BadRequest("Invalid OTP request");

            // one verification at a time per number; lockout after repeated wrong OTPs;
            // an OTP already verified once is not accepted again
            var verifyLock = _otpGuard.VerifyLock(req.MobileNo);
            if (!await verifyLock.WaitAsync(TimeSpan.FromSeconds(10)))
                return StatusCode(StatusCodes.Status429TooManyRequests, new { Message = "Please try again." });

            int isValid;
            try
            {
                if (_otpGuard.IsVerifyLocked(req.MobileNo))
                    return StatusCode(StatusCodes.Status429TooManyRequests,
                        new { IsValid = false, Message = "Too many wrong OTP attempts. Please request a new OTP after some time." });

                if (_otpGuard.WasAlreadyUsed(req.MobileNo, req.Otp))
                {
                    isValid = 0;
                }
                else
                {
                    using var db = CreateConnection();

                    isValid = await db.ExecuteScalarAsync<int>(
                        "USP_OTP_PROCESS",
                        new
                        {
                            Action = "VERIFY",
                            MobileNo = req.MobileNo,
                            OTP = req.Otp
                        },
                        commandType: CommandType.StoredProcedure);
                }

                if (isValid == 1) _otpGuard.RecordSuccess(req.MobileNo, req.Otp);
                else _otpGuard.RecordFailure(req.MobileNo);
            }
            finally
            {
                verifyLock.Release();
            }

            // A verified OTP also returns a one-time token (10 min, bound to this mobile number);
            // auth/login-user requires it, so a trader session can no longer be opened with the
            // mobile number alone.
            return Ok(new
            {
                IsValid = isValid == 1,
                OtpToken = isValid == 1
                    ? Gba.TradeLicense.Api.Services.OtpLoginTokens.Issue(_cache, req.MobileNo)
                    : null
            });
        }

        /* ==========================================================
           PROVISIONAL CERTIFICATE
        ========================================================== */
        [Gba.TradeLicense.Api.Services.AdminOnly]
        [HttpPost("provisional")]
        public async Task<IActionResult> Provisional(
            [FromBody] ApplicationSmsRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.ApplicationNo)
                || string.IsNullOrWhiteSpace(req.MobileNo))
                return BadRequest("Invalid request");

            await _sms.SendAsync(
                "PROVISIONAL_ISSUED",
                req.MobileNo,
                req.ApplicationNo);

            return Ok(new { Message = "SMS sent successfully" });
        }

        /* ==========================================================
           FINAL DECISION
        ========================================================== */
        [Gba.TradeLicense.Api.Services.AdminOnly]
        [HttpPost("final-decision")]
        public async Task<IActionResult> FinalDecision(
            [FromBody] FinalDecisionSmsRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Status)
                || string.IsNullOrWhiteSpace(req.ApplicationNo)
                || string.IsNullOrWhiteSpace(req.OfficeName)
                || string.IsNullOrWhiteSpace(req.MobileNo))
                return BadRequest("Invalid request");

            string templateKey =
                req.Status.Equals("APPROVED", StringComparison.OrdinalIgnoreCase)
                ? "APP_APPROVED"
                : "APP_REJECTED";

            await _sms.SendAsync(
                templateKey,
                req.MobileNo,
                req.ApplicationNo,
                req.OfficeName);

            return Ok(new { Message = "SMS sent successfully" });
        }
    }
}
