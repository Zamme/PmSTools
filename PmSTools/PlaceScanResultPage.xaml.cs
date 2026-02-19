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
    private const bool EnableGeocodeDebugPopup = true;
    private const double PhoneCandidatesMinHeight = 170;
    private const double PhoneCandidatesMaxHeight = 320;
    private const double TabletCandidatesMinHeight = 220;
    private const double TabletCandidatesMaxHeight = 460;

    // holds the parsed OCR/place data and (later) selected coordinates
    private PlaceInfoItem? _currentPlace;

    // candidates from Nominatim shown in native CollectionView
    private System.Collections.ObjectModel.ObservableCollection<GeocodeCandidate> _candidates = new System.Collections.ObjectModel.ObservableCollection<GeocodeCandidate>();

    // keep last generated map HTML so we can show the exact same map full-screen
    private string _lastMapHtml = string.Empty;

    private bool _isEditMode;

    public PlaceScanResultPage()
    {
        InitializeComponent();
        CandidatesList.ItemsSource = _candidates;
        SizeChanged += OnPageSizeChanged;
        UpdateCandidatesListHeight();
    }

    public PlaceScanResultPage(string ocrResult)
    {
        InitializeComponent();
        CandidatesList.ItemsSource = _candidates;
        SizeChanged += OnPageSizeChanged;
        UpdateCandidatesListHeight();
        FillPageWithMap(ocrResult);
    }

    public PlaceScanResultPage(PlaceInfoItem placeInfo)
    {
        InitializeComponent();
        CandidatesList.ItemsSource = _candidates;
        SizeChanged += OnPageSizeChanged;
        UpdateCandidatesListHeight();
        FillPageWithPlaceInfo(placeInfo);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateCandidatesListHeight();
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        UpdateCandidatesListHeight();
    }

    private void UpdateCandidatesListHeight()
    {
        if (CandidatesList == null)
            return;

        if (Height <= 0)
            return;

        var isTablet = DeviceInfo.Idiom == DeviceIdiom.Tablet || DeviceInfo.Idiom == DeviceIdiom.Desktop;
        var smallPhone = !isTablet && (Width > 0 && Width <= 400 || Height <= 760);
        var ratio = isTablet ? 0.30 : (smallPhone ? 0.27 : 0.24);
        var calculatedHeight = Height * ratio;

        var minHeight = isTablet ? TabletCandidatesMinHeight : PhoneCandidatesMinHeight;
        var maxHeight = isTablet ? TabletCandidatesMaxHeight : PhoneCandidatesMaxHeight;

        CandidatesList.HeightRequest = Math.Clamp(calculatedHeight, minHeight, maxHeight);
    }

    private class GeocodeCandidate
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lon { get; set; }
    }

    private void UpdateResultLabels(PlaceInfoItem placeInfo)
    {
        NameResultText.Text = placeInfo.Name ?? "Unknown Name";
        StreetNameResultText.Text = placeInfo.StreetName ?? "Unknown Street Name";
        StreetNumberResultText.Text = placeInfo.StreetNumber ?? "Unknown Street Number";
        PostalCodeResultText.Text = placeInfo.PostalCode ?? "Unknown Postal Code";
        CityResultText.Text = placeInfo.City ?? "Unknown City";
        CountryResultText.Text = placeInfo.Country ?? "Unknown Country";
    }

    private void PopulateEditFields(PlaceInfoItem placeInfo)
    {
        EditNameEntry.Text = placeInfo.Name ?? string.Empty;
        EditStreetNameEntry.Text = placeInfo.StreetName ?? string.Empty;
        EditStreetNumberEntry.Text = placeInfo.StreetNumber ?? string.Empty;
        EditPostalCodeEntry.Text = placeInfo.PostalCode ?? string.Empty;
        EditCityEntry.Text = placeInfo.City ?? string.Empty;
        EditCountryEntry.Text = placeInfo.Country ?? string.Empty;
    }

    private void SetEditMode(bool enabled)
    {
        _isEditMode = enabled;
        EditableFieldsPanel.IsVisible = enabled;
        EditFieldsButton.Text = enabled ? "Hide editor" : "Edit fields";
    }

    private async void FillPageWithMap(string ocrResult)
    {
        try
        {
            PlaceInfoItem filteredLines = FilterScanResult(ocrResult);
            if (filteredLines == null)
                return;
            EnsureStreetParts(filteredLines);
            SaveLoadData.SaveLastPlaceInfo(filteredLines);
            // keep a reference so JS->C# can populate chosen coordinates
            _currentPlace = filteredLines; 
            
            // Set UI fields first - critical
            try
            {
                UpdateResultLabels(filteredLines);
                PopulateEditFields(filteredLines);
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

            EnsureStreetParts(placeInfo);
            SaveLoadData.SaveLastPlaceInfo(placeInfo);
            _currentPlace = placeInfo;

            try
            {
                UpdateResultLabels(placeInfo);
                PopulateEditFields(placeInfo);
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
        // Build address string for Nominatim geocoding (postal code first improves matching)
        string address = BuildGeocodeAddress(placeInfo);

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

    private string BuildGeocodeAddress(PlaceInfoItem placeInfo)
    {
        return BuildGeocodeQueries(placeInfo).FirstOrDefault() ?? string.Empty;
    }

    private void EnsureStreetParts(PlaceInfoItem? placeInfo)
    {
        if (placeInfo == null)
            return;

        if (!string.IsNullOrWhiteSpace(placeInfo.StreetName) || !string.IsNullOrWhiteSpace(placeInfo.StreetNumber))
            return;

        var (streetName, streetNumber) = SplitStreetParts(placeInfo.Street);
        placeInfo.StreetName = streetName;
        placeInfo.StreetNumber = streetNumber;
    }

    private (string StreetName, string StreetNumber) SplitStreetParts(string? street)
    {
        if (string.IsNullOrWhiteSpace(street))
            return (string.Empty, string.Empty);

        var normalized = System.Text.RegularExpressions.Regex.Replace(street.Trim(), @"\s+", " ");
        var match = System.Text.RegularExpressions.Regex.Match(
            normalized,
            @"^(?<name>.+?)\s+(?:(?:n\.?|nº|no\.?|num\.?)\s*)?(?<number>\d{1,5}[A-Za-z]?)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (!match.Success)
            return (normalized, string.Empty);

        return (
            match.Groups["name"].Value.Trim(),
            match.Groups["number"].Value.Trim());
    }

    private string SimplifyStreetForGeocoding(string street)
    {
        if (string.IsNullOrWhiteSpace(street))
            return string.Empty;

        var cleaned = street.Trim();

        // Keep only the main street line before apartment/floor details after commas.
        var commaSplit = cleaned.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .Where(part => !string.IsNullOrEmpty(part))
            .ToList();

        if (commaSplit.Count > 0)
        {
            cleaned = commaSplit[0];

            // Keep house number when OCR splits it as a second comma-separated token.
            // Example: "CTRA. COMTESSA DOLÇA, 11" -> "CTRA. COMTESSA DOLÇA 11"
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

        // Remove inline apartment/floor suffixes if OCR placed them in the same segment.
        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"\b(?:PLANTA|PISO|PUERTA|PORTAL|ESC\.?|BLOQUE|BQ\.?|LOCAL)\b.*$",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();

        // Remove dotted initials attached to a following token.
        // Example: "J.TREPAT" / "J. TREPAT" -> "TREPAT"
        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"\b([A-ZÁÉÍÓÚÑ])\.\s*([A-ZÁÉÍÓÚÑ]{2,})\b",
            "$2",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Normalize punctuation around tokens while preserving street number separators.
        cleaned = cleaned.Replace(".", " ");

        // Remove standalone one-letter tokens (initials) from geocoding query.
        // Example: "AVDA J TREPAT I GALCERAN 2" -> "AVDA TREPAT GALCERAN 2"
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\b\p{L}\b", " ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Use the core street name for geocoding (Nominatim often matches better without type+connector prefix).
        // Example: "Calle de Francesc Moragas 4" -> "Francesc Moragas 4"
        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"^(?:Calle|C\.|C\/|C\b|Avenida|Av\.|Avda\.?|Avgda\.?|Plaza|Pza\.|Paseo|Ps\.|Passeig|Pg\.?|Carrera|Cr\.|Cr\/|Carretera|Ctra\.?|Camino|Cam\.?|Traves[ií]a|Trav\.?|Travessera|Carrer|Carr\.|Avinguda|Ronda|Rda\.?|Rambla|Pla[cç]a|Pol[íi]gono|Pol\.?|Urbanizaci[oó]n|Urb\.?|Via|R[uú]a)\b\s*",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"^(?:de|del|de la|de les|de los|de l'|d')\b\s*",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // OCR sometimes appends floor/door values as plain numbers after the street number.
        // Example: "C SANTA CLARA 7 2 1" -> "C SANTA CLARA 7"
        // Keep the first house number and remove trailing short numeric tokens.
        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"^(.*?\b\d{1,5}[A-Za-z]?)(?:\s+\d{1,5}[A-Za-z]?){1,4}\s*$",
            "$1",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Remove dash-separated floor/door chunks after the main house number.
        // Examples:
        // "JUAN TOUS I SANABRA 1 - 3 1" -> "JUAN TOUS I SANABRA 1"
        // "FRANCESC MORAGAS 4-1-1" -> "FRANCESC MORAGAS 4"
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

        // Also handle slash form frequently used for floor/door: "7/2/1", "7/2", "12/B/2".
        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"^(.*?\b\d{1,5}[A-Za-z]?)(?:\s*/\s*[0-9A-Za-z]{1,5}){1,4}\s*$",
            "$1",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Normalize leading zeros in numeric tokens used as house numbers.
        // Example: "0001" -> "1"
        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"\b0+(\d{1,5}[A-Za-z]?)\b",
            "$1",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ").Trim();

        return cleaned;
    }

    private List<string> BuildGeocodeQueries(PlaceInfoItem placeInfo)
    {
        var queries = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var postalCode = (placeInfo.PostalCode ?? string.Empty).Trim();
        var city = (placeInfo.City ?? string.Empty).Trim();
        var street = (placeInfo.Street ?? string.Empty).Trim();
        var streetMain = SimplifyStreetForGeocoding(street);
        var streetVariants = BuildStreetVariants(streetMain);
        var country = (placeInfo.Country ?? string.Empty).Trim();
        var name = (placeInfo.Name ?? string.Empty).Trim();

        var postalCity = string.Join(" ", new[] { postalCode, city }.Where(v => !string.IsNullOrWhiteSpace(v))).Trim();

        void AddQuery(params string[] parts)
        {
            var query = string.Join(", ", parts.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()));
            if (string.IsNullOrWhiteSpace(query))
                return;
            if (seen.Add(query))
                queries.Add(query);
        }

        // Prefer precise but practical queries first.
        foreach (var streetVariant in streetVariants)
        {
            AddQuery(streetVariant, postalCity, country);
            AddQuery(postalCity, streetVariant, country);
            AddQuery(streetVariant, city, country);
        }

        AddQuery(street, postalCity, country);
        AddQuery(postalCity, country);
        AddQuery(city, country);
        AddQuery(name, city, country);

        return queries;
    }

    private string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalized = text.Normalize(NormalizationForm.FormD);
        var filtered = normalized.Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray();
        return new string(filtered).Normalize(NormalizationForm.FormC);
    }

    private string RemoveStreetConnectors(string street)
    {
        if (string.IsNullOrWhiteSpace(street))
            return string.Empty;

        // Fallback-only normalization: remove common connector words that sometimes hurt OCR geocoding matches.
        var normalized = street;
        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            @"\b(?:de|del|dels|de\s+la|de\s+les|de\s+los|de\s+l')\b",
            " ",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ").Trim();
        return normalized;
    }

    private string RemoveTrailingStreetNumber(string street)
    {
        if (string.IsNullOrWhiteSpace(street))
            return string.Empty;

        return System.Text.RegularExpressions.Regex.Replace(
            street,
            @"\s+\d{1,4}[A-Za-z]?\s*$",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
    }

    private List<string> BuildStreetVariants(string street)
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

        // OCR may collapse "4 1 1" into "411"; prioritize this variant first so geocoding tries house number 4.
        var collapsedFloorToken = System.Text.RegularExpressions.Regex.Replace(
            street,
            @"(\s+)(\d)(\d{2})(\s*)$",
            "$1$2$4",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
        Add(collapsedFloorToken);
        Add(RemoveStreetConnectors(collapsedFloorToken));

        foreach (var typoVariant in BuildStreetTypoVariants(street))
        {
            Add(typoVariant);
            Add(RemoveStreetConnectors(typoVariant));
        }

        Add(street);

        var noConnectors = RemoveStreetConnectors(street);
        Add(noConnectors);

        var noTrailingNumber = RemoveTrailingStreetNumber(street);
        Add(noTrailingNumber);
        Add(RemoveStreetConnectors(noTrailingNumber));

        return variants;
    }

    private List<string> BuildStreetTypoVariants(string street)
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

            // Common OCR miss: missing "i" in endings like "...bria" (e.g. SANABRA -> SANABRIA).
            if (System.Text.RegularExpressions.Regex.IsMatch(token, @"bra$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                var fixedToken = System.Text.RegularExpressions.Regex.Replace(token, @"bra$", "bria", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                AddVariantFromToken(i, fixedToken);
            }

            // Common OCR truncation: missing trailing vowel in a street token.
            // Example: "SANTACAN" -> "SANTACANA".
            if (token.Length >= 6 &&
                System.Text.RegularExpressions.Regex.IsMatch(token, @"[bcdfghjklmnñpqrstvwxyz]$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                AddVariantFromToken(i, token + "a");
            }
        }

        return variants;
    }

    private List<string> BuildGeocodeUrls(PlaceInfoItem placeInfo, bool restrictToSpain = true)
    {
        var urls = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var street = SimplifyStreetForGeocoding((placeInfo.Street ?? string.Empty).Trim());
        var streetVariants = BuildStreetVariants(street);
        var city = (placeInfo.City ?? string.Empty).Trim();
        var postal = (placeInfo.PostalCode ?? string.Empty).Trim();
        var country = string.IsNullOrWhiteSpace(placeInfo.Country) ? "Spain" : placeInfo.Country.Trim();

        var cityNoAcc = RemoveDiacritics(city);

        var baseUrl = "https://nominatim.openstreetmap.org/search?format=json&limit=5&addressdetails=1";
        if (restrictToSpain)
            baseUrl += "&countrycodes=es";

        void AddUrl(string url)
        {
            if (!string.IsNullOrWhiteSpace(url) && seen.Add(url))
                urls.Add(url);
        }

        void AddStructured(string streetValue, string cityValue, string postalValue, string countryValue)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(streetValue)) parts.Add("street=" + Uri.EscapeDataString(streetValue));
            if (!string.IsNullOrWhiteSpace(cityValue)) parts.Add("city=" + Uri.EscapeDataString(cityValue));
            if (!string.IsNullOrWhiteSpace(postalValue)) parts.Add("postalcode=" + Uri.EscapeDataString(postalValue));
            if (!string.IsNullOrWhiteSpace(countryValue)) parts.Add("country=" + Uri.EscapeDataString(countryValue));
            if (parts.Count > 0) AddUrl(baseUrl + "&" + string.Join("&", parts));
        }

        // Structured queries first (more reliable than free text).
        foreach (var streetVariant in streetVariants)
        {
            var streetNoAcc = RemoveDiacritics(streetVariant);

            AddStructured(streetVariant, city, postal, country);
            AddStructured(streetVariant, cityNoAcc, postal, country);
            AddStructured(streetNoAcc, cityNoAcc, postal, country);
            AddStructured(streetVariant, city, string.Empty, country);
            AddStructured(streetNoAcc, cityNoAcc, string.Empty, country);
        }

        AddStructured(string.Empty, city, postal, country);
        AddStructured(string.Empty, cityNoAcc, postal, country);

        // Free-text fallbacks.
        foreach (var query in BuildGeocodeQueries(placeInfo))
        {
            AddUrl(baseUrl + "&q=" + Uri.EscapeDataString(query));

            var queryNoAcc = RemoveDiacritics(query);
            if (!string.Equals(query, queryNoAcc, StringComparison.Ordinal))
                AddUrl(baseUrl + "&q=" + Uri.EscapeDataString(queryNoAcc));
        }

        return urls;
    }

    private string NormalizePostalCodeCandidate(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return string.Empty;

        var normalized = token.Trim().ToUpperInvariant()
            .Replace('O', '0')
            .Replace('Q', '0')
            .Replace('I', '1')
            .Replace('L', '1')
            .Replace('Z', '2')
            .Replace('S', '5')
            .Replace('B', '8');

        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[^0-9]", string.Empty);

        if (normalized.Length > 5)
            normalized = normalized.Substring(0, 5);

        return normalized;
    }

    private string NormalizeCityForGeocoding(string city)
    {
        if (string.IsNullOrWhiteSpace(city))
            return string.Empty;

        var normalized = city.Trim();

        // Remove parenthesized province/region chunks from OCR like "TARREGA (LLEIDA)".
        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            @"\([^)]*\)",
            " ",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        normalized = normalized.Trim(' ', '-', ',', ';', '.');
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ").Trim();

        return normalized;
    }

    private bool TryExtractPostalAndCity(string line, out string postalCode, out string city)
    {
        postalCode = string.Empty;
        city = string.Empty;

        if (string.IsNullOrWhiteSpace(line))
            return false;

        var match = System.Text.RegularExpressions.Regex.Match(
            line,
            @"^\s*(?:C\.?\s*P\.?\s*)?([0-9OQILSZB]{4,6})\s*[-,]?\s*(.+)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (!match.Success)
            return false;

        var normalizedPostal = NormalizePostalCodeCandidate(match.Groups[1].Value);
        var cityText = NormalizeCityForGeocoding(match.Groups[2].Value);

        if (normalizedPostal.Length != 5 || string.IsNullOrWhiteSpace(cityText))
            return false;

        postalCode = normalizedPostal;
        city = cityText;
        return true;
    }

    private bool IsLikelyStreetLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        var trimmed = line.Trim();
        if (trimmed.Length < 6 || trimmed.Length > 90)
            return false;

        if (LooksLikeCodeLine(trimmed))
            return false;

        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d{4,5}\b"))
            return false;

        if (IsStreetPrefixLine(trimmed))
            return true;

        var hasLetters = System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"\p{L}");
        var hasStreetNumber = System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"\b\d{1,4}[A-Za-z]?\b");
        var hasSn = System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"\bS\s*/\s*N\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return hasLetters && (hasStreetNumber || hasSn);
    }

    private bool LooksLikeCodeLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        var trimmed = line.Trim();
        if (trimmed.Contains(' '))
            return false;

        if (trimmed.Length < 10)
            return false;

        return System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[A-Z0-9]+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private bool IsStreetPrefixLine(string line)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(
            line ?? string.Empty,
            @"^(?:Calle|C\.|C\/|C\b|Avenida|Av\.|Avda\.?|Avgda\.?|Plaza|Pza\.|Paseo|Ps\.|Passeig|Pg\.?|Carrera|Cr\.|Cr\/|Carretera|Ctra\.?|Camino|Cam\.?|Travesía|Trav\.?|Travessera|Carrer|Carr\.|Avinguda|Ronda|Rda\.?|Rambla|Plaça|Placa|Pol[íi]gono|Pol\.?|Urbanizaci[oó]n|Urb\.?|Via|R[uú]a)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private bool IsStreetContinuationLine(string line)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(
            line ?? string.Empty,
            @"^(?:Planta|Piso|Puerta|Portal|Esc\.?|Bloque|Bq\.?|Num\.?|Nº|No\.|Dept\.?|Apt\.?|Local)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private bool IsStreetNameContinuationLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        var trimmed = line.Trim();
        if (LooksLikeCodeLine(trimmed))
            return false;

        if (TryExtractPostalAndCity(trimmed, out _, out _))
            return false;

        if (IsStreetPrefixLine(trimmed) || IsStreetContinuationLine(trimmed))
            return false;

        // Common split after a bare prefix line: "Calle" + "de Francesc Moragas 4 1 1"
        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^(?:de|del|de la|de l'|d')\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            return true;

        // Also accept lines containing the house number as continuation.
        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"\b\d{1,4}[A-Za-z]?\b"))
            return true;

        // Accept normal street-name lines split after a prefix line:
        // "Calle de" + "Francesc Moragas"
        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[\p{L}'\-]+(?:\s+[\p{L}'\-]+){1,5}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            return true;

        return false;
    }

    private bool IsIncompleteStreetPrefixLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        var trimmed = line.Trim();

        // Bare or connector-only prefix lines that need continuation.
        // Examples: "Calle", "C/", "Calle de", "Avda del"
        return System.Text.RegularExpressions.Regex.IsMatch(
            trimmed,
            @"^(?:Calle|C\.|C\/|C\b|Avenida|Av\.|Avda\.?|Avgda\.?|Carrer|Carr\.|Cr\.|Cr\/|Passeig|Pg\.?|Plaza|Pza\.?|Ronda|Rda\.?|Rambla)(?:\s+(?:de|del|de la|de l'|d'))?\s*$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private bool IsNumericAddressDetailLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        return System.Text.RegularExpressions.Regex.IsMatch(
            line.Trim(),
            @"^\d{1,4}(?:\s+[0-9A-Za-z]{1,3}){0,3}$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private string ExtractPrimaryHouseNumber(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return string.Empty;

        var match = System.Text.RegularExpressions.Regex.Match(
            line.Trim(),
            @"^(\d{1,4}[A-Za-z]?)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private bool HasHouseNumber(string street)
    {
        if (string.IsNullOrWhiteSpace(street))
            return false;

        return System.Text.RegularExpressions.Regex.IsMatch(
            street,
            @"\b\d{1,4}[A-Za-z]?\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private bool IsStreetNameOnlyLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        var trimmed = line.Trim();
        if (LooksLikeCodeLine(trimmed))
            return false;

        if (TryExtractPostalAndCity(trimmed, out _, out _))
            return false;

        // 2-6 words, letters/apostrophes/hyphens only.
        return System.Text.RegularExpressions.Regex.IsMatch(
            trimmed,
            @"^[\p{L}'\-]+(?:\s+[\p{L}'\-]+){1,5}$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private bool IsLikelyNameLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        var trimmed = line.Trim();
        if (trimmed.Length < 5 || trimmed.Length > 70)
            return false;

        if (LooksLikeCodeLine(trimmed))
            return false;

        if (IsStreetPrefixLine(trimmed) || IsStreetContinuationLine(trimmed))
            return false;

        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d{4,5}"))
            return false;

        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"\d"))
            return false;

        // Keep only person/company-like text (letters, spaces and a few separators).
        if (!System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[\p{L}\s'\-\.]+$"))
            return false;

        var words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length >= 2;
    }

    private int ScoreNameLine(string line, int index, int firstStreetIndex, List<string> lines)
    {
        int score = 0;
        var trimmed = line.Trim();
        var words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words.Length >= 2 && words.Length <= 6) score += 4;
        else if (words.Length > 6) score -= 2;

        if (trimmed.Length >= 10 && trimmed.Length <= 45) score += 3;
        else score -= 1;

        // Name line is commonly just above street line.
        if (firstStreetIndex > 0 && index == firstStreetIndex - 1) score += 5;

        // Very common parcel pattern: code line followed by recipient line.
        if (index > 0 && LooksLikeCodeLine(lines[index - 1])) score += 4;

        // Mild penalty for heavy punctuation noise.
        int punctCount = trimmed.Count(c => ".,;:/\\|_~`".Contains(c));
        if (punctCount > 1) score -= 2;

        return score;
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

        // Extract name from upper block (before street/postal) while avoiding OCR code/reference lines.
        if (lines.Count > 0)
        {
            int firstStreetIndex = lines.FindIndex(IsStreetPrefixLine);
            int firstPostalIndex = lines.FindIndex(l => System.Text.RegularExpressions.Regex.IsMatch(l, @"^\d{4,5}"));

            int nameSearchEnd = lines.Count;
            if (firstStreetIndex >= 0)
                nameSearchEnd = Math.Min(nameSearchEnd, firstStreetIndex);
            if (firstPostalIndex >= 0)
                nameSearchEnd = Math.Min(nameSearchEnd, firstPostalIndex);

            // Pick the best candidate name line by score (more robust against OCR odd lines).
            int bestScore = int.MinValue;
            string? bestName = null;

            for (int i = 0; i < nameSearchEnd; i++)
            {
                var line = lines[i];
                if (!IsLikelyNameLine(line))
                    continue;

                int score = ScoreNameLine(line, i, firstStreetIndex, lines);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestName = line;
                }
            }

            if (!string.IsNullOrWhiteSpace(bestName))
            {
                placeInfo.Name = bestName;
                System.Diagnostics.Debug.WriteLine($"FilterScanResult - Name: {placeInfo.Name}");
            }

            // Secondary pass: keep previous relaxed behavior if no primary candidate was found.
            if (string.IsNullOrEmpty(placeInfo.Name))
            {
                for (int i = 0; i < nameSearchEnd; i++)
                {
                    var line = lines[i];
                    if (line.Length > 3 &&
                        !IsStreetPrefixLine(line) &&
                        !IsStreetContinuationLine(line) &&
                        !System.Text.RegularExpressions.Regex.IsMatch(line, @"^\d{4,5}") &&
                        !LooksLikeCodeLine(line))
                    {
                        placeInfo.Name = line;
                        break;
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"FilterScanResult - Total lines: {lines.Count}");
            for (int i = 0; i < lines.Count; i++)
            {
                System.Diagnostics.Debug.WriteLine($"  Line {i}: {lines[i]}");
            }
        }

        int postalLineIndex = -1;

        // Extract postal code + city with OCR-tolerant matching.
        // Matches: "28001 Madrid", "CP 08013 Barcelona", "O8013 - Barcelona"
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (TryExtractPostalAndCity(line, out var postalCode, out var cityText))
            {
                placeInfo.PostalCode = postalCode;
                placeInfo.City = cityText;
                postalLineIndex = i;
                break;
            }

            // Handle split style OCR: one line with CP/code and next line with city.
            var splitMatch = System.Text.RegularExpressions.Regex.Match(
                line,
                @"^\s*(?:C\.?\s*P\.?\s*)?([0-9OQILSZB]{4,6})\s*$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!splitMatch.Success)
                continue;

            var normalized = NormalizePostalCodeCandidate(splitMatch.Groups[1].Value);
            if (normalized.Length != 5)
                continue;
            if (i + 1 >= lines.Count)
                continue;

            var nextLine = NormalizeCityForGeocoding(lines[i + 1]);
            if (string.IsNullOrWhiteSpace(nextLine) || LooksLikeCodeLine(nextLine) || IsStreetPrefixLine(nextLine))
                continue;

            placeInfo.PostalCode = normalized;
            placeInfo.City = nextLine;
            postalLineIndex = i;
            break;
        }

        // Backward compatibility: if line-by-line parsing did not find a postal code, try multiline OCR text.
        if (string.IsNullOrEmpty(placeInfo.PostalCode) || string.IsNullOrEmpty(placeInfo.City))
        {
            var postalCityMatch = System.Text.RegularExpressions.Regex.Match(
                ocrResult,
                @"(?:^|\n)\s*(?:C\.?\s*P\.?\s*)?([0-9OQILSZB]{4,6})\s+(.+)$",
                System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (postalCityMatch.Success)
            {
                var normalizedPostal = NormalizePostalCodeCandidate(postalCityMatch.Groups[1].Value);
                if (normalizedPostal.Length == 5)
                {
                    placeInfo.PostalCode = normalizedPostal;
                    placeInfo.City = NormalizeCityForGeocoding(postalCityMatch.Groups[2].Value);
                }
            }
        }

        // Extract street from OCR lines first (more stable than multiline regex and avoids truncation).
        int prefixStreetIndex = lines.FindIndex(IsStreetPrefixLine);
        if (prefixStreetIndex >= 0)
        {
            var streetParts = new List<string> { lines[prefixStreetIndex] };
            bool needsNameContinuation = IsIncompleteStreetPrefixLine(lines[prefixStreetIndex]);

            // Add immediate continuation line if present (e.g., "PLANTA ...", "PUERTA ...")
            if (prefixStreetIndex + 1 < lines.Count)
            {
                var nextLine = lines[prefixStreetIndex + 1];
                if (IsStreetContinuationLine(nextLine))
                {
                    streetParts.Add(nextLine);
                }
                else if (IsStreetNameContinuationLine(nextLine))
                {
                    streetParts.Add(nextLine);
                    needsNameContinuation = false;

                    // Optional second continuation for patterns like:
                    // "Calle" + "de Francesc Moragas" + "4 1 1"
                    if (prefixStreetIndex + 2 < lines.Count)
                    {
                        var thirdLine = lines[prefixStreetIndex + 2];
                        if (IsStreetNameContinuationLine(thirdLine) || IsStreetContinuationLine(thirdLine))
                            streetParts.Add(thirdLine);
                    }
                }
                else if (needsNameContinuation)
                {
                    // Force-append the next plausible line when prefix is incomplete ("Calle de").
                    if (!LooksLikeCodeLine(nextLine) &&
                        !TryExtractPostalAndCity(nextLine, out _, out _) &&
                        !IsStreetContinuationLine(nextLine))
                    {
                        streetParts.Add(nextLine);

                        if (prefixStreetIndex + 2 < lines.Count)
                        {
                            var thirdLine = lines[prefixStreetIndex + 2];
                            if (IsStreetNameContinuationLine(thirdLine) || IsStreetContinuationLine(thirdLine))
                                streetParts.Add(thirdLine);
                        }
                    }
                }
            }

            placeInfo.Street = string.Join(" ", streetParts).Trim();
        }

        // If OCR left us with an incomplete prefix-only street ("Calle de"), rebuild it
        // using strongest nearby lines (prefer lines immediately before postal code).
        if (!string.IsNullOrWhiteSpace(placeInfo.Street) && IsIncompleteStreetPrefixLine(placeInfo.Street))
        {
            var rebuilt = new List<string> { placeInfo.Street.Trim() };

            int nameLineIndex = -1;
            int numberLineIndex = -1;

            if (postalLineIndex > 0)
            {
                var prev = lines[postalLineIndex - 1].Trim();
                if (IsNumericAddressDetailLine(prev))
                {
                    numberLineIndex = postalLineIndex - 1;
                    nameLineIndex = postalLineIndex - 2;
                }
                else
                {
                    nameLineIndex = postalLineIndex - 1;
                }
            }

            bool TryAddStreetNameFromIndex(int idx)
            {
                if (idx < 0 || idx >= lines.Count)
                    return false;

                var candidate = lines[idx].Trim();
                if (string.IsNullOrWhiteSpace(candidate))
                    return false;

                if (LooksLikeCodeLine(candidate) || IsStreetPrefixLine(candidate) || IsStreetContinuationLine(candidate))
                    return false;

                if (TryExtractPostalAndCity(candidate, out _, out _))
                    return false;

                if (!string.IsNullOrWhiteSpace(placeInfo.Name) && string.Equals(candidate, placeInfo.Name, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (IsStreetNameOnlyLine(candidate) || IsStreetNameContinuationLine(candidate) || IsLikelyStreetLine(candidate))
                {
                    rebuilt.Add(candidate);
                    return true;
                }

                return false;
            }

            bool hasName = TryAddStreetNameFromIndex(nameLineIndex);

            // Secondary search around prefix line if postal-near context did not yield a name.
            if (!hasName)
            {
                for (int i = prefixStreetIndex + 1; i < lines.Count && i <= prefixStreetIndex + 4; i++)
                {
                    if (TryAddStreetNameFromIndex(i))
                    {
                        hasName = true;
                        break;
                    }
                }
            }

            if (numberLineIndex >= 0 && numberLineIndex < lines.Count)
            {
                var numberLine = lines[numberLineIndex].Trim();
                if (IsNumericAddressDetailLine(numberLine))
                    rebuilt.Add(numberLine);
            }

            if (rebuilt.Count > 1)
                placeInfo.Street = string.Join(" ", rebuilt).Trim();
        }

        // If we have a street name but no house number, try to append the main house number
        // from a nearby numeric line (e.g., "4 1 1" -> append "4").
        if (!string.IsNullOrWhiteSpace(placeInfo.Street) && !HasHouseNumber(placeInfo.Street))
        {
            string detectedNumber = string.Empty;

            if (postalLineIndex > 0)
            {
                var nearPostalLine = lines[postalLineIndex - 1].Trim();
                if (IsNumericAddressDetailLine(nearPostalLine))
                    detectedNumber = ExtractPrimaryHouseNumber(nearPostalLine);
            }

            if (string.IsNullOrWhiteSpace(detectedNumber) && prefixStreetIndex >= 0)
            {
                for (int i = prefixStreetIndex + 1; i < lines.Count && i <= prefixStreetIndex + 4; i++)
                {
                    var candidate = lines[i].Trim();
                    if (!IsNumericAddressDetailLine(candidate))
                        continue;

                    detectedNumber = ExtractPrimaryHouseNumber(candidate);
                    if (!string.IsNullOrWhiteSpace(detectedNumber))
                        break;
                }
            }

            if (!string.IsNullOrWhiteSpace(detectedNumber))
                placeInfo.Street = $"{placeInfo.Street} {detectedNumber}".Trim();
        }

        // OCR can merge the street type abbreviation with the name: "CTALLADELL" / "CTalladell".
        // Normalize by inserting a space, while preserving the leading "C" token in parsed output.
        if (!string.IsNullOrWhiteSpace(placeInfo.Street))
        {
            placeInfo.Street = System.Text.RegularExpressions.Regex.Replace(
                placeInfo.Street,
                @"^\s*C(?=(?:[A-ZÁÉÍÓÚÜÑ]{4,}\b|[A-ZÁÉÍÓÚÜÑ][a-záéíóúüñ]{2,}\b))",
                "C ",
                System.Text.RegularExpressions.RegexOptions.None).Trim();
        }

        // Fallback regex if no street line was found by prefixes
        if (string.IsNullOrEmpty(placeInfo.Street))
        {
            var streetMatch = System.Text.RegularExpressions.Regex.Match(
                ocrResult,
                @"((?:Calle|C\.|C\/|C\b|Avenida|Av\.|Avda\.?|Avgda\.?|Plaza|Pza\.|Paseo|Ps\.|Passeig|Pg\.?|Carrera|Cr\.|Cr\/|Carretera|Ctra\.?|Camino|Cam\.?|Travesía|Trav\.?|Travessera|Carrer|Carr\.|Avinguda|Ronda|Rda\.?|Rambla|Plaça|Placa|Pol[íi]gono|Pol\.?|Urbanizaci[oó]n|Urb\.?|Via|R[uú]a)\s+[^\r\n]{4,}(?:\s*[,]?\s*\d+[a-z]?)?)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);

            if (streetMatch.Success)
                placeInfo.Street = streetMatch.Groups[1].Value.Trim();
        }

        if (string.IsNullOrEmpty(placeInfo.Street))
        {
            // Fallback 1: if we found postal line, street is usually just above it
            if (postalLineIndex > 0)
            {
                var streetParts = new List<string>();
                for (int i = postalLineIndex - 1; i >= 0 && streetParts.Count < 2; i--)
                {
                    var line = lines[i];
                    if (line.Length <= 4)
                        continue;

                    if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^\d{4,5}"))
                        continue;

                    if (!string.IsNullOrEmpty(placeInfo.Name) && string.Equals(line, placeInfo.Name, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!string.IsNullOrEmpty(placeInfo.City) && string.Equals(line, placeInfo.City, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (LooksLikeCodeLine(line))
                        continue;

                    if (streetParts.Count == 0)
                    {
                        if (!IsLikelyStreetLine(line))
                            continue;

                        streetParts.Insert(0, line);
                        continue;
                    }

                    if (IsStreetPrefixLine(line) || IsStreetContinuationLine(streetParts[0]))
                        streetParts.Insert(0, line);

                    break;
                }

                if (streetParts.Count > 0)
                    placeInfo.Street = string.Join(", ", streetParts);
            }

            // Fallback 2: look for the first plausible address line near the top
            if (string.IsNullOrEmpty(placeInfo.Street) && lines.Count >= 2)
            {
                for (int i = 1; i < lines.Count - 1 && i < 4; i++)
                {
                    var line = lines[i];
                    if (IsLikelyStreetLine(line) &&
                        !string.Equals(line, placeInfo.City, StringComparison.OrdinalIgnoreCase))
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

        EnsureStreetParts(placeInfo);

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
        var queryUrlsSpain = new List<string>();
        var queryUrlsGlobal = new List<string>();
        var debugReason = string.Empty;

        try
        {
            if (placeInfo == null) return;
            queryUrlsSpain = BuildGeocodeUrls(placeInfo, restrictToSpain: true);
            queryUrlsGlobal = BuildGeocodeUrls(placeInfo, restrictToSpain: false);

            using var http = new HttpClient();
            // Nominatim requires a valid User-Agent — set one so the request is accepted
            try
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("PmSTools/1.0 (+https://github.com/pmstools)");
                http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ca,es;q=0.9,en;q=0.6");
            }
            catch { }

            var foundCandidates = new List<GeocodeCandidate>();

            async Task QueryCandidatesAsync(List<string> queryUrls)
            {
                foreach (var queryUrl in queryUrls)
                {
                    try
                    {
                        using var response = await http.GetAsync(queryUrl);
                        if (!response.IsSuccessStatusCode)
                        {
                            if (string.IsNullOrEmpty(debugReason))
                                debugReason = $"HTTP {(int)response.StatusCode} for geocode query.";
                            continue;
                        }

                        var resp = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(resp);
                        var root = doc.RootElement;

                        if (root.ValueKind != JsonValueKind.Array)
                            continue;

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

                                foundCandidates.Add(new GeocodeCandidate { DisplayName = display, Type = type, Lat = lat, Lon = lon });
                            }
                            catch { /* ignore malformed items */ }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (string.IsNullOrEmpty(debugReason))
                            debugReason = ex.Message;
                        continue;
                    }

                    if (foundCandidates.Count > 0)
                        break;
                }
            }

            await QueryCandidatesAsync(queryUrlsSpain);
            if (foundCandidates.Count == 0)
            {
                // Fallback: remove country-code restriction for OCR inputs that produce out-of-country or noisy locality terms.
                await QueryCandidatesAsync(queryUrlsGlobal);
            }

            if (EnableGeocodeDebugPopup)
            {
                if (string.IsNullOrWhiteSpace(debugReason))
                    debugReason = foundCandidates.Count == 0 ? "No candidates returned from geocoder." : "Debug mode enabled.";

                await ShowGeocodeDebugPopupAsync(placeInfo, queryUrlsSpain, queryUrlsGlobal, debugReason, foundCandidates.Count);
            }

            MainThread.BeginInvokeOnMainThread(() => _candidates.Clear());
            foreach (var cand in foundCandidates)
            {
                MainThread.BeginInvokeOnMainThread(() => _candidates.Add(cand));
            }

            // Apply first candidate by default so coordinates are immediately available.
            if (foundCandidates.Count > 0)
            {
                MainThread.BeginInvokeOnMainThread(() => CandidatesList.SelectedItem = foundCandidates[0]);
                await ApplyCandidateSelectionAsync(foundCandidates[0], showAlert: false);
            }
        }
        catch (Exception ex)
        {
            if (EnableGeocodeDebugPopup)
            {
                await ShowGeocodeDebugPopupAsync(placeInfo, queryUrlsSpain, queryUrlsGlobal, ex.Message, 0);
            }
        }
    }

    private string BuildDebugQueryPreview(string url)
    {
        try
        {
            var uri = new Uri(url);
            var qs = (uri.Query ?? string.Empty).TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Split('=', 2))
                .Where(kv => kv.Length == 2)
                .ToDictionary(
                    kv => Uri.UnescapeDataString(kv[0]),
                    kv => Uri.UnescapeDataString(kv[1]),
                    StringComparer.OrdinalIgnoreCase);

            if (qs.TryGetValue("q", out var freeText) && !string.IsNullOrWhiteSpace(freeText))
                return "q=" + freeText;

            var parts = new List<string>();
            if (qs.TryGetValue("street", out var street) && !string.IsNullOrWhiteSpace(street)) parts.Add("street=" + street);
            if (qs.TryGetValue("postalcode", out var postal) && !string.IsNullOrWhiteSpace(postal)) parts.Add("postal=" + postal);
            if (qs.TryGetValue("city", out var city) && !string.IsNullOrWhiteSpace(city)) parts.Add("city=" + city);
            if (qs.TryGetValue("country", out var country) && !string.IsNullOrWhiteSpace(country)) parts.Add("country=" + country);

            return parts.Count > 0 ? string.Join(", ", parts) : (uri.Query ?? string.Empty);
        }
        catch
        {
            return url;
        }
    }

    private async Task ShowGeocodeDebugPopupAsync(PlaceInfoItem placeInfo, List<string> spainUrls, List<string> globalUrls, string reason = "", int candidateCount = 0)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("Parsed OCR:");
            sb.AppendLine($"Name: {placeInfo.Name ?? "(empty)"}");
            sb.AppendLine($"Street: {placeInfo.Street ?? "(empty)"}");
            sb.AppendLine($"StreetName: {placeInfo.StreetName ?? "(empty)"}");
            sb.AppendLine($"StreetNumber: {placeInfo.StreetNumber ?? "(empty)"}");
            sb.AppendLine($"PostalCode: {placeInfo.PostalCode ?? "(empty)"}");
            sb.AppendLine($"City: {placeInfo.City ?? "(empty)"}");
            sb.AppendLine($"Country: {placeInfo.Country ?? "(empty)"}");
            sb.AppendLine($"Candidates: {candidateCount}");

            if (!string.IsNullOrWhiteSpace(reason))
            {
                sb.AppendLine();
                sb.AppendLine("Reason:");
                sb.AppendLine(reason);
            }

            sb.AppendLine();
            sb.AppendLine("Top Spain queries:");
            foreach (var item in spainUrls.Take(5).Select((url, idx) => $"{idx + 1}. {BuildDebugQueryPreview(url)}"))
                sb.AppendLine(item);

            sb.AppendLine();
            sb.AppendLine("Top Global queries:");
            foreach (var item in globalUrls.Take(5).Select((url, idx) => $"{idx + 1}. {BuildDebugQueryPreview(url)}"))
                sb.AppendLine(item);

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlertAsync("Geocode debug (no results)", sb.ToString(), "OK");
            });
        }
        catch
        {
            // ignore debug popup errors
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

    private void EditFieldsButton_Clicked(object sender, EventArgs e)
    {
        if (_currentPlace != null)
            PopulateEditFields(_currentPlace);

        SetEditMode(!_isEditMode);
    }

    private void CancelEditsButton_Clicked(object sender, EventArgs e)
    {
        if (_currentPlace != null)
            PopulateEditFields(_currentPlace);

        SetEditMode(false);
    }

    private async void RepeatPhotoButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (Navigation.NavigationStack.Count > 1)
            {
                await Navigation.PopAsync();
                return;
            }

            await Navigation.PushAsync(new FindPlacePage());
        }
        catch
        {
            await DisplayAlertAsync("Navigation", "Could not return to photo capture page.", "OK");
        }
    }

    private async void ApplyEditsButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            _currentPlace ??= new PlaceInfoItem();

            _currentPlace.Name = (EditNameEntry.Text ?? string.Empty).Trim();
            _currentPlace.StreetName = (EditStreetNameEntry.Text ?? string.Empty).Trim();
            _currentPlace.StreetNumber = (EditStreetNumberEntry.Text ?? string.Empty).Trim();
            _currentPlace.PostalCode = (EditPostalCodeEntry.Text ?? string.Empty).Trim();
            _currentPlace.City = (EditCityEntry.Text ?? string.Empty).Trim();
            _currentPlace.Country = (EditCountryEntry.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(_currentPlace.Country))
                _currentPlace.Country = "Spain";

            EnsureStreetParts(_currentPlace);
            SaveLoadData.SaveLastPlaceInfo(_currentPlace);
            UpdateResultLabels(_currentPlace);

            _currentPlace.Latitude = null;
            _currentPlace.Longitude = null;
            LatitudeText.Text = "—";
            LongitudeText.Text = "—";
            CandidatesList.SelectedItem = null;
            _candidates.Clear();

            await ShowMapForLocation(_currentPlace);
            await PopulateCandidatesAsync(_currentPlace);

            SetEditMode(false);
        }
        catch
        {
            await DisplayAlertAsync("Error", "Could not apply manual corrections.", "OK");
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
