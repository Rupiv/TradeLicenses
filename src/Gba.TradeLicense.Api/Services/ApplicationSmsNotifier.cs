using System.Data;
using Dapper;
using Gba.TradeLicense.Application.Abstractions;
using Microsoft.Data.SqlClient;

namespace Gba.TradeLicense.Api.Services
{
    /// <summary>
    /// Sends the applicant SMS alerts (SMS_Template_Master) for application events.
    /// An SMS failure is logged and never fails the business action that triggered it.
    /// </summary>
    public class ApplicationSmsNotifier
    {
        // Licence_Process IDs used by the approver screens
        public const int ProcessApproved = 3;
        public const int ProcessRejected = 4;

        private readonly ISmsService _sms;
        private readonly IConfiguration _config;
        private readonly ILogger<ApplicationSmsNotifier> _logger;

        public ApplicationSmsNotifier(ISmsService sms, IConfiguration config, ILogger<ApplicationSmsNotifier> logger)
        {
            _sms = sms;
            _config = config;
            _logger = logger;
        }

        // "... {#var#} City Corporation Trade License." -> same value the OTP SMS uses
        private string CorporationName => _config["Sms:CorporationName"] ?? "BBMP";

        // APP_RECEIVED : "Your Application Number {#var#} for Trade License has been received."
        public Task ApplicationReceivedAsync(long licenceApplicationId, string? applicationNumber = null)
            => SendForApplicationAsync("APP_RECEIVED", licenceApplicationId, applicationNumber, includeCorporation: false);

        // PROVISIONAL_ISSUED : "Your Application Number {#var#} has been received hence provisional Trade License is hereby issued."
        public Task ProvisionalIssuedAsync(long licenceApplicationId, string? applicationNumber = null)
            => SendForApplicationAsync("PROVISIONAL_ISSUED", licenceApplicationId, applicationNumber, includeCorporation: false);

        // APP_APPROVED / APP_REJECTED after an approver action
        public Task ProcessActionAsync(long licenceApplicationId, int licenceProcessId)
            => licenceProcessId switch
            {
                ProcessApproved => SendForApplicationAsync("APP_APPROVED", licenceApplicationId, null, includeCorporation: true),
                ProcessRejected => SendForApplicationAsync("APP_REJECTED", licenceApplicationId, null, includeCorporation: false),
                _ => Task.CompletedTask
            };

        private async Task SendForApplicationAsync(
            string templateKey, long licenceApplicationId, string? applicationNumber, bool includeCorporation)
        {
            try
            {
                using IDbConnection db = new SqlConnection(_config.GetConnectionString("Default"));

                var row = await db.QueryFirstOrDefaultAsync(
                    "usp_LicenceApplication_CRUD",
                    new { Action = "GET_BY_ID", LicenceApplicationID = licenceApplicationId },
                    commandType: CommandType.StoredProcedure) as IDictionary<string, object>;

                string? Field(params string[] names)
                {
                    if (row == null) return null;
                    foreach (var name in names)
                    {
                        var hit = row.FirstOrDefault(kv => string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase));
                        if (hit.Value != null && !string.IsNullOrWhiteSpace(hit.Value.ToString()))
                            return hit.Value.ToString();
                    }
                    return null;
                }

                var mobile = Field("mobileNumber", "mobileNo", "applicantMobile");
                var appNo = applicationNumber ?? Field("applicationNumber", "licenceApplicationNumber")
                            ?? licenceApplicationId.ToString();

                if (string.IsNullOrWhiteSpace(mobile))
                {
                    _logger.LogWarning("SMS {TemplateKey} skipped for application {Id}: no mobile number",
                        templateKey, licenceApplicationId);
                    return;
                }

                var response = includeCorporation
                    ? await _sms.SendAsync(templateKey, mobile, appNo, CorporationName)
                    : await _sms.SendAsync(templateKey, mobile, appNo);

                _logger.LogInformation("SMS {TemplateKey} for application {Id} -> {Response}",
                    templateKey, licenceApplicationId, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMS {TemplateKey} failed for application {Id}", templateKey, licenceApplicationId);
            }
        }
    }
}
