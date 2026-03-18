using System;
using System.Collections.ObjectModel;

namespace PmSTools.Models
{
    public class DeliveryRoute
    {
        public string? Name { get; set; }
        public ObservableCollection<DeliveryRouteStop> Stops { get; } = new ObservableCollection<DeliveryRouteStop>();

        public DeliveryRouteStop AddStop(DeliveryRouteStop? stop = null)
        {
            var routeStop = stop ?? new DeliveryRouteStop();
            Stops.Add(routeStop);
            RenumberStops();
            return routeStop;
        }

        public bool RemoveStop(DeliveryRouteStop stop)
        {
            if (!Stops.Remove(stop))
                return false;

            RenumberStops();
            return true;
        }

        public void RenumberStops()
        {
            var totalStops = Stops.Count;
            for (var index = 0; index < totalStops; index++)
            {
                var stop = Stops[index];
                stop.Order = index + 1;
                stop.IsFirst = index == 0;
                stop.IsLast = index == totalStops - 1;
            }
        }

        public DeliveryRouteStop AddStop(PlaceInfoItem place)
        {
            if (place == null)
                throw new ArgumentNullException(nameof(place));

            var stop = new DeliveryRouteStop
            {
                Name = place.Name,
                StreetName = place.StreetName,
                StreetNumber = place.StreetNumber,
                StreetExtraDetails = DeliveryRouteStop.ExtractStreetExtraDetails(place.Street),
                PostalCode = place.PostalCode,
                City = place.City,
                Country = place.Country,
                Latitude = place.Latitude,
                Longitude = place.Longitude
            };

            return AddStop(stop);
        }
    }
}
