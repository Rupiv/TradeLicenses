using System.Data;
using Dapper;
using Gba.TradeLicense.Application.Abstractions;

using Gba.TradeLicense.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Gba.TradeLicense.Api.Controllers
{
    [ApiController]
    [Route("api/sms")]
    public class SmsController : ControllerBase
    {
        private readonly ISmsService _sms;
        private readonly IConfiguration _config;

        public SmsController(ISmsService sms, IConfiguration config)
        {
            _sms = sms;
            _config = config;
        }

        private IDbConnection CreateConnection()
            => new SqlConnection(_config.GetConnectionString("Default"));

        /* ==========================================================
           APPLICATION RECEIVED
        ========================================================== */
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
        [HttpPost("otp/send")]
        public async Task<IActionResult> SendOtp(
            [FromBody] OtpRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.MobileNo))
                return BadRequest("Mobile number is required");

            using var db = CreateConnection();

            string otp = await db.ExecuteScalarAsync<string>(
                "USP_OTP_PROCESS",
                new
                {
                    Action = "GENERATE",
                    MobileNo = req.MobileNo
                },
                commandType: CommandType.StoredProcedure);

            string date = DateTime.Now.ToString("dd-MM-yyyy");
            string time = DateTime.Now.ToString("HH:mm");

            await _sms.SendAsync(
                "OTP_PAYMENT",
                req.MobileNo,
                otp,
                date,
                time);

            return Ok(new { Message = "OTP sent successfully" });
        }

        /* ==========================================================
           VERIFY OTP
        ========================================================== */
        [HttpPost("otp/verify")]
        public async Task<IActionResult> VerifyOtp(
            [FromBody] OtpVerifyRequestDto req)
        {
            if (string.IsNullOrWhiteSpace(req.MobileNo)
                || string.IsNullOrWhiteSpace(req.Otp))
                return BadRequest("Invalid OTP request");

            using var db = CreateConnection();

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
           PROVISIONAL CERTIFICATE
        ========================================================== */
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
