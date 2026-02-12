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
        PlaceInfoItem filteredLines = FilterScanResult(ocrResult);
        if (filteredLines == null)
            return;
        NameResultText.Text = filteredLines.Name;
        if (filteredLines.Street != null)
        {
            StreetResultText.Text = filteredLines.Street;
        }
        if (filteredLines.PostalCode != null)
        {
            PostalCodeResultText.Text = filteredLines.PostalCode;
        }
        if (filteredLines.City != null)
        {
            CityResultText.Text = filteredLines.City;
        }
        if (filteredLines.Country != null)
        {
            CountryResultText.Text = filteredLines.Country;
        }
    }

    private void FillPageWithMap(string ocrResult)
    {
        PlaceInfoItem filteredLines = FilterScanResult(ocrResult);
        if (filteredLines == null)
            return;
        
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
                System.Diagnostics.Debug.WriteLine("Found postal code: " + line);
                postalCode = line;
                postalCodeLineIndex = i;
                break;
            }
                
        }

        if (postalCode != "")
        {
            if (ocrResultLines.Count > 2)
            {
                placeInfoItem.Name = ocrResultLines[postalCodeLineIndex - 2];
            }
            if (ocrResultLines.Count > 1)
            {
                placeInfoItem.Street = ocrResultLines[postalCodeLineIndex - 1];
            }
            if (ocrResultLines.Count > postalCodeLineIndex)
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
            if (ocrResultLines.Count > postalCodeLineIndex + 1)
            {                
                placeInfoItem.City = ocrResultLines[postalCodeLineIndex + 1];
            }
            else
            {
                // City could be in the same line as postal code, if the OCR result is not very good, so we try to extract it from the postal code line
                string postalCodeLine = ocrResultLines[postalCodeLineIndex];
                postalCodeLine = postalCodeLine.Replace(placeInfoItem.PostalCode, "").Trim(); // Remove postal code from line to try to extract city
                if (postalCodeLine != "")
                {
                    placeInfoItem.City = postalCodeLine; // If there is still some text left after removing postal code, we take it as city
                }
                else
                {
                    placeInfoItem.City = "Unknown City"; // Default city if not found in OCR result
                }
            }
            if (ocrResultLines.Count > postalCodeLineIndex + 2)
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

    private bool Is5DigitNumber(string str)
    {
        System.Diagnostics.Debug.WriteLine("Checking if line is a 5-digit number: " + str);
        return str.Length == 5 && int.TryParse(str, out _);
    }

}