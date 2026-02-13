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
    public class ApplicationTimelineDto
    {
        public int LicenceFlowID { get; set; }
        public int LicenceApplicationID { get; set; }
        public int LoginID { get; set; }
        public string UpdatedByUser { get; set; }
        public string LicenceProcessName { get; set; }
        public string Status { get; set; }
        public string Remarks { get; set; }
        public string ActionReasonIds { get; set; }
        public DateTime EntryDate { get; set; }
    }

    public class LicenceStatusDto
    {
        public int LicenceApplicationStatusID { get; set; }
        public string LicenceApplicationStatusName { get; set; }
    }
    public class LicenceActionRequest
    {
        public int LicenceApplicationID { get; set; }
        public int LoginID { get; set; }
        public int LicenceProcessID { get; set; }   // 3,4,5,6
        public int CurrentStatus { get; set; }   // APPROVED / REJECTED / OBJECTION
        public string Remarks { get; set; }
        public string ActionReasonIds { get; set; } // optional
    }

}
