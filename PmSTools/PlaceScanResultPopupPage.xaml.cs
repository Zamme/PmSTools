using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MauiPopup.Views;
using SkiaSharp.Views.Maui.Controls.Hosting;
using PmSTools.Models;
using PmSTools.Resources.Languages;


/*
using Microsoft.Maui.Controls.Maps;

using Microsoft.Maui.Maps;
*/
/*
using Map = Microsoft.Maui.Controls.Maps.Map;
*/

namespace PmSTools;

public partial class PlaceScanResultPopupPage : BasePopupPage
{
    private const int PopupGeocodeRequestTimeoutSeconds = 4;
    private const int PopupGeocodePerQueryResultLimit = 6;
    private const int PopupGeocodeMaxQueryAttempts = 2;
    private PlaceInfoItem? _currentPlace;
    private bool _isEditMode;
    private bool _hasSavedSelection;
    private readonly ObservableCollection<GeocodeCandidate> _candidates = new();

    private class GeocodeCandidate
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string MatchBadge { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lon { get; set; }
        public string HouseNumber { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public int SourceOrder { get; set; }
    }

    public PlaceScanResultPopupPage()
    {
        InitializeComponent();
        CandidatesList.ItemsSource = _candidates;
    }

    public PlaceScanResultPopupPage(string ocrResult)
    {
        InitializeComponent();
        CandidatesList.ItemsSource = _candidates;
        FillPage(ocrResult);
    }

    private void FillPage(string ocrResult)
    {
        try
        {
            PlaceInfoItem filteredLines = FilterScanResult(ocrResult);
            if (filteredLines == null)
                return;

            EnsureStreetParts(filteredLines);
            _currentPlace = filteredLines;

            UpdateResultLabels(filteredLines);
            PopulateEditFields(filteredLines);

            _ = PopulateCandidatesAsync(filteredLines);
        }
        catch (Exception)
        {
            // System.Diagnostics.Debug.WriteLine($"FillPage error: {ex.Message}");
        }
    }

    private void UpdateResultLabels(PlaceInfoItem placeInfo)
    {
        NameResultText.Text = placeInfo.Name ?? LangResources.UnknownNameText;
        StreetNameResultText.Text = placeInfo.StreetName ?? LangResources.UnknownStreetNameText;
        StreetNumberResultText.Text = placeInfo.StreetNumber ?? LangResources.UnknownStreetNumberText;
        PostalCodeResultText.Text = placeInfo.PostalCode ?? LangResources.UnknownPostalCodeText;
        CityResultText.Text = placeInfo.City ?? LangResources.UnknownCityText;
        CountryResultText.Text = placeInfo.Country ?? LangResources.UnknownCountryText;
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
        EditFieldsButton.Text = enabled ? LangResources.HideEditorText : LangResources.EditFieldsText;
    }

    private void SaveSelectedPlace()
    {
        if (_currentPlace == null)
        {
            return;
        }

        if (_hasSavedSelection)
        {
            SaveLoadData.UpdateMostRecentPlaceInfo(_currentPlace);
            return;
        }

        SaveLoadData.SaveLastPlaceInfo(_currentPlace);
        _hasSavedSelection = true;
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
        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            @"\b([\p{L}'\-]{2,})(\d{1,5}[A-Za-z]?)\b",
            "$1 $2",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var match = System.Text.RegularExpressions.Regex.Match(
            normalized,
            @"^(?<name>.+?)\s+(?<number>\d{1,5}[A-Za-z]?)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (!match.Success)
            return (normalized, string.Empty);

        return (
            match.Groups["name"].Value.Trim(),
            match.Groups["number"].Value.Trim());
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

    private void CandidatesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var sel = CandidatesList.SelectedItem as GeocodeCandidate;
        if (sel == null || _currentPlace == null)
        {
            return;
        }

        _currentPlace.Latitude = sel.Lat;
        _currentPlace.Longitude = sel.Lon;
        SaveSelectedPlace();
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
                _currentPlace.Country = LangResources.DefaultCountryText;

            EnsureStreetParts(_currentPlace);
            UpdateResultLabels(_currentPlace);
            PopulateEditFields(_currentPlace);
            if (_hasSavedSelection)
            {
                SaveLoadData.UpdateMostRecentPlaceInfo(_currentPlace);
            }

            CandidatesList.SelectedItem = null;
            _candidates.Clear();
            _ = PopulateCandidatesAsync(_currentPlace);

            SetEditMode(false);
            await DisplayAlertAsync(LangResources.SavedTitleText, LangResources.ManualCorrectionsAppliedText, LangResources.OkText);
        }
        catch
        {
            await DisplayAlertAsync(LangResources.ErrorTitleText, LangResources.ManualCorrectionsApplyErrorText, LangResources.OkText);
        }
    }

    private PlaceInfoItem FilterScanResult(string ocrResult)
    {
        var placeInfoItem = new PlaceInfoItem();

        var ocrResultLines = ocrResult
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        var postalCodeLineIndex = -1;

        for (int i = 0; i < ocrResultLines.Count; i++)
        {
            var line = ocrResultLines[i];
            if (TryExtractPostalAndCity(line, out var postalCode, out var city))
            {
                placeInfoItem.PostalCode = postalCode;
                placeInfoItem.City = city;
                postalCodeLineIndex = i;
                break;
            }

            // Split-line OCR style: line with only CP, next line with city.
            var splitMatch = System.Text.RegularExpressions.Regex.Match(
                line,
                @"^\s*(?:C\.?\s*P\.?\s*)?([0-9OQILSZB]{4,6})\s*$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (!splitMatch.Success)
                continue;

            var normalizedPostal = NormalizePostalCodeCandidate(splitMatch.Groups[1].Value);
            if (normalizedPostal.Length != 5)
                continue;

            placeInfoItem.PostalCode = normalizedPostal;
            postalCodeLineIndex = i;

            if (i + 1 < ocrResultLines.Count)
            {
                var candidateCity = NormalizeCityForGeocoding(ocrResultLines[i + 1]);
                if (!string.IsNullOrWhiteSpace(candidateCity) && !Contains5DigitNumber(candidateCity))
                    placeInfoItem.City = candidateCity;
            }

            break;
        }

        if (postalCodeLineIndex >= 2)
            placeInfoItem.Name = ocrResultLines[postalCodeLineIndex - 2];

        if (postalCodeLineIndex >= 1)
            placeInfoItem.Street = ocrResultLines[postalCodeLineIndex - 1];

        if (string.IsNullOrWhiteSpace(placeInfoItem.City) && postalCodeLineIndex >= 0)
            placeInfoItem.City = FindNearbyCityCandidate(ocrResultLines, postalCodeLineIndex);

        if (postalCodeLineIndex + 2 < ocrResultLines.Count)
            placeInfoItem.Country = ocrResultLines[postalCodeLineIndex + 2];

        if (string.IsNullOrWhiteSpace(placeInfoItem.Country))
            placeInfoItem.Country = LangResources.DefaultCountryText;

        if (string.IsNullOrWhiteSpace(placeInfoItem.City))
            placeInfoItem.City = LangResources.UnknownCityText;

        return placeInfoItem;
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
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\([^)]*\)", " ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
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

    private string FindNearbyCityCandidate(List<string> lines, int anchorIndex)
    {
        if (lines == null || lines.Count == 0 || anchorIndex < 0 || anchorIndex >= lines.Count)
            return string.Empty;

        for (int distance = 1; distance <= 2; distance++)
        {
            foreach (var offset in new[] { -distance, distance })
            {
                int idx = anchorIndex + offset;
                if (idx < 0 || idx >= lines.Count)
                    continue;

                var candidate = NormalizeCityForGeocoding(lines[idx]);
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                if (Contains5DigitNumber(candidate))
                    continue;

                if (candidate.Length < 3)
                    continue;

                return candidate;
            }
        }

        return string.Empty;
    }

    private bool Contains5DigitNumber(string str)
    {
        // System.Diagnostics.Debug.WriteLine("Checking if line contains a 5-digit number: " + str);
        bool contains5DigitNumber = System.Text.RegularExpressions.Regex.IsMatch(str, @"\d{5}");
        // System.Diagnostics.Debug.WriteLine("Contains 5-digit number: " + contains5DigitNumber);
        return contains5DigitNumber;
    }

    private bool Is5DigitNumber(string str)
    {
        // System.Diagnostics.Debug.WriteLine("Checking if line is a 5-digit number: " + str);
        return str.Length == 5 && int.TryParse(str, out _);
    }

    private string BuildGeocodeAddress(PlaceInfoItem placeInfo)
    {
        var street = (placeInfo.Street ?? string.Empty).Trim();
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

        var parts = new[]
        {
            street,
            string.Join(" ", new[] { placeInfo.PostalCode, placeInfo.City }.Where(v => !string.IsNullOrWhiteSpace(v))),
            placeInfo.Country
        };

        return string.Join(", ", parts.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim()));
    }

    private List<string> BuildPopupGeocodeUrls(PlaceInfoItem placeInfo)
    {
        var urls = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddUrl(string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && seen.Add(value))
                urls.Add(value);
        }

        var street = (placeInfo.Street ?? string.Empty).Trim();
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
        var postal = NormalizePostalCodeCandidate(placeInfo.PostalCode ?? string.Empty);
        var city = NormalizeCityForGeocoding(placeInfo.City ?? string.Empty);
        var country = string.IsNullOrWhiteSpace(placeInfo.Country) ? LangResources.DefaultCountryText : placeInfo.Country.Trim();

        var baseUrl = "https://nominatim.openstreetmap.org/search?format=json&limit=" + PopupGeocodePerQueryResultLimit.ToString(CultureInfo.InvariantCulture) + "&addressdetails=1";

        // Fast path first: postal-only lookup tends to be most stable with OCR noise.
        if (!string.IsNullOrWhiteSpace(postal))
            AddUrl(baseUrl + "&postalcode=" + Uri.EscapeDataString(postal) + "&country=" + Uri.EscapeDataString(country));

        if (!string.IsNullOrWhiteSpace(street) && !string.IsNullOrWhiteSpace(postal))
        {
            AddUrl(baseUrl + "&street=" + Uri.EscapeDataString(street) + "&postalcode=" + Uri.EscapeDataString(postal) + "&country=" + Uri.EscapeDataString(country));
        }

        if (!string.IsNullOrWhiteSpace(street) && !string.IsNullOrWhiteSpace(city))
            AddUrl(baseUrl + "&street=" + Uri.EscapeDataString(street) + "&city=" + Uri.EscapeDataString(city) + "&country=" + Uri.EscapeDataString(country));

        var fullAddress = BuildGeocodeAddress(placeInfo);
        if (!string.IsNullOrWhiteSpace(fullAddress))
            AddUrl(baseUrl + "&q=" + Uri.EscapeDataString(fullAddress));

        return urls;
    }

    private string NormalizeHouseNumberForCompare(string? value)
    {
        return GeocodeScoring.NormalizeHouseNumberForCompare(value);
    }

    private string NormalizeCityForCompare(string? value)
    {
        return GeocodeScoring.NormalizeCityForCompare(NormalizeCityForGeocoding(value ?? string.Empty));
    }

    private bool CandidateMatchesHouseNumber(GeocodeCandidate candidate, string desiredHouseNumber)
    {
        if (candidate == null)
            return false;

        return GeocodeScoring.CandidateMatchesHouseNumber(candidate.HouseNumber, desiredHouseNumber);
    }

    private bool CandidateHasHouseNumber(GeocodeCandidate candidate)
    {
        if (candidate == null)
            return false;

        return GeocodeScoring.CandidateHasHouseNumber(candidate.HouseNumber);
    }

    private int ComputeCandidateScore(GeocodeCandidate candidate, string desiredHouseNumber, string desiredPostalCode, string desiredCity)
    {
        if (candidate == null)
            return int.MinValue;

        return GeocodeScoring.ComputeCandidateScore(
            candidate.Type,
            candidate.HouseNumber,
            candidate.PostalCode,
            candidate.City,
            desiredHouseNumber,
            desiredPostalCode,
            NormalizeCityForGeocoding(desiredCity),
            candidate.SourceOrder);
    }

    private async Task PopulateCandidatesAsync(PlaceInfoItem placeInfo)
    {
        try
        {
            if (placeInfo == null)
                return;

            var queryUrls = BuildPopupGeocodeUrls(placeInfo);
            if (queryUrls.Count == 0)
                return;

            var desiredHouseNumber = !string.IsNullOrWhiteSpace(placeInfo.StreetNumber)
                ? placeInfo.StreetNumber
                : SplitStreetParts(placeInfo.Street).StreetNumber;

            using var http = new HttpClient();
            try
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("PmSTools/1.0 (+https://github.com/pmstools)");
                http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ca,es;q=0.9,en;q=0.6");
            }
            catch { }
            http.Timeout = TimeSpan.FromSeconds(PopupGeocodeRequestTimeoutSeconds);

            var foundCandidates = new List<GeocodeCandidate>();
            int sourceOrder = 0;

            int attempts = 0;
            foreach (var url in queryUrls)
            {
                if (attempts >= PopupGeocodeMaxQueryAttempts)
                    break;

                attempts++;

                using var response = await http.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    continue;

                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);

                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    try
                    {
                        var display = item.TryGetProperty("display_name", out var dn) ? dn.GetString() ?? string.Empty : string.Empty;
                        var type = item.TryGetProperty("type", out var t) ? t.GetString() ?? string.Empty : string.Empty;
                        var houseNumber = string.Empty;
                        var candidateCity = string.Empty;
                        var candidatePostalCode = string.Empty;

                        if (item.TryGetProperty("address", out var addressProp) && addressProp.ValueKind == JsonValueKind.Object)
                        {
                            if (addressProp.TryGetProperty("house_number", out var houseNumberProp))
                                houseNumber = houseNumberProp.GetString() ?? string.Empty;

                            foreach (var key in new[] { "city", "town", "village", "municipality", "hamlet", "county" })
                            {
                                if (addressProp.TryGetProperty(key, out var cityProp))
                                {
                                    var value = cityProp.GetString() ?? string.Empty;
                                    if (!string.IsNullOrWhiteSpace(value))
                                    {
                                        candidateCity = NormalizeCityForGeocoding(value);
                                        break;
                                    }
                                }
                            }

                            if (addressProp.TryGetProperty("postcode", out var postcodeProp))
                                candidatePostalCode = NormalizePostalCodeCandidate(postcodeProp.GetString() ?? string.Empty);
                        }

                        double lat = 0;
                        double lon = 0;
                        if (item.TryGetProperty("lat", out var latProp))
                            double.TryParse(latProp.GetString() ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out lat);
                        if (item.TryGetProperty("lon", out var lonProp))
                            double.TryParse(lonProp.GetString() ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out lon);

                        if (string.IsNullOrWhiteSpace(display))
                            continue;

                        foundCandidates.Add(new GeocodeCandidate
                        {
                            DisplayName = display,
                            Type = type,
                            Lat = lat,
                            Lon = lon,
                            HouseNumber = houseNumber,
                            City = candidateCity,
                            PostalCode = candidatePostalCode,
                            SourceOrder = sourceOrder++
                        });
                    }
                    catch { }
                }

                if (string.IsNullOrWhiteSpace(desiredHouseNumber))
                {
                    if (foundCandidates.Count >= 4)
                        break;
                }
                else
                {
                    var exactMatches = foundCandidates.Count(c => CandidateMatchesHouseNumber(c, desiredHouseNumber));
                    var anyNumberMatches = foundCandidates.Count(CandidateHasHouseNumber);
                    if (exactMatches >= 2 || anyNumberMatches >= 4)
                        break;
                }
            }

            if (foundCandidates.Count == 0)
                return;

            var desiredPostal = NormalizePostalCodeCandidate(placeInfo.PostalCode ?? string.Empty);
            if (desiredPostal.Length == 5)
            {
                var postalMatches = foundCandidates
                    .Where(c => NormalizePostalCodeCandidate(c.PostalCode) == desiredPostal)
                    .ToList();

                if (postalMatches.Count > 0)
                    foundCandidates = postalMatches;
            }

            foundCandidates = foundCandidates
                .GroupBy(c =>
                    $"{Math.Round(c.Lat, 6).ToString(CultureInfo.InvariantCulture)}|{Math.Round(c.Lon, 6).ToString(CultureInfo.InvariantCulture)}|{NormalizeHouseNumberForCompare(c.HouseNumber)}|{c.Type}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderBy(c => c.SourceOrder).First())
                .OrderByDescending(c => ComputeCandidateScore(c, desiredHouseNumber, placeInfo.PostalCode ?? string.Empty, placeInfo.City ?? string.Empty))
                .ThenByDescending(c => CandidateMatchesHouseNumber(c, desiredHouseNumber))
                .ThenByDescending(c => CandidateHasHouseNumber(c))
                .ThenBy(c => c.SourceOrder)
                .Take(10)
                .ToList();

            foreach (var candidate in foundCandidates)
                candidate.MatchBadge = string.Empty;

            if (foundCandidates.Count > 0 && CandidateMatchesHouseNumber(foundCandidates[0], desiredHouseNumber))
                foundCandidates[0].MatchBadge = LangResources.ExactNumberMatchBadgeText;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _candidates.Clear();
                foreach (var candidate in foundCandidates)
                    _candidates.Add(candidate);
            });
        }
        catch
        {
            // ignore candidate fetch errors on popup flow
        }
    }

}