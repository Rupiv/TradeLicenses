using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Gba.TradeLicense.Api.Controllers.Approver
{
    [ApiController]
    [Route("api/trade-licence/approver")]
    public class TradeLicenceApproverController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public TradeLicenceApproverController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private IDbConnection Db()
        {
            return new SqlConnection(
                _configuration.GetConnectionString("Default"));
        }

        // ======================================================
        // APPROVER – APPLIED APPLICATIONS (ZONE WISE)
        // ======================================================
        [HttpGet("applications")]
        public async Task<IActionResult> GetApplications(
            int loginId,                      // approver loginID
            int? mohId,
            int? wardId,
            int? licenceApplicationId,
            string? applicationNumber,
            int pageNumber = 1,
            int pageSize = 10
        )
        {
            using var con = Db();

            var p = new DynamicParameters();
            p.Add("@LoginID", loginId);
            p.Add("@MohID", mohId);
            p.Add("@WardID", wardId);
            p.Add("@LicenceApplicationID", licenceApplicationId);
            p.Add("@ApplicationNumber", applicationNumber);
            p.Add("@PageNumber", pageNumber);
            p.Add("@PageSize", pageSize);
            p.Add("@TotalCount", dbType: DbType.Int32, direction: ParameterDirection.Output);

            var data = await con.QueryAsync(
                "sp_GetTradeLicenceApplications_Approver",
                p,
                commandType: CommandType.StoredProcedure
            );

            return Ok(new
            {
                Role = "Approver",
                Status = "APPLIED",
                LoginID = loginId,
                TotalRecords = p.Get<int>("@TotalCount"),
                PageNumber = pageNumber,
                PageSize = pageSize,
                Data = data
            });
        }
    }
}
