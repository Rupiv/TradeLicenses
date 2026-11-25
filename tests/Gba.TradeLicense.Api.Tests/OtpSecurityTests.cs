using Gba.TradeLicense.Api.Services;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Gba.TradeLicense.Api.Tests;

public class OtpGuardTests
{
    private static OtpGuard NewGuard() => new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public void Send_IsAllowedOnce_ThenCooldownApplies()
    {
        var guard = NewGuard();
        Assert.Null(guard.TryReserveSend("9876543210", "10.0.0.1"));
        Assert.NotNull(guard.TryReserveSend("9876543210", "10.0.0.1"));
    }

    [Fact]
    public void Send_CooldownIsPerMobileNumber_AndIgnoresFormatting()
    {
        var guard = NewGuard();
        Assert.Null(guard.TryReserveSend("+91 98765 43210", "10.0.0.1"));
        Assert.NotNull(guard.TryReserveSend("9876543210", "10.0.0.2"));   // same number, other format / IP
        Assert.Null(guard.TryReserveSend("9123456789", "10.0.0.1"));      // another number is unaffected
    }

    [Fact]
    public void Send_PerIpLimit_StopsSprayingManyNumbers()
    {
        var guard = NewGuard();
        for (var i = 0; i < OtpGuard.MaxSendsPerIp; i++)
            Assert.Null(guard.TryReserveSend($"90000000{i:00}", "10.9.9.9"));
        Assert.NotNull(guard.TryReserveSend("9111111111", "10.9.9.9"));
        Assert.Null(guard.TryReserveSend("9111111111", "10.1.1.1"));
    }

    [Fact]
    public void Verify_LocksAfterMaxFailures_AndSuccessResets()
    {
        var guard = NewGuard();
        for (var i = 0; i < OtpGuard.MaxFailedVerifications - 1; i++)
            guard.RecordFailure("9876543210");
        Assert.False(guard.IsVerifyLocked("9876543210"));

        guard.RecordSuccess("9876543210", "123456");
        Assert.False(guard.IsVerifyLocked("9876543210"));

        for (var i = 0; i < OtpGuard.MaxFailedVerifications; i++)
            guard.RecordFailure("9876543210");
        Assert.True(guard.IsVerifyLocked("9876543210"));
        Assert.False(guard.IsVerifyLocked("9123456789"));
    }

    [Fact]
    public void Verify_SameOtpCannotBeUsedTwice()
    {
        var guard = NewGuard();
        Assert.False(guard.WasAlreadyUsed("9876543210", "123456"));
        guard.RecordSuccess("9876543210", "123456");
        Assert.True(guard.WasAlreadyUsed("9876543210", "123456"));
        Assert.False(guard.WasAlreadyUsed("9876543210", "654321"));
        Assert.False(guard.WasAlreadyUsed("9123456789", "123456"));
    }

    [Fact]
    public async Task Verify_RunsOneAtATimePerNumber()
    {
        var guard = NewGuard();
        var first = guard.VerifyLock("9876543210");
        Assert.Same(first, guard.VerifyLock("+919876543210"));
        Assert.True(await first.WaitAsync(0));
        Assert.False(await guard.VerifyLock("9876543210").WaitAsync(0));
        first.Release();
        Assert.True(await guard.VerifyLock("9876543210").WaitAsync(0));
    }
}

public class OtpLoginTokenTests
{
    [Fact]
    public void Token_LogsInOnce_ForTheVerifiedNumberOnly()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var token = OtpLoginTokens.Issue(cache, "9876543210");

        Assert.False(OtpLoginTokens.TryConsume(cache, token, "9123456789")); // other number
        Assert.True(OtpLoginTokens.TryConsume(cache, token, "+91 98765 43210"));
        Assert.False(OtpLoginTokens.TryConsume(cache, token, "9876543210")); // already used
    }

    [Fact]
    public void Token_MissingOrForged_IsRejected()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        Assert.False(OtpLoginTokens.TryConsume(cache, null, "9876543210"));
        Assert.False(OtpLoginTokens.TryConsume(cache, "", "9876543210"));
        Assert.False(OtpLoginTokens.TryConsume(cache, "ABCDEF", "9876543210"));
    }

    [Fact]
    public async Task Token_ConcurrentUse_SucceedsExactlyOnce()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var token = OtpLoginTokens.Issue(cache, "9876543210");
        var results = await Task.WhenAll(Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => OtpLoginTokens.TryConsume(cache, token, "9876543210"))));
        Assert.Equal(1, results.Count(r => r));
    }
}

public class PaymentRedirectKeyTests
{
    [Fact]
    public void GeneratedKeyPair_RoundTripsThePaymentRedirectPayload()
    {
        // Same steps as payment-success (encrypt) and decrypt-payment (decrypt)
        using var aes = System.Security.Cryptography.Aes.Create();
        var data = Gba.TradeLicense.Infrastructure.Security.CryptoHelper.EncryptAES("{\"txnid\":\"T1\"}", aes.Key, aes.IV);
        var wrappedKey = Gba.TradeLicense.Infrastructure.Security.CryptoHelper.EncryptRSA(
            aes.Key, Gba.TradeLicense.Infrastructure.Security.CryptoHelper.GetPublicKey());

        var key = Gba.TradeLicense.Infrastructure.Security.CryptoHelper.DecryptRSA(
            wrappedKey, Gba.TradeLicense.Infrastructure.Security.CryptoHelper.GetPrivateKey());
        Assert.Equal("{\"txnid\":\"T1\"}",
            Gba.TradeLicense.Infrastructure.Security.CryptoHelper.DecryptAES(data, key, aes.IV));
    }
}
