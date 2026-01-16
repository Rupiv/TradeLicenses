namespace Gba.TradeLicense.Api.Controllers.Master
{
    public class AssemblyConstituencyModel
    {
        public int constituencyID { get; set; }
        public string constituencyCode { get; set; }
        public string constituencyName { get; set; }
        public string constituencyNativeName { get; set; }
        public int zoneID { get; set; }
        public DateTime entryDate { get; set; }
    }
}
