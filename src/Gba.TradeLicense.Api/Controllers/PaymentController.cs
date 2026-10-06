using Dapper;
using Gba.TradeLicense.Domain.Entities;
using Gba.TradeLicense.Infrastructure.Security;
using Gba.TradeLicense.Infrastructure.Services.PaymentGateway;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;

namespace Gba.TradeLicense.Api.Controllers
{
    [Authorize]
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
        /* =========================================================
           HELPER : URLS (from appsettings, with safe fallbacks)
        ========================================================= */
        // Where Easebuzz posts the result back (surl / furl) -> this controller's payment-success
        private string GatewayReturnUrl(string configKey)
        {
            var url = _config[configKey];
            return string.IsNullOrWhiteSpace(url)
                ? $"{Request.Scheme}://{Request.Host}{Request.PathBase}/api/payment/payment-success"
                : url;
        }

        // Angular app base, e.g. https://gbatrade.karnataka.gov.in/gba
        private string FrontendBaseUrl()
            => (_config["Easebuzz:FrontendBaseUrl"] ?? "https://gbatrade.karnataka.gov.in/gba").TrimEnd('/');

        private IActionResult RedirectToFailed(string txnid, string error)
            => Redirect($"{FrontendBaseUrl()}/trader/payment-failed" +
                        $"?txnid={Uri.EscapeDataString(txnid ?? string.Empty)}" +
                        $"&error={Uri.EscapeDataString(error ?? string.Empty)}");

        [HttpPost("initiate")]
        public async Task<IActionResult> InitiatePayment([FromBody] InitiatePaymentDto req)
        {
            if (req == null || req.LicenceApplicationId <= 0 || req.Amount <= 0)
                return BadRequest(new { Message = "Invalid payment request" });

            if (string.IsNullOrWhiteSpace(req.ApplicantName) ||
                string.IsNullOrWhiteSpace(req.Email) ||
                string.IsNullOrWhiteSpace(req.Phone))
                return BadRequest(new { Message = "Applicant name, email and phone are required for payment" });

            var cfg = GetGatewayConfig(req.CorporationId);
            var easebuzz = new Easebuzz(cfg.Salt, cfg.Key, cfg.Env);

            // Easebuzz needs a unique txnid per attempt (a retry after failure must not reuse it)
            string txnid = $"GBA-TL-{req.LicenceApplicationId}-{DateTime.Now:yyMMddHHmmss}";

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

            // 1️⃣ initiateLink -> access key
            var link = await easebuzz.InitiatePaymentLinkAsync(
                req.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                req.ApplicantName,
                req.Email,
                req.Phone,
                "Trade Licence Fee",
                GatewayReturnUrl("Easebuzz:SuccessUrl"),
                GatewayReturnUrl("Easebuzz:FailureUrl"),
                txnid,
                req.CorporationId.ToString(),        // udf1
                req.LicenceApplicationId.ToString()  // udf2
            );

            if (!link.Success)
            {
                con.Execute("usp_Payment_Audit_Log", new
                {
                    LicenceApplicationId = req.LicenceApplicationId,
                    CorporationId = req.CorporationId,
                    TxnId = txnid,
                    Amount = req.Amount,
                    PaymentStage = "INITIATE_FAILED",
                    GatewayStatus = "FAILED",
                    ResponsePayload = link.RawResponse
                }, commandType: CommandType.StoredProcedure);

                return BadRequest(new { Message = link.Error, TxnId = txnid });
            }

            // 2️⃣ checkout page: https://pay.easebuzz.in/pay/{accesskey}
            string paymentUrl = easebuzz.GetCheckoutUrl(link.AccessKey);

            return Ok(new
            {
                TxnId = txnid,
                AccessKey = link.AccessKey,
                PaymentUrl = paymentUrl,
                // kept so an older frontend build (which submits this form) still works
                Html = $"<form id=\"PostForm\" name=\"PostForm\" action=\"{paymentUrl}\" method=\"GET\"></form>"
            });
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
        // Transaction status API v2.1 (only Txnid + CorporationId are needed)
        [HttpPost("verify")]
        public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentDto req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Txnid))
                return BadRequest(new { Message = "Txnid is required" });

            var cfg = GetGatewayConfig(req.CorporationId);
            var easebuzz = new Easebuzz(cfg.Salt, cfg.Key, cfg.Env);

            string response = await easebuzz.RetrieveTransactionV21Async(req.Txnid);

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
        [HttpPost("successs")]
        public IActionResult PaymentSuccesss([FromForm] IFormCollection form)
        {
            var txnid = form["txnid"].ToString();
            var amount = form["amount"].ToString();
            var email = form["email"].ToString();
            var phone = form["phone"].ToString();
            var corporationId = form["udf1"].ToString(); // you sent this
            var licenceApplicationId = form["udf2"].ToString();

            // (optional) verify hash here

            return Redirect(
                $"http://localhost:4200/trader/payment-success" +
                $"?txnid={txnid}" +
                $"&amount={amount}" +
                $"&email={email}" +
                $"&phone={phone}" +
                $"&corporationId={corporationId}" +
                $"&applicationNo={licenceApplicationId}"
            );
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


        /* ====================================================
                         SUCCESS
          ========================================================*/
        // Easebuzz posts here from the user's browser (surl AND furl) -> no JWT, so anonymous;
        // the response hash is verified before anything is trusted.
        [AllowAnonymous]
        [HttpPost("payment-success")]
        public async Task<IActionResult> PaymentSuccess([FromForm] IFormCollection form)
        {
            var txnid = form["txnid"].ToString();
            try
            {
                // ✅ Safe parsing
                if (!int.TryParse(form["udf1"], out var corporationId))
                    return RedirectToFailed(txnid, "Invalid payment response");

                if (!long.TryParse(form["udf2"], out var applicationId))
                    return RedirectToFailed(txnid, "Invalid payment response");

                if (!decimal.TryParse(form["amount"], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
                    return RedirectToFailed(txnid, "Invalid payment response");

                var status = form["status"].ToString();
                var email = form["email"].ToString();
                var phone = form["phone"].ToString();

                // 🔐 Verify Easebuzz response hash (rejects forged "status=success" posts)
                var cfg = GetGatewayConfig(corporationId);
                var easebuzz = new Easebuzz(cfg.Salt, cfg.Key, cfg.Env);
                var formValues = form.ToDictionary(x => x.Key, x => x.Value.ToString());

                if (!easebuzz.VerifyResponseHash(formValues))
                    return RedirectToFailed(txnid, "Payment response could not be verified");

                // 🔐 Double-check with Transaction API v2.1 before marking the application paid
                if (status.Equals("success", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var gatewayStatus = Easebuzz.ReadTransactionStatus(
                            await easebuzz.RetrieveTransactionV21Async(txnid));

                        if (gatewayStatus != null &&
                            !gatewayStatus.Equals("success", StringComparison.OrdinalIgnoreCase))
                        {
                            status = gatewayStatus;
                        }
                    }
                    catch
                    {
                        // status API unreachable -> rely on the verified hash
                    }
                }

                using var con = new SqlConnection(_connectionString);

                // 🔒 Audit log (UNCHANGED)
                con.Execute("usp_Payment_Audit_Log", new
                {
                    LicenceApplicationId = applicationId,
                    CorporationId = corporationId,
                    TxnId = txnid,
                    Amount = amount,
                    PaymentStage = status.ToUpper(),
                    GatewayStatus = status,
                    ResponsePayload = JsonConvert.SerializeObject(
                        form.ToDictionary(x => x.Key, x => x.Value.ToString()))
                }, commandType: CommandType.StoredProcedure);

                // ✅ Business logic (UNCHANGED)
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
                else
                {
                    var reason = form["error_Message"].ToString();
                    if (string.IsNullOrWhiteSpace(reason)) reason = form["error"].ToString();
                    if (string.IsNullOrWhiteSpace(reason)) reason = $"Payment {status}";
                    return RedirectToFailed(txnid, reason);
                }

                // 🔐 STEP 1: Create payload
                var payload = new
                {
                    txnid,
                    amount,
                    applicationId,
                    email,
                    phone,
                    corporationId,
                    status
                };

                var json = JsonConvert.SerializeObject(payload);

                // 🔐 STEP 2: AES Generate
                using var aes = Aes.Create();
                aes.GenerateKey();
                aes.GenerateIV();

                // 🔐 STEP 3: Encrypt data using helper
                var encryptedData = CryptoHelper.EncryptAES(json, aes.Key, aes.IV);

                // 🔐 STEP 4: Get public key from file
                var basePath = Directory.GetCurrentDirectory();
                var publicKey = CryptoHelper.GetPublicKey();

                // 🔐 STEP 5: Encrypt AES key using RSA
                var encryptedKey = CryptoHelper.EncryptRSA(aes.Key, publicKey);

                var iv = Convert.ToBase64String(aes.IV);

                // 🔐 FINAL REDIRECT (encrypted only)
                return Redirect(
                    $"{FrontendBaseUrl()}/trader/payment-success" +
                    $"?data={Uri.EscapeDataString(encryptedData)}" +
                    $"&key={Uri.EscapeDataString(encryptedKey)}" +
                    $"&iv={Uri.EscapeDataString(iv)}"
                );
            }
            catch
            {
                return RedirectToFailed(txnid, "Payment received but could not be processed. Please contact support with this Transaction ID.");
            }
        }
        [HttpGet("decrypt-payment")]
        public IActionResult DecryptPayment(string data, string key, string iv)
        {
            try
            {
                var basePath = Directory.GetCurrentDirectory();

                // 🔐 Get private key
                var privateKey = CryptoHelper.GetPrivateKey();

                // 🔓 Decrypt AES key
                var aesKey = CryptoHelper.DecryptRSA(key, privateKey);

                // 🔓 Decrypt data
                var decryptedJson = CryptoHelper.DecryptAES(
                    data,
                    aesKey,
                    Convert.FromBase64String(iv)
                );

                return Ok(decryptedJson);
            }
            catch
            {
                return BadRequest("Decryption failed");
            }
        }

    }
}
