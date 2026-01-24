/*
using CommunityToolkit.Maui.Core;
*/
using Plugin.Maui.OCR;
using PmSTools.Models;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;
using System.Collections.ObjectModel;

namespace PmSTools;

public partial class Code2Bar : ContentPage
{
    private const string NoLastCodes = "No last codes";
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
            DisplayAlertAsync("Error", "Prefixes count is empty", "OK");
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
                noLastCodesLabel.Text = NoLastCodes;
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
                                
                                Button newSaveCodeButton = new Button
                                {
                                    Text = "S",
                                    HorizontalOptions = LayoutOptions.Fill
                                };
                                newSaveCodeButton.Clicked += async (sender, args) => OnSaveCodeButtonClick(sender, args, modTextPart);
                                newHSL.Add(newVSL, 0, 0);
                                newHSL.Add(newSaveCodeButton, 1, 0);

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
                    Label newLabel = new Label { Text = NoLastCodes,
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
            noLastCodesLabel.Text = NoLastCodes;
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
    }

    private void UpdatePrefixesPrefs()
    {
        SaveLoadData.CleanPrefixesPrefs();
        SaveLoadData.CleanActivePrefixesPrefs();
        SaveLoadData.SavePrefixesPrefs(notiPrefixes);
        SaveLoadData.SaveActivePrefixesPrefs(notiActivePrefixes);
    }
        
    private async void OnReadocrClicked()
    {
        try
        {
            var pickResult = await MediaPicker.Default.CapturePhotoAsync();
                
            if (pickResult != null)
            {
                using var imageAsStream = await pickResult.OpenReadAsync();
                var imageAsBytes = new byte[imageAsStream.Length];
                await imageAsStream.ReadAsync(imageAsBytes);

                var ocrResult = await OcrPlugin.Default.RecognizeTextAsync(imageAsBytes);

                if (!ocrResult.Success)
                {
                    await DisplayAlert("No success", "No OCR possible", "OK");
                    return;
                }

                /*await DisplayAlert("OCR Result", ocrResult.AllText, "OK");*/
                MauiPopup.PopupAction.DisplayPopup(new Code2BarcodePopupPage(ocrResult.AllText, notiPrefixes));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
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
    private void OnScanNewCodeButtonClicked(object? sender, EventArgs e)
    {
        InitOcrAsync();
        OnReadocrClicked();
    }

    private void PrefixesMenuItem_OnClicked(object? sender, EventArgs e)
    {
        MauiPopup.PopupAction.DisplayPopup(new PrefixesPopupPage());
    }

    private void UpdatePrefixesInfoLabel()
    {
        string infoLabelText = "Prefixes: ";
        int pendingPrefixes = notiPrefixes.Count;
        foreach (var notiPrefix in notiPrefixes)
        {
            infoLabelText += notiPrefix;
            if (pendingPrefixes > 1)
            {
                infoLabelText += ", ";
                pendingPrefixes--;
            }
        }
        PrefixesInfoLabel.Text = infoLabelText;
        /*PrefixesInfoLabel.Text = prefixesCount.ToString();*/
    }

    private void SavedMenuItem_OnClicked(object? sender, EventArgs e)
    {
        MauiPopup.PopupAction.DisplayPopup(new SavedBarcodesPopupPage());
    }
}