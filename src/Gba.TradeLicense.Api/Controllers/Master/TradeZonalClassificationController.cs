using System.Data;
using Gba.TradeLicense.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Gba.TradeLicense.Api.Controllers.Master
{
    [Route("api/[controller]")]
    [ApiController]
    public class TradeZonalClassificationController : ControllerBase
    {
        private readonly IConfiguration _config;

        public TradeZonalClassificationController(IConfiguration config)
        {
            _config = config;
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(
                _config.GetConnectionString("Default"));
        }

        /* ================= GET ALL ================= */
        [HttpGet]
        public IActionResult GetAll()
        {
            List<TradeZonalClassification> list = new();

            using SqlConnection con = GetConnection();
            using SqlCommand cmd = new SqlCommand(
                "usp_TradeZonalClassification_CRUD", con);

            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@Action", "GET_ALL");

            con.Open();
            using SqlDataReader dr = cmd.ExecuteReader();
            while (dr.Read())
            {
                list.Add(MapZonalClassification(dr));
            }

            return Ok(list);
        }

        /* ================= GET BY ID ================= */
        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            TradeZonalClassification? item = null;

            using SqlConnection con = GetConnection();
            using SqlCommand cmd = new SqlCommand(
                "usp_TradeZonalClassification_CRUD", con);

            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@Action", "GET_BY_ID");
            cmd.Parameters.AddWithValue("@zonalClassificationID", id);

            con.Open();
            using SqlDataReader dr = cmd.ExecuteReader();
            if (dr.Read())
            {
                item = MapZonalClassification(dr);
            }

            if (item == null)
                return NotFound();

            return Ok(item);
        }

        /* ================= INSERT ================= */
        [HttpPost]
        public IActionResult Insert(TradeZonalClassification model)
        {
            using SqlConnection con = GetConnection();
            using SqlCommand cmd = new SqlCommand(
                "usp_TradeZonalClassification_CRUD", con);

            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@Action", "INSERT");
            cmd.Parameters.AddWithValue("@zonalCode", model.ZonalCode);
            cmd.Parameters.AddWithValue("@zonalClassificationName", model.ZonalClassificationName);
            cmd.Parameters.AddWithValue("@zonalClassificationNativeName", model.ZonalClassificationNativeName);

            con.Open();
            int id = Convert.ToInt32(cmd.ExecuteScalar());

            return Ok(new { zonalClassificationID = id });
        }

        /* ================= UPDATE ================= */
        [HttpPut]
        public IActionResult Update(TradeZonalClassification model)
        {
            using SqlConnection con = GetConnection();
            using SqlCommand cmd = new SqlCommand(
                "usp_TradeZonalClassification_CRUD", con);

            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@Action", "UPDATE");
            cmd.Parameters.AddWithValue("@zonalClassificationID", model.ZonalClassificationID);
            cmd.Parameters.AddWithValue("@zonalCode", model.ZonalCode);
            cmd.Parameters.AddWithValue("@zonalClassificationName", model.ZonalClassificationName);
            cmd.Parameters.AddWithValue("@zonalClassificationNativeName", model.ZonalClassificationNativeName);
            cmd.Parameters.AddWithValue("@isActive", model.IsActive ? "Y" : "N");

            con.Open();
            cmd.ExecuteNonQuery();

            return Ok(new { Updated = true });
        }

        /* ================= DELETE (SOFT) ================= */
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            using SqlConnection con = GetConnection();
            using SqlCommand cmd = new SqlCommand(
                "usp_TradeZonalClassification_CRUD", con);

            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@Action", "DELETE");
            cmd.Parameters.AddWithValue("@zonalClassificationID", id);

            con.Open();
            cmd.ExecuteNonQuery();

            return Ok(new { Deleted = true });
        }

        /* ================= COMMON MAPPER ================= */
        private static TradeZonalClassification MapZonalClassification(SqlDataReader dr)
        {
            return new TradeZonalClassification
            {
                ZonalClassificationID = Convert.ToInt32(dr["zonalClassificationID"]),
                ZonalCode = dr["zonalCode"].ToString(),
                ZonalClassificationName = dr["zonalClassificationName"].ToString(),
                ZonalClassificationNativeName = dr["zonalClassificationNativeName"].ToString(),
                IsActive =
                    dr["isActive"].ToString() == "Y" ||
                    dr["isActive"].ToString() == "1" ||
                    dr["isActive"].ToString().ToLower() == "true"
            };
        }
    }
}
