using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PmSTools.Models
{
    public class DeliveryRouteStop : INotifyPropertyChanged
    {
        private int _order;
        public int Order
        {
            get => _order;
            set
            {
                if (_order == value)
                    return;

                _order = value;
                OnPropertyChanged();
            }
        }

        private string? _name;
        public string? Name
        {
            get => _name;
            set
            {
                if (_name == value)
                    return;

                _name = value;
                OnPropertyChanged();
            }
        }

        private string? _streetName;
        public string? StreetName
        {
            get => _streetName;
            set
            {
                if (_streetName == value)
                    return;

                _streetName = value;
                OnPropertyChanged();
            }
        }

        private string? _streetNumber;
        public string? StreetNumber
        {
            get => _streetNumber;
            set
            {
                var (number, extras) = SplitStreetNumberAndExtras(value);
                if (_streetNumber == number && _streetExtraDetails == extras)
                    return;

                _streetNumber = number;
                _streetExtraDetails = extras;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StreetExtraDetails));
                OnPropertyChanged(nameof(StreetNumberDisplay));
            }
        }

        private string? _streetExtraDetails;
        public string? StreetExtraDetails
        {
            get => _streetExtraDetails;
            set
            {
                var normalized = NormalizeCommaSpaces(value);
                if (_streetExtraDetails == normalized)
                    return;

                _streetExtraDetails = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StreetNumberDisplay));
            }
        }

        public string StreetNumberDisplay
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_streetNumber))
                    return string.Empty;

                if (string.IsNullOrWhiteSpace(_streetExtraDetails))
                    return _streetNumber;

                return $"{_streetNumber} {_streetExtraDetails}".Trim();
            }
        }

        private string? _postalCode;
        public string? PostalCode
        {
            get => _postalCode;
            set
            {
                if (_postalCode == value)
                    return;

                _postalCode = value;
                OnPropertyChanged();
            }
        }

        private string? _city;
        public string? City
        {
            get => _city;
            set
            {
                if (_city == value)
                    return;

                _city = value;
                OnPropertyChanged();
            }
        }

        private string? _country;
        public string? Country
        {
            get => _country;
            set
            {
                if (_country == value)
                    return;

                _country = value;
                OnPropertyChanged();
            }
        }

        private double? _latitude;
        public double? Latitude
        {
            get => _latitude;
            set
            {
                if (_latitude == value)
                    return;

                _latitude = value;
                OnPropertyChanged();
            }
        }

        private double? _longitude;
        public double? Longitude
        {
            get => _longitude;
            set
            {
                if (_longitude == value)
                    return;

                _longitude = value;
                OnPropertyChanged();
            }
        }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value)
                    return;

                _isExpanded = value;
                OnPropertyChanged();
            }
        }

        private bool _isFirst;
        public bool IsFirst
        {
            get => _isFirst;
            set
            {
                if (_isFirst == value)
                    return;

                _isFirst = value;
                OnPropertyChanged();
            }
        }

        private bool _isLast;
        public bool IsLast
        {
            get => _isLast;
            set
            {
                if (_isLast == value)
                    return;

                _isLast = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        internal static string ExtractStreetExtraDetails(string? street)
        {
            if (string.IsNullOrWhiteSpace(street))
                return string.Empty;

            var normalized = NormalizeCommaSpaces(street);
            var match = System.Text.RegularExpressions.Regex.Match(
                normalized,
                @"\b\d{1,5}[A-Za-z]?\b(?<rest>.*)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (!match.Success)
                return string.Empty;

            var rest = match.Groups["rest"].Value;
            if (string.IsNullOrWhiteSpace(rest) || !System.Text.RegularExpressions.Regex.IsMatch(rest, @"\d"))
                return string.Empty;

            rest = System.Text.RegularExpressions.Regex.Replace(rest, @"^[\s\-/,]+", " ");
            return NormalizeCommaSpaces(rest);
        }

        private static (string Number, string Extras) SplitStreetNumberAndExtras(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return (string.Empty, string.Empty);

            var normalized = NormalizeCommaSpaces(value);
            var match = System.Text.RegularExpressions.Regex.Match(
                normalized,
                @"\b\d{1,5}[A-Za-z]?\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (!match.Success)
                return (normalized, string.Empty);

            var number = match.Value.Trim();
            var rest = normalized.Substring(match.Index + match.Length);
            if (string.IsNullOrWhiteSpace(rest) || !System.Text.RegularExpressions.Regex.IsMatch(rest, @"\d"))
                return (number, string.Empty);

            rest = System.Text.RegularExpressions.Regex.Replace(rest, @"^[\s\-/,]+", " ");
            return (number, NormalizeCommaSpaces(rest));
        }

        private static string NormalizeCommaSpaces(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var normalized = value.Replace(',', ' ');
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ").Trim();
            return normalized;
        }
    }
}
