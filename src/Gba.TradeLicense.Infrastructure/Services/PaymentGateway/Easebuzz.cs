using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using XSystem.Security.Cryptography;

namespace Gba.TradeLicense.Infrastructure.Services.PaymentGateway
{
    public class Easebuzz
    {

        public string easebuzz_action_url = string.Empty;
        public string gen_hash;
        public string txnid = string.Empty;
        public string salt = string.Empty;
        public string Key = string.Empty;
        public string env = string.Empty;

        public Easebuzz(string SALT, string KEY, string ENV)
        {
            salt = SALT;
            Key = KEY;
            env = ENV;
        }
        // this function is required to initiate payment
        public string initiatePaymentAPI(string Amount, String Firstname, String Email, String Phone, String Productinfo, String Surl, String Furl, String Txnid, String Udf1, String Udf2, String Udf3, String Udf4, String Udf5, String Udf6, String Udf7, String Udf8, String Udf9, String Udf10, String Show_payment_mode, String split_payments)
        {
            string[] hashVarsSeq;
            string hash_string = string.Empty;
            string saltvalue = salt;
            string amount = Amount;
            string firstname = Firstname;
            string email = Email;
            string phone = Phone;
            string productinfo = Productinfo;
            string surl = Surl;
            string furl = Furl;
            string udf1 = Udf1;
            string udf2 = Udf2;
            string udf3 = Udf3;
            string udf4 = Udf4;
            string udf5 = Udf5;
            string udf6 = Udf6;
            string udf7 = Udf7;
            string udf8 = Udf8;
            string udf9 = Udf9;
            string udf10 = Udf10;
            string ShowPaymentMode = Show_payment_mode;
            // Generate transaction ID -> make sure this is unique for all transactions
            Random rnd = new Random();
            string strHash = Easebuzz_Generatehash512(rnd.ToString() + DateTime.Now);
            //txnid = strHash.ToString().Substring(0, 20);
            txnid = Txnid;

            string paymentUrl = getURL();
            // Get configs from web config
            easebuzz_action_url = paymentUrl + "/pay/secure";

            // generate hash table
            System.Collections.Hashtable data = new System.Collections.Hashtable(); // adding values in gash table for data post
            data.Add("txnid", txnid);
            data.Add("key", Key);
            //string AmountForm = Convert.ToDecimal(amount.Trim()).ToString("g29");// eliminating trailing zeros
            amount = amount;
            data.Add("amount", amount);
            data.Add("firstname", firstname.Trim());
            data.Add("email", email.Trim());
            data.Add("phone", phone.Trim());
            data.Add("productinfo", productinfo.Trim());
            data.Add("surl", surl.Trim());
            data.Add("furl", furl.Trim());
            data.Add("udf1", udf1.Trim());
            data.Add("udf2", udf2.Trim());
            data.Add("udf3", udf3.Trim());
            data.Add("udf4", udf4.Trim());
            data.Add("udf5", udf5.Trim());
            data.Add("udf6", udf6.Trim());
            data.Add("udf7", udf7.Trim());
            data.Add("udf8", udf8.Trim());
            data.Add("udf9", udf9.Trim());
            data.Add("udf10", udf10.Trim());
            // generate hash
            hashVarsSeq = "key|txnid|amount|productinfo|firstname|email|udf1|udf2|udf3|udf4|udf5|udf6|udf7|udf8|udf9|udf10".Split('|'); // spliting hash sequence from config
            hash_string = "";
            foreach (string hash_var in hashVarsSeq)
            {
                hash_string = hash_string + (data.ContainsKey(hash_var) ? data[hash_var].ToString() : "");
                hash_string = hash_string + '|';
            }
            hash_string += salt;// appending SALT
            gen_hash = Easebuzz_Generatehash512(hash_string).ToLower();        //generating hash
            data.Add("hash", gen_hash);
            data.Add("show_payment_mode", ShowPaymentMode.Trim());
            if (split_payments.Length > 0)
            {
                data.Add("split_payments", split_payments);
            }

            string strForm = Easebuzz_PreparePOSTForm(easebuzz_action_url, data);
            return strForm;

        }

        //prepare a postform for redirection to payment gateway
        public string Easebuzz_PreparePOSTForm(string url, System.Collections.Hashtable data)
        {
            //Set a name for the form
            string formID = "PostForm";
            //Build the form using the specified data to be posted.
            StringBuilder strForm = new StringBuilder();
            strForm.Append("<form id=\"" + formID + "\" name=\"" +
                           formID + "\" action=\"" + url +
                           "\" method=\"POST\">");

            foreach (System.Collections.DictionaryEntry key in data)
            {

                strForm.Append("<input type=\"hidden\" name=\"" + key.Key +
                               "\" value='" + key.Value + "'>");
            }
            strForm.Append("</form>");
            //Build the JavaScript which will do the Posting operation.
            StringBuilder strScript = new StringBuilder();
            strScript.Append("<script language='javascript'>");
            strScript.Append("var v" + formID + " = document." +
                             formID + ";");
            strScript.Append("v" + formID + ".submit();");
            strScript.Append("</script>");
            //Return the form and the script concatenated.
            //(The order is important, Form then JavaScript)
            return strForm.ToString() + strScript.ToString();
        }

        // hashcode generation
        public string Easebuzz_Generatehash512(string text)
        {

            byte[] message = Encoding.UTF8.GetBytes(text);

            UnicodeEncoding UE = new UnicodeEncoding();
            byte[] hashValue;
            SHA512Managed hashString = new SHA512Managed();
            string hex = "";
            hashValue = hashString.ComputeHash(message);
            foreach (byte x in hashValue)
            {
                hex += String.Format("{0:x2}", x);
            }
            return hex;

        }



        //get url using env varibale
        public string getURL()
        {
            if (env == "test")
            {
                string paymentUrl = "https://testpay.easebuzz.in";
                return paymentUrl;
            }
            else
            {
                string paymentUrl = "https://pay.easebuzz.in";
                return paymentUrl;
            }
        }

        public string getDashboardURL()
        {
            return env == "test"
                ? "https://testdashboard.easebuzz.in"
                : "https://dashboard.easebuzz.in";
        }

        /* =========================================================
           SEAMLESS / HOSTED CHECKOUT (initiateLink -> access key)
        ========================================================= */
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        public sealed class InitiateLinkResult
        {
            public bool Success { get; set; }
            public string AccessKey { get; set; } = string.Empty;
            public string Error { get; set; } = string.Empty;
            public string RawResponse { get; set; } = string.Empty;
        }

        // Step 1: POST /payment/initiateLink -> returns access key
        public async Task<InitiateLinkResult> InitiatePaymentLinkAsync(
            string amount, string firstname, string email, string phone, string productinfo,
            string surl, string furl, string txnId,
            string udf1 = "", string udf2 = "", string udf3 = "", string udf4 = "", string udf5 = "",
            string udf6 = "", string udf7 = "", string udf8 = "", string udf9 = "", string udf10 = "")
        {
            var data = new Dictionary<string, string>
            {
                ["key"] = Key,
                ["txnid"] = txnId.Trim(),
                ["amount"] = amount.Trim(),
                ["productinfo"] = productinfo.Trim(),
                ["firstname"] = firstname.Trim(),
                ["phone"] = phone.Trim(),
                ["email"] = email.Trim(),
                ["surl"] = surl.Trim(),
                ["furl"] = furl.Trim(),
                ["udf1"] = udf1.Trim(),
                ["udf2"] = udf2.Trim(),
                ["udf3"] = udf3.Trim(),
                ["udf4"] = udf4.Trim(),
                ["udf5"] = udf5.Trim(),
                ["udf6"] = udf6.Trim(),
                ["udf7"] = udf7.Trim(),
                ["udf8"] = udf8.Trim(),
                ["udf9"] = udf9.Trim(),
                ["udf10"] = udf10.Trim()
            };

            // key|txnid|amount|productinfo|firstname|email|udf1|...|udf10|salt
            string hashString = string.Join("|", new[]
            {
                data["key"], data["txnid"], data["amount"], data["productinfo"], data["firstname"], data["email"],
                data["udf1"], data["udf2"], data["udf3"], data["udf4"], data["udf5"],
                data["udf6"], data["udf7"], data["udf8"], data["udf9"], data["udf10"]
            }) + "|" + salt;
            data["hash"] = Easebuzz_Generatehash512(hashString).ToLower();

            var result = new InitiateLinkResult();
            try
            {
                using var response = await _http.PostAsync(
                    getURL() + "/payment/initiateLink",
                    new FormUrlEncodedContent(data));
                result.RawResponse = await response.Content.ReadAsStringAsync();

                // { "status": 1, "data": "<access key>" }  |  { "status": 0, "error_desc": "...", "data": "..." }
                using var doc = JsonDocument.Parse(result.RawResponse);
                var root = doc.RootElement;
                int status = root.TryGetProperty("status", out var s)
                    ? (s.ValueKind == JsonValueKind.Number ? s.GetInt32() : (int.TryParse(s.ToString(), out var n) ? n : 0))
                    : 0;
                string dataValue = root.TryGetProperty("data", out var d) ? d.ToString() : string.Empty;

                if (status == 1 && !string.IsNullOrWhiteSpace(dataValue))
                {
                    result.Success = true;
                    result.AccessKey = dataValue;
                }
                else
                {
                    result.Error = root.TryGetProperty("error_desc", out var e) && !string.IsNullOrWhiteSpace(e.ToString())
                        ? e.ToString()
                        : (string.IsNullOrWhiteSpace(dataValue) ? "Payment initiation failed" : dataValue);
                }
            }
            catch (Exception ex)
            {
                result.Error = "Unable to reach payment gateway: " + ex.Message;
            }
            return result;
        }

        // Step 2: browser is sent to https://pay.easebuzz.in/pay/{accessKey}
        public string GetCheckoutUrl(string accessKey)
        {
            return getURL() + "/pay/" + Uri.EscapeDataString(accessKey);
        }

        // Step 3: verify the hash Easebuzz posts back to surl / furl
        // salt|status|udf10|udf9|...|udf1|email|firstname|productinfo|amount|txnid|key
        public bool VerifyResponseHash(IDictionary<string, string> form)
        {
            string Get(string k) => form.TryGetValue(k, out var v) ? (v ?? string.Empty) : string.Empty;

            string hashString = string.Join("|", new[]
            {
                salt, Get("status"),
                Get("udf10"), Get("udf9"), Get("udf8"), Get("udf7"), Get("udf6"),
                Get("udf5"), Get("udf4"), Get("udf3"), Get("udf2"), Get("udf1"),
                Get("email"), Get("firstname"), Get("productinfo"), Get("amount"), Get("txnid"), Key
            });

            string expected = Easebuzz_Generatehash512(hashString).ToLower();
            return string.Equals(expected, Get("hash"), StringComparison.OrdinalIgnoreCase);
        }

        // Step 4: Transaction status API v2.1 (server-to-server)
        // POST https://dashboard.easebuzz.in/transaction/v2.1/retrieve   hash = key|txnid|salt
        public async Task<string> RetrieveTransactionV21Async(string txnId)
        {
            string hash = Easebuzz_Generatehash512(Key + "|" + txnId.Trim() + "|" + salt).ToLower();
            var body = JsonSerializer.Serialize(new { txnid = txnId.Trim(), key = Key, hash });

            using var response = await _http.PostAsync(
                getDashboardURL() + "/transaction/v2.1/retrieve",
                new StringContent(body, Encoding.UTF8, "application/json"));
            return await response.Content.ReadAsStringAsync();
        }

        // Reads the transaction status ("success", "failure", "userCancelled", ...) from a v2.1 response.
        // Returns null when the response does not contain a transaction.
        public static string? ReadTransactionStatus(string v21Response)
        {
            try
            {
                using var doc = JsonDocument.Parse(v21Response);
                if (!doc.RootElement.TryGetProperty("msg", out var msg)) return null;

                var txn = msg.ValueKind == JsonValueKind.Array
                    ? (msg.GetArrayLength() > 0 ? msg[0] : default)
                    : msg;

                return txn.ValueKind == JsonValueKind.Object && txn.TryGetProperty("status", out var st)
                    ? st.GetString()
                    : null;
            }
            catch
            {
                return null;
            }
        }

        //initiate refund api 
        public string RefundAPI(string txnid, string refund_amount, string phone, string amount, string email)
        {
            System.Collections.Hashtable data = new System.Collections.Hashtable(); // adding values in gash table for data post
            data.Add("txnid", txnid.Trim());
            data.Add("refund_amount", refund_amount.Trim());
            data.Add("key", Key);
            //string AmountForm = Convert.ToDecimal(amount.Trim()).ToString("g29");// eliminating trailing zeros
            //amount = AmountForm;
            data.Add("amount", amount);
            data.Add("email", email.Trim());
            data.Add("phone", phone.Trim());
            // generate hash
            string[] hashVarsSeq = "key|txnid|amount|refund_amount|email|phone".Split('|'); // spliting hash sequence from config
            string hash_string = "";
            foreach (string hash_var in hashVarsSeq)
            {
                hash_string = hash_string + (data.ContainsKey(hash_var) ? data[hash_var].ToString() : "");
                hash_string = hash_string + '|';
            }
            hash_string += salt;// appending SALT
            Console.WriteLine(hash_string);
            gen_hash = Easebuzz_Generatehash512(hash_string).ToLower();        //generating hash
            data.Add("hash", gen_hash);

            var postData = "txnid=" + txnid;
            postData += "&refund_amount=" + refund_amount;
            postData += "&phone=" + phone;
            postData += "&key=" + Key;
            postData += "&amount=" + amount;
            postData += "&email=" + email;
            postData += "&hash=" + gen_hash;

            string url = "https://dashboard.easebuzz.in/transaction/v1/refund";

            var request = (HttpWebRequest)WebRequest.Create(url);

            var Ndata = Encoding.ASCII.GetBytes(postData);

            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = Ndata.Length;

            using (var stream = request.GetRequestStream())
            {
                stream.Write(Ndata, 0, Ndata.Length);
            }

            var response = (HttpWebResponse)request.GetResponse();

            var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
            return responseString;
        }

        //initiates transaction api 
        public string transactionAPI(string txnid, string amount, string email, string phone)
        {
            System.Collections.Hashtable data = new System.Collections.Hashtable();
            data.Add("key", Key);
            data.Add("txnid", txnid);
            data.Add("amount", amount);
            data.Add("email", email);
            data.Add("phone", phone);

            // generate hash
            string[] hashVarsSeq = "key|txnid|amount|email|phone".Split('|'); // spliting hash sequence from config
            string hash_string = "";
            foreach (string hash_var in hashVarsSeq)
            {
                hash_string = hash_string + (data.ContainsKey(hash_var) ? data[hash_var].ToString() : "");
                hash_string = hash_string + '|';
            }
            hash_string += salt;// appending SALT
            Console.WriteLine(hash_string);
            gen_hash = Easebuzz_Generatehash512(hash_string).ToLower();        //generating hash
            data.Add("hash", gen_hash);

            string url = "https://dashboard.easebuzz.in/transaction/v1/retrieve";
            var request = (HttpWebRequest)WebRequest.Create(url);

            var postData = "txnid=" + txnid;
            postData += "&amount=" + amount;
            postData += "&email=" + email;
            postData += "&phone=" + phone;
            postData += "&key=" + Key;
            postData += "&hash=" + gen_hash;

            var Ndata = Encoding.ASCII.GetBytes(postData);

            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = Ndata.Length;

            using (var stream = request.GetRequestStream())
            {
                stream.Write(Ndata, 0, Ndata.Length);
            }

            var response = (HttpWebResponse)request.GetResponse();

            var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
            //Response.Write(responseString);
            //string testResponse = "take it or leave it </br>";
            return responseString;
        }

        //iniitiate transactionDateAPI api 
        public string transactionDateAPI(string merchant_email, string transaction_date)
        {
            System.Collections.Hashtable data = new System.Collections.Hashtable();
            data.Add("key", Key);
            data.Add("merchant_email", merchant_email);
            data.Add("transaction_date", transaction_date);
            // generate hash
            string[] hashVarsSeq = "key|merchant_email|transaction_date".Split('|'); // spliting hash sequence from config
            string hash_string = "";
            foreach (string hash_var in hashVarsSeq)
            {
                hash_string = hash_string + (data.ContainsKey(hash_var) ? data[hash_var].ToString() : "");
                hash_string = hash_string + '|';
            }
            hash_string += salt;// appending SALT
            gen_hash = Easebuzz_Generatehash512(hash_string).ToLower();        //generating hash
            data.Add("hash", gen_hash);

            string url = "https://dashboard.easebuzz.in/transaction/v1/retrieve/date";
            var request = (HttpWebRequest)WebRequest.Create(url);

            var postData = "merchant_key=" + Key;
            postData += "&merchant_email=" + merchant_email;
            postData += "&transaction_date=" + transaction_date;
            postData += "&hash=" + gen_hash;

            var Ndata = Encoding.ASCII.GetBytes(postData);

            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = Ndata.Length;

            using (var stream = request.GetRequestStream())
            {
                stream.Write(Ndata, 0, Ndata.Length);
            }

            var response = (HttpWebResponse)request.GetResponse();

            var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
            return responseString;
        }

        //initiate payoutAPI api 
        public string payoutAPI(string merchant_email, string payout_date)
        {
            System.Collections.Hashtable data = new System.Collections.Hashtable();
            data.Add("key", Key);
            data.Add("merchant_email", merchant_email);
            data.Add("payout_date", payout_date);
            // generate hash
            string[] hashVarsSeq = "key|merchant_email|payout_date".Split('|'); // spliting hash sequence from config
            string hash_string = "";
            foreach (string hash_var in hashVarsSeq)
            {
                hash_string = hash_string + (data.ContainsKey(hash_var) ? data[hash_var].ToString() : "");
                hash_string = hash_string + '|';
            }
            hash_string += salt;// appending SALT
            gen_hash = Easebuzz_Generatehash512(hash_string).ToLower();        //generating hash
            data.Add("hash", gen_hash);

            string url = "https://dashboard.easebuzz.in/payout/v1/retrieve";
            var request = (HttpWebRequest)WebRequest.Create(url);

            var postData = "merchant_key=" + Key;
            postData += "&merchant_email=" + merchant_email;
            postData += "&payout_date=" + payout_date;
            postData += "&hash=" + gen_hash;
            var Ndata = Encoding.ASCII.GetBytes(postData);

            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = Ndata.Length;

            using (var stream = request.GetRequestStream())
            {
                stream.Write(Ndata, 0, Ndata.Length);
            }

            var response = (HttpWebResponse)request.GetResponse();

            var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
            return responseString;
        }


    }
}
