using System.IO;
using System.Text.Json;
using Plugin.Maui.OCR;
using PmSTools.Models;

namespace PmSTools;

public partial class RouteEditorPage : ContentPage
{
    private bool _pendingImportFromLastPlace;
    private bool _subscriptionsAttached;
    private readonly System.Collections.ObjectModel.ObservableCollection<DeliveryRoute> _routes;

    public DeliveryRoute Route { get; }
    public System.Collections.ObjectModel.ObservableCollection<DeliveryRouteStop> Stops => Route.Stops;

    public RouteEditorPage(System.Collections.ObjectModel.ObservableCollection<DeliveryRoute> routes, DeliveryRoute route, int routeNumber, int selectedStopIndex = -1)
    {
        InitializeComponent();

        _routes = routes ?? throw new ArgumentNullException(nameof(routes));
        Route = route ?? throw new ArgumentNullException(nameof(route));
        Route.RenumberStops();

        BindingContext = this;
        AttachRouteSubscriptions();
        UpdateEmptyStopsVisibility();
        UpdateRouteMap();

        if (selectedStopIndex >= 0 && selectedStopIndex < Route.Stops.Count)
        {
            var selectedStopNumber = selectedStopIndex + 1;
            Title = BuildRouteTitle(routeNumber, Route.Name, selectedStopNumber);
        }
        else
        {
            Title = BuildRouteTitle(routeNumber, Route.Name);
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        AttachRouteSubscriptions();

        if (_pendingImportFromLastPlace)
        {
            _pendingImportFromLastPlace = false;
            TryAddLastScannedPlaceToRoute();
        }

        UpdateRouteMap();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        DetachRouteSubscriptions();
    }

    private void OnStopsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var oldItem in e.OldItems.OfType<DeliveryRouteStop>())
            {
                oldItem.PropertyChanged -= OnStopPropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (var newItem in e.NewItems.OfType<DeliveryRouteStop>())
            {
                newItem.PropertyChanged += OnStopPropertyChanged;
            }
        }

        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
        {
            foreach (var stop in Route.Stops)
            {
                stop.PropertyChanged -= OnStopPropertyChanged;
                stop.PropertyChanged += OnStopPropertyChanged;
            }
        }

        SaveLoadData.SaveDeliveryRoutes(_routes);
        UpdateEmptyStopsVisibility();
        UpdateRouteMap();
    }

    private void OnStopPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        SaveLoadData.SaveDeliveryRoutes(_routes);

        if (e.PropertyName == nameof(DeliveryRouteStop.StreetName) ||
            e.PropertyName == nameof(DeliveryRouteStop.StreetNumber) ||
            e.PropertyName == nameof(DeliveryRouteStop.PostalCode) ||
            e.PropertyName == nameof(DeliveryRouteStop.City) ||
            e.PropertyName == nameof(DeliveryRouteStop.Country))
        {
            UpdateRouteMap();
        }
    }

    private void AttachRouteSubscriptions()
    {
        if (_subscriptionsAttached)
            return;

        Route.Stops.CollectionChanged += OnStopsCollectionChanged;
        foreach (var stop in Route.Stops)
        {
            stop.PropertyChanged += OnStopPropertyChanged;
        }

        _subscriptionsAttached = true;
    }

    private void DetachRouteSubscriptions()
    {
        if (!_subscriptionsAttached)
            return;

        Route.Stops.CollectionChanged -= OnStopsCollectionChanged;
        foreach (var stop in Route.Stops)
        {
            stop.PropertyChanged -= OnStopPropertyChanged;
        }

        _subscriptionsAttached = false;
    }

    private void UpdateEmptyStopsVisibility()
    {
        EmptyStopsLabel.IsVisible = !Route.Stops.Any();
    }

    private void OnAddRouteStopClicked(object? sender, EventArgs e)
    {
        var stop = new DeliveryRouteStop
        {
            Name = ToNullIfWhiteSpace(ManualNameEntry.Text),
            StreetName = ToNullIfWhiteSpace(ManualStreetNameEntry.Text),
            StreetNumber = ToNullIfWhiteSpace(ManualStreetNumberEntry.Text),
            PostalCode = ToNullIfWhiteSpace(ManualPostalCodeEntry.Text),
            City = ToNullIfWhiteSpace(ManualCityEntry.Text),
            Country = ToNullIfWhiteSpace(ManualCountryEntry.Text)
        };

        Route.AddStop(stop);
        ClearManualEntryFields();
    }

    private void OnStopCardTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not DeliveryRouteStop stop)
            return;

        stop.IsExpanded = !stop.IsExpanded;
    }

    private void OnDeleteRouteStopClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: DeliveryRouteStop stop })
            return;

        Route.RemoveStop(stop);
    }

    private async void OnTakeAddressPhotoClicked(object? sender, EventArgs e)
    {
        try
        {
            await OcrPlugin.Default.InitAsync();
            var photoResult = await MediaPicker.Default.CapturePhotoAsync();

            if (photoResult == null)
                return;

            using var imageAsStream = await photoResult.OpenReadAsync();
            using var memoryStream = new MemoryStream();
            await imageAsStream.CopyToAsync(memoryStream);

            await NavigateToOcrResultAsync(memoryStream.ToArray());
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async void OnOpenAddressPictureClicked(object? sender, EventArgs e)
    {
        try
        {
            var fileResult = await FilePicker.Default.PickAsync(PickOptions.Images);
            if (fileResult == null)
                return;

            await OcrPlugin.Default.InitAsync();
            using var stream = await fileResult.OpenReadAsync();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);

            await NavigateToOcrResultAsync(memoryStream.ToArray());
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async Task NavigateToOcrResultAsync(byte[] imageBytes)
    {
        var ocrResult = await OcrPlugin.Default.RecognizeTextAsync(imageBytes);
        if (!ocrResult.Success)
        {
            await DisplayAlertAsync("No success", "No OCR possible", "OK");
            return;
        }

        _pendingImportFromLastPlace = true;
        await Navigation.PushAsync(new PlaceScanResultPage(ocrResult.AllText));
    }

    private void TryAddLastScannedPlaceToRoute()
    {
        if (!SaveLoadData.TryGetLastPlaceInfo(out var placeInfo) || placeInfo == null)
            return;

        Route.AddStop(placeInfo);
    }

    private void UpdateRouteMap()
    {
        RouteMapWebView.Source = new HtmlWebViewSource
        {
            Html = BuildRouteMapHtml()
        };
    }

    private string BuildRouteMapHtml()
    {
        var routeStops = Route.Stops
            .Select(stop => new
            {
                order = stop.Order,
                address = BuildStopAddress(stop),
                lat = stop.Latitude,
                lon = stop.Longitude
            })
            .Where(stop => !string.IsNullOrWhiteSpace(stop.address) || (stop.lat.HasValue && stop.lon.HasValue))
            .ToList();

        var stopsJson = JsonSerializer.Serialize(routeStops);

        return "<!DOCTYPE html>\n" +
               "<html>\n" +
               "<head>\n" +
               "    <meta charset='utf-8' />\n" +
               "    <meta name='viewport' content='width=device-width, initial-scale=1.0'>\n" +
               "    <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />\n" +
               "    <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></" + "script>\n" +
               "    <style>\n" +
               "        body { margin: 0; padding: 0; }\n" +
               "        #map { width: 100%; height: 100vh; }\n" +
               "    </style>\n" +
               "</head>\n" +
               "<body>\n" +
               "    <div id='map'></" + "div>\n" +
               "    <script>\n" +
               "        const map = L.map('map').setView([40, 0], 4);\n" +
               "        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {\n" +
               "            attribution: '© OpenStreetMap contributors',\n" +
               "            maxZoom: 19\n" +
               "        }).addTo(map);\n" +
               "        const stops = " + stopsJson + ";\n" +
               "        const points = [];\n" +
               "\n" +
               "        function buildPopup(stop) {\n" +
               "            const title = 'Stop #' + stop.order;\n" +
               "            if (stop.address && stop.address.trim().length > 0) {\n" +
               "                return title + '<br/>' + stop.address;\n" +
               "            }\n" +
               "            return title;\n" +
               "        }\n" +
               "\n" +
               "        async function geocodeStop(stop) {\n" +
               "            if (stop.lat != null && stop.lon != null) {\n" +
               "                const point = [stop.lat, stop.lon];\n" +
               "                points.push(point);\n" +
               "                L.marker(point).addTo(map).bindPopup(buildPopup(stop));\n" +
               "                return;\n" +
               "            }\n" +
               "\n" +
               "            if (!stop.address || stop.address.trim().length === 0) return;\n" +
               "            const url = 'https://nominatim.openstreetmap.org/search?format=json&limit=1&q=' + encodeURIComponent(stop.address);\n" +
               "            const response = await fetch(url);\n" +
               "            const data = await response.json();\n" +
               "            if (!data || data.length === 0) return;\n" +
               "            const lat = parseFloat(data[0].lat);\n" +
               "            const lon = parseFloat(data[0].lon);\n" +
               "            if (isNaN(lat) || isNaN(lon)) return;\n" +
               "\n" +
               "            const point = [lat, lon];\n" +
               "            points.push(point);\n" +
               "            L.marker(point).addTo(map).bindPopup(buildPopup(stop));\n" +
               "        }\n" +
               "\n" +
               "        Promise.all(stops.map(geocodeStop)).then(() => {\n" +
               "            if (points.length === 0) {\n" +
               "                map.setView([40, 0], 4);\n" +
               "                return;\n" +
               "            }\n" +
               "\n" +
               "            if (points.length > 1) {\n" +
               "                const routeLine = L.polyline(points, { color: 'blue', weight: 4 }).addTo(map);\n" +
               "                map.fitBounds(routeLine.getBounds(), { padding: [20, 20] });\n" +
               "                return;\n" +
               "            }\n" +
               "\n" +
               "            map.setView(points[0], 15);\n" +
               "        });\n" +
               "    </" + "script>\n" +
               "</body>\n" +
               "</html>";
    }

    private static string BuildStopAddress(DeliveryRouteStop stop)
    {
        return string.Join(", ", new[]
        {
            BuildStreet(stop),
            stop.PostalCode,
            stop.City,
            stop.Country
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string BuildStreet(DeliveryRouteStop stop)
    {
        return string.Join(" ", new[]
        {
            stop.StreetName,
            stop.StreetNumber
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string? ToNullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private void ClearManualEntryFields()
    {
        ManualNameEntry.Text = string.Empty;
        ManualStreetNameEntry.Text = string.Empty;
        ManualStreetNumberEntry.Text = string.Empty;
        ManualPostalCodeEntry.Text = string.Empty;
        ManualCityEntry.Text = string.Empty;
        ManualCountryEntry.Text = string.Empty;
    }

    private static string BuildRouteTitle(int routeNumber, string? routeName, int? stopNumber = null)
    {
        var baseTitle = string.IsNullOrWhiteSpace(routeName)
            ? $"Route {routeNumber}"
            : $"Route {routeNumber} - {routeName.Trim()}";

        return stopNumber.HasValue
            ? $"{baseTitle} - Stop {stopNumber.Value}"
            : baseTitle;
    }
}
