using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace TradeLicence.API.Controllers
{
    [ApiController]
    [Route("api/trade-licence/admin")]
    public class TradeLicenceAdminController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public TradeLicenceAdminController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private IDbConnection Db()
        {
            return new SqlConnection(
                _configuration.GetConnectionString("Default"));
        }

        // ============================================================
        // ADMIN – GET ALL APPLICATIONS WITH FILTERS
        // ============================================================

        [HttpGet("applications")]
        public async Task<IActionResult> GetApplications(
      int? zoneId,
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
            p.Add("@ZoneID", zoneId);
            p.Add("@MohID", mohId);
            p.Add("@WardID", wardId);
            p.Add("@LicenceApplicationID", licenceApplicationId);
            p.Add("@ApplicationNumber", applicationNumber);
            p.Add("@PageNumber", pageNumber);
            p.Add("@PageSize", pageSize);
            p.Add("@TotalCount", dbType: DbType.Int32, direction: ParameterDirection.Output);

            var data = await con.QueryAsync(
                "sp_GetTradeLicenceApplications_Admin",
                p,
                commandType: CommandType.StoredProcedure
            );

            return Ok(new
            {
                TotalRecords = p.Get<int>("@TotalCount"),
                PageNumber = pageNumber,
                PageSize = pageSize,
                Data = data
            });
        }



        // ============================================================
        // GET ALL ZONES
        // ============================================================
        [HttpGet("zones")]
        public async Task<IActionResult> GetZones()
        {
            using var con = Db();

            var data = await con.QueryAsync(
                "SELECT zoneID, zoneName FROM Master_BBMPZone ORDER BY zoneName");

            return Ok(data);
        }

        // ============================================================
        // GET MOH BY ZONE
        // ============================================================
        [HttpGet("moh-by-zone/{zoneId}")]
        public async Task<IActionResult> GetMohByZone(int zoneId)
        {
            using var con = Db();

            var data = await con.QueryAsync(
                @"SELECT mohcd, mohname 
                  FROM moh 
                  WHERE majzonecd = 
                        (SELECT zoneCode FROM Master_BBMPZone WHERE zoneID = @zoneId)",
                new { zoneId });

            return Ok(data);
        }

        // ============================================================
        // GET WARDS BY ZONE
        // ============================================================
        [HttpGet("wards-by-zone/{zoneId}")]
        public async Task<IActionResult> GetWardsByZone(int zoneId)
        {
            using var con = Db();

            var data = await con.QueryAsync(
                @"SELECT wardID, wardName 
                  FROM Master_BBMPWard 
                  WHERE zoneID = @zoneId",
                new { zoneId });

            return Ok(data);
        }

        // ============================================================
        // SEARCH APPLICATION BY ID
        // ============================================================
        [HttpGet("application/{applicationId}")]
        public async Task<IActionResult> GetApplicationById(int applicationId)
        {
            using var con = Db();

            var data = await con.QueryFirstOrDefaultAsync(
                "SELECT * FROM Licence_Application WHERE licenceApplicationID = @applicationId",
                new { applicationId });

            if (data == null)
                return NotFound("Application not found");

            return Ok(data);
        }
    }
}
