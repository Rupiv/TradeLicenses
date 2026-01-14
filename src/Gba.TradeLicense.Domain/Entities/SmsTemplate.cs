using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gba.TradeLicense.Domain.Entities
{
    public class SmsTemplate
    {
        public string TemplateKey { get; set; }
        public string TemplateId { get; set; }
        public string TemplateText { get; set; }
        public string SmsType { get; set; }
        public bool IsActive { get; set; }
    }
}
