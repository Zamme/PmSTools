using System.Collections.ObjectModel;
using System.Diagnostics;
using MauiPopup.Views;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;
using PmSTools.Models;
using Xamarin.Google.MLKit.Vision.Text;

namespace PmSTools;
 
public partial class Code2BarcodePopupPage : BasePopupPage
{
    // TODO : Refer tot el tema de validació de codis a un servei separat, per tenir el codi més net i poder-lo reutilitzar en altres parts de l'app si cal
    // TODO : Hi ha codis que no agafa. Mira-ho amb arxiu que tens de proves.
    const string NoPrefixFound = "No prefix found";
    ObservableCollection<BarcodeItem> barcodeItems = new ObservableCollection<BarcodeItem>();
    public ObservableCollection<BarcodeItem> BarcodeItems { get { return barcodeItems; } }
    private string lastCodesToSave = "";
    public void ConstructPageOld(string _text, List<string> newPrefixes)
    {
        char[] charSeparators = new char[] { ' ', '\n' };
        string[] textParts = _text.Split(charSeparators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var textPart in textParts)
        {
            string modTextPart = new string(textPart.ToUpper());
            foreach (var notiPrefix in newPrefixes)
            {
                modTextPart = modTextPart.Replace("O", "0");
                if (modTextPart.Length > 8)
                {
                    if (modTextPart.StartsWith(notiPrefix))
                    {
                        BarcodeItem newBarcodeItem = new BarcodeItem
                        {
                            Code = modTextPart,
                            BarcodeView = new BarcodeGeneratorView
                            {
                                Format = BarcodeFormat.Code39,
                                ForegroundColor = Colors.Black,
                                BackgroundColor = Colors.White,
                                WidthRequest = 400,
                                HeightRequest = 100,
                                Value = modTextPart
                            }
                        };
                        barcodeItems.Add(newBarcodeItem);

                        Label newLabel = new Label { Text = newBarcodeItem.Code,
                            VerticalOptions=LayoutOptions.Center, 
                            HorizontalOptions=LayoutOptions.Center,
                            BackgroundColor=Colors.White,
                            TextColor=Colors.Black };
                        VerticalStackLayout newVSL = new VerticalStackLayout
                        {
                            Spacing = 5,
                            BackgroundColor = Colors.White,
                            Padding = 5
                        };
                        newVSL.Add(newBarcodeItem.BarcodeView);
                        newVSL.Add(newLabel);
                        Border newBorder = new Border
                        {
                            Padding = 2,
                            BackgroundColor = Colors.White,
                            Content = newVSL
                        };
                        CodesStack.Add(newBorder);
                        lastCodesToSave += modTextPart + ",";
                    }
                }
            }
            Preferences.Set(SaveLoadData.LastCodesPrefKey, lastCodesToSave);
        }

        if (barcodeItems.Count < 1)
        {
            Label newLabel = new Label { Text = NoPrefixFound,
                VerticalOptions=LayoutOptions.Center, 
                HorizontalOptions=LayoutOptions.Center,
                BackgroundColor=Colors.White,
                TextColor=Colors.Black };
            CodesStack.Add(newLabel);
        }
        /*BarcodesCollectionView.ItemsSource = barcodeItems;*/
        /*string codeFound = new string(BarcodeItems[0].Code);
        BarcodeImage.Value = codeFound;
        CodiString.Text = codeFound;*/
    }
    
    public void ConstructPage(string _text, List<string> newPrefixes)
    {
        const int ShortValidLength = 13;
        const int LongValidLength = 23;
        char[] charSeparators = new char[] { ' ', '\n' };
        string[] textParts = _text.Split(charSeparators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var textPart in textParts)
        {
            string upperTextPart = textPart.ToUpper();
            string modTextPart = upperTextPart.Replace("O", "0");
            bool isShortCode = modTextPart.Length == ShortValidLength;
            bool isLongCode = modTextPart.Length == LongValidLength;
            bool hasValidLength = isShortCode || isLongCode;
            if (!hasValidLength)
            {
                LogRejectedTextPart(modTextPart, $"invalid length ({modTextPart.Length})");
                continue;
            }

            bool startsWithTwoLetters = upperTextPart.Length >= 2 && char.IsLetter(upperTextPart[0]) && char.IsLetter(upperTextPart[1]);
            bool startsWith90 = modTextPart.StartsWith("90");
            if (isShortCode && !startsWithTwoLetters)
            {
                LogRejectedTextPart(modTextPart, "13-char code does not start with two letters");
                continue;
            }

            if (isLongCode && !startsWithTwoLetters && !startsWith90)
            {
                LogRejectedTextPart(modTextPart, "23-char code does not start with two letters or 90");
                continue;
            }

            bool requiresDniControl = isLongCode && char.IsLetter(upperTextPart[^1]);
            if (requiresDniControl && !HasValidDniLikeControl(modTextPart))
            {
                LogRejectedTextPart(modTextPart, "invalid DNI-like control character");
                continue;
            }

            bool hasMatchingPrefix = false;
            foreach (var notiPrefix in newPrefixes)
            {
                if (modTextPart.StartsWith(notiPrefix))
                {
                    hasMatchingPrefix = true;
                    BarcodeItem newBarcodeItem = new BarcodeItem
                    {
                        Code = modTextPart,
                        BarcodeView = new BarcodeGeneratorView
                        {
                            Format = BarcodeFormat.Code39,
                            ForegroundColor = Colors.Black,
                            BackgroundColor = Colors.White,
                            WidthRequest = 400,
                            HeightRequest = 100,
                            Value = modTextPart
                        }
                    };
                    barcodeItems.Add(newBarcodeItem);

                    Label newLabel = new Label { Text = newBarcodeItem.Code,
                        VerticalOptions=LayoutOptions.Center, 
                        HorizontalOptions=LayoutOptions.Center,
                        BackgroundColor=Colors.White,
                        TextColor=Colors.Black };
                    VerticalStackLayout newVSL = new VerticalStackLayout
                    {
                        Spacing = 5,
                        BackgroundColor = Colors.White,
                        Padding = 5
                    };
                    newVSL.Add(newBarcodeItem.BarcodeView);
                    newVSL.Add(newLabel);
                    Border newBorder = new Border
                    {
                        Padding = 2,
                        BackgroundColor = Colors.White,
                        Content = newVSL
                    };
                    CodesStack.Add(newBorder);
                    lastCodesToSave += modTextPart + ",";
                }
            }

            if (!hasMatchingPrefix)
            {
                LogRejectedTextPart(modTextPart, "no matching prefix");
            }

            Preferences.Set(SaveLoadData.LastCodesPrefKey, lastCodesToSave);
        }

        if (barcodeItems.Count < 1)
        {
            Label newLabel = new Label { Text = NoPrefixFound,
                VerticalOptions=LayoutOptions.Center, 
                HorizontalOptions=LayoutOptions.Center,
                BackgroundColor=Colors.White,
                TextColor=Colors.Black };
            CodesStack.Add(newLabel);
        }
        /*BarcodesCollectionView.ItemsSource = barcodeItems;*/
        /*string codeFound = new string(BarcodeItems[0].Code);
        BarcodeImage.Value = codeFound;
        CodiString.Text = codeFound;*/
    }

    private static bool HasValidDniLikeControl(string modTextPart)
    {
        if (modTextPart.Length != 23)
        {
            return true;
        }

        char providedControl = modTextPart[^1];
        if (!char.IsLetter(providedControl))
        {
            return false;
        }

        string dataPart = modTextPart[..^1];
        int remainder = 0;
        foreach (char currentChar in dataPart)
        {
            if (!char.IsDigit(currentChar))
            {
                return false;
            }

            remainder = (remainder * 10 + (currentChar - '0')) % 23;
        }

        const string dniLetters = "TRWAGMYFPDXBNJZSQVHLCKE";
        char expectedControl = dniLetters[remainder];
        return char.ToUpperInvariant(providedControl) == expectedControl;
    }

    private static void LogRejectedTextPart(string value, string reason)
    {
        Debug.WriteLine($"[ConstructPage] Rejected '{value}': {reason}");
    }


    public Code2BarcodePopupPage()
    {
        InitializeComponent();
    }
    
    public Code2BarcodePopupPage(string _text, List<string> newPrefixes)
    {
        InitializeComponent();
        ConstructPage(_text, newPrefixes);
    }
}