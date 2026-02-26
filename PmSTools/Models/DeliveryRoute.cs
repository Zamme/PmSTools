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
            routeStop.Order = Stops.Count + 1;
            Stops.Add(routeStop);
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
            for (var index = 0; index < Stops.Count; index++)
            {
                Stops[index].Order = index + 1;
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
