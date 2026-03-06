using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using Plugin.Maui.OCR;
using PmSTools.Models;
using PmSTools.Resources.Languages;

namespace PmSTools;

public partial class RouteEditorPage : ContentPage
{
    private const int RouteGeocodeLimit = 1;
    private const int RouteGeocodeTimeoutSeconds = 6;
    private const int RouteGeocodeRetryDelayMs = 900;
    private const int RouteGeocodeMaxRetries = 2;
    private bool _pendingImportFromLastPlace;
    private bool _subscriptionsAttached;
    private readonly System.Collections.ObjectModel.ObservableCollection<DeliveryRoute> _routes;
    private string _lastRouteMapHtml = string.Empty;
    private bool _isMapDirty;
    private int _mapLoadingCount;
    private bool _mapUpdatePending;
    private bool _isManualStopVisible;

    private static readonly HashSet<string> AddressProperties = new(StringComparer.Ordinal)
    {
        nameof(DeliveryRouteStop.Name),
        nameof(DeliveryRouteStop.StreetName),
        nameof(DeliveryRouteStop.StreetNumber),
        nameof(DeliveryRouteStop.PostalCode),
        nameof(DeliveryRouteStop.City),
        nameof(DeliveryRouteStop.Country)
    };


    public DeliveryRoute Route { get; }
    public System.Collections.ObjectModel.ObservableCollection<DeliveryRouteStop> Stops => Route.Stops;

    public bool IsMapDirty
    {
        get => _isMapDirty;
        private set
        {
            if (_isMapDirty == value)
                return;

            _isMapDirty = value;
            OnPropertyChanged();
        }
    }

    public bool IsManualStopVisible
    {
        get => _isManualStopVisible;
        private set
        {
            if (_isManualStopVisible == value)
                return;

            _isManualStopVisible = value;
            OnPropertyChanged();
        }
    }

    public RouteEditorPage(System.Collections.ObjectModel.ObservableCollection<DeliveryRoute> routes, DeliveryRoute route, int routeNumber, int selectedStopIndex = -1)
    {
        InitializeComponent();

        _routes = routes ?? throw new ArgumentNullException(nameof(routes));
        Route = route ?? throw new ArgumentNullException(nameof(route));
        Route.RenumberStops();

        BindingContext = this;
        AttachRouteSubscriptions();
        UpdateEmptyStopsVisibility();
        _ = UpdateRouteMapAsync();

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
        SetMapDirty();
    }

    private void OnStopPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        SaveLoadData.SaveDeliveryRoutes(_routes);

        if (sender is not DeliveryRouteStop stop)
            return;

        var propertyName = e.PropertyName;
        if (!string.IsNullOrWhiteSpace(propertyName) && AddressProperties.Contains(propertyName))
        {
            if (stop.Latitude.HasValue || stop.Longitude.HasValue)
            {
                stop.Latitude = null;
                stop.Longitude = null;
            }
        }

        if (!string.Equals(propertyName, nameof(DeliveryRouteStop.IsExpanded), StringComparison.Ordinal))
        {
            SetMapDirty();
        }
    }

    private void SetMapDirty()
    {
        IsMapDirty = true;
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
        if (!IsManualStopVisible)
        {
            IsManualStopVisible = true;
            return;
        }

        if (!HasManualInput())
        {
            IsManualStopVisible = false;
            return;
        }

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
        IsManualStopVisible = false;
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

    private async void OnRefreshRouteMapClicked(object? sender, EventArgs e)
    {
        await UpdateRouteMapAsync();
        IsMapDirty = false;
    }

    private async void OnOptimizeRouteClicked(object? sender, EventArgs e)
    {
        if (Route.Stops.Count < 2)
        {
            await DisplayAlertAsync(
                LangResources.OptimizeRouteTitleText,
                LangResources.OptimizeNeedTwoStopsText,
                LangResources.OkText);
            return;
        }

        BeginMapLoading();

        try
        {
            var routeStops = await BuildRouteMapStopsAsync();
            var geocodedStops = routeStops
                .Where(stop => stop.SourceStop != null && stop.Lat.HasValue && stop.Lon.HasValue)
                .ToList();
            var missingStops = routeStops
                .Where(stop => stop.SourceStop != null && (!stop.Lat.HasValue || !stop.Lon.HasValue))
                .OrderBy(stop => stop.Order)
                .ToList();

            if (geocodedStops.Count < 2)
            {
                await DisplayAlertAsync(
                    LangResources.OptimizeRouteTitleText,
                    LangResources.OptimizeNotEnoughStopsWithCoordsText,
                    LangResources.OkText);
                return;
            }

            var location = await GetCurrentLocationAsync();
            List<RouteMapStop> optimizedStops;
            if (location != null)
            {
                var nearest = geocodedStops
                    .OrderBy(stop => ComputeDistanceKm(location.Latitude, location.Longitude, stop.Lat!.Value, stop.Lon!.Value))
                    .FirstOrDefault();
                optimizedStops = nearest == null
                    ? BuildNearestNeighborRoute(geocodedStops)
                    : BuildNearestNeighborRouteFromStart(geocodedStops, nearest);
            }
            else
            {
                optimizedStops = BuildNearestNeighborRoute(geocodedStops);
            }
            var orderedStops = optimizedStops
                .Select(stop => stop.SourceStop!)
                .Concat(missingStops.Select(stop => stop.SourceStop!))
                .ToList();

            ApplyRouteStopOrder(orderedStops);

            await UpdateRouteMapAsync();
            IsMapDirty = false;

            if (missingStops.Count > 0)
            {
                await DisplayAlertAsync(
                    LangResources.OptimizeRouteTitleText,
                    LangResources.SomeStopsCouldNotBeGeocodedText,
                    LangResources.OkText);
            }
            else if (location == null)
            {
                await DisplayAlertAsync(
                    LangResources.OptimizeRouteTitleText,
                    LangResources.OptimizeNoLocationText,
                    LangResources.OkText);
            }
        }
        finally
        {
            EndMapLoading();
        }
    }

    private async void OnStartNearMeClicked(object? sender, EventArgs e)
    {
        if (Route.Stops.Count < 2)
        {
            await DisplayAlertAsync(
                LangResources.StartNearMeText,
                LangResources.StartNearMeNeedTwoStopsText,
                LangResources.OkText);
            return;
        }

        BeginMapLoading();

        try
        {
            var location = await GetCurrentLocationAsync();
            if (location == null)
            {
                await DisplayAlertAsync(
                    LangResources.StartNearMeText,
                    LangResources.StartNearMeNoLocationText,
                    LangResources.OkText);
                return;
            }

            var routeStops = await BuildRouteMapStopsAsync();
            var geocodedStops = routeStops
                .Where(stop => stop.SourceStop != null && stop.Lat.HasValue && stop.Lon.HasValue)
                .ToList();

            if (geocodedStops.Count == 0)
            {
                await DisplayAlertAsync(
                    LangResources.StartNearMeText,
                    LangResources.StartNearMeNoStopsWithCoordsText,
                    LangResources.OkText);
                return;
            }

            var nearest = geocodedStops
                .OrderBy(stop => ComputeDistanceKm(location.Latitude, location.Longitude, stop.Lat!.Value, stop.Lon!.Value))
                .FirstOrDefault();

            if (nearest?.SourceStop == null)
            {
                await DisplayAlertAsync(
                    LangResources.StartNearMeText,
                    LangResources.StartNearMeNearestStopErrorText,
                    LangResources.OkText);
                return;
            }

            var optimizedStops = BuildNearestNeighborRouteFromStart(geocodedStops, nearest);
            var missingStops = routeStops
                .Where(stop => stop.SourceStop != null && (!stop.Lat.HasValue || !stop.Lon.HasValue))
                .OrderBy(stop => stop.Order)
                .ToList();

            var orderedStops = optimizedStops
                .Select(stop => stop.SourceStop!)
                .Concat(missingStops.Select(stop => stop.SourceStop!))
                .ToList();

            ApplyRouteStopOrder(orderedStops);

            await UpdateRouteMapAsync();
            IsMapDirty = false;

            if (missingStops.Count > 0)
            {
                await DisplayAlertAsync(
                    LangResources.StartNearMeText,
                    LangResources.SomeStopsCouldNotBeGeocodedText,
                    LangResources.OkText);
            }
        }
        finally
        {
            EndMapLoading();
        }
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
            await DisplayAlertAsync(LangResources.ErrorTitleText, ex.Message, LangResources.OkText);
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
            await DisplayAlertAsync(LangResources.ErrorTitleText, ex.Message, LangResources.OkText);
        }
    }

    private async Task NavigateToOcrResultAsync(byte[] imageBytes)
    {
        var ocrResult = await OcrPlugin.Default.RecognizeTextAsync(imageBytes);
        if (!ocrResult.Success)
        {
            await DisplayAlertAsync(LangResources.NoSuccessTitleText, LangResources.NoOcrPossibleText, LangResources.OkText);
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

    private async Task UpdateRouteMapAsync()
    {
        BeginMapLoading();
        _mapUpdatePending = true;
        var sourceSet = false;

        try
        {
            var routeStops = await BuildRouteMapStopsAsync();
            _lastRouteMapHtml = BuildRouteMapHtml(routeStops, interactive: false);

            Dispatcher.Dispatch(() =>
            {
                RouteMapWebView.Source = new HtmlWebViewSource
                {
                    Html = _lastRouteMapHtml
                };
            });
            sourceSet = true;
        }
        finally
        {
            if (!sourceSet)
            {
                _mapUpdatePending = false;
                EndMapLoading();
            }
        }
    }

    private void OnRouteMapNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (!_mapUpdatePending)
            return;

        _mapUpdatePending = false;
        EndMapLoading();
    }

    private void BeginMapLoading()
    {
        _mapLoadingCount++;
        if (_mapLoadingCount != 1)
            return;

        Dispatcher.Dispatch(() => { RouteMapLoadingOverlay.IsVisible = true; });
    }

    private void EndMapLoading()
    {
        if (_mapLoadingCount == 0)
            return;

        _mapLoadingCount--;
        if (_mapLoadingCount != 0)
            return;

        Dispatcher.Dispatch(() => { RouteMapLoadingOverlay.IsVisible = false; });
    }

    private async Task<List<RouteMapStop>> BuildRouteMapStopsAsync()
    {
        var routeStops = Route.Stops
            .Select((stop, index) => new RouteMapStop
            {
                SourceStop = stop,
                Order = index + 1,
                Address = BuildStopAddress(stop),
                Lat = stop.Latitude,
                Lon = stop.Longitude
            })
            .ToList();

        var needsGeocode = routeStops
            .Where(stop => (!stop.Lat.HasValue || !stop.Lon.HasValue) && !string.IsNullOrWhiteSpace(stop.Address))
            .ToList();

        if (needsGeocode.Count == 0)
            return routeStops;

        using var http = new System.Net.Http.HttpClient();
        http.Timeout = TimeSpan.FromSeconds(RouteGeocodeTimeoutSeconds);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("PmSTools/1.0 (+https://github.com/pmstools)");
        http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ca,es;q=0.9,en;q=0.6");

        var updatedAny = false;
        foreach (var stop in needsGeocode)
        {
            var geocodeQueries = BuildGeocodeQueriesForStop(stop.SourceStop ?? Route.Stops.FirstOrDefault(s => s.Order == stop.Order));
            var (lat, lon) = await TryGeocodeAddressAsync(geocodeQueries, http);
            stop.Lat = lat;
            stop.Lon = lon;

            if (lat.HasValue && lon.HasValue && stop.SourceStop != null)
            {
                stop.SourceStop.Latitude = lat;
                stop.SourceStop.Longitude = lon;
                updatedAny = true;
            }

            await Task.Delay(RouteGeocodeRetryDelayMs);
        }

        if (updatedAny)
            SaveLoadData.SaveDeliveryRoutes(_routes);

        return routeStops;
    }

    private void ApplyRouteStopOrder(IReadOnlyList<DeliveryRouteStop> orderedStops)
    {
        if (orderedStops.Count != Route.Stops.Count)
            return;

        DetachRouteSubscriptions();
        Route.Stops.Clear();
        foreach (var stop in orderedStops)
        {
            Route.Stops.Add(stop);
        }
        Route.RenumberStops();
        AttachRouteSubscriptions();
        SaveLoadData.SaveDeliveryRoutes(_routes);
        UpdateEmptyStopsVisibility();
        SetMapDirty();
    }

    private static List<RouteMapStop> BuildNearestNeighborRoute(List<RouteMapStop> stops)
    {
        if (stops.Count <= 1)
            return stops;

        var distance = BuildDistanceMatrix(stops);
        var bestOrder = new List<int>();
        var bestLength = double.MaxValue;

        for (var start = 0; start < stops.Count; start++)
        {
            var unvisited = new HashSet<int>(Enumerable.Range(0, stops.Count));
            var order = new List<int>(stops.Count);
            var current = start;
            var total = 0.0;

            order.Add(current);
            unvisited.Remove(current);

            while (unvisited.Count > 0)
            {
                var next = -1;
                var nextDistance = double.MaxValue;

                foreach (var candidate in unvisited)
                {
                    var d = distance[current, candidate];
                    if (d < nextDistance)
                    {
                        nextDistance = d;
                        next = candidate;
                    }
                }

                if (next == -1)
                    break;

                total += nextDistance;
                current = next;
                order.Add(current);
                unvisited.Remove(current);
            }

            if (order.Count == stops.Count && total < bestLength)
            {
                bestLength = total;
                bestOrder = order;
            }
        }

        if (bestOrder.Count == 0)
            return stops;

        return bestOrder.Select(index => stops[index]).ToList();
    }

    private static List<RouteMapStop> BuildNearestNeighborRouteFromStart(List<RouteMapStop> stops, RouteMapStop start)
    {
        if (stops.Count <= 1)
            return stops;

        var startIndex = stops.IndexOf(start);
        if (startIndex < 0)
            return BuildNearestNeighborRoute(stops);

        var distance = BuildDistanceMatrix(stops);
        var unvisited = new HashSet<int>(Enumerable.Range(0, stops.Count));
        var order = new List<int>(stops.Count);
        var current = startIndex;

        order.Add(current);
        unvisited.Remove(current);

        while (unvisited.Count > 0)
        {
            var next = -1;
            var nextDistance = double.MaxValue;

            foreach (var candidate in unvisited)
            {
                var d = distance[current, candidate];
                if (d < nextDistance)
                {
                    nextDistance = d;
                    next = candidate;
                }
            }

            if (next == -1)
                break;

            current = next;
            order.Add(current);
            unvisited.Remove(current);
        }

        if (order.Count != stops.Count)
            return BuildNearestNeighborRoute(stops);

        return order.Select(index => stops[index]).ToList();
    }

    private static double[,] BuildDistanceMatrix(IReadOnlyList<RouteMapStop> stops)
    {
        var count = stops.Count;
        var matrix = new double[count, count];

        for (var i = 0; i < count; i++)
        {
            var lat1 = stops[i].Lat ?? 0;
            var lon1 = stops[i].Lon ?? 0;
            for (var j = i + 1; j < count; j++)
            {
                var lat2 = stops[j].Lat ?? 0;
                var lon2 = stops[j].Lon ?? 0;
                var d = ComputeDistanceKm(lat1, lon1, lat2, lon2);
                matrix[i, j] = d;
                matrix[j, i] = d;
            }
        }

        return matrix;
    }

    private static double ComputeDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double radiusKm = 6371.0;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Asin(Math.Min(1, Math.Sqrt(a)));
        return radiusKm * c;
    }

    private static double ToRadians(double degrees)
    {
        return degrees * (Math.PI / 180.0);
    }

    private static async Task<Location?> GetCurrentLocationAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (status != PermissionStatus.Granted)
                return null;

            var last = await Geolocation.GetLastKnownLocationAsync();
            if (last != null)
                return last;

            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
            return await Geolocation.GetLocationAsync(request);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<(double? Lat, double? Lon)> TryGeocodeAddressAsync(IEnumerable<string> queries, System.Net.Http.HttpClient http)
    {
        if (queries == null)
            return (null, null);

        try
        {
            foreach (var query in queries.Where(q => !string.IsNullOrWhiteSpace(q)))
            {
                var url = "https://nominatim.openstreetmap.org/search?format=json&limit=" +
                          RouteGeocodeLimit.ToString(CultureInfo.InvariantCulture) +
                          "&q=" + Uri.EscapeDataString(query);

                for (var attempt = 0; attempt <= RouteGeocodeMaxRetries; attempt++)
                {
                    using var response = await http.GetAsync(url);
                    if ((int)response.StatusCode == 429 || (int)response.StatusCode == 503)
                    {
                        await Task.Delay(RouteGeocodeRetryDelayMs);
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                        break;

                    var json = await response.Content.ReadAsStringAsync();
                    if (string.IsNullOrWhiteSpace(json))
                        break;

                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                        break;

                    var first = doc.RootElement[0];
                    if (!first.TryGetProperty("lat", out var latProp) || !first.TryGetProperty("lon", out var lonProp))
                        break;

                    var latText = latProp.GetString();
                    var lonText = lonProp.GetString();

                    if (double.TryParse(latText, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) &&
                        double.TryParse(lonText, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
                    {
                        return (lat, lon);
                    }
                }
            }
        }
        catch
        {
        }

        return (null, null);
    }

    private string BuildRouteMapHtml(List<RouteMapStop> routeStops, bool interactive)
    {
        var stopsJson = JsonSerializer.Serialize(routeStops, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var mapOptions = interactive
            ? "{ zoomControl: true }"
            : "{ attributionControl: false, zoomControl: false, dragging: false, scrollWheelZoom: false, doubleClickZoom: false, touchZoom: false, boxZoom: false, keyboard: false }";

        var attribution = interactive ? "© OpenStreetMap contributors" : string.Empty;
        var stopLabelPrefix = LangResources.StopLabelPrefixText.Replace("'", "\\'");

         return "<!DOCTYPE html>\n" +
             "<html>\n" +
             "<head>\n" +
             "    <meta charset='utf-8' />\n" +
             "    <meta name='viewport' content='width=device-width, initial-scale=1.0'>\n" +
             "    <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />\n" +
             "    <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></" + "script>\n" +
             "    <style>\n" +
             "        html, body { margin: 0; padding: 0; height: 100%; }\n" +
             "        body { font-family: -apple-system, Roboto, 'Segoe UI', Arial; }\n" +
             "        #map { position: absolute; top: 0; bottom: 0; width: 100%; }\n" +
             "    </style>\n" +
             "</head>\n" +
             "<body>\n" +
             "    <div id='map'></" + "div>\n" +
             "    <script>\n" +
             "        var map = L.map('map', " + mapOptions + ").setView([40, 0], 4);\n" +
             "        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {\n" +
             "            attribution: '" + attribution + "',\n" +
             "            maxZoom: 19\n" +
             "        }).addTo(map);\n" +
             "        var stops = " + stopsJson + ";\n" +
             "        var points = [];\n" +
             "        var segmentColors = ['#1e88e5', '#43a047', '#f4511e', '#8e24aa', '#6d4c41', '#00897b'];\n" +
             "\n" +
             "        function buildPopup(stop) {\n" +
             "            const title = '" + stopLabelPrefix + "' + stop.order;\n" +
             "            if (stop.address && stop.address.trim().length > 0) {\n" +
             "                return title + '<br/>' + stop.address;\n" +
             "            }\n" +
             "            return title;\n" +
             "        }\n" +
             "\n" +
             "        var sortedStops = stops\n" +
             "            .filter(stop => stop.lat != null && stop.lon != null)\n" +
             "            .sort((a, b) => a.order - b.order);\n" +
             "\n" +
             "        sortedStops.forEach(stop => {\n" +
             "            const point = [stop.lat, stop.lon];\n" +
             "            points.push(point);\n" +
             "            L.marker(point).addTo(map).bindPopup(buildPopup(stop));\n" +
             "        });\n" +
             "\n" +
             "        function drawFallbackPolyline() {\n" +
             "            if (points.length > 1) {\n" +
             "                for (let i = 0; i < points.length - 1; i++) {\n" +
             "                    const segment = [points[i], points[i + 1]];\n" +
             "                    L.polyline(segment, { color: segmentColors[i % segmentColors.length], weight: 4 }).addTo(map);\n" +
             "                }\n" +
             "            }\n" +
             "        }\n" +
             "\n" +
             "        function findNearestRouteIndex(routePoints, stopPoint, startIndex) {\n" +
             "            let bestIndex = -1;\n" +
             "            let bestDist = Number.POSITIVE_INFINITY;\n" +
             "            for (let i = startIndex; i < routePoints.length; i++) {\n" +
             "                const rp = routePoints[i];\n" +
             "                const dx = rp[0] - stopPoint[0];\n" +
             "                const dy = rp[1] - stopPoint[1];\n" +
             "                const dist = dx * dx + dy * dy;\n" +
             "                if (dist < bestDist) {\n" +
             "                    bestDist = dist;\n" +
             "                    bestIndex = i;\n" +
             "                }\n" +
             "            }\n" +
             "            return bestIndex;\n" +
             "        }\n" +
             "\n" +
             "        function drawSegmentedRoute(routePoints, stopPoints) {\n" +
             "            if (routePoints.length < 2 || stopPoints.length < 2) return false;\n" +
             "            const indices = [];\n" +
             "            let lastIndex = 0;\n" +
             "            for (let i = 0; i < stopPoints.length; i++) {\n" +
             "                const nearest = findNearestRouteIndex(routePoints, stopPoints[i], lastIndex);\n" +
             "                if (nearest === -1) return false;\n" +
             "                indices.push(nearest);\n" +
             "                lastIndex = nearest;\n" +
             "            }\n" +
             "            for (let i = 0; i < indices.length - 1; i++) {\n" +
             "                const start = indices[i];\n" +
             "                const end = indices[i + 1];\n" +
             "                if (end <= start) return false;\n" +
             "                const segment = routePoints.slice(start, end + 1);\n" +
             "                if (segment.length > 1) {\n" +
             "                    L.polyline(segment, { color: segmentColors[i % segmentColors.length], weight: 4 }).addTo(map);\n" +
             "                }\n" +
             "            }\n" +
             "            return true;\n" +
             "        }\n" +
             "\n" +
             "        function fitMap() {\n" +
             "            if (points.length === 0) {\n" +
             "                map.setView([40, 0], 4);\n" +
             "                return;\n" +
             "            }\n" +
             "            if (points.length === 1) {\n" +
             "                map.setView(points[0], 16);\n" +
             "                return;\n" +
             "            }\n" +
             "            map.fitBounds(L.latLngBounds(points), { padding: [20, 20] });\n" +
             "        }\n" +
             "\n" +
             "        function buildOsrmUrl() {\n" +
             "            if (points.length < 2) return null;\n" +
             "            const coords = points.map(p => p[1] + ',' + p[0]).join(';');\n" +
             "            return 'https://router.project-osrm.org/route/v1/driving/' + coords + '?overview=full&geometries=geojson';\n" +
             "        }\n" +
             "\n" +
             "        (function(){\n" +
             "            if (points.length === 0) {\n" +
             "                fitMap();\n" +
             "                return;\n" +
             "            }\n" +
             "\n" +
             "            var osrmUrl = buildOsrmUrl();\n" +
             "            if (!osrmUrl) {\n" +
             "                fitMap();\n" +
             "                return;\n" +
             "            }\n" +
             "\n" +
             "            fetch(osrmUrl)\n" +
             "                .then(response => response.ok ? response.json() : null)\n" +
             "                .then(data => {\n" +
             "                    if (!data || !data.routes || !data.routes.length) {\n" +
             "                        drawFallbackPolyline();\n" +
             "                        fitMap();\n" +
             "                        return;\n" +
             "                    }\n" +
             "                    const coords = data.routes[0].geometry.coordinates;\n" +
             "                    const routePoints = coords.map(c => [c[1], c[0]]);\n" +
             "                    if (!drawSegmentedRoute(routePoints, points)) {\n" +
             "                        drawFallbackPolyline();\n" +
             "                    }\n" +
             "                    map.fitBounds(L.latLngBounds(routePoints), { padding: [20, 20] });\n" +
             "                })\n" +
             "                .catch(() => {\n" +
             "                    drawFallbackPolyline();\n" +
             "                    fitMap();\n" +
             "                });\n" +
             "        })();\n" +
             "    </" + "script>\n" +
             "</body>\n" +
             "</html>";
    }

    private sealed class RouteMapStop
    {
        [JsonIgnore]
        public DeliveryRouteStop? SourceStop { get; set; }
        public int Order { get; set; }
        public string Address { get; set; } = string.Empty;
        public double? Lat { get; set; }
        public double? Lon { get; set; }
    }


    private static string BuildStopAddress(DeliveryRouteStop stop)
    {
        var parts = new List<string>();

        var street = BuildStreet(stop);
        if (!string.IsNullOrWhiteSpace(street))
            parts.Add(street);

        if (string.IsNullOrWhiteSpace(street) && !string.IsNullOrWhiteSpace(stop.Name))
            parts.Add(stop.Name);

        if (!string.IsNullOrWhiteSpace(stop.PostalCode))
            parts.Add(stop.PostalCode);

        if (!string.IsNullOrWhiteSpace(stop.City))
            parts.Add(stop.City);

        if (!string.IsNullOrWhiteSpace(stop.Country))
            parts.Add(stop.Country);

        return string.Join(", ", parts.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static List<string> BuildGeocodeQueriesForStop(DeliveryRouteStop? stop)
    {
        if (stop == null)
            return new List<string>();

        var queries = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var postalCode = (stop.PostalCode ?? string.Empty).Trim();
        var city = (stop.City ?? string.Empty).Trim();
        var country = (stop.Country ?? string.Empty).Trim();
        var street = BuildStreet(stop).Trim();
        var name = (stop.Name ?? string.Empty).Trim();

        street = System.Text.RegularExpressions.Regex.Replace(
            street,
            @"^\s*CL(?:\.|\b)\s*",
            "Carrer ",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
        street = System.Text.RegularExpressions.Regex.Replace(
            street,
            @"^\s*CR(?:\.|\b)\s*",
            "Carrer ",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();

        var streetMain = SimplifyStreetForGeocoding(street);
        var streetVariants = BuildStreetVariants(streetMain);

        var postalCity = string.Join(" ", new[] { postalCode, city }.Where(v => !string.IsNullOrWhiteSpace(v))).Trim();
        var postalOnly = postalCode;

        void AddQuery(params string[] parts)
        {
            var query = string.Join(", ", parts.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()));
            if (string.IsNullOrWhiteSpace(query))
                return;
            if (seen.Add(query))
                queries.Add(query);
        }

        foreach (var streetVariant in streetVariants)
        {
            AddQuery(streetVariant, postalOnly, country);
            AddQuery(streetVariant, postalCity, country);
            AddQuery(postalCity, streetVariant, country);
            AddQuery(streetVariant, city, country);
        }

        AddQuery(street, postalCity, country);
        AddQuery(street, postalOnly, country);
        AddQuery(postalOnly, country);
        AddQuery(postalCity, country);
        AddQuery(city, country);
        AddQuery(name, city, country);

        return queries;
    }

    private static string BuildStreet(DeliveryRouteStop stop)
    {
        return string.Join(" ", new[]
        {
            stop.StreetName,
            stop.StreetNumber
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string SimplifyStreetForGeocoding(string street)
    {
        if (string.IsNullOrWhiteSpace(street))
            return string.Empty;

        var cleaned = street.Trim();

        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"^\s*CL(?:\.|\b)\s*",
            "Carrer ",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();

        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"^\s*CR(?:\.|\b)\s*",
            "Carrer ",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();

        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"\b(?:n\.?|nº|no\.?|num\.?)\s*(\d{1,5}[A-Za-z]?)\b",
            "$1",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"\b(?:n\.?|nº|no\.?|num\.?)\b",
            " ",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var commaSplit = cleaned.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .Where(part => !string.IsNullOrEmpty(part))
            .ToList();

        if (commaSplit.Count > 0)
        {
            cleaned = commaSplit[0];

            if (commaSplit.Count > 1)
            {
                var numberMatch = System.Text.RegularExpressions.Regex.Match(
                    commaSplit[1],
                    @"^(\d{1,5}[A-Za-z]?)\b",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (numberMatch.Success)
                    cleaned = $"{cleaned} {numberMatch.Groups[1].Value}".Trim();
            }
        }

        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"\b(?:PLANTA|PISO|PUERTA|PORTAL|ESC\.?|BLOQUE|BQ\.?|LOCAL)\b.*$",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();

        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"\b([A-ZÁÉÍÓÚÑ])\.\s*([A-ZÁÉÍÓÚÑ]{2,})\b",
            "$2",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        cleaned = cleaned.Replace(".", " ");

        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\b\p{L}\b", " ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"^(?:Calle|C\.|C/|C\b|CL(?:\.|\b)|Avenida|Av\.|Avda\.?|Avgda\.?|Plaza|Pza\.|Paseo|Ps\.|Passeig|Pg\.?|Passatge|PTGE(?:\.|\b)|PTG(?:\.|\b)|Carrera|Cr(?:\.|/|\b)|Carretera|Ctra\.?|Camino|Cam\.?|Traves[ií]a|Trav\.?|Travessera|Carrer|CARRE(?:\.|\b)|Carr\.|Avinguda|Ronda|Rda\.?|Rambla|Pla[cç]a|Pol[íi]gono|Pol\.?|Urbanizaci[oó]n|Urb\.?|Via|R[uú]a)\b\s*",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"^(?:de|del|de la|de les|de los|de l'|d')\b\s*",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"^(.*?\b\d{1,5}[A-Za-z]?)(?:\s+\d{1,5}[A-Za-z]?){1,4}\s*$",
            "$1",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"^(.*?\b\d{1,5}[A-Za-z]?)(?:\s*[-–—]\s*[0-9A-Za-z]{1,5})+(?:\s+[0-9A-Za-z]{1,5})*\s*$",
            "$1",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"^(.*?\b\d{1,5}[A-Za-z]?)(?:\s+[\-–—]\s*[0-9A-Za-z]{1,5}){1,4}\s*$",
            "$1",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"^(.*?\b\d{1,5}[A-Za-z]?)(?:\s*/\s*[0-9A-Za-z]{1,5}){1,4}\s*$",
            "$1",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"^(.*?\b\d{1,5}[A-Za-z]?)(?:\s+[0-9A-Za-z]{1,4}[-/][0-9A-Za-z]{1,4})(?:\s+[0-9A-Za-z]{1,4}(?:[-/][0-9A-Za-z]{1,4})?)*\s*$",
            "$1",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"\b0+(\d{1,5}[A-Za-z]?)\b",
            "$1",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ").Trim();

        return cleaned;
    }

    private static (string StreetName, string StreetNumber) SplitStreetParts(string? street)
    {
        if (string.IsNullOrWhiteSpace(street))
            return (string.Empty, string.Empty);

        var normalized = System.Text.RegularExpressions.Regex.Replace(street.Trim(), @"\s+", " ");
        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            @"\b([\p{L}'\-]{2,})(\d{1,5}[A-Za-z]?)\b",
            "$1 $2",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var match = System.Text.RegularExpressions.Regex.Match(
            normalized,
            @"^(?<name>.+?)\s+(?:(?:n\.?|nº|no\.?|num\.?)\s*)?(?<number>\d{1,5}[A-Za-z]?)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (match.Success)
        {
            return (match.Groups["name"].Value.Trim(), match.Groups["number"].Value.Trim());
        }

        return (normalized, string.Empty);
    }

    private static List<string> BuildStreetVariants(string street)
    {
        var variants = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            var normalized = System.Text.RegularExpressions.Regex.Replace(value, @"\s+", " ").Trim();
            if (normalized.Length < 3)
                return;

            if (seen.Add(normalized))
                variants.Add(normalized);
        }

        var (parsedStreetName, parsedStreetNumber) = SplitStreetParts(street);
        if (!string.IsNullOrWhiteSpace(parsedStreetName))
        {
            var parsedMainStreet = string.IsNullOrWhiteSpace(parsedStreetNumber)
                ? parsedStreetName
                : $"{parsedStreetName} {parsedStreetNumber}";

            Add(parsedMainStreet);
            Add(RemoveStreetConnectors(parsedMainStreet));
            Add(parsedStreetName);
            Add(RemoveStreetConnectors(parsedStreetName));
        }

        var collapsedFloorToken = System.Text.RegularExpressions.Regex.Replace(
            street,
            @"(\s+)(\d)(\d{2})(\s*)$",
            "$1$2$4",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();

        bool canUseCollapsedFloorToken = false;
        var parsedNumberDigits = System.Text.RegularExpressions.Regex.Match(parsedStreetNumber ?? string.Empty, @"^\d+").Value;
        if (int.TryParse(parsedNumberDigits, out int parsedNumberValue) && parsedNumberValue > 0 && parsedNumberValue < 100)
            canUseCollapsedFloorToken = true;

        if (canUseCollapsedFloorToken && !string.Equals(collapsedFloorToken, street, StringComparison.OrdinalIgnoreCase))
        {
            Add(collapsedFloorToken);
            Add(RemoveStreetConnectors(collapsedFloorToken));
        }

        foreach (var typoVariant in BuildStreetTypoVariants(street))
        {
            Add(typoVariant);
            Add(RemoveStreetConnectors(typoVariant));
        }

        Add(street);

        var withoutInitials = RemoveStreetInitials(street);
        Add(withoutInitials);
        Add(RemoveStreetConnectors(withoutInitials));

        var noConnectors = RemoveStreetConnectors(street);
        Add(noConnectors);

        var noTrailingNumber = RemoveTrailingStreetNumber(street);
        Add(noTrailingNumber);
        Add(RemoveStreetConnectors(noTrailingNumber));

        return variants;
    }

    private static List<string> BuildStreetTypoVariants(string street)
    {
        var variants = new List<string>();

        if (string.IsNullOrWhiteSpace(street))
            return variants;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tokens = street.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToArray();

        void AddVariantFromToken(int tokenIndex, string newToken)
        {
            if (tokenIndex < 0 || tokenIndex >= tokens.Length)
                return;

            if (string.IsNullOrWhiteSpace(newToken) || string.Equals(tokens[tokenIndex], newToken, StringComparison.OrdinalIgnoreCase))
                return;

            var copy = (string[])tokens.Clone();
            copy[tokenIndex] = newToken;
            var variant = string.Join(" ", copy).Trim();
            if (!string.IsNullOrWhiteSpace(variant) && seen.Add(variant))
                variants.Add(variant);
        }

        for (int i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (!System.Text.RegularExpressions.Regex.IsMatch(token, @"^[\p{L}'\-]{4,}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                continue;

            if (System.Text.RegularExpressions.Regex.IsMatch(token, @"bra$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                var fixedToken = System.Text.RegularExpressions.Regex.Replace(token, @"bra$", "bria", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                AddVariantFromToken(i, fixedToken);
            }

            if (token.Length >= 6 &&
                System.Text.RegularExpressions.Regex.IsMatch(token, @"[bcdfghjklmnñpqrstvwxyz]$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                AddVariantFromToken(i, token + "a");
            }
        }

        return variants;
    }

    private static string RemoveStreetConnectors(string street)
    {
        if (string.IsNullOrWhiteSpace(street))
            return string.Empty;

        var normalized = street;
        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            @"\b(?:de|del|dels|de\s+la|de\s+les|de\s+los|de\s+l')\b",
            " ",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ").Trim();
        return normalized;
    }

    private static string RemoveStreetInitials(string street)
    {
        if (string.IsNullOrWhiteSpace(street))
            return string.Empty;

        var normalized = street;

        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            @"\b\p{L}\.(?=\s|$)",
            " ",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            @"(?<=^|\s)\p{L}(?=\s|$)",
            " ",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ").Trim();
        return normalized;
    }

    private static string RemoveTrailingStreetNumber(string street)
    {
        if (string.IsNullOrWhiteSpace(street))
            return string.Empty;

        return System.Text.RegularExpressions.Regex.Replace(
            street,
            @"\s+\d{1,4}[A-Za-z]?\s*$",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
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

    private bool HasManualInput()
    {
        return !string.IsNullOrWhiteSpace(ManualNameEntry.Text)
            || !string.IsNullOrWhiteSpace(ManualStreetNameEntry.Text)
            || !string.IsNullOrWhiteSpace(ManualStreetNumberEntry.Text)
            || !string.IsNullOrWhiteSpace(ManualPostalCodeEntry.Text)
            || !string.IsNullOrWhiteSpace(ManualCityEntry.Text)
            || !string.IsNullOrWhiteSpace(ManualCountryEntry.Text);
    }

    private static string BuildRouteTitle(int routeNumber, string? routeName, int? stopNumber = null)
    {
        var baseTitle = string.IsNullOrWhiteSpace(routeName)
            ? string.Format(LangResources.RouteTitleFormatText, routeNumber)
            : string.Format(LangResources.RouteTitleWithNameFormatText, routeNumber, routeName.Trim());

        return stopNumber.HasValue
            ? string.Format(LangResources.RouteTitleWithStopFormatText, baseTitle, stopNumber.Value)
            : baseTitle;
    }

    private async Task OpenFullScreenRouteMapAsync()
    {
        var routeStops = await BuildRouteMapStopsAsync();
        var html = BuildRouteMapHtml(routeStops, interactive: true);
        await Navigation.PushModalAsync(new FullScreenMapPage(html));
    }


    private async void FullRouteMap_FloatingClicked(object? sender, EventArgs e)
    {
        await OpenFullScreenRouteMapAsync();
    }

    private async void FullRouteMap_ToolbarClicked(object? sender, EventArgs e)
    {
        await OpenFullScreenRouteMapAsync();
    }
}
