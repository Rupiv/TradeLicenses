using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace Gba.TradeLicense.Api.Services
{
    /// <summary>
    /// Server-side abuse protection around the existing OTP procedures (USP_OTP_PROCESS):
    ///   - send: 20 s cooldown per mobile number (same as the login page countdown), at most
    ///     5 OTPs per number and 20 per client IP in 15 minutes;
    ///   - verify: at most 5 wrong OTPs per number in 15 minutes, then locked until the window ends;
    ///     an OTP that was already verified once is refused (one-time use, whatever the procedure does);
    ///     verifications of the same number run one at a time (no parallel guessing / double use).
    /// In-memory, per API instance (like the forgot-password reset token).
    /// </summary>
    public sealed class OtpGuard
    {
        public static readonly TimeSpan SendCooldown = TimeSpan.FromSeconds(20);
        public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
        public const int MaxSendsPerMobile = 5;
        public const int MaxSendsPerIp = 20;
        public const int MaxFailedVerifications = 5;

        private readonly IMemoryCache _cache;
        private static readonly object Sync = new();

        public OtpGuard(IMemoryCache cache) => _cache = cache;

        public static string NormalizeMobile(string? mobile)
        {
            var digits = new string((mobile ?? string.Empty).Where(char.IsDigit).ToArray());
            return digits.Length > 10 ? digits[^10..] : digits;
        }

        private sealed class Counter
        {
            public int Count;
            public DateTimeOffset WindowEnds;
        }

        private Counter GetCounter(string key)
        {
            lock (Sync)
            {
                if (!_cache.TryGetValue(key, out Counter? c) || c == null || c.WindowEnds <= DateTimeOffset.UtcNow)
                {
                    c = new Counter { WindowEnds = DateTimeOffset.UtcNow.Add(Window) };
                    _cache.Set(key, c, c.WindowEnds);
                }
                return c;
            }
        }

        /// <summary>Null when an OTP may be sent now (and records it); otherwise the reason to show.</summary>
        public string? TryReserveSend(string? mobile, string? clientIp)
        {
            var m = NormalizeMobile(mobile);
            lock (Sync)
            {
                var cooldownKey = "otp-send-cooldown:" + m;
                if (_cache.TryGetValue(cooldownKey, out _))
                    return "Please wait a few seconds before requesting another OTP.";

                var perMobile = GetCounter("otp-send:" + m);
                var perIp = GetCounter("otp-send-ip:" + (clientIp ?? "unknown"));
                if (perMobile.Count >= MaxSendsPerMobile || perIp.Count >= MaxSendsPerIp)
                    return "Too many OTP requests. Please try again after some time.";

                perMobile.Count++;
                perIp.Count++;
                _cache.Set(cooldownKey, true, SendCooldown);
                return null;
            }
        }

        /// <summary>One verification at a time per mobile number.</summary>
        public SemaphoreSlim VerifyLock(string? mobile)
        {
            var key = "otp-verify-lock:" + NormalizeMobile(mobile);
            lock (Sync)
            {
                if (!_cache.TryGetValue(key, out SemaphoreSlim? sem) || sem == null)
                {
                    sem = new SemaphoreSlim(1, 1);
                    _cache.Set(key, sem, new MemoryCacheEntryOptions { SlidingExpiration = Window });
                }
                return sem;
            }
        }

        public bool IsVerifyLocked(string? mobile) =>
            GetCounter("otp-fail:" + NormalizeMobile(mobile)).Count >= MaxFailedVerifications;

        private static string OtpFingerprint(string mobile, string otp) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(mobile + "|" + otp.Trim())));

        /// <summary>True when this exact OTP was already verified successfully for this number.</summary>
        public bool WasAlreadyUsed(string? mobile, string? otp) =>
            _cache.TryGetValue("otp-used:" + OtpFingerprint(NormalizeMobile(mobile), otp ?? string.Empty), out _);

        public void RecordFailure(string? mobile)
        {
            lock (Sync) { GetCounter("otp-fail:" + NormalizeMobile(mobile)).Count++; }
        }

        public void RecordSuccess(string? mobile, string? otp)
        {
            var m = NormalizeMobile(mobile);
            lock (Sync)
            {
                _cache.Remove("otp-fail:" + m);
                _cache.Set("otp-used:" + OtpFingerprint(m, otp ?? string.Empty), true, Window);
            }
        }
    }
}
