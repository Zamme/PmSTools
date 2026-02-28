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
                if (_streetNumber == value)
                    return;

                _streetNumber = value;
                OnPropertyChanged();
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
    }
}
