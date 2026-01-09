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
    private const string PrefixesCountPrefName = "prefixes_count";
    private const string PrefixesPrefsKeyPrefix = "c2bp_";
    
    public static string LastCodesPrefKey = "last_codes";

    private int prefixesCount = 0;
    private string[] defaultNotiPrefixes = ["NV", "NT", "NE", "NA", "C1", "CD", "PK", "PQ", "PS", "90", "CX", "PH"];
    List<string> notiPrefixes = new List<string>();

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
        if (Preferences.ContainsKey(PrefixesCountPrefName))
        {
            UpdateFromPrefixesPrefs();
        }
        else
        {
            notiPrefixes = new List<string>(defaultNotiPrefixes);
            UpdatePrefixesPrefs();
        }
        UpdatePrefixesInfoLabel();
        
        UpdateLastCodesScroll();
    }

    private void UpdateFromPrefixesPrefs()
    {
        prefixesCount = Preferences.Get(PrefixesCountPrefName, 0);
        notiPrefixes.Clear();
        if (prefixesCount < 1)
        {
            DisplayAlertAsync("Error", "Prefixes count is empty", "OK");
        }
        else
        {
            for (int prefixCounter = 0; prefixCounter < prefixesCount; prefixCounter++)
            {
                string currentPrefixKey = PrefixesPrefsKeyPrefix + prefixCounter.ToString();
                string currentPrefix = Preferences.Get(currentPrefixKey, "null");
                if (currentPrefix == "null")
                {
                    // TODO: What to do if it doesn't find prefix pref key
                }
                else
                {
                    notiPrefixes.Add(currentPrefix);
                }
            }
        }
    }

    private void UpdateLastCodesScroll()
    {
        VerticalStackLayout lastCodesStack = new VerticalStackLayout();
        lastCodesStack.Spacing = 20;
        lastCodesStack.VerticalOptions = LayoutOptions.Center;
        lastCodesStack.HorizontalOptions = LayoutOptions.Center;
        if (Preferences.ContainsKey(LastCodesPrefKey))
        {
            string lastCodesString = Preferences.Get(LastCodesPrefKey, "");
            if (lastCodesString != "")
            {
                // TODO : last codes key with no last codes
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
                                lastCodesStack.Add(newBorder);
                            }
                        }
                    }
                }

                if (barcodeItems.Count < 1)
                {
                    Label newLabel = new Label { Text = "ERROR",
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
            noLastCodesLabel.Text = "No last codes.";
            noLastCodesLabel.VerticalOptions = LayoutOptions.Center;
            noLastCodesLabel.HorizontalOptions = LayoutOptions.Center;
            noLastCodesLabel.BackgroundColor = Colors.White;
            noLastCodesLabel.TextColor = Colors.Black;
            lastCodesStack.Add(noLastCodesLabel);
        }
        LastCodesScroll.Content = lastCodesStack;
    }
    private void UpdatePrefixesPrefs()
    {
        prefixesCount = notiPrefixes.Count;
        Preferences.Set(PrefixesCountPrefName, prefixesCount);
        int prefixCounter = -1;
        foreach (string prefix in notiPrefixes)
        {
            prefixCounter++;
            string prefixKey = PrefixesPrefsKeyPrefix + prefixCounter.ToString();
            Preferences.Set(prefixKey, prefix);
        }
        // TODO: Clean onwards prefixes!
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
                MauiPopup.PopupAction.DisplayPopup(new PopupPage(ocrResult.AllText, notiPrefixes));
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
        MauiPopup.PopupAction.DisplayPopup(new PrefixesPopupPage(notiPrefixes));
    }

    private void UpdatePrefixesInfoLabel()
    {
        string infoLabelText = "Prefixes: ";
        foreach (var notiPrefix in notiPrefixes)
        {
            infoLabelText += notiPrefix;
            infoLabelText += ", ";
        }
        PrefixesInfoLabel.Text = infoLabelText;
        /*PrefixesInfoLabel.Text = prefixesCount.ToString();*/
    }
}