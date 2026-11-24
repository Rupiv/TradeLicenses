using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gba.TradeLicense.Domain.Entities
{
    public class TradeZonalClassification
    {
        public int ZonalClassificationID { get; set; }
        // Optional: the Master Data screen sends only the name (see BBMPZoneModel)
        public string? ZonalCode { get; set; }
        public string ZonalClassificationName { get; set; }
        public string? ZonalClassificationNativeName { get; set; }
        public bool IsActive { get; set; }
    }

}
