using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Gba.TradeLicense.Infrastructure.Sms
{
    namespace esms_client
    {

        public class SMSHttpPostClient
        {

           

            // Method for sending single SMS.

            public string sendSingleSMS(
                string username,
                string password,
                string senderid,
                string mobileNo,
                string message,
                string secureKey,
                string templateid)
            {
                Stream dataStream;

                // ✅ Force TLS 1.2 (Required by Govt SMS Gateway)
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(
                    "http://smsmobile1.karnataka.gov.in/index.php/sendmsg"
                );

                request.ProtocolVersion = HttpVersion.Version10;
                request.KeepAlive = false;
                request.ServicePoint.ConnectionLimit = 1;
                request.Method = "POST";

                // User-Agent (Govt sample compatible)
                request.UserAgent = "Mozilla/5.0";

                // 🔐 Encrypt password & generate secure key
                string encryptedPassword = encryptedPasswod(password);
                string newSecureKey = hashGenerator(
                    username.Trim(),
                    senderid.Trim(),
                    message.Trim(),
                    secureKey.Trim()
                );

                string smsServiceType = "singlemsg";

                string query =
                    "username=" + HttpUtility.UrlEncode(username.Trim()) +
                    "&password=" + HttpUtility.UrlEncode(encryptedPassword) +
                    "&smsservicetype=" + HttpUtility.UrlEncode(smsServiceType) +
                    "&content=" + HttpUtility.UrlEncode(message.Trim()) +
                    "&mobileno=" + HttpUtility.UrlEncode(mobileNo.Trim()) +
                    "&senderid=" + HttpUtility.UrlEncode(senderid.Trim()) +
                    "&key=" + HttpUtility.UrlEncode(newSecureKey) +
                    "&templateid=" + HttpUtility.UrlEncode(templateid.Trim());

                byte[] byteArray = Encoding.ASCII.GetBytes(query);

                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = byteArray.Length;

                // Write request
                using (dataStream = request.GetRequestStream())
                {
                    dataStream.Write(byteArray, 0, byteArray.Length);
                }

                // Read response
                using (WebResponse response = request.GetResponse())
                using (dataStream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(dataStream))
                {
                    return reader.ReadToEnd();
                }
            }




            // method for sending bulk SMS

            public string sendBulkSMS(
            string username,
            string password,
            string senderid,
            string mobileNos,
            string message,
            string secureKey,
            string templateid)
            {
                Stream dataStream;

                // ✅ Enforce TLS 1.2 (Govt requirement)
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(
                    "http://smsmobile1.karnataka.gov.in/index.php/sendmsg"
                );

                request.ProtocolVersion = HttpVersion.Version10;
                request.KeepAlive = false;
                request.ServicePoint.ConnectionLimit = 1;
                request.Method = "POST";

                // User-Agent as per Govt sample
                request.UserAgent = "Mozilla/5.0";

                // 🔐 Encrypt password & generate secure key (UNCHANGED)
                string encryptedPassword = encryptedPasswod(password);
                string newSecureKey = hashGenerator(
                    username.Trim(),
                    senderid.Trim(),
                    message.Trim(),
                    secureKey.Trim()
                );

                string smsServiceType = "bulkmsg";

                string query =
                    "username=" + HttpUtility.UrlEncode(username.Trim()) +
                    "&password=" + HttpUtility.UrlEncode(encryptedPassword) +
                    "&smsservicetype=" + HttpUtility.UrlEncode(smsServiceType) +
                    "&content=" + HttpUtility.UrlEncode(message.Trim()) +
                    "&bulkmobno=" + HttpUtility.UrlEncode(mobileNos.Trim()) +
                    "&senderid=" + HttpUtility.UrlEncode(senderid.Trim()) +
                    "&key=" + HttpUtility.UrlEncode(newSecureKey.Trim()) +
                    "&templateid=" + HttpUtility.UrlEncode(templateid.Trim());

                byte[] byteArray = Encoding.ASCII.GetBytes(query);

                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = byteArray.Length;

                // Write request
                using (dataStream = request.GetRequestStream())
                {
                    dataStream.Write(byteArray, 0, byteArray.Length);
                }

                // Read response
                using (WebResponse response = request.GetResponse())
                using (dataStream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(dataStream))
                {
                    return reader.ReadToEnd();
                }
            }


            /// <summary>
            /// method for Sending unicode..
            /// </summary>
            /// <param name="username"> Registered user name
            /// <param name="password"> Valid login password
            /// <param name="senderid">Sender ID 
            /// <param name="mobileNo"> valid Mobile Numbers 
            /// <param name="Unicodemessage">Unicodemessage Message Content 
            /// <param name="secureKey">Department generate key by login to services portal
            /// <param name="templateid">templateid unique for each template message content

            //method for Sending unicode message..

            public String sendUnicodeSMS(
      String username,
      String password,
      String senderid,
      String mobileNos,
      String Unicodemessage,
      String secureKey,
      String templateid)
            {
                Stream dataStream;

                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; //forcing .Net framework to use TLSv1.2

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://smsmobile1.karnataka.gov.in/index.php/sendmsg");
                request.ProtocolVersion = HttpVersion.Version10;
                request.KeepAlive = false;
                request.ServicePoint.ConnectionLimit = 1;

                //((HttpWebRequest)request).UserAgent = ".NET Framework Example Client";
                ((HttpWebRequest)request).UserAgent = "Mozilla/4.0 (compatible; MSIE 5.0; Windows 98; DigExt)";

                request.Method = "POST";

                // ❌ REMOVED: CertificatePolicy (obsolete in .NET Core)

                String U_Convertedmessage = "";

                foreach (char c in Unicodemessage)
                {
                    int j = (int)c;
                    String sss = "&#" + j + ";";
                    U_Convertedmessage = U_Convertedmessage + sss;
                }

                String encryptedPassword = encryptedPasswod(password);
                String NewsecureKey = hashGenerator(
                    username.Trim(),
                    senderid.Trim(),
                    U_Convertedmessage.Trim(),
                    secureKey.Trim()
                );

                String smsservicetype = "unicodemsg"; // for unicode msg

                String query =
                    "username=" + HttpUtility.UrlEncode(username.Trim()) +
                    "&password=" + HttpUtility.UrlEncode(encryptedPassword) +
                    "&smsservicetype=" + HttpUtility.UrlEncode(smsservicetype) +
                    "&content=" + HttpUtility.UrlEncode(U_Convertedmessage.Trim()) +
                    "&bulkmobno=" + HttpUtility.UrlEncode(mobileNos) +
                    "&senderid=" + HttpUtility.UrlEncode(senderid.Trim()) +
                    "&key=" + HttpUtility.UrlEncode(NewsecureKey.Trim()) +
                    "&templateid=" + HttpUtility.UrlEncode(templateid.Trim());

                byte[] byteArray = Encoding.ASCII.GetBytes(query);

                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = byteArray.Length;

                dataStream = request.GetRequestStream();
                dataStream.Write(byteArray, 0, byteArray.Length);
                dataStream.Close();

                WebResponse response = request.GetResponse();
                String Status = ((HttpWebResponse)response).StatusDescription;

                dataStream = response.GetResponseStream();
                StreamReader reader = new StreamReader(dataStream);
                String responseFromServer = reader.ReadToEnd();

                reader.Close();
                dataStream.Close();
                response.Close();

                return responseFromServer;
            }



            /// <summary>
            /// Method for sending OTP MSG.
            /// </summary>
            /// <param name="username"> Registered user name
            /// <param name="password"> Valid login password
            /// <param name="senderid">Sender ID 
            /// <param name="mobileNo"> valid single  Mobile Number 
            /// <param name="message">Message Content 
            /// <param name="secureKey">Department generate key by login to services portal
            /// <param name="templateid">templateid unique for each template message content

            // Method for sending OTP MSG.

            public String sendOTPMSG(
      String username,
      String password,
      String senderid,
      String mobileNo,
      String message,
      String secureKey,
      String templateid)
            {
                Stream dataStream;

                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                // forcing .Net framework to use TLSv1.2

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(
                    "http://smsmobile1.karnataka.gov.in/index.php/sendmsg"
                );
                request.ProtocolVersion = HttpVersion.Version10;
                request.KeepAlive = false;
                request.ServicePoint.ConnectionLimit = 1;

                //((HttpWebRequest)request).UserAgent = ".NET Framework Example Client";
                ((HttpWebRequest)request).UserAgent =
                    "Mozilla/4.0 (compatible; MSIE 5.0; Windows 98; DigExt)";

                request.Method = "POST";

                // ❌ REMOVED (obsolete in .NET Core / .NET 6+)
                // System.Net.ServicePointManager.CertificatePolicy = new MyPolicy();

                String encryptedPassword = encryptedPasswod(password);
                String key = hashGenerator(
                    username.Trim(),
                    senderid.Trim(),
                    message.Trim(),
                    secureKey.Trim()
                );

                String smsservicetype = "otpmsg"; // For OTP message

                String query =
                    "username=" + HttpUtility.UrlEncode(username.Trim()) +
                    "&password=" + HttpUtility.UrlEncode(encryptedPassword) +
                    "&smsservicetype=" + HttpUtility.UrlEncode(smsservicetype) +
                    "&content=" + HttpUtility.UrlEncode(message.Trim()) +
                    "&mobileno=" + HttpUtility.UrlEncode(mobileNo) +
                    "&senderid=" + HttpUtility.UrlEncode(senderid.Trim()) +
                    "&key=" + HttpUtility.UrlEncode(key.Trim()) +
                    "&templateid=" + HttpUtility.UrlEncode(templateid.Trim());

                byte[] byteArray = Encoding.ASCII.GetBytes(query);

                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = byteArray.Length;

                dataStream = request.GetRequestStream();
                dataStream.Write(byteArray, 0, byteArray.Length);
                dataStream.Close();

                WebResponse response = request.GetResponse();
                String Status = ((HttpWebResponse)response).StatusDescription;

                dataStream = response.GetResponseStream();
                StreamReader reader = new StreamReader(dataStream);
                String responseFromServer = reader.ReadToEnd();

                reader.Close();
                dataStream.Close();
                response.Close();

                return responseFromServer;
            }


            // Method for sending UnicodeOTP MSG.

            /// <summary>
            /// method for Sending unicode..
            /// </summary>
            /// <param name="username"> Registered user name
            /// <param name="password"> Valid login password
            /// <param name="senderid">Sender ID 
            /// <param name="mobileNo"> valid Mobile Numbers 
            /// <param name="Unicodemessage">Unicodemessage Message Content 
            /// <param name="secureKey">Department generate key by login to services portal
            /// <param name="templateid">templateid unique for each template message content

            //method for Sending unicode message..

            public String sendUnicodeOTPSMS(
                String username,
                String password,
                String senderid,
                String mobileNos,
                String UnicodeOTPmsg,
                String secureKey,
                String templateid)
            {
                Stream dataStream;

                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                // forcing .Net framework to use TLSv1.2

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(
                    "http://smsmobile1.karnataka.gov.in/index.php/sendmsg"
                );
                request.ProtocolVersion = HttpVersion.Version10;
                request.KeepAlive = false;
                request.ServicePoint.ConnectionLimit = 1;

                //((HttpWebRequest)request).UserAgent = ".NET Framework Example Client";
                ((HttpWebRequest)request).UserAgent =
                    "Mozilla/4.0 (compatible; MSIE 5.0; Windows 98; DigExt)";

                request.Method = "POST";

                // ❌ REMOVED (obsolete in .NET Core / .NET 6+)
                // System.Net.ServicePointManager.CertificatePolicy = new MyPolicy();

                String U_Convertedmessage = "";

                foreach (char c in UnicodeOTPmsg)
                {
                    int j = (int)c;
                    String sss = "&#" + j + ";";
                    U_Convertedmessage = U_Convertedmessage + sss;
                }

                String encryptedPassword = encryptedPasswod(password);
                String NewsecureKey = hashGenerator(
                    username.Trim(),
                    senderid.Trim(),
                    U_Convertedmessage.Trim(),
                    secureKey.Trim()
                );

                String smsservicetype = "unicodeotpmsg"; // for unicode OTP msg

                String query =
                    "username=" + HttpUtility.UrlEncode(username.Trim()) +
                    "&password=" + HttpUtility.UrlEncode(encryptedPassword) +
                    "&smsservicetype=" + HttpUtility.UrlEncode(smsservicetype) +
                    "&content=" + HttpUtility.UrlEncode(U_Convertedmessage.Trim()) +
                    "&bulkmobno=" + HttpUtility.UrlEncode(mobileNos) +
                    "&senderid=" + HttpUtility.UrlEncode(senderid.Trim()) +
                    "&key=" + HttpUtility.UrlEncode(NewsecureKey.Trim()) +
                    "&templateid=" + HttpUtility.UrlEncode(templateid.Trim());

                byte[] byteArray = Encoding.ASCII.GetBytes(query);

                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = byteArray.Length;

                dataStream = request.GetRequestStream();
                dataStream.Write(byteArray, 0, byteArray.Length);
                dataStream.Close();

                WebResponse response = request.GetResponse();
                String Status = ((HttpWebResponse)response).StatusDescription;

                dataStream = response.GetResponseStream();
                StreamReader reader = new StreamReader(dataStream);
                String responseFromServer = reader.ReadToEnd();

                reader.Close();
                dataStream.Close();
                response.Close();

                return responseFromServer;
            }

            /// <summary>
            /// Method to get Encrypted the password 
            /// </summary>
            /// <param name="password"> password as String"

            protected String encryptedPasswod(String password)
            {

                byte[] encPwd = Encoding.UTF8.GetBytes(password);
                //static byte[] pwd = new byte[encPwd.Length];
                HashAlgorithm sha1 = HashAlgorithm.Create("SHA1");
                byte[] pp = sha1.ComputeHash(encPwd);
                // static string result = System.Text.Encoding.UTF8.GetString(pp);
                StringBuilder sb = new StringBuilder();
                foreach (byte b in pp)
                {

                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();

            }

            /// <summary>
            /// Method to Generate hash code  
            /// </summary>
            /// <param name="secure_key">your last generated Secure_key 

            protected String hashGenerator(String Username, String sender_id, String message, String secure_key)
            {

                StringBuilder sb = new StringBuilder();
                sb.Append(Username).Append(sender_id).Append(message).Append(secure_key);
                byte[] genkey = Encoding.UTF8.GetBytes(sb.ToString());
                //static byte[] pwd = new byte[encPwd.Length];
                HashAlgorithm sha1 = HashAlgorithm.Create("SHA512");
                byte[] sec_key = sha1.ComputeHash(genkey);

                StringBuilder sb1 = new StringBuilder();
                for (int i = 0; i < sec_key.Length; i++)
                {
                    sb1.Append(sec_key[i].ToString("x2"));
                }
                return sb1.ToString();
            }

        }
       
    }
   
}
