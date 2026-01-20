using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Gba.TradeLicense.Application.Abstractions;
using Gba.TradeLicense.Application.Models;
using Gba.TradeLicense.Infrastructure.Security;

namespace Gba.TradeLicense.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private readonly IConfiguration _config;
    private readonly JwtTokenService _jwt;
    private readonly string _connStr;

    public AuthService(IConfiguration config, JwtTokenService jwt)
    {
        _config = config;
        _jwt = jwt;

        _connStr = _config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' not found.");
    }

    private IDbConnection CreateConnection()
        => new SqlConnection(_connStr);
    public async Task<LoginResult> LoginUserByMobileAsync(
    string mobileNumber,
    CancellationToken ct)
    {
        using var db = CreateConnection();

        var user = await db.QueryFirstOrDefaultAsync<LoginSpResult>(
            "usp_UserAuth_CRUD",
            new
            {
                Action = "LOGIN",
                MobileNumber = mobileNumber
            },
            commandType: CommandType.StoredProcedure
        );

        if (user == null)
        {
            return new LoginResult(
                Success: false,
                AccessToken: null,
                Error: "User not found or inactive",
                OtpRequired: false
            );
        }

        // 🔐 FORCE DESIGNATION AS TRADE_OWNER
        var token = _jwt.CreateAccessToken(
            user.UserID,
            user.FullName,
            user.MobileNumber,
            "TRADE_OWNER"
        );

        return new LoginResult(
            Success: true,
            AccessToken: token,
            Error: null,
            OtpRequired: false
        );
    }

    // ================= LOGIN =================
    public async Task<LoginResult> LoginAsync(
        string usernameOrPhone,
        string password,
        string ipAddress,
        string browser,
        CancellationToken ct)
    {
        using var db = CreateConnection();

        var result = await db.QueryFirstOrDefaultAsync<LoginSpResult>(
            "usp_LoginUser",
            new
            {
                UsernameOrPhone = usernameOrPhone,
                Password = password,
                LoginIP = ipAddress,
                BrowserType = browser
            },
            commandType: CommandType.StoredProcedure
        );

        if (result == null || !result.Success)
        {
            return new LoginResult(
                Success: false,
                AccessToken: null,
                Error: result?.Message ?? "Invalid credentials",
                OtpRequired: false
            );
        }

        if (result.OtpRequired)
        {
            return new LoginResult(
                Success: true,
                AccessToken: null,
                Error: null,
                OtpRequired: true
            );
        }

        // 🔐 CREATE JWT WITH DESIGNATION AS ROLE
        var token = _jwt.CreateAccessToken(
      result.UserID,
      result.FullName,
      result.MobileNumber,
      result.UserDesignationName
  );

      

        return new LoginResult(
            Success: true,
            AccessToken: token,
            Error: null,
            OtpRequired: false
        );
    }

    // ================= SEND OTP =================
    public async Task<OtpSendResult> SendOtpAsync(OtpSendRequest request, CancellationToken ct)
    {
        using var db = CreateConnection();

        var otp = Random.Shared.Next(100000, 999999).ToString();
        var secret = _config["Otp:Secret"] ?? _config["Jwt:Key"]!;
        var hash = OtpHasher.Hash(request.Phone, request.Purpose, otp, secret);

        await db.ExecuteAsync(
            "usp_SendOtp",
            new
            {
                Phone = request.Phone,
                Purpose = request.Purpose,
                OtpHash = hash,
                ExpiryMinutes = 10
            },
            commandType: CommandType.StoredProcedure
        );

        return new OtpSendResult(true, "OTP sent successfully.");
    }

    // ================= VERIFY OTP =================
    public async Task<OtpVerifyResult> VerifyOtpAsync(OtpVerifyRequest request, CancellationToken ct)
    {
        using var db = CreateConnection();

        var secret = _config["Otp:Secret"] ?? _config["Jwt:Key"]!;
        var hash = OtpHasher.Hash(request.Phone, request.Purpose, request.Otp, secret);

        var result = await db.QueryFirstOrDefaultAsync<OtpVerifySpResult>(
            "usp_VerifyOtp",
            new
            {
                Phone = request.Phone,
                Purpose = request.Purpose,
                OtpHash = hash
            },
            commandType: CommandType.StoredProcedure
        );

        if (result == null || !result.Success)
        {
            return new OtpVerifyResult(
                Success: false,
                AccessToken: null,
                Error: result?.Message ?? "OTP verification failed"
            );
        }

        var token = _jwt.CreateAccessToken(
      result.loginID,
      result.LoginName,
      result.MobileNo,
      ""
  );

        return new OtpVerifyResult(
            Success: true,
            AccessToken: token,
            Error: null
        );

    }
}
