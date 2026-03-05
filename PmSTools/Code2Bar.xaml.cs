/*
using CommunityToolkit.Maui.Core;
*/
using Plugin.Maui.OCR;
using System.IO;
using PmSTools.Models;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;
using System.Collections.ObjectModel;
using PmSTools.Resources.Languages;
using System.Diagnostics;

namespace PmSTools;

public partial class Code2Bar : ContentPage
{
    private int prefixesCount = 0;
    List<string> notiPrefixes = new List<string>();
    List<bool> notiActivePrefixes = new List<bool>();
    private string[] defaultNotiPrefixes = ["NV", "NT", "NE", "NA", "C1", "CD", "PK", "PQ", "PS", "90", "CX", "PH"];
    private bool[] defaultNotiActivePrefixes = [true, true, true, true, true, true, true, true, true, true, true, true];

    ObservableCollection<BarcodeItem> barcodeItems = new ObservableCollection<BarcodeItem>();
    public ObservableCollection<BarcodeItem> BarcodeItems { get { return barcodeItems; } }

    public Code2Bar()
    {
        InitializeComponent();
    }
    
    protected async override void OnAppearing()
    {
        base.OnAppearing();
        /*Preferences.Default.Clear();
        Preferences.Clear();*/
        if (Preferences.ContainsKey(SaveLoadData.PrefixesCountPrefName))
        {
            UpdateFromPrefixesPrefs();
        }
        else
        {
            notiPrefixes = new List<string>(defaultNotiPrefixes);
            notiActivePrefixes = new List<bool>(defaultNotiActivePrefixes);
            UpdatePrefixesPrefs();
        }
        UpdatePrefixesInfoLabel();
        
        UpdateLastCodesScroll();
    }
    
    private void UpdateFromPrefixesPrefs()
    {
        prefixesCount = Preferences.Get(SaveLoadData.PrefixesCountPrefName, 0);
        notiPrefixes.Clear();
        notiActivePrefixes.Clear();
        if (prefixesCount < 1)
        {
            DisplayAlertAsync(LangResources.ErrorTitleText, LangResources.PrefixesCountEmptyText, LangResources.OkText);
        }
        else
        {
            for (int prefixCounter = 0; prefixCounter < prefixesCount; prefixCounter++)
            {
                string currentPrefixKey = SaveLoadData.PrefixesPrefsKeyPrefix + prefixCounter.ToString();
                string currentPrefix = Preferences.Get(currentPrefixKey, "null");
                string currentActivePrefixKey = SaveLoadData.ActivePrefixesPrefsKeyPrefix + prefixCounter.ToString();
                bool currentActivePrefix = Preferences.Get(currentActivePrefixKey, true);
                if (currentPrefix == "null")
                {
                    // TODO: What to do if it doesn't find prefix pref key
                }
                else
                {
                    if (currentActivePrefix)
                    {
                        notiPrefixes.Add(currentPrefix);
                        notiActivePrefixes.Add(currentActivePrefix);
                    }
                }
            }
        }
    }

    private void UpdateLastCodesScroll()
    {
        VerticalStackLayout lastCodesStack = new VerticalStackLayout();
        lastCodesStack.Spacing = 10;
        lastCodesStack.VerticalOptions = LayoutOptions.Center;
        lastCodesStack.HorizontalOptions = LayoutOptions.Fill;
        if (Preferences.ContainsKey(SaveLoadData.LastCodesPrefKey))
        {
            string lastCodesString = Preferences.Get(SaveLoadData.LastCodesPrefKey, "");
            if (lastCodesString == "")
            {
                Label noLastCodesLabel = new Label();
                noLastCodesLabel.Text = LangResources.NoLastCodesText;
                noLastCodesLabel.VerticalOptions = LayoutOptions.Center;
                noLastCodesLabel.HorizontalOptions = LayoutOptions.Center;
                noLastCodesLabel.BackgroundColor = Colors.White;
                noLastCodesLabel.TextColor = Colors.Black;
                lastCodesStack.Add(noLastCodesLabel);
            }
            else
            {
                string _text = lastCodesString;
                char[] charSeparators = new char[] { ',' };
                string[] textParts = _text.Split(charSeparators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                foreach (var textPart in textParts)
                {
                    string modTextPart = new string(textPart.ToUpper());
                    if (modTextPart.Length == 22 && TryAppendDniLikeControl(modTextPart, out string codeWithControl))
                    {
                        Debug.WriteLine($"[UpdateLastCodesScroll] Appended DNI-like control: '{modTextPart}' -> '{codeWithControl}'");
                        modTextPart = codeWithControl;
                    }

                    foreach (var notiPrefix in notiPrefixes)
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
                                        HorizontalOptions = LayoutOptions.Fill,
                                        HeightRequest = 60,
                                        Value = modTextPart
                                    }
                                };
                                barcodeItems.Add(newBarcodeItem);

                                Label newLabel = new Label { Text = newBarcodeItem.Code,
                                    VerticalOptions=LayoutOptions.Center, 
                                    HorizontalOptions=LayoutOptions.Center,
                                    BackgroundColor=Colors.White,
                                    TextColor=Colors.Black };
                                Grid newHSL = new Grid()
                                {
                                    RowDefinitions =
                                    {
                                        new RowDefinition(),
                                        new RowDefinition(GridLength.Auto)
                                    },
                                    ColumnDefinitions =
                                    {
                                        new ColumnDefinition(GridLength.Star),
                                        new ColumnDefinition(GridLength.Auto)
                                    },
                                    BackgroundColor = Colors.White,
                                    HorizontalOptions = LayoutOptions.Fill,
                                    Padding = 5
                                };
                                VerticalStackLayout newVSL = new VerticalStackLayout
                                {
                                    Spacing = 5,
                                    BackgroundColor = Colors.White,
                                    HorizontalOptions = LayoutOptions.Fill,
                                    Padding = 5
                                };
                                newVSL.Add(newBarcodeItem.BarcodeView);
                                newVSL.Add(newLabel);
                                if (!SaveLoadData.IsCodeSaved(newBarcodeItem.Code))
                                {
                                    Button newSaveCodeButton = new Button
                                    {
                                        Text = LangResources.SaveText,
                                        HorizontalOptions = LayoutOptions.Fill
                                    };
                                    newSaveCodeButton.Clicked += async (sender, args) =>
                                        OnSaveCodeButtonClick(sender, args, modTextPart);
                                    newVSL.Add(newSaveCodeButton);
                                }
                                else
                                {
                                    Label newCodeSavedLabel = new Label
                                    {
                                        Text = LangResources.SavedText,
                                        HorizontalOptions = LayoutOptions.Center,
                                        BackgroundColor = Colors.White,
                                        TextColor = Colors.Green,
                                        FontAttributes = FontAttributes.Bold,
                                        FontSize = 18
                                    };
                                    newVSL.Add(newCodeSavedLabel);
                                }

                                newHSL.Add(newVSL, 0, 0);
                                
                                Border newBorder = new Border
                                {
                                    Padding = 2,
                                    BackgroundColor = Colors.White,
                                    HorizontalOptions = LayoutOptions.Fill,
                                    Content = newHSL
                                };
                                lastCodesStack.Add(newBorder);
                            }
                        }
                    }
                }

                if (barcodeItems.Count < 1)
                {
                    Label newLabel = new Label { Text = LangResources.NoLastCodesText,
                        VerticalOptions=LayoutOptions.Center, 
                        HorizontalOptions=LayoutOptions.Center,
                        BackgroundColor=Colors.White,
                        TextColor=Colors.Black };
                    lastCodesStack.Add(newLabel);
                }
            }
        }
        else
        {
            Label noLastCodesLabel = new Label();
            noLastCodesLabel.Text = LangResources.NoLastCodesText;
            noLastCodesLabel.VerticalOptions = LayoutOptions.Center;
            noLastCodesLabel.HorizontalOptions = LayoutOptions.Center;
            noLastCodesLabel.BackgroundColor = Colors.White;
            noLastCodesLabel.TextColor = Colors.Black;
            lastCodesStack.Add(noLastCodesLabel);
        }
        LastCodesScroll.Content = lastCodesStack;
    }

    private void OnSaveCodeButtonClick(object? sender, EventArgs args, string _text)
    {
        SaveLoadData.SaveCode(_text.ToString());
        UpdateLastCodesScroll();
    }

    private void UpdatePrefixesPrefs()
    {
        SaveLoadData.CleanPrefixesPrefs();
        SaveLoadData.CleanActivePrefixesPrefs();
        SaveLoadData.SavePrefixesPrefs(notiPrefixes);
        SaveLoadData.SaveActivePrefixesPrefs(notiActivePrefixes);
    }
        
    private async void OnReadCameraOcrClicked()
    {
        try
        {
            var pickResult = await MediaPicker.Default.CapturePhotoAsync();
                
            if (pickResult != null)
            {
                using var imageAsStream = await pickResult.OpenReadAsync();
                using var memoryStream = new MemoryStream();
                await imageAsStream.CopyToAsync(memoryStream);
                var imageAsBytes = memoryStream.ToArray();

                var ocrResult = await OcrPlugin.Default.RecognizeTextAsync(imageAsBytes);

                if (!ocrResult.Success)
                {
                    await DisplayAlertAsync(LangResources.NoSuccessTitleText, LangResources.NoOcrPossibleText, LangResources.OkText);
                    return;
                }

                /*await DisplayAlert("OCR Result", ocrResult.AllText, "OK");*/
                await Navigation.PushAsync(new Code2BarcodePopupPage(ocrResult.AllText, notiPrefixes));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(LangResources.ErrorTitleText, ex.Message, LangResources.OkText);
        }
    }

    private void OnBackHomeFromCode2BarClicked(object? sender, EventArgs e)
    {
        Navigation.PopAsync();
    }

    protected async void InitOcrAsync()
    {
        await OcrPlugin.Default.InitAsync();
    }
    private void OnTakeCodeFromCameraButtonClicked(object? sender, EventArgs e)
    {
        InitOcrAsync();
        OnReadCameraOcrClicked();
    }

    private void PrefixesMenuItem_OnClicked(object? sender, EventArgs e)
    {
        MauiPopup.PopupAction.DisplayPopup(new PrefixesPopupPage());
    }

    private void UpdatePrefixesInfoLabel()
    {
        var prefixesText = string.Join(", ", notiPrefixes);
        PrefixesInfoLabel.Text = string.Format(LangResources.PrefixesLabelFormatText, prefixesText);
        /*PrefixesInfoLabel.Text = prefixesCount.ToString();*/
    }

    private void SavedMenuItem_OnClicked(object? sender, EventArgs e)
    {
        MauiPopup.PopupAction.DisplayPopup(new SavedBarcodesPopupPage());
    }
    
    private async void OnTakeCodeFromFileButtonClicked(object? sender, EventArgs e)
    {
        var result = await FilePicker.Default.PickAsync(PickOptions.Images);
        if (result != null)
        {
            await OcrPlugin.Default.InitAsync();
            using var stream = await result.OpenReadAsync();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            var imageAsBytes = memoryStream.ToArray();
            var ocrResult = await OcrPlugin.Default.RecognizeTextAsync(imageAsBytes);

            if (!ocrResult.Success)
            {
                await DisplayAlertAsync(LangResources.NoSuccessTitleText, LangResources.NoOcrPossibleText, LangResources.OkText);
                return;
            }

            await Navigation.PushAsync(new Code2BarcodePopupPage(ocrResult.AllText, notiPrefixes));
        }
        else
        {
            await DisplayAlertAsync(LangResources.ResultErrorTitleText, LangResources.ResultIsNullText, LangResources.OkText);
        }
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
}