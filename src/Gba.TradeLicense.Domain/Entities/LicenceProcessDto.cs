using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gba.TradeLicense.Domain.Entities
{
    public class LicenceProcessDto
    {
        public int LicenceProcessID { get; set; }
        public string LicenceProcessName { get; set; }
        public string IsActive { get; set; }
    }
    public class LicenceStatusDto
    {
        public int LicenceApplicationStatusID { get; set; }
        public string LicenceApplicationStatusName { get; set; }
    }

}
