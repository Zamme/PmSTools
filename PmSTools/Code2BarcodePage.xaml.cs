using System.Collections.ObjectModel;
using System.Linq;
using System.Diagnostics;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;
using PmSTools.Models;
using Xamarin.Google.MLKit.Vision.Text;
using PmSTools.Resources.Languages;

namespace PmSTools;
 
public partial class Code2BarcodePage : ContentPage
{
    ObservableCollection<BarcodeItem> barcodeItems = new ObservableCollection<BarcodeItem>();
    public ObservableCollection<BarcodeItem> BarcodeItems { get { return barcodeItems; } }
    private string lastCodesToSave = "";
    private const string EditButtonText = "Edit";
    private const string SaveButtonText = "Save";
    private const string CancelButtonText = "Cancel";

    private void UpdateSavedCodesPreference()
    {
        lastCodesToSave = string.Join(",", barcodeItems.Select(item => item.Code)) + (barcodeItems.Count > 0 ? "," : "");
        Preferences.Set(SaveLoadData.LastCodesPrefKey, lastCodesToSave);
    }

    private void AddBarcodeItem(BarcodeItem newBarcodeItem)
    {
        barcodeItems.Add(newBarcodeItem);
        CodesStack.Add(BuildBarcodeCard(newBarcodeItem));
    }

    private View BuildBarcodeCard(BarcodeItem barcodeItem)
    {
        var codeLabel = new Label
        {
            Text = barcodeItem.Code,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            BackgroundColor = Colors.White,
            TextColor = Colors.Black
        };

        var codeEntry = new Entry
        {
            Text = barcodeItem.Code,
            IsVisible = false,
            BackgroundColor = Colors.White,
            TextColor = Colors.Black,
            HorizontalOptions = LayoutOptions.FillAndExpand
        };

        var editButton = new Button { Text = EditButtonText };
        var saveButton = new Button { Text = SaveButtonText, IsVisible = false };
        var cancelButton = new Button { Text = CancelButtonText, IsVisible = false };

        editButton.Clicked += (_, __) =>
        {
            codeEntry.Text = barcodeItem.Code;
            codeEntry.IsVisible = true;
            saveButton.IsVisible = true;
            cancelButton.IsVisible = true;
            codeLabel.IsVisible = false;
            editButton.IsVisible = false;
        };

        saveButton.Clicked += (_, __) =>
        {
            var newCode = (codeEntry.Text ?? string.Empty).ToUpperInvariant();
            barcodeItem.Code = newCode;
            barcodeItem.BarcodeView.Value = newCode;
            codeLabel.Text = newCode;
            codeEntry.Text = newCode;
            UpdateSavedCodesPreference();

            codeEntry.IsVisible = false;
            saveButton.IsVisible = false;
            cancelButton.IsVisible = false;
            codeLabel.IsVisible = true;
            editButton.IsVisible = true;
        };

        cancelButton.Clicked += (_, __) =>
        {
            codeEntry.Text = barcodeItem.Code;
            codeEntry.IsVisible = false;
            saveButton.IsVisible = false;
            cancelButton.IsVisible = false;
            codeLabel.IsVisible = true;
            editButton.IsVisible = true;
        };

        var actionRow = new HorizontalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.Center,
            Children = { editButton, saveButton, cancelButton }
        };

        var contentLayout = new VerticalStackLayout
        {
            Spacing = 5,
            BackgroundColor = Colors.White,
            Padding = 5
        };
        contentLayout.Add(barcodeItem.BarcodeView);
        contentLayout.Add(codeLabel);
        contentLayout.Add(codeEntry);
        contentLayout.Add(actionRow);

        return new Border
        {
            Padding = 2,
            BackgroundColor = Colors.White,
            Content = contentLayout
        };
    }
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
                        AddBarcodeItem(newBarcodeItem);
                    }
                }
            }
            UpdateSavedCodesPreference();
        }

        if (barcodeItems.Count < 1)
        {
            Label newLabel = new Label { Text = LangResources.NoPrefixFoundText,
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
                    AddBarcodeItem(newBarcodeItem);
                }
            }

            if (!hasMatchingPrefix)
            {
                LogRejectedTextPart(modTextPart, "no matching prefix");
            }

            UpdateSavedCodesPreference();
        }

        if (barcodeItems.Count < 1)
        {
            Label newLabel = new Label { Text = LangResources.NoPrefixFoundText,
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
        if (!TryGetDniLikeControlCharacter(dataPart, out char expectedControl))
        {
            return false;
        }

        return char.ToUpperInvariant(providedControl) == expectedControl;
    }

    private static bool TryAppendDniLikeControl(string dataPart, out string codeWithControl)
    {
        codeWithControl = dataPart;
        if (dataPart.Length != 22)
        {
            return false;
        }

        if (!TryGetDniLikeControlCharacter(dataPart, out char control))
        {
            return false;
        }

        codeWithControl = dataPart + control;
        return true;
    }

    private static bool TryGetDniLikeControlCharacter(string dataPart, out char control)
    {
        control = '\0';

        bool hasAtLeastOneDigit = false;
        int remainder = 0;
        foreach (char currentChar in dataPart)
        {
            if (char.IsDigit(currentChar))
            {
                hasAtLeastOneDigit = true;
                remainder = (remainder * 10 + (currentChar - '0')) % 23;
                continue;
            }

            if (!char.IsLetter(currentChar))
            {
                return false;
            }
        }

        if (!hasAtLeastOneDigit)
        {
            return false;
        }

        const string dniLetters = "TRWAGMYFPDXBNJZSQVHLCKE";
        control = dniLetters[remainder];
        return true;
    }

    private static void LogRejectedTextPart(string value, string reason)
    {
        Debug.WriteLine($"[ConstructPage] Rejected '{value}': {reason}");
    }

    private static void LogDniControlAppended(string originalValue, string normalizedValue)
    {
        Debug.WriteLine($"[ConstructPage] Appended DNI-like control: '{originalValue}' -> '{normalizedValue}'");
    }


    public Code2BarcodePage()
    {
        InitializeComponent();
    }
    
    public Code2BarcodePage(string _text, List<string> newPrefixes)
    {
        InitializeComponent();
        ConstructPage(_text, newPrefixes);
    }
}