using System.Data;
using Gba.TradeLicense.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Gba.TradeLicense.Api.Controllers.Licence
{
    [ApiController]
    [Route("api/licence/certificate")]
    public class LicenceCertificateController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public LicenceCertificateController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("approved/{applicationNumber}")]
        public async Task<IActionResult> GetApprovedLicenceCertificate(string applicationNumber)
        {
            var result = new List<ApprovedLicenceCertificateDto>();

            try
            {
                using SqlConnection con = new SqlConnection(
                    _configuration.GetConnectionString("Default"));

                using SqlCommand cmd = new SqlCommand(
                    "sp_GetApprovedLicenceCertificate_FullTrade", con);

                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@applicationNumber", applicationNumber);

                await con.OpenAsync();

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();

                if (!reader.HasRows)
                {
                    return NotFound(new
                    {
                        message = "No approved licence found for this application number"
                    });
                }

                while (await reader.ReadAsync())
                {
                    result.Add(new ApprovedLicenceCertificateDto
                    {
                        LicenceApplicationID =
                            reader.GetInt64(reader.GetOrdinal("licenceApplicationID")),

                        ApplicationNumber =
                            reader["applicationNumber"]?.ToString(),

                        FinancialYear =
                            reader["financialYear"]?.ToString(),

                        LicenceNumber =
                            reader["LicenceNumber"]?.ToString(),

                        ApplicantName =
                            reader["applicantName"]?.ToString(),

                        TradeName =
                            reader["tradeName"]?.ToString(),

                        TradeAddress =
                            reader["TradeAddress"]?.ToString(),

                        TradeMajorName =
                            reader["tradeMajorName"]?.ToString(),

                        TradeMinorName =
                            reader["tradeMinorName"]?.ToString(),

                        TradeSubName =
                            reader["tradeSubName"]?.ToString(),

                        LicenceFromDate =
                            reader["licenceFromDate"] == DBNull.Value
                                ? null
                                : Convert.ToDateTime(reader["licenceFromDate"]),

                        LicenceToDate =
                            reader["licenceToDate"] == DBNull.Value
                                ? null
                                : Convert.ToDateTime(reader["licenceToDate"]),

                        ReceiptNumber =
                            reader["receiptNumber"]?.ToString(),

                        ReceiptDate =
                            reader["receiptDate"] == DBNull.Value
                                ? null
                                : Convert.ToDateTime(reader["receiptDate"]),

                        TradeFee =
                            reader["tradeFee"] == DBNull.Value
                                ? 0
                                : Convert.ToDecimal(reader["tradeFee"]),
                        WardID =
                        reader["wardID"] == DBNull.Value
                            ? null
                            : Convert.ToInt32(reader["wardID"]),

                        WardName =
                        reader["wardName"]?.ToString(),

                        ApplicationStatus =
                            reader["ApplicationStatus"]?.ToString()
                    });
                }

                return Ok(result);
            }
            catch (SqlException ex)
            {
                // Handles RAISERROR from stored procedure
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
        }
    }
}
