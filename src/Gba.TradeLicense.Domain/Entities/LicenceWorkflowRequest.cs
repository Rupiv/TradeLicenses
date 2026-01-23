using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gba.TradeLicense.Domain.Entities
{
    public class LicenceWorkflowRequest
    {
        public long LicenceApplicationID { get; set; }
        public int LicenceProcessID { get; set; }     // from Master_LicenceProcess
        public string Remarks { get; set; }
        public string ActionReasonIds { get; set; }   // optional
    }

}
