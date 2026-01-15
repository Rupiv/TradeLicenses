using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gba.TradeLicense.Domain.Entities
{
    public class BBMPWardModel
    {
        public int wardID { get; set; }
        public string wardCode { get; set; }
        public string wardName { get; set; }
        public string wardNativeName { get; set; }
        public int zoneID { get; set; }
        public int constituencyID { get; set; }
        public DateTime entryDate { get; set; }
    }
}
