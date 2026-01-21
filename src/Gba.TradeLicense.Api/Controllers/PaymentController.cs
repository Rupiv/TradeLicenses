using System;
using System.Data;
using System.Linq;
using Dapper;
using Gba.TradeLicense.Domain.Entities;
using Gba.TradeLicense.Infrastructure.Services.PaymentGateway;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gba.TradeLicense.Api.Controllers
{
    [ApiController]
    [Route("api/payment")]
    public class PaymentController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly string _connectionString;

        public PaymentController(IConfiguration config)
        {
            _config = config;
            _connectionString = _config.GetConnectionString("Default");
        }

        /* =========================================================
           HELPER : GET MERCHANT CONFIG PER CORPORATION
        ========================================================= */
        private (string Key, string Salt, string Email, string Env)
            GetGatewayConfig(int corporationId)
        {
            using var con = new SqlConnection(_connectionString);

            return con.QuerySingle<(string, string, string, string)>(
                @"SELECT MerchantKey, MerchantSalt, MerchantEmail, Environment
                  FROM Payment_Gateway_Config
                  WHERE CorporationId = @CorporationId AND IsActive = 1",
                new { CorporationId = corporationId }
            );
        }

        /* =========================================================
           1️⃣ INITIATE PAYMENT
        ========================================================= */
        [HttpPost("initiate")]
        public IActionResult InitiatePayment([FromBody] InitiatePaymentDto req)
        {
            var cfg = GetGatewayConfig(req.CorporationId);
            var easebuzz = new Easebuzz(cfg.Salt, cfg.Key, cfg.Env);

            string txnid = $"GBA-TL-{req.LicenceApplicationId}";

            using var con = new SqlConnection(_connectionString);

            // 🔒 AUDIT LOG
            con.Execute("usp_Payment_Audit_Log", new
            {
                LicenceApplicationId = req.LicenceApplicationId,
                CorporationId = req.CorporationId,
                TxnId = txnid,
                Amount = req.Amount,
                PaymentStage = "INITIATED",
                GatewayStatus = "PENDING",
                RequestPayload = JsonConvert.SerializeObject(req)
            }, commandType: CommandType.StoredProcedure);

            string htmlForm = easebuzz.initiatePaymentAPI(
                req.Amount.ToString("0.00"),
                req.ApplicantName,
                req.Email,
                req.Phone,
                "Trade Licence Fee",
                _config["Easebuzz:SuccessUrl"],
                _config["Easebuzz:FailureUrl"],
                txnid,
                req.CorporationId.ToString(),        // udf1
                req.LicenceApplicationId.ToString(), // udf2
                "", "", "", "", "", "", "", "", "", ""
            );

            return Ok(new { TxnId = txnid, Html = htmlForm });
        }

        /* =========================================================
           2️⃣ EASEBUZZ CALLBACK (SURL / FURL)
        ========================================================= */
        [HttpPost("callback")]
        public IActionResult EasebuzzCallback([FromForm] IFormCollection form)
        {
            string status = form["status"];
            string txnid = form["txnid"];
            string amount = form["amount"];
            string email = form["email"];
            string firstname = form["firstname"];
            string receivedHash = form["hash"];

            int corporationId = int.Parse(form["udf1"]);
            long applicationId = long.Parse(form["udf2"]);

            var cfg = GetGatewayConfig(corporationId);
            var easebuzz = new Easebuzz(cfg.Salt, cfg.Key, cfg.Env);

            string hashString =
                $"{cfg.Key}|{txnid}|{amount}|Trade Licence Fee|{firstname}|{email}|" +
                $"{form["udf1"]}|{form["udf2"]}|||||||||{cfg.Salt}";

            string generatedHash =
                easebuzz.Easebuzz_Generatehash512(hashString).ToLower();

            if (!string.Equals(receivedHash, generatedHash, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Invalid hash");
            }

            using var con = new SqlConnection(_connectionString);

            // 🔒 AUDIT LOG
            con.Execute("usp_Payment_Audit_Log", new
            {
                LicenceApplicationId = applicationId,
                CorporationId = corporationId,
                TxnId = txnid,
                Amount = decimal.Parse(amount),
                PaymentStage = status.ToUpper(),
                GatewayStatus = status,
                ResponsePayload = JsonConvert.SerializeObject(form.ToDictionary(x => x.Key, x => x.Value.ToString()))
            }, commandType: CommandType.StoredProcedure);

            if (status.Equals("success", StringComparison.OrdinalIgnoreCase))
            {
                con.Execute("usp_LicenceApplication_CRUD",
                    new
                    {
                        Action = "PAYMENT_SUCCESS",
                        licenceApplicationID = applicationId
                    },
                    commandType: CommandType.StoredProcedure);
            }

            return Ok("OK");
        }

        /* =========================================================
           3️⃣ VERIFY PAYMENT (READ-ONLY)
        ========================================================= */
        [HttpPost("verify")]
        public IActionResult VerifyPayment([FromBody] VerifyPaymentDto req)
        {
            var cfg = GetGatewayConfig(req.CorporationId);
            var easebuzz = new Easebuzz(cfg.Salt, cfg.Key, cfg.Env);

            string response = easebuzz.transactionAPI(
                req.Txnid,
                req.Amount.ToString("0.00"),
                req.Email,
                req.Phone
            );

            return Ok(JObject.Parse(response));
        }

        /* =========================================================
           4️⃣ REFUND PAYMENT
        ========================================================= */
        [HttpPost("refund")]
        public IActionResult RefundPayment([FromBody] RefundPaymentDto req)
        {
            var cfg = GetGatewayConfig(req.CorporationId);
            var easebuzz = new Easebuzz(cfg.Salt, cfg.Key, cfg.Env);

            string response = easebuzz.RefundAPI(
                req.Txnid,
                req.RefundAmount.ToString("0.00"),
                req.Phone,
                req.OriginalAmount.ToString("0.00"),
                req.Email
            );

            return Ok(JObject.Parse(response));
        }

        /* =========================================================
           5️⃣ TRANSACTIONS BY DATE
        ========================================================= */
        [HttpGet("transactions-by-date")]
        public IActionResult TransactionsByDate(int corporationId, DateTime transactionDate)
        {
            var cfg = GetGatewayConfig(corporationId);
            var easebuzz = new Easebuzz(cfg.Salt, cfg.Key, cfg.Env);

            return Ok(JObject.Parse(
                easebuzz.transactionDateAPI(cfg.Email, transactionDate.ToString("yyyy-MM-dd"))
            ));
        }

        /* =========================================================
           6️⃣ PAYOUTS
        ========================================================= */
        [HttpGet("payouts")]
        public IActionResult Payouts(int corporationId, DateTime payoutDate)
        {
            var cfg = GetGatewayConfig(corporationId);
            var easebuzz = new Easebuzz(cfg.Salt, cfg.Key, cfg.Env);

            return Ok(JObject.Parse(
                easebuzz.payoutAPI(cfg.Email, payoutDate.ToString("yyyy-MM-dd"))
            ));
        }
    }
}
