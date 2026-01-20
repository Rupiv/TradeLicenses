using System.Data;
using System.Globalization;
using System.Text;
using Gba.TradeLicense.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
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

        // STEP 1: Call KGIS API
        [HttpPost("fetch-road")]
        public async Task<IActionResult> FetchRoadDetails([FromBody] GeoInputDto model)
        {
            var requestBody = new
            {
                applicantId = "1_Get_Roadwidth",
                parameter = "P1$|$P2",
                values = model.Longitude.ToString(CultureInfo.InvariantCulture)
                         + "$|$"
                         + model.Latitude.ToString(CultureInfo.InvariantCulture)
            };

            using HttpClient client = new HttpClient();

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

            // 🔥 FIX IS HERE
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
            [FromBody] dynamic model
        )
        {
            using SqlConnection con = new SqlConnection(
                _config.GetConnectionString("DefaultConnection")
            );

            using SqlCommand cmd = new SqlCommand(
                "usp_LicenceApplication_Geo_Save", con);

            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@LicenceApplicationID", (int)model.licenceApplicationID);
            cmd.Parameters.AddWithValue("@Latitude", (decimal)model.latitude);
            cmd.Parameters.AddWithValue("@Longitude", (decimal)model.longitude);
            cmd.Parameters.AddWithValue("@RoadID", (string)model.roadID);
            cmd.Parameters.AddWithValue("@RoadWidthMtrs", (int)model.roadWidthMtrs);
            cmd.Parameters.AddWithValue("@RoadCategoryCode", (string)model.roadCategoryCode);
            cmd.Parameters.AddWithValue("@RoadCategory", (string)model.roadCategory);
            cmd.Parameters.AddWithValue("@LoginID", (int)model.loginID);

            con.Open();
            cmd.ExecuteNonQuery();

            return Ok(new { success = true, message = "Location confirmed and saved" });
        }
    }

}
