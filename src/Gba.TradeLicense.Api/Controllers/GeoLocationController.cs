using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Gba.TradeLicense.Domain.Entities;
using Gba.TradeLicense.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace Gba.TradeLicense.Api.Controllers
{
    [ApiController]
    [Route("api/geolocation")]
    public class GeoLocationController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly BbmpBoundaryService _bbmpService;

        public GeoLocationController(
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            BbmpBoundaryService bbmpService)
        {
            _config = config;
            _httpClientFactory = httpClientFactory;
            _bbmpService = bbmpService;
        }

        // ================= FETCH ROAD DETAILS =================
        [HttpPost("fetch-road")]
        public async Task<IActionResult> FetchRoadDetails(
       [FromBody] GeoInputDto model,
       CancellationToken ct)
        {
            if (model == null)
                return BadRequest(new { message = "Invalid request payload" });

            decimal latitude = Math.Round(Convert.ToDecimal(model.Latitude), 6);
            decimal longitude = Math.Round(Convert.ToDecimal(model.Longitude), 6);

            // 🚫 Outside BBMP / GBA boundary
            if (!_bbmpService.IsInsideBbmp(latitude, longitude))
            {
                return BadRequest(new
                {
                    code = "OUTSIDE_GBA",
                    message = "Location is outside Greater Bengaluru Authority licence jurisdiction"
                });
            }

            var requestBody = new
            {
                applicantId = "1_Get_Roadwidth",
                parameter = "P1$|$P2",
                values =
                    longitude.ToString("F4", CultureInfo.InvariantCulture)
                    + "$|$"
                    + latitude.ToString("F4", CultureInfo.InvariantCulture)
            };

            var client = _httpClientFactory.CreateClient("KgisClient");

            var content = new StringContent(
                JsonConvert.SerializeObject(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            HttpResponseMessage response;

            try
            {
                response = await client.PostAsync(
                    "https://kgis.ksrsac.in/generic/api/genericselect",
                    content,
                    ct
                );
            }
            catch (TaskCanceledException)
            {
                return StatusCode(504, new
                {
                    code = "KGIS_TIMEOUT",
                    message = "KGIS service timeout. Please try again later."
                });
            }

            var responseText = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode(502, new
                {
                    code = "KGIS_FAILED",
                    message = "KGIS API failed",
                    details = "The KGIS road service returned an error."
                });
            }

            var roadData =
                JsonConvert.DeserializeObject<List<KgisRoadResponse>>(responseText);

            return Ok(new
            {
                code = "SUCCESS",
                message = "Road width data fetched successfully",
                data = roadData
            });
        }

        // ================= GET SAVED GEO LOCATION =================
        [HttpGet("get/{licenceApplicationID}")]
        public async Task<IActionResult> GetGeoLocation(int licenceApplicationID)
        {
            GeoLocationDto result = null;

            using SqlConnection con =
                new SqlConnection(_config.GetConnectionString("Default"));

            // Traders: own applications only; officers: their GBA jurisdiction
            if (!await Gba.TradeLicense.Api.Services.CallerAccess.MayAccessApplicationAsync(con, User, licenceApplicationID))
                return Forbid();

            using SqlCommand cmd =
                new SqlCommand("usp_LicenceApplication_Geo_Get", con);

            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@LicenceApplicationID", licenceApplicationID);

            con.Open();
            using SqlDataReader dr = cmd.ExecuteReader();

            if (dr.Read())
            {
                result = new GeoLocationDto
                {
                    Success = Convert.ToBoolean(dr["Success"]),
                    Message = dr["Message"].ToString(),
                    Latitude = dr["Latitude"] as decimal?,
                    Longitude = dr["Longitude"] as decimal?,
                    RoadID = dr["RoadID"]?.ToString(),
                    RoadWidthMtrs = dr["RoadWidthMtrs"] as int?,
                    RoadCategoryCode = dr["RoadCategoryCode"]?.ToString(),
                    RoadCategory = dr["RoadCategory"]?.ToString(),
                    IsConfirmed = Convert.ToBoolean(dr["IsConfirmed"]),
                    EntryDate = dr["EntryDate"] as DateTime?
                };
            }

            return Ok(result);
        }

        // ================= CONFIRM & SAVE =================
        [HttpPost("confirm-save")]
        public async Task<IActionResult> ConfirmAndSave(
            [FromBody] LicenceGeoConfirmDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            using SqlConnection con =
                new SqlConnection(_config.GetConnectionString("Default"));

            // A trader may only set the location of their own application (officers: jurisdiction)
            if (!await Gba.TradeLicense.Api.Services.CallerAccess.MayAccessApplicationAsync(con, User, model.LicenceApplicationID))
                return Forbid();
            // Recorded against the signed-in user (the form already sends the token's own id)
            if (Gba.TradeLicense.Api.Services.CallerAccess.CallerId(User) is long caller && caller > 0 && caller <= int.MaxValue)
                model.LoginID = (int)caller;

            using SqlCommand cmd =
                new SqlCommand("usp_LicenceApplication_Geo_Save", con);

            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@LicenceApplicationID", model.LicenceApplicationID);
            cmd.Parameters.AddWithValue("@Latitude", model.Latitude);
            cmd.Parameters.AddWithValue("@Longitude", model.Longitude);
            cmd.Parameters.AddWithValue("@RoadID", model.RoadID ?? string.Empty);
            cmd.Parameters.AddWithValue("@RoadWidthMtrs", (int)Math.Round(model.RoadWidthMtrs, MidpointRounding.AwayFromZero));
            cmd.Parameters.AddWithValue("@RoadCategoryCode", model.RoadCategoryCode ?? string.Empty);
            cmd.Parameters.AddWithValue("@RoadCategory", model.RoadCategory ?? string.Empty);
            cmd.Parameters.AddWithValue("@LoginID", model.LoginID);

            con.Open();
            cmd.ExecuteNonQuery();

            return Ok(new
            {
                success = true,
                message = "Location confirmed and saved"
            });
        }
    }
}
