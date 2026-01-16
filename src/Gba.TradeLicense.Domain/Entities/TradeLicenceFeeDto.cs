using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gba.TradeLicense.Domain.Entities
{
    public class TradeLicenceFeeDto
    {
        public int TradeFeeID { get; set; }
        public int TradeSubID { get; set; }
        public decimal TradeLicenceFee { get; set; }
        public string TradeApproveAuth { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int BlockPeriodID { get; set; }
        public string? Remarks { get; set; }
    }

}
