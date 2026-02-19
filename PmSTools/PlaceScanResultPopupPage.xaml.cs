using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MauiPopup.Views;
using SkiaSharp.Views.Maui.Controls.Hosting;
using PmSTools.Models;


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
    private PlaceInfoItem? _currentPlace;
    private bool _isEditMode;

    public PlaceScanResultPopupPage()
    {
        InitializeComponent();
    }

    public PlaceScanResultPopupPage(string ocrResult)
    {
        InitializeComponent();
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

            SaveLoadData.SaveLastPlaceInfo(filteredLines);
        }
        catch (Exception)
        {
            // System.Diagnostics.Debug.WriteLine($"FillPage error: {ex.Message}");
        }
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
            UpdateResultLabels(_currentPlace);
            PopulateEditFields(_currentPlace);
            SaveLoadData.SaveLastPlaceInfo(_currentPlace);

            SetEditMode(false);
            await DisplayAlertAsync("Saved", "Manual corrections applied.", "OK");
        }
        catch
        {
            await DisplayAlertAsync("Error", "Could not apply manual corrections.", "OK");
        }
    }

    private PlaceInfoItem FilterScanResult(string ocrResult)
    {
        // Lines : 0 = Name, 1 = Street, 2 = Postal Code, 3 = City, 4 = Country
        List<string> retLines = new List<string>();
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
                // System.Diagnostics.Debug.WriteLine("Found postal code: " + line);
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
                    placeInfoItem.PostalCode = postalCodeLine; // If the line only contains the postal code, we take it as is
                }
            }
            if (postalCodeLineIndex + 1 < ocrResultLines.Count)
            {                
                placeInfoItem.City = ocrResultLines[postalCodeLineIndex + 1];
            }
            else
            {
                // City could be in the same line as postal code, if the OCR result is not very good, so we try to extract it from the postal code line
                string postalCodeLine = ocrResultLines[postalCodeLineIndex];
                if (!string.IsNullOrEmpty(placeInfoItem.PostalCode))
                {
                    postalCodeLine = postalCodeLine.Replace(placeInfoItem.PostalCode, "").Trim(); // Remove postal code from line to try to extract city
                }
                if (postalCodeLine != "")
                {
                    placeInfoItem.City = postalCodeLine; // If there is still some text left after removing postal code, we take it as city
                }
                else
                {
                    placeInfoItem.City = "Unknown City"; // Default city if not found in OCR result
                }
            }
            if (postalCodeLineIndex + 2 < ocrResultLines.Count)
            {                
                placeInfoItem.Country = ocrResultLines[postalCodeLineIndex + 2];
            }
            else
            {
                placeInfoItem.Country = "Spain"; // Default country if not found in OCR result, as the app is currently only used in Spain
            }
        }
        else
        {
            // System.Diagnostics.Debug.WriteLine("No postal code found in OCR result.");
        }
        
        return placeInfoItem;
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

}