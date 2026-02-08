using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gba.TradeLicense.Domain.Entities
{
    public class TradeLicenceApproverApplicationDto
    {
        public long LicenceApplicationID { get; set; }
        public string ApplicationNumber { get; set; }
        public DateTime? ApplicationSubmitDate { get; set; }

        public int LicenceApplicationStatusID { get; set; }
        public string LicenceApplicationStatusName { get; set; }

        public long TradeLicenceID { get; set; }
        public string ApplicantName { get; set; }
        public string TradeName { get; set; }
        public string MobileNumber { get; set; }
        public string EmailID { get; set; }

        public int? MohID { get; set; }
        public string MohName { get; set; }

        public int? WardID { get; set; }
        public string WardName { get; set; }

        /* ===== GEO LOCATION ===== */
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public int? RoadID { get; set; }
        public decimal? RoadWidthMtrs { get; set; }
        public string RoadCategoryCode { get; set; }
        public string RoadCategory { get; set; }
        public bool IsConfirmed { get; set; }
        public DateTime? EntryDate { get; set; }
    }
}
