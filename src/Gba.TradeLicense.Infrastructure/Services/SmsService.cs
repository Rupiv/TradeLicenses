using Dapper;

using Gba.TradeLicense.Application.Abstractions;
using Gba.TradeLicense.Infrastructure.Sms.esms_client;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

public class SmsService : ISmsService
{
    private readonly IConfiguration _config;
    private readonly SMSHttpPostClient _client;
    private readonly string _connStr;

    public SmsService(IConfiguration config)
    {
        _config = config;
        _client = new SMSHttpPostClient();
        _connStr = _config.GetConnectionString("Default");
    }

    public async Task<string> SendAsync(
        string templateKey,
        string mobileNo,
        params string[] variables)
    {
        using var db = new SqlConnection(_connStr);

        var template = await db.QueryFirstOrDefaultAsync<dynamic>(
            @"SELECT TemplateId, TemplateText, SmsType
              FROM SMS_Template_Master
              WHERE TemplateKey = @templateKey AND IsActive = 'Y'",
            new { templateKey });

        if (template == null)
            throw new Exception("SMS template not found");

        string message = template.TemplateText;

        foreach (var v in variables)
            message = message.Replace("{#var#}", v, StringComparison.OrdinalIgnoreCase);

        var smsCfg = _config.GetSection("Sms");

        return template.SmsType switch
        {
            "OTP" => _client.sendOTPMSG(
                        smsCfg["Username"],
                        smsCfg["Password"],
                        smsCfg["SenderId"],
                        mobileNo,
                        message,
                        smsCfg["SecureKey"],
                        template.TemplateId),

            "UNICODE" => _client.sendUnicodeSMS(
                        smsCfg["Username"],
                        smsCfg["Password"],
                        smsCfg["SenderId"],
                        mobileNo,
                        message,
                        smsCfg["SecureKey"],
                        template.TemplateId),

            "UNICODE_OTP" => _client.sendUnicodeOTPSMS(
                        smsCfg["Username"],
                        smsCfg["Password"],
                        smsCfg["SenderId"],
                        mobileNo,
                        message,
                        smsCfg["SecureKey"],
                        template.TemplateId),

            _ => _client.sendSingleSMS(
                        smsCfg["Username"],
                        smsCfg["Password"],
                        smsCfg["SenderId"],
                        mobileNo,
                        message,
                        smsCfg["SecureKey"],
                        template.TemplateId)
        };
    }
}
