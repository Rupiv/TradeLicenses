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
        {
            await LogAsync(templateKey, null, null, mobileNo, variables, null, "FAILED", null, "SMS template not found");
            throw new Exception("SMS template not found");
        }

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

        string smsType = (string)template.SmsType;
        string templateId = (string)template.TemplateId;
        string response;

        try
        {
            response = SendViaGateway(smsType, mobileNo, message, templateId, smsCfg);
        }
        catch (Exception ex)
        {
            await LogAsync(templateKey, templateId, smsType, mobileNo, variables, message, "FAILED", null, ex.Message);
            throw;
        }

        // Karnataka (CDAC MSDG) gateway returns "402,MsgID = ..." when the message is accepted
        bool accepted = response != null && response.TrimStart().StartsWith("402");
        await LogAsync(templateKey, templateId, smsType, mobileNo, variables, message,
            accepted ? "SENT" : "FAILED", response, accepted ? null : "Gateway did not accept the message");

        return response;
    }

    /* ==========================================================
       SMS LOG -> dbo.SMS_Message_Log (via usp_SMS_Message_Log)
       Never throws: a logging problem must not stop the SMS flow.
    ========================================================== */
    private async Task LogAsync(
        string templateKey, string? templateId, string? smsType, string mobileNo, string[] variables,
        string? message, string status, string? gatewayResponse, string? error)
    {
        try
        {
            bool isOtp = smsType != null && smsType.Contains("OTP", StringComparison.OrdinalIgnoreCase)
                         || templateKey.StartsWith("OTP", StringComparison.OrdinalIgnoreCase);

            // Never store a usable OTP: mask the OTP value (first variable) in the logged text
            string? loggedText = message;
            if (isOtp && loggedText != null && variables.Length > 0 && !string.IsNullOrEmpty(variables[0]))
                loggedText = loggedText.Replace(variables[0], new string('*', variables[0].Length));

            // For application alerts the first variable is the Application Number
            string? referenceNo = !isOtp && variables.Length > 0 ? variables[0] : null;

            string? gatewayMsgId = null;
            var m = Regex.Match(gatewayResponse ?? string.Empty, @"MsgID\s*=\s*(\S+)", RegexOptions.IgnoreCase);
            if (m.Success) gatewayMsgId = m.Groups[1].Value;

            using var db = new SqlConnection(_connStr);
            await db.ExecuteAsync(
                "usp_SMS_Message_Log",
                new
                {
                    Action = "INSERT",
                    TemplateKey = templateKey,
                    TemplateId = templateId,
                    SmsType = smsType,
                    MobileNo = mobileNo,
                    ReferenceNo = referenceNo,
                    MessageText = Truncate(loggedText, 1000),
                    Status = status,
                    GatewayResponse = Truncate(gatewayResponse, 1000),
                    GatewayMessageId = Truncate(gatewayMsgId, 100),
                    ErrorMessage = Truncate(error, 1000)
                },
                commandType: System.Data.CommandType.StoredProcedure);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not write SMS log for {TemplateKey} to {MobileNo}", templateKey, mobileNo);
        }
    }

    private static string? Truncate(string? value, int max)
        => value == null || value.Length <= max ? value : value.Substring(0, max);

    private string SendViaGateway(string smsType, string mobileNo, string message, string templateId,
        IConfigurationSection smsCfg)
    {
        return smsType switch
        {
            "OTP" => _client.sendOTPMSG(
                        smsCfg["Username"],
                        smsCfg["Password"],
                        smsCfg["SenderId"],
                        mobileNo,
                        message,
                        smsCfg["SecureKey"],
                        templateId),

            "UNICODE" => _client.sendUnicodeSMS(
                        smsCfg["Username"],
                        smsCfg["Password"],
                        smsCfg["SenderId"],
                        mobileNo,
                        message,
                        smsCfg["SecureKey"],
                        templateId),

            "UNICODE_OTP" => _client.sendUnicodeOTPSMS(
                        smsCfg["Username"],
                        smsCfg["Password"],
                        smsCfg["SenderId"],
                        mobileNo,
                        message,
                        smsCfg["SecureKey"],
                        templateId),

            _ => _client.sendSingleSMS(
                        smsCfg["Username"],
                        smsCfg["Password"],
                        smsCfg["SenderId"],
                        mobileNo,
                        message,
                        smsCfg["SecureKey"],
                        templateId)
        };
    }

}
