using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Gba.TradeLicense.Domain.Entities
{
    public class LicenceDocumentUploadDto
    {
        public long LicenceApplicationID { get; set; }
        public int DocumentID { get; set; }
        public IFormFile File { get; set; }
        public int LoginID { get; set; }
        public int? ApplicationDocumentID { get; set; }
    }


}
