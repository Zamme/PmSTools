using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;
using System.Globalization;
using System.Collections.ObjectModel;
using PmSTools.Models;

namespace PmSTools;

public partial class PlaceScanResultPage : ContentPage
{
    // holds the parsed OCR/place data and (later) selected coordinates
    private PlaceInfoItem? _currentPlace;

    // candidates from Nominatim shown in native CollectionView
    private System.Collections.ObjectModel.ObservableCollection<GeocodeCandidate> _candidates = new System.Collections.ObjectModel.ObservableCollection<GeocodeCandidate>();

    // keep last generated map HTML so we can show the exact same map full-screen
    private string _lastMapHtml = string.Empty;

    public PlaceScanResultPage()
    {
        InitializeComponent();
        CandidatesList.ItemsSource = _candidates;
    }

    public PlaceScanResultPage(string ocrResult)
    {
        InitializeComponent();
        CandidatesList.ItemsSource = _candidates;
        FillPageWithMap(ocrResult);
    }

    public PlaceScanResultPage(PlaceInfoItem placeInfo)
    {
        InitializeComponent();
        CandidatesList.ItemsSource = _candidates;
        FillPageWithPlaceInfo(placeInfo);
    }

    private class GeocodeCandidate
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lon { get; set; }
    }
    private async void FillPageWithMap(string ocrResult)
    {
        try
        {
            PlaceInfoItem filteredLines = FilterScanResult(ocrResult);
            if (filteredLines == null)
                return;
            SaveLoadData.SaveLastPlaceInfo(filteredLines);
            // keep a reference so JS->C# can populate chosen coordinates
            _currentPlace = filteredLines; 
            
            // Set UI fields first - critical
            try
            {
                NameResultText.Text = filteredLines.Name ?? "Unknown Name";
                StreetResultText.Text = filteredLines.Street ?? "Unknown Street";
                PostalCodeResultText.Text = filteredLines.PostalCode ?? "Unknown Postal Code";
                CityResultText.Text = filteredLines.City ?? "Unknown City";
                CountryResultText.Text = filteredLines.Country ?? "Unknown Country";
            }
            catch (Exception)
            {
                // System.Diagnostics.Debug.WriteLine($"UI update error: {uiEx.Message}");
            }
            
            // Try to show map centered on city/street (wrapped in try-catch to prevent startup crash)
            try
            {
                // Request location permission before geocoding
                await RequestLocationPermission();
                await ShowMapForLocation(filteredLines);

                // populate the native candidates list (Nominatim)
                await PopulateCandidatesAsync(filteredLines);
            }
            catch (Exception)
            {
                // System.Diagnostics.Debug.WriteLine($"Map initialization error: {mapEx.Message}\n{mapEx.StackTrace}");
            }
        }
        catch (Exception)
        {
            // System.Diagnostics.Debug.WriteLine($"FillPageWithMap error: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private async void FillPageWithPlaceInfo(PlaceInfoItem placeInfo)
    {
        try
        {
            if (placeInfo == null)
            {
                return;
            }

            SaveLoadData.SaveLastPlaceInfo(placeInfo);
            _currentPlace = placeInfo;

            try
            {
                NameResultText.Text = placeInfo.Name ?? "Unknown Name";
                StreetResultText.Text = placeInfo.Street ?? "Unknown Street";
                PostalCodeResultText.Text = placeInfo.PostalCode ?? "Unknown Postal Code";
                CityResultText.Text = placeInfo.City ?? "Unknown City";
                CountryResultText.Text = placeInfo.Country ?? "Unknown Country";
            }
            catch (Exception)
            {
            }

            try
            {
                await RequestLocationPermission();
                await ShowMapForLocation(placeInfo);
                await PopulateCandidatesAsync(placeInfo);
            }
            catch (Exception)
            {
            }
        }
        catch (Exception)
        {
        }
    }

    private async Task RequestLocationPermission()
    {
        // Location permission not required for OpenStreetMap (no Google API)
        // System.Diagnostics.Debug.WriteLine("OpenStreetMap loaded - no location permission needed");
    }

    public class LocationWhenInUse : Permissions.BasePlatformPermission
    {
    }

    private async Task ShowMapForLocation(PlaceInfoItem filteredLines)
    {
        try
        {
            // Check if WebView control exists
            var osmMap = (WebView)this.FindByName("OsmMap");
            if (osmMap == null)
            {
                // System.Diagnostics.Debug.WriteLine("OsmMap control not found");
                return;
            }

            // Generate OpenStreetMap HTML with Leaflet
            string htmlContent = GenerateOpenStreetMapHtml(filteredLines);
            // store HTML so full-screen page can reuse the exact same map content
            _lastMapHtml = htmlContent;
            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    osmMap.Source = new HtmlWebViewSource { Html = htmlContent };
                    // inject extra on-map controls (zoom / fit) after the page loads
                    _ = InjectMapControlsAsync(osmMap);
                    // System.Diagnostics.Debug.WriteLine($"OpenStreetMap loaded for {filteredLines.Name ?? \"Place\"}");
                }
                catch (Exception)
                {
                    // System.Diagnostics.Debug.WriteLine($"Map update error: {ex.Message}\n{ex.StackTrace}");
                }
            });
        }
        catch (Exception)
        {
            // System.Diagnostics.Debug.WriteLine($"ShowMapForLocation error: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private string GenerateOpenStreetMapHtml(PlaceInfoItem placeInfo)
    {
        // Build address string for Nominatim geocoding
        string address = $"{placeInfo.Street}, {placeInfo.City}";
        if (!string.IsNullOrEmpty(placeInfo.Country))
        {
            address += $", {placeInfo.Country}";
        }

        // Escape address for JavaScript
        string escapedAddress = address.Replace("\\", "\\\\").Replace("\"", "\\\"");
        string escapedLabel = (placeInfo.Name ?? "Place").Replace("\\", "\\\\").Replace("\"", "\\\"");

        string html = "<!DOCTYPE html>\n" +
            "<html>\n" +
            "<head>\n" +
            "    <meta charset='utf-8' />\n" +
            "    <meta name='viewport' content='width=device-width, initial-scale=1.0'>\n" +
            "    <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />\n" +
            "    <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></" + "script>\n" +
            "    <style>\n" +
            "        body { margin: 0; padding: 0; font-family: -apple-system, Roboto, 'Segoe UI', Arial; }\n" +
            "        #map { position: absolute; top: 0; bottom: 0; width: 100%; }\n" +
            "        .no-results { position: absolute; left: 10px; top: 10px; background: rgba(255,255,255,0.95); padding: 8px; border-radius: 6px; box-shadow: 0 6px 20px rgba(0,0,0,0.25); z-index: 400; }\n" +
            "    </style>\n" +
            "</head>\n" +
            "<body>\n" +
            "    <div id='map'></" + "div>\n" +
            "    <div id='noResults' class='no-results' style='display:none;'>No location matches found</div>\n" +
            "    <script>\n" +
            "        var map = L.map('map').setView([40, 0], 4);\n" +
            "        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {\n" +
            "            attribution: '© OpenStreetMap contributors',\n" +
            "            maxZoom: 19\n" +
            "        }).addTo(map);\n" +
            "        // markers array (accessible to native code)\n" +
            "        var markers = [];\n" +
            "\n" +
            "        var address = \"" + escapedAddress + "\";\n" +
            "        var label = \"" + escapedLabel + "\";\n" +
            "\n" +
            "        // Request up to 5 candidates from Nominatim; native UI displays the list\n" +
            "        fetch('https://nominatim.openstreetmap.org/search?format=json&limit=5&q=' + encodeURIComponent(address))\n" +
            "            .then(response => response.json())\n" +
            "            .then(data => {\n" +
            "                if (!data || data.length === 0) { document.getElementById('noResults').style.display = 'block'; return; }\n" +
            "\n" +
            "                var bounds = L.latLngBounds();\n" +
            "\n" +
            "                data.forEach(function(item, index) {\n" +
            "                    var lat = parseFloat(item.lat); var lon = parseFloat(item.lon);\n" +
            "                    var title = item.display_name || (label + ' - ' + address);\n" +
            "\n" +
            "                    var marker = L.marker([lat, lon]).addTo(map)\n" +
            "                        .bindPopup('<div style=\"font-size:14px;\"><strong>' + (item.type || label) + '</strong><br/>' + title + '<br/><a href=\"app://selected?lat=' + lat + '&lon=' + lon + '&label=' + encodeURIComponent(item.display_name) + '\" style=\"color:#0078d4; text-decoration:none;\">Use this location</a></div>');\n" +
            "\n" +
            "                    markers.push({ marker: marker, item: item, index: index });\n" +
            "                    bounds.extend([lat, lon]);\n" +
            "\n" +
            "                    // keep marker popup behavior only (native list handles selection)\n" +
            "                    marker.on('click', function(){ marker.openPopup(); });\n" +
            "                });\n" +
            "\n" +
            "                if (!bounds.isValid()) { map.setView([parseFloat(data[0].lat), parseFloat(data[0].lon)], 16); } else { map.fitBounds(bounds, { padding: [50, 50] }); }\n" +
            "\n" +
            "                // open first marker popup by default\n" +
            "                if (markers.length > 0) { markers[0].marker.openPopup(); }\n" +
            "            })\n" +
            "            .catch(error => { document.getElementById('noResults').style.display = 'block'; });\n" +
            "    </script>\n" +
            "</body>\n" +
            "</html>";

        return html;
    }

    // Injects CSS + small on-map controls into the already-loaded Leaflet map inside the WebView.
    // Uses existing `map` and `markers` variables from the embedded HTML so no HTML-string edits are required.
    private async Task InjectMapControlsAsync(WebView web)
    {
        if (web == null) return;

        // small delay to give the WebView time to parse the HTML/Leaflet map
        await Task.Delay(250);
        try
        {
            string js = @"(function(){
                try{
                    var css = '.custom-zoom-control { background: rgba(255,255,255,0.92); border-radius: 8px; box-shadow: 0 4px 12px rgba(0,0,0,0.25); padding: 6px; display: flex; gap: 6px; } .custom-zoom-control button { background: transparent; border:none; width:36px; height:36px; border-radius:6px; font-size:18px; cursor:pointer; }';
                    var style = document.createElement('style'); style.type='text/css'; style.appendChild(document.createTextNode(css)); document.head.appendChild(style);
                }catch(e){}
                try{ if(typeof L !== 'undefined' && typeof map !== 'undefined'){ L.control.scale({ position: 'bottomleft' }).addTo(map); }
                }catch(e){}
                try{
                    var custom = L.control({ position: 'topright' });
                    custom.onAdd = function(){ var d = L.DomUtil.create('div','custom-zoom-control'); d.innerHTML = '<button id=\'zoomIn\'>+</button><button id=\'zoomOut\'>−</button><button id=\'fitBounds\'>⤢</button>'; return d; };
                    custom.addTo(map);
                    var el = document.getElementsByClassName('custom-zoom-control')[0]; if(el) L.DomEvent.disableClickPropagation(el);
                    document.addEventListener('click', function(ev){ var id = ev.target && ev.target.id; if(id==='zoomIn') map.zoomIn(); else if(id==='zoomOut') map.zoomOut(); else if(id==='fitBounds'){ try{ if(markers && markers.length>0){ var b = L.latLngBounds(); for(var i=0;i<markers.length;i++){ b.extend(markers[i].marker.getLatLng()); } map.fitBounds(b, {padding:[50,50]}); } }catch(e){} } });
                }catch(e){}
            })();";

            await web.EvaluateJavaScriptAsync(js);
        }
        catch { /* ignore injection errors */ }
    }

    private PlaceInfoItem FilterScanResult(string ocrResult)
    {
        var placeInfo = new PlaceInfoItem();
        
        var lines = ocrResult.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();

        // Extract name: skip very short lines (likely OCR noise) and find first substantial line
        // Look for lines with multiple words or reasonable length before hitting street/postal code
        if (lines.Count > 0)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                // Skip if too short (OCR noise), or if it's a street/postal code line
                if (line.Length > 3 && 
                    !System.Text.RegularExpressions.Regex.IsMatch(line, @"^(?:Calle|C\.|C\/|Avenida|Av\.|Plaza|Pza\.|Paseo|Ps\.|Carrera|Cr\.|Travesía|Trav\.|Carrer|Carr\.|Avinguda|Avda\.)", System.Text.RegularExpressions.RegexOptions.IgnoreCase) &&
                    !System.Text.RegularExpressions.Regex.IsMatch(line, @"^\d{4,5}"))
                {
                    placeInfo.Name = line;
                    System.Diagnostics.Debug.WriteLine($"FilterScanResult - Name: {placeInfo.Name}");
                    break;
                }
            }
            System.Diagnostics.Debug.WriteLine($"FilterScanResult - Total lines: {lines.Count}");
            for (int i = 0; i < lines.Count; i++)
            {
                System.Diagnostics.Debug.WriteLine($"  Line {i}: {lines[i]}");
            }
        }

        // Extract postal code and city with pattern matching (postal code followed by city name)
        // Matches: "28001 Madrid" or "08002 Barcelona", etc.
        var postalCityMatch = System.Text.RegularExpressions.Regex.Match(
            ocrResult, 
            @"(?:^|\n)\s*([0-9]{4,5})\s+([A-Z][a-záéíóúñüA-Z\s]{2,})",
            System.Text.RegularExpressions.RegexOptions.Multiline);
        
        if (postalCityMatch.Success)
        {
            placeInfo.PostalCode = postalCityMatch.Groups[1].Value.Trim();
            placeInfo.City = postalCityMatch.Groups[2].Value.Trim();
        }

        // Extract street address (patterns like "Calle/Avenida + name + optional number")
        // Supports Spanish (Calle, Avenida, Plaza, etc.) and Catalan (Carrer, Avinguda, etc.)
        // Matches: "Calle Mayor 123" or "Carrer Font, 4" or "C/ Font 4", etc.
        var streetMatch = System.Text.RegularExpressions.Regex.Match(
            ocrResult, 
            @"((?:Calle|C\.|C\/|Avenida|Av\.|Plaza|Pza\.|Paseo|Ps\.|Carrera|Cr\.|Travesía|Trav\.|Carrer|Carr\.|Avinguda|Avda\.)\s+.{4,}?(?:\s*[,]?\s*\d+[a-z]?)?)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);
        
        if (streetMatch.Success)
        {
            placeInfo.Street = streetMatch.Groups[1].Value.Trim();
        }
        else
        {
            // Fallback: look for lines between Name and PostalCode that might be street address
            if (lines.Count >= 2)
            {
                // Typically the street is the second line (after name, before postal code)
                for (int i = 1; i < lines.Count - 1 && i < 3; i++)
                {
                    var line = lines[i];
                    // Skip if looks like postal code or city
                    if (!System.Text.RegularExpressions.Regex.IsMatch(line, @"^\d{4,5}") && 
                        line.Length > 4 && 
                        !string.IsNullOrEmpty(placeInfo.PostalCode) && 
                        line != placeInfo.City)
                    {
                        placeInfo.Street = line;
                        break;
                    }
                }
            }
        }

        // Set default country to Spain if not found
        if (string.IsNullOrEmpty(placeInfo.Country))
            placeInfo.Country = "Spain";

        return placeInfo;
    }

    // Called from the WebView when user chooses a candidate (JS navigates to app://selected?...)
    private async void OsmMap_Navigating(object sender, WebNavigatingEventArgs e)
    {
        if (e == null)
        {
            return;
        }

        try
        {
            var url = e!.Url ?? string.Empty;
            if (!url.StartsWith("app://selected", StringComparison.OrdinalIgnoreCase))
                return;

            // prevent the WebView from actually navigating
            e.Cancel = true;

            // parse query string without System.Web dependency
            var uri = new Uri(url);
            double lat = 0, lon = 0;
            string label = string.Empty;
            var qs = (uri.Query ?? string.Empty).TrimStart('?');
            foreach (var part in qs.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split(new[] { '=' }, 2);
                if (kv.Length != 2) continue;
                var key = Uri.UnescapeDataString(kv[0]);
                var value = Uri.UnescapeDataString(kv[1]);
                if (key == "lat") double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out lat);
                else if (key == "lon") double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out lon);
                else if (key == "label") label = value;
            }

            // store into the model and update UI
            if (_currentPlace != null)
            {
                _currentPlace.Latitude = lat;
                _currentPlace.Longitude = lon;
            }

            LatitudeText.Text = lat != 0 ? lat.ToString("F6", CultureInfo.InvariantCulture) : "—";
            LongitudeText.Text = lon != 0 ? lon.ToString("F6", CultureInfo.InvariantCulture) : "—";

            await DisplayAlertAsync("Location selected", $"{label}\nLat: {lat:F6}, Lon: {lon:F6}", "OK");
        }
        catch (Exception)
        {
            // ignore parsing errors but don't crash the WebView handler
        }
    }


    private async Task PopulateCandidatesAsync(PlaceInfoItem placeInfo)
    {
        try
        {
            if (placeInfo == null) return;
            string address = $"{placeInfo.Street}, {placeInfo.City}" + (string.IsNullOrEmpty(placeInfo.Country) ? string.Empty : $", {placeInfo.Country}");
            using var http = new HttpClient();
            // Nominatim requires a valid User-Agent — set one so the request is accepted
            try
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("PmSTools/1.0 (+https://github.com/pmstools)");
            }
            catch { }

            var url = "https://nominatim.openstreetmap.org/search?format=json&limit=5&q=" + Uri.EscapeDataString(address);
            using var response = await http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                // leave candidates empty — native UI will show nothing
                return;
            }

            var resp = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(resp);
            var root = doc.RootElement;

            MainThread.BeginInvokeOnMainThread(() => _candidates.Clear());

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    try
                    {
                        var display = item.TryGetProperty("display_name", out var dn) ? dn.GetString() ?? string.Empty : string.Empty;
                        var type = item.TryGetProperty("type", out var t) ? t.GetString() ?? string.Empty : string.Empty;

                        double lat = 0, lon = 0;
                        if (item.TryGetProperty("lat", out var latProp)) double.TryParse(latProp.GetString() ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out lat);
                        if (item.TryGetProperty("lon", out var lonProp)) double.TryParse(lonProp.GetString() ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out lon);

                        if (string.IsNullOrEmpty(display))
                            display = $"{placeInfo.Name ?? string.Empty} - {placeInfo.Street}, {placeInfo.City}".Trim();

                        var cand = new GeocodeCandidate { DisplayName = display, Type = type, Lat = lat, Lon = lon };
                        MainThread.BeginInvokeOnMainThread(() => _candidates.Add(cand));
                    }
                    catch { /* ignore malformed items */ }
                }

                // Select first candidate by default
                MainThread.BeginInvokeOnMainThread(() => {
                    if (_candidates.Count > 0)
                        CandidatesList.SelectedItem = _candidates[0];
                });
            }
        }
        catch (Exception)
        {
            // ignore network/parse errors — native list will remain empty
        }
    }

    private async Task ApplyCandidateSelectionAsync(GeocodeCandidate cand, bool showAlert = true)
    {
        if (cand == null) return;

        // update model and UI
        if (_currentPlace != null)
        {
            _currentPlace.Latitude = cand.Lat;
            _currentPlace.Longitude = cand.Lon;
        }

        LatitudeText.Text = cand.Lat.ToString("F6", CultureInfo.InvariantCulture);
        LongitudeText.Text = cand.Lon.ToString("F6", CultureInfo.InvariantCulture);

        // try to center map and open corresponding popup (if markers exist in JS)
        try
        {
            var web = (WebView)this.FindByName("OsmMap");
            if (web != null)
            {
                string js = $@"(function(){{
                    var lat = {cand.Lat.ToString(CultureInfo.InvariantCulture)};
                    var lon = {cand.Lon.ToString(CultureInfo.InvariantCulture)};
                    try{{
                        if(typeof markers !== 'undefined'){{
                            for(var i=0;i<markers.length;i++){{
                                var m = markers[i];
                                var p = m.marker.getLatLng();
                                if(Math.abs(p.lat - lat) < 0.00001 && Math.abs(p.lng - lon) < 0.00001){{ map.setView([lat,lon],18); m.marker.openPopup(); return true; }}
                            }}
                        }}
                        var mk = L.marker([lat, lon]).addTo(map).bindPopup('Selected location').openPopup();
                        map.setView([lat, lon], 18);
                        return true;
                    }}catch(e){{return false;}}
                }})();";

                await web.EvaluateJavaScriptAsync(js);
            }
        }
        catch { /* ignore JS errors */ }

        if (showAlert)
        {
            await DisplayAlertAsync("Location selected", $"{cand.DisplayName}\nLat: {cand.Lat:F6}, Lon: {cand.Lon:F6}", "OK");
        }
    }

    private async void CandidatesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var sel = CandidatesList.SelectedItem as GeocodeCandidate;
        if (sel != null)
        {
            // selecting a candidate now *applies* it and shows confirmation
            await ApplyCandidateSelectionAsync(sel, showAlert: true);
        }
    }

    private async Task OpenFullScreenMapAsync()
    {
        try
        {
            var html = !string.IsNullOrEmpty(_lastMapHtml) ? _lastMapHtml : string.Empty;
            await Navigation.PushModalAsync(new FullScreenMapPage(html));
        }
        catch { /* ignore navigation errors */ }
    }

    private async void FullScreenButton_Clicked(object sender, EventArgs e)
    {
        await OpenFullScreenMapAsync();
    }

    private async void FullScreenMap_ToolbarClicked(object sender, EventArgs e)
    {
        // same action as the floating button
        await OpenFullScreenMapAsync();
    }

    private void OnBackClicked(object sender, EventArgs e)
    {
        Navigation.PopAsync();
    }
}
