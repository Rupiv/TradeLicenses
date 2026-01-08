using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gba.TradeLicense.Application.Models
{
    public sealed class TradeTypeDto
    {
        public int TradeTypeID { get; set; }
        public string TradeTypeCode { get; set; } = "";
        public string TradeTypeName { get; set; } = "";
        public string IsActive { get; set; } = "";
        public DateTime EntryDate { get; set; }
    }



    public sealed class TradeMajorDto
    {
        public int TradeMajorID { get; set; }
        public string TradeMajorCode { get; set; } = "";
        public string TradeMajorName { get; set; } = "";
        public string TradeMajorNativeName { get; set; } = "";
    }

    public sealed class TradeMinorDto
    {
        public int TradeMinorID { get; set; }
        public string TradeMinorCode { get; set; } = "";
        public string TradeMinorName { get; set; } = "";
        public string TradeMinorNativeName { get; set; } = "";
    }

    public sealed class TradeSubDto
    {
        public int TradeSubID { get; set; }
        public string TradeSubCode { get; set; } = "";
        public string TradeSubName { get; set; } = "";
        public string TradeSubNativeName { get; set; } = "";
        public int BlockPeriodID { get; set; }
    }


}
