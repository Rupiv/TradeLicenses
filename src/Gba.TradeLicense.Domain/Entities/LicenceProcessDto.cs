using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Invalid LicenceApplicationID")]
        public int LicenceApplicationID { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Invalid LoginID")]
        public int LoginID { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Invalid LicenceProcessID")]
        public int LicenceProcessID { get; set; }   // 3,4,5,6

        [Required]
        [Range(1, 10, ErrorMessage = "Invalid CurrentStatus")]
        public int CurrentStatus { get; set; }   // numeric status

        // 🔴 MAIN VULNERABLE FIELD (FIXED)
        [Required]
        [StringLength(500, ErrorMessage = "Remarks too long")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-\.,()]*$",
            ErrorMessage = "Remarks contains invalid characters")]
        public string Remarks { get; set; }

        // optional (comma separated ids)
        [RegularExpression(@"^[0-9,]*$", ErrorMessage = "Invalid ActionReasonIds")]
        public string ActionReasonIds { get; set; } // optional
    }

}
