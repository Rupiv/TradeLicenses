using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gba.TradeLicense.Domain.Entities
{
    public class OtpRequest
    {
        public string MobileNo { get; set; }
    }

    public class OtpVerifyRequestDto
    {
        public string MobileNo { get; set; }
        public string Otp { get; set; }
    }

    public class ApplicationSmsRequest
    {
        public string ApplicationNo { get; set; }
        public string MobileNo { get; set; }
    }

    public class FinalDecisionSmsRequest
    {
        public string Status { get; set; }
        public string ApplicationNo { get; set; }
        public string OfficeName { get; set; }
        public string MobileNo { get; set; }
    }


}
