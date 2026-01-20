using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Gba.TradeLicense.Domain.Entities
{
    public class GeoInputDto
    {
        public int LicenceApplicationID { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
    }

    public class RoadApiRequest
    {
        public string applicantId { get; set; }
        public string parameter { get; set; }
        public string values { get; set; }
    }
    public class GeoLocationDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }

        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string RoadID { get; set; }
        public int? RoadWidthMtrs { get; set; }
        public string RoadCategoryCode { get; set; }
        public string RoadCategory { get; set; }
        public bool IsConfirmed { get; set; }
        public DateTime? EntryDate { get; set; }
    }
    public class LicenceGeoConfirmDto
    {
        public long LicenceApplicationID { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string RoadID { get; set; }
        public int RoadWidthMtrs { get; set; }
        public string RoadCategoryCode { get; set; }
        public string RoadCategory { get; set; }
        public int LoginID { get; set; }
    }

    public class KgisRoadResponse
    {
        public string RoadType { get; set; }

       
        public int? RoadWidthMtrs { get; set; }

        public string RoadCategory { get; set; }
        public string RoadCategoryCode { get; set; }
    }


}
