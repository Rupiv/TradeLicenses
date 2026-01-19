using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gba.TradeLicense.Domain.Entities
{
    public sealed record LoginMasterDto
    {
      public  string Login { get; set; }
     public   string Password { get; set; }
     public   int OfficeDetailsID    { get; set; }
    public    int UserDesignationID  { get; set; }
      public  string? SakalaDO_Code { get; set; }
    public string? MobileNo { get; set; }
     public int UpdatedBy { get; set; }
    }
    public sealed record OfficeDetailsDto
    {
        public string OfficeName { get; set; }
        public string OfficeID { get; set; }
        public int OfficeTypeID { get; set; }
        public string? BranchAddress { get; set; }
        public string? ContactPerson { get; set; }
        public string? EmailID { get; set; }
        public string? ContactNO { get; set; }
    }

}
