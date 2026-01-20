namespace Gba.TradeLicense.Application.Models;

#region LOGIN

// Incoming login request from client
// Client sends ONLY credentials (no IP, no browser)
public sealed record LoginRequest(
    string UsernameOrPhone,
    string Password
);
public sealed record RegisterUserDto
(
    string FullName,
    string MobileNumber,
    string? EmailID
);

public sealed record LoginDto
(
    string MobileNumber
);

// Result returned by Login Stored Procedure (usp_LoginUser)
public sealed class LoginSpResult
{

    public int UserID { get; set; }          // MUST match UserID
    public string FullName { get; set; }     // MUST match FullName
    public string MobileNumber { get; set; } // MUST match MobileNumber
    public string EmailID { get; set; }
    public bool Success { get; set; }

    

 


    public int UserDesignationID { get; set; }     // ✅ MATCH SP
    public string UserDesignationName { get; set; } = string.Empty; // ✅ MATCH SP

    public bool OtpRequired { get; set; }

    public string? Message { get; set; }
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
