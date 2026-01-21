using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Gba.TradeLicense.Domain.Entities;
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

        public GeoLocationController(IConfiguration config)
        {
            _config = config;
        }

        [HttpPost("fetch-road")]
        public async Task<IActionResult> FetchRoadDetails([FromBody] GeoInputDto model)
        {
            var requestBody = new
            {
                applicantId = "1_Get_Roadwidth",
                parameter = "P1$|$P2",
                values = model.Latitude.ToString(CultureInfo.InvariantCulture)
                       + "$|$"
                       + model.Longitude.ToString(CultureInfo.InvariantCulture)
            };

            using HttpClient client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(3)
            };

            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

            var content = new StringContent(
                JsonConvert.SerializeObject(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            var response = await client.PostAsync(
                "https://kgis.ksrsac.in/generic/api/genericselect",
                content
            );

            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return BadRequest(new
                {
                    message = "KGIS API failed",
                    kgisResponse = responseText
                });
            }

            var roadData = JsonConvert.DeserializeObject<List<KgisRoadResponse>>(responseText);
            return Ok(roadData);
        }



        [HttpGet("get/{licenceApplicationID}")]
        public IActionResult GetGeoLocation(int licenceApplicationID)
        {
            GeoLocationDto result = null;

            using SqlConnection con = new SqlConnection(
                _config.GetConnectionString("DefaultConnection"));

            using SqlCommand cmd = new SqlCommand(
                "usp_LicenceApplication_Geo_Get", con);

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

        // STEP 2: Save after confirmation
        [HttpPost("confirm-save")]
        public IActionResult ConfirmAndSave(
     [FromBody] LicenceGeoConfirmDto model
 )
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            using SqlConnection con = new SqlConnection(
                _config.GetConnectionString("Default")
            );

            using SqlCommand cmd = new SqlCommand(
                "usp_LicenceApplication_Geo_Save", con
            );

            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@LicenceApplicationID", model.LicenceApplicationID);
            cmd.Parameters.AddWithValue("@Latitude", model.Latitude);
            cmd.Parameters.AddWithValue("@Longitude", model.Longitude);
            cmd.Parameters.AddWithValue("@RoadID", model.RoadID);
            cmd.Parameters.AddWithValue("@RoadWidthMtrs", model.RoadWidthMtrs);
            cmd.Parameters.AddWithValue("@RoadCategoryCode", model.RoadCategoryCode);
            cmd.Parameters.AddWithValue("@RoadCategory", model.RoadCategory);
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
