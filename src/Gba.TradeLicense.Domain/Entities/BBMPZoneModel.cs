using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gba.TradeLicense.Domain.Entities
{
    public class BBMPZoneModel
    {
        public int zoneID { get; set; }
        public string zoneCode { get; set; }
        public string zoneCodeOld { get; set; }
        public string zoneName { get; set; }
        public string zoneNativeName { get; set; }
        public DateTime entryDate { get; set; }
    }
    public class BBMPWardDropdownModel
    {
        public int wardID { get; set; }
        public string wardName { get; set; }
    }
}
