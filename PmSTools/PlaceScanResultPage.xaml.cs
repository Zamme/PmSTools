using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PmSTools.Models;

namespace PmSTools;

public partial class PlaceScanResultPage : ContentPage
{
    public PlaceScanResultPage()
    {
        InitializeComponent();
    }

    public PlaceScanResultPage(string ocrResult)
    {
        InitializeComponent();
        FillPageWithMap(ocrResult);
    }

    private async void FillPageWithMap(string ocrResult)
    {
        try
        {
            PlaceInfoItem filteredLines = FilterScanResult(ocrResult);
            if (filteredLines == null)
                return;
            
            // Set UI fields first - critical
            try
            {
                NameResultText.Text = filteredLines.Name ?? "Unknown Name";
                StreetResultText.Text = filteredLines.Street ?? "Unknown Street";
                PostalCodeResultText.Text = filteredLines.PostalCode ?? "Unknown Postal Code";
                CityResultText.Text = filteredLines.City ?? "Unknown City";
                CountryResultText.Text = filteredLines.Country ?? "Unknown Country";
            }
            catch (Exception uiEx)
            {
                System.Diagnostics.Debug.WriteLine($"UI update error: {uiEx.Message}");
            }
            
            // Try to show map centered on city/street (wrapped in try-catch to prevent startup crash)
            try
            {
                // Request location permission before geocoding
                await RequestLocationPermission();
                await ShowMapForLocation(filteredLines);
            }
            catch (Exception mapEx)
            {
                System.Diagnostics.Debug.WriteLine($"Map initialization error: {mapEx.Message}\n{mapEx.StackTrace}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FillPageWithMap error: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private async Task RequestLocationPermission()
    {
        // Location permission not required for OpenStreetMap (no Google API)
        System.Diagnostics.Debug.WriteLine("OpenStreetMap loaded - no location permission needed");
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
                System.Diagnostics.Debug.WriteLine("OsmMap control not found");
                return;
            }

            // Generate OpenStreetMap HTML with Leaflet
            string htmlContent = GenerateOpenStreetMapHtml(filteredLines);
            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    osmMap.Source = new HtmlWebViewSource { Html = htmlContent };
                    System.Diagnostics.Debug.WriteLine($"OpenStreetMap loaded for {filteredLines.Name ?? "Place"}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Map update error: {ex.Message}\n{ex.StackTrace}");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ShowMapForLocation error: {ex.Message}\n{ex.StackTrace}");
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
            "        body { margin: 0; padding: 0; }\n" +
            "        #map { position: absolute; top: 0; bottom: 0; width: 100%; }\n" +
            "        .info { padding: 10px; background: white; border-radius: 5px; box-shadow: 0 0 15px rgba(0,0,0,0.2); }\n" +
            "    </style>\n" +
            "</head>\n" +
            "<body>\n" +
            "    <div id='map'></" + "div>\n" +
            "    <script>\n" +
            "        var map = L.map('map').setView([40, 0], 4);\n" +
            "        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {\n" +
            "            attribution: '© OpenStreetMap contributors',\n" +
            "            maxZoom: 19\n" +
            "        }).addTo(map);\n" +
            "        \n" +
            "        var address = \"" + escapedAddress + "\";\n" +
            "        var label = \"" + escapedLabel + "\";\n" +
            "        \n" +
            "        fetch('https://nominatim.openstreetmap.org/search?format=json&q=' + encodeURIComponent(address))\n" +
            "            .then(response => response.json())\n" +
            "            .then(data => {\n" +
            "                if (data.length > 0) {\n" +
            "                    var lat = parseFloat(data[0].lat);\n" +
            "                    var lon = parseFloat(data[0].lon);\n" +
            "                    var bounds = L.latLngBounds(\n" +
            "                        [parseFloat(data[0].boundingbox[0]), parseFloat(data[0].boundingbox[2])],\n" +
            "                        [parseFloat(data[0].boundingbox[1]), parseFloat(data[0].boundingbox[3])]\n" +
            "                    );\n" +
            "                    map.fitBounds(bounds, { padding: [50, 50] });\n" +
            "                    L.marker([lat, lon]).addTo(map)\n" +
            "                        .bindPopup('<div class=\"info\"><strong>' + label + '</strong><br/>' + address + '</div>')\n" +
            "                        .openPopup();\n" +
            "                }\n" +
            "            })\n" +
            "            .catch(error => console.log('Geocoding error:', error));\n" +
            "    </script>\n" +
            "</body>\n" +
            "</html>";
        
        return html;
    }

    private PlaceInfoItem FilterScanResult(string ocrResult)
    {
        // Lines : 0 = Name, 1 = Street, 2 = Postal Code, 3 = City, 4 = Country
        PlaceInfoItem placeInfoItem = new PlaceInfoItem();

        List<string> ocrResultLines = ocrResult.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        
        string postalCode = "";
        int postalCodeLineIndex = -1;
        for(int i = 0; i < ocrResultLines.Count; i++)
        {
            string line = ocrResultLines[i];

            // Searching by postal code, as it is the most likely to be correctly recognized by OCR and can be used to determine the position of other information
            if (Contains5DigitNumber(line))
            {
                System.Diagnostics.Debug.WriteLine("Found postal code: " + line);
                postalCode = line;
                postalCodeLineIndex = i;
                break;
            }
        }

        if (postalCode != "")
        {
            if (postalCodeLineIndex >= 2)
            {
                placeInfoItem.Name = ocrResultLines[postalCodeLineIndex - 2];
            }
            if (postalCodeLineIndex >= 1)
            {
                placeInfoItem.Street = ocrResultLines[postalCodeLineIndex - 1];
            }
            if (postalCodeLineIndex < ocrResultLines.Count)
            {
                string postalCodeLine = ocrResultLines[postalCodeLineIndex];
                string[] postalCodeLineParts = postalCodeLine.Split(' ');
                if (postalCodeLineParts.Length > 1)
                {                    
                    placeInfoItem.PostalCode = postalCodeLineParts[0];
                }
                else
                {
                    placeInfoItem.PostalCode = postalCodeLine;
                }
            }
            if (postalCodeLineIndex + 1 < ocrResultLines.Count)
            {                
                placeInfoItem.City = ocrResultLines[postalCodeLineIndex + 1];
            }
            else
            {
                // City could be in the same line as postal code
                string postalCodeLine = ocrResultLines[postalCodeLineIndex];
                if (!string.IsNullOrEmpty(placeInfoItem.PostalCode))
                {
                    postalCodeLine = postalCodeLine.Replace(placeInfoItem.PostalCode, "").Trim();
                }
                if (postalCodeLine != "")
                {
                    placeInfoItem.City = postalCodeLine;
                }
                else
                {
                    placeInfoItem.City = "Unknown City";
                }
            }
            if (postalCodeLineIndex + 2 < ocrResultLines.Count)
            {                
                placeInfoItem.Country = ocrResultLines[postalCodeLineIndex + 2];
            }
            else
            {
                placeInfoItem.Country = "Spain";
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("No postal code found in OCR result.");
        }
        
        return placeInfoItem;
    }

    private bool Contains5DigitNumber(string str)
    {
        System.Diagnostics.Debug.WriteLine("Checking if line contains a 5-digit number: " + str);
        bool contains5DigitNumber = System.Text.RegularExpressions.Regex.IsMatch(str, @"\d{5}");
        System.Diagnostics.Debug.WriteLine("Contains 5-digit number: " + contains5DigitNumber);
        return contains5DigitNumber;
    }

    private void OnBackClicked(object sender, EventArgs e)
    {
        Navigation.PopAsync();
    }
}
