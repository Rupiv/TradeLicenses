
using Gba.TradeLicense.Infrastructure.Sms.esms_client;
using Microsoft.Extensions.Configuration;

namespace Gba.TradeLicense.Infrastructure.Sms;

public sealed class KarnatakaSmsService
{
    private readonly IConfiguration _config;
    private readonly SMSHttpPostClient _client;

    public KarnatakaSmsService(IConfiguration config)
    {
        _config = config;
        _client = new SMSHttpPostClient();
    }

    public string SendOtp(string mobile, string otpMessage)
    {
        var sms = _config.GetSection("KarnatakaSms");

        return _client.sendOTPMSG(
            username: sms["Username"]!,
            password: sms["Password"]!,
            senderid: sms["SenderId"]!,
            mobileNo: mobile,
            message: otpMessage,
            secureKey: sms["SecureKey"]!,
            templateid: sms["TemplateId"]!
        );
    }

    public string SendSingleSms(string mobile, string message)
    {
        var sms = _config.GetSection("KarnatakaSms");

        return _client.sendSingleSMS(
            username: sms["Username"]!,
            password: sms["Password"]!,
            senderid: sms["SenderId"]!,
            mobileNo: mobile,
            message: message,
            secureKey: sms["SecureKey"]!,
            templateid: sms["TemplateId"]!
        );
    }

    public string SendBulkSms(string mobilesCsv, string message)
    {
        var sms = _config.GetSection("KarnatakaSms");

        return _client.sendBulkSMS(
            username: sms["Username"]!,
            password: sms["Password"]!,
            senderid: sms["SenderId"]!,
            mobileNos: mobilesCsv,
            message: message,
            secureKey: sms["SecureKey"]!,
            templateid: sms["TemplateId"]!
        );
    }
}
