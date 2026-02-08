using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gba.TradeLicense.Domain.Entities
{
    public class ApprovedLicenceCertificateDto
    {
        public long LicenceApplicationID { get; set; }
        public string ApplicationNumber { get; set; }
        public string FinancialYear { get; set; }

        public string LicenceNumber { get; set; }
        public string ApplicantName { get; set; }
        public string TradeName { get; set; }
        public string TradeAddress { get; set; }

        public string TradeMajorName { get; set; }
        public string TradeMinorName { get; set; }
        public string TradeSubName { get; set; }

        public DateTime? LicenceFromDate { get; set; }
        public DateTime? LicenceToDate { get; set; }

        public string ReceiptNumber { get; set; }
        public DateTime? ReceiptDate { get; set; }
        public decimal TradeFee { get; set; }
        public int? WardID { get; set; }
        public string WardName { get; set; }
        public string ApplicationStatus { get; set; }
    }

}
