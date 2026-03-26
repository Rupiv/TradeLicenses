using System.Data;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Gba.TradeLicense.Api.Controllers
{
    [Authorize]
    [Route("api/reports")]
    [ApiController]
    public class ReportsController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public ReportsController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private IDbConnection CreateConnection()
        {
            return new SqlConnection(_configuration.GetConnectionString("Default"));
        }

        [HttpGet("control-sheet")]
        public async Task<IActionResult> GetControlSheetReport(
            int? financialYearID,
            int? zoneID,
            int? statusID)
        {
            try
            {
                using var db = CreateConnection();

                var result = await db.QueryMultipleAsync(
                    "usp_Report_ControlSheet",
                    new
                    {
                        FinancialYearID = financialYearID,
                        ZoneID = zoneID,
                        StatusID = statusID
                    },
                    commandType: CommandType.StoredProcedure
                );

                var statusSummary = result.Read().ToList();
                var totals = result.ReadFirstOrDefault();

                return Ok(new
                {
                    totals,
                    statusSummary
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Error generating report",
                    detail = ex.Message
                });
            }
        }

        [HttpGet("ward-wise-licenses")]
        public async Task<IActionResult> GetWardWiseLicenses(
        int? financialYearID,
        int? zoneID,
        int? wardID,
        int? statusID)
        {
            using var db = CreateConnection();

            var result = await db.QueryMultipleAsync(
                "usp_Report_WardWiseLicenses",
                new
                {
                    FinancialYearID = financialYearID,
                    ZoneID = zoneID,
                    WardID = wardID,
                    StatusID = statusID
                },
                commandType: CommandType.StoredProcedure
            );

            var table = result.Read().ToList();
            var summary = result.ReadFirstOrDefault();

            return Ok(new
            {
                summary,
                table
            });
        }
        [HttpGet("revenue-collection")]
        public async Task<IActionResult> GetRevenueCollection(
      int? corporationId,
      DateTime? fromDate,
      DateTime? toDate)
        {
            using var db = CreateConnection();

            var result = await db.QueryMultipleAsync(
                "usp_Report_RevenueCollection",
                new
                {
                    CorporationId = corporationId,
                    FromDate = fromDate,
                    ToDate = toDate
                },
                commandType: CommandType.StoredProcedure
            );

            var summary = result.ReadFirstOrDefault();
            var corporationSummary = result.Read().ToList();

            return Ok(new
            {
                summary,
                corporationSummary
            });
        }
    }
}
