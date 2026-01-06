namespace Gba.TradeLicense.Application.Models;

#region LOGIN

// Incoming login request from client
// Client sends ONLY credentials (no IP, no browser)
public sealed record LoginRequest(
    string UsernameOrPhone,
    string Password
);

// Result returned by Login Stored Procedure (usp_LoginUser)
public sealed class LoginSpResult
{
    public bool Success { get; set; }
    public int loginID { get; set; }
    public string LoginName { get; set; } = "";
    public string MobileNo { get; set; } = "";

    public int UserDesignationID { get; set; }
    public string DesignationName { get; set; } = "";   // 🔑 IMPORTANT

    public bool OtpRequired { get; set; }
    public string Message { get; set; } = "";
}


// Final response returned to API caller
public sealed record LoginResult(
    bool Success,
    string? AccessToken,
    string? Error,
    bool OtpRequired
);

#endregion


#region OTP

// Request to send OTP
public sealed record OtpSendRequest(
    string Phone,
    string Purpose = "login"
);

// Result after sending OTP
public sealed record OtpSendResult(
    bool Success,
    string Message
);

// Request to verify OTP
public sealed record OtpVerifyRequest(
    string Phone,
    string Otp,
    string Purpose = "login"
);

// Result returned by OTP verification Stored Procedure
public sealed class OtpVerifySpResult
{
    public bool Success { get; set; }
    public int loginID { get; set; }                // Login_Master.loginID
    public string LoginName { get; set; } = "";     // Login_Master.login
    public string MobileNo { get; set; } = "";      // Login_Master.MobileNo
    public string Message { get; set; } = "";
}

// Final response returned to API caller after OTP verification
public sealed record OtpVerifyResult(
    bool Success,
    string? AccessToken,
    string? Error
);

#endregion
