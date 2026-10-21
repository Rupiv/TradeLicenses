using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gba.TradeLicense.Domain.Entities
{
    public class ForgotPasswordRequest
    {
        public string? Login { get; set; }
        public string? MobileNo { get; set; }
    }

    public class ForgotPasswordVerifyRequest
    {
        public string? Login { get; set; }
        public string? Otp { get; set; }
    }

    public class ForgotPasswordUser
    {
        public int loginID { get; set; }
        public string? login { get; set; }
        public string? MobileNo { get; set; }
    }
    public class ResetPasswordRequest
    {
        public string? Login { get; set; }
        public string? NewPassword { get; set; }
        public string? ResetToken { get; set; }
    }

}
