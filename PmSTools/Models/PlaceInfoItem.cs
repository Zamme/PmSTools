namespace PmSTools.Models
{
    public class PlaceInfoItem
    {
        private string? _street;

        public string? Name { get; set; }
        public string? StreetName { get; set; }
        public string? StreetNumber { get; set; }
        public string? Street
        {
            get
            {
                var combined = CombineStreet(StreetName, StreetNumber);
                return !string.IsNullOrWhiteSpace(combined) ? combined : _street;
            }
            set
            {
                _street = value;

                if (!string.IsNullOrWhiteSpace(StreetName) || !string.IsNullOrWhiteSpace(StreetNumber))
                    return;

                SplitStreet(value, out var streetName, out var streetNumber);
                StreetName = streetName;
                StreetNumber = streetNumber;
            }
        }
        public string? PostalCode { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }

        // Optional geocoded coordinates (populated when user chooses a result)
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        private static string CombineStreet(string? streetName, string? streetNumber)
        {
            var name = (streetName ?? string.Empty).Trim();
            var number = (streetNumber ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(number))
                return string.Empty;

            return string.Join(" ", new[] { name, number }.Where(v => !string.IsNullOrWhiteSpace(v)));
        }

        private static void SplitStreet(string? street, out string streetName, out string streetNumber)
        {
            streetName = string.Empty;
            streetNumber = string.Empty;

            if (string.IsNullOrWhiteSpace(street))
                return;

            var normalized = System.Text.RegularExpressions.Regex.Replace(street.Trim(), @"\s+", " ");
            var match = System.Text.RegularExpressions.Regex.Match(
                normalized,
                @"^(?<name>.+?)\s+(?:(?:n\.?|nº|no\.?|num\.?)\s*)?(?<number>\d{1,5}[A-Za-z]?)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (match.Success)
            {
                streetName = match.Groups["name"].Value.Trim();
                streetNumber = match.Groups["number"].Value.Trim();
                return;
            }

            streetName = normalized;
        }
    }
}
