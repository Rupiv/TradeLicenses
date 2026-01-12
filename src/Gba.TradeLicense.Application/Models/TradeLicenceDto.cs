using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gba.TradeLicense.Application.Models
{
    public class TradeLicenceDto
    {
        public string applicantName { get; set; }
        public string doorNumber { get; set; }
        public string address1 { get; set; }
        public string address2 { get; set; }
        public string address3 { get; set; }
        public string pincode { get; set; }
        public string landLineNumber { get; set; }
        public string mobileNumber { get; set; }
        public string emailID { get; set; }
        public string tradeName { get; set; }
        public int? zonalClassificationID { get; set; }
        public int? mohID { get; set; }
        public int? wardID { get; set; }
        public int? PropertyID { get; set; }
        public string PIDNumber { get; set; }
        public string khathaNumber { get; set; }
        public string surveyNumber { get; set; }
        public string street { get; set; }
        public string GISNumber { get; set; }
        public string licenceNumber { get; set; }
        public DateTime? licenceCommencementDate { get; set; }
        public int? licenceStatusID { get; set; }
        public string oldapplicationNumber { get; set; }
        public string newlicenceNumber { get; set; }
    }
}
