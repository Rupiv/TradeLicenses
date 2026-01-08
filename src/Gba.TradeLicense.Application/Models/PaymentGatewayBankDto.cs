using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gba.TradeLicense.Application.Models
{
    public sealed class PaymentGatewayBankDto
    {
        public int Pg_ID { get; set; }
        public string Pg_Bank { get; set; } = string.Empty;
        public int Pg_TypeID { get; set; }
    }
}
