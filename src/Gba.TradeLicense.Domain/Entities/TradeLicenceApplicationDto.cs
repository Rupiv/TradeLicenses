using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gba.TradeLicense.Domain.Entities
{
    public class UserApplicationResponse
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string MobileNumber { get; set; }
        public string EmailId { get; set; }
        public List<ApplicationDto> Applications { get; set; } = new();
    }
    public class ApplicationDto
    {
        public int LicenceApplicationID { get; set; }
        public string ApplicationNumber { get; set; }
        public DateTime? ApplicationSubmitDate { get; set; }
        public DateTime? LicenceFromDate { get; set; }
        public DateTime? LicenceToDate { get; set; }
        public string ApplicationStatus { get; set; }
        public string CurrentStatus { get; set; }
        public int TradeLicenceID { get; set; }
        public string ApplicantName { get; set; }
        public string TradeName { get; set; }
        public GeoLocationDtos? GeoLocation { get; set; }
        public List<DocumentDto> Documents { get; set; } = new();
    }
    public class GeoLocationDtos
    {
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public int RoadWidthMtrs { get; set; }
        public string? RoadCategory { get; set; }
    }
    public class DocumentDto
    {
        public int DocumentID { get; set; }
        public string? DocumentName { get; set; }
        public string? FileName { get; set; }
        public string? FilePath { get; set; }
    }
    public class LicenceTradeDetailsUpsertDto
    {
        public long LicenceApplicationID { get; set; }
        public int TradeSubID { get; set; }
        public decimal TradeFee { get; set; }
    }


}
