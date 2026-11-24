using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace Gba.TradeLicense.Api.Services
{
    /// <summary>
    /// One-time proof that a mobile number's OTP was verified by POST /api/sms/otp/verify.
    /// Issued there, consumed by POST /api/auth/login-user (same pattern as the forgot-password
    /// reset token). In-memory: a token is valid on the API instance that issued it, for 10 minutes.
    /// </summary>
    public static class OtpLoginTokens
    {
        private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

        private static string Key(string token) => "otp-login:" + token;

        private static string LastTenDigits(string? mobile)
        {
            var digits = new string((mobile ?? string.Empty).Where(char.IsDigit).ToArray());
            return digits.Length > 10 ? digits[^10..] : digits;
        }

        public static string Issue(IMemoryCache cache, string? mobileNo)
        {
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            cache.Set(Key(token), LastTenDigits(mobileNo), Lifetime);
            return token;
        }

        public static bool TryConsume(IMemoryCache cache, string? token, string? mobileNo)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            var key = Key(token.Trim());
            if (!cache.TryGetValue(key, out string? verifiedMobile))
                return false;

            var expected = LastTenDigits(mobileNo);
            if (string.IsNullOrEmpty(expected) || !string.Equals(verifiedMobile, expected, StringComparison.Ordinal))
                return false;

            cache.Remove(key);
            return true;
        }
    }
}
