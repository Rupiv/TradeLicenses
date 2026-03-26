using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gba.TradeLicense.Application.Models
{
    public class GeneratedLicenceCertificate
    {
        public int Id { get; set; }
        public int LicenceApplicationID { get; set; }
        public string ApplicationNumber { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/pdf";
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }

        public long? FileSizeBytes { get; set; }   // NEW
        public string? FileHash { get; set; }      // NEW
    }

}
