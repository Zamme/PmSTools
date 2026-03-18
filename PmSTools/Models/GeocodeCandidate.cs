namespace PmSTools.Models
{
    public class GeocodeCandidate
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string MatchBadge { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lon { get; set; }
        public string HouseNumber { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public int SourceOrder { get; set; }
    }
}
