using Dapper;

using Gba.TradeLicense.Application.Abstractions;
using Gba.TradeLicense.Infrastructure.Sms.esms_client;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

public class SmsService : ISmsService
{
    private readonly IConfiguration _config;
    private readonly SMSHttpPostClient _client;
    private readonly string _connStr;
    private readonly ILogger<SmsService> _logger;

    public SmsService(IConfiguration config, ILogger<SmsService> logger)
    {
        _config = config;
        _logger = logger;
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

        // ✅ FIX: Replace {0}, {1}, {2} placeholders
        for (int i = 0; i < variables.Length; i++)
        {
            message = message.Replace("{" + i + "}", variables[i]);
        }

        // DLT-style templates use {#var#} for every variable; fill them in order.
        // The surrounding brace is matched loosely because copied templates sometimes carry
        // non-ASCII braces (which show up as "?#var#?" on the handset).
        int varIndex = 0;
        message = Regex.Replace(message, @"\S#var#\S", m =>
            varIndex < variables.Length ? (variables[varIndex++] ?? string.Empty) : m.Value);

        var smsCfg = _config.GetSection("Sms");

        _logger.LogInformation(
            "Sending SMS {TemplateKey}: SmsType={SmsType}, TemplateId={TemplateId}, Content=[{Content}]",
            templateKey, (string)template.SmsType, (string)template.TemplateId, message);

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
