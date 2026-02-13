namespace PmSTools.Models
{
    public class PlaceInfoItem
    {
        public string? Name { get; set; }
        public string? Street { get; set; }
        public string? PostalCode { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }

        // Optional geocoded coordinates (populated when user chooses a result)
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}
