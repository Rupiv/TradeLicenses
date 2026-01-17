using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gba.TradeLicense.Domain.Entities
{
    public class MLC
    {
        public int mlccd { get; set; }
        public string mlcname { get; set; }
    }
    public class MasterMohDto
    {
        public int MohID { get; set; }
        public string? MohCode { get; set; }
        public string? MohCodeOld { get; set; }
        public string MohName { get; set; } = string.Empty;
        public string? MohNativeName { get; set; }
        public string? MohShortName { get; set; }
        public int? ZoneID { get; set; }
        public int? ConstituencyID { get; set; }
        public DateTime EntryDate { get; set; }
        public int? HoId { get; set; }
        public int? JcId { get; set; }
        public int? DhoId { get; set; }
        public int? AdId { get; set; }
        public int? DdId { get; set; }
        public int? JdId { get; set; }
    }

    public class MohDto
    {
        public int MohCd { get; set; }
        public string MohName { get; set; } = string.Empty;
        public string? MohNameK { get; set; }
        public string? MohShortName { get; set; }
        public int? MajZoneCd { get; set; }
        public string? Address { get; set; }
    }

}
