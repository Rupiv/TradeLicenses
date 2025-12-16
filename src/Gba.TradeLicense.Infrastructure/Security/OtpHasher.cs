using System.Security.Cryptography;
using System.Text;

namespace Gba.TradeLicense.Infrastructure.Security;

public static class OtpHasher
{
    // HMAC-SHA256(phone|purpose|otp|secret) so OTP isn't stored in plaintext
    public static string Hash(string phone, string purpose, string otp, string secret)
    {
        var payload = $"{phone}|{purpose}|{otp}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }
}
