using Gba.TradeLicense.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/sms")]
public class SmsController : ControllerBase
{
    private readonly ISmsService _sms;

    public SmsController(ISmsService sms)
    {
        _sms = sms;
    }

    [HttpPost("application-received")]
    public async Task<IActionResult> ApplicationReceived(
        string applicationNo,
        string mobileNo)
    {
        await _sms.SendAsync("APP_RECEIVED", mobileNo, applicationNo);
        return Ok();
    }

    [HttpPost("send-otp")]
    public async Task<IActionResult> SendOtp(
        string otp,
        string date,
        string time,
        string mobileNo)
    {
        await _sms.SendAsync("OTP_PAYMENT", mobileNo, otp, date, time);
        return Ok();
    }

    [HttpPost("provisional")]
    public async Task<IActionResult> Provisional(
        string applicationNo,
        string mobileNo)
    {
        await _sms.SendAsync("PROVISIONAL_ISSUED", mobileNo, applicationNo);
        return Ok();
    }

    [HttpPost("final-decision")]
    public async Task<IActionResult> FinalDecision(
        string status,
        string applicationNo,
        string officeName,
        string mobileNo)
    {
        var key = status == "APPROVED" ? "APP_APPROVED" : "APP_REJECTED";
        await _sms.SendAsync(key, mobileNo, applicationNo, officeName);
        return Ok();
    }
}
