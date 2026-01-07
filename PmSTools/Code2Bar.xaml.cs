using CommunityToolkit.Maui.Core;
using Plugin.Maui.OCR;

namespace PmSTools;

public partial class Code2Bar : ContentPage
{
    private string prefixesPrefsKeyPrefix = "c2bp_";
    private int prefixesCount = -1;
    string[] defaultNotiPrefixes = ["NV", "NT", "NE", "NA", "C1", "CD", "PK", "PQ", "PS", "90", "CX", "PH"];
    string[] notiPrefixes = [];

    public Code2Bar()
    {
        InitializeComponent();
    }
    
    protected async override void OnAppearing()
    {
        base.OnAppearing();
        if (notiPrefixes.Length == 0)
        {
            if (Preferences.ContainsKey("prefixes_count"))
            {
                UpdateFromPrefixesPrefs();
            }
            else
            {
                notiPrefixes = defaultNotiPrefixes;
                UpdatePrefixesPrefs();
            }
            UpdatePrefixesInfoLabel();
        }
        else
        {
            
        }
    }

    private void UpdateFromPrefixesPrefs()
    {
        prefixesCount = Preferences.Get("prefixes_count", -1);
        if (prefixesCount < 1)
        {
            DisplayAlertAsync("Error", "Prefixes count is empty", "OK");
        }
        else
        {
            for (int prefixCounter = 0; prefixCounter < prefixesCount; prefixCounter++)
            {
                string currentPrefixKey = prefixesPrefsKeyPrefix + prefixCounter;
                string currentPrefix = Preferences.Get(currentPrefixKey, "null");
                if (currentPrefix == "null")
                {
                    // TODO: What to do if it doesn't find prefix pref key
                }
                else
                {
                    notiPrefixes.Append(currentPrefix);
                }
            }
        }
    }
    private void UpdatePrefixesPrefs()
    {
        prefixesCount = notiPrefixes.Length;
        Preferences.Set("prefixes_count", prefixesCount);
        int prefixCounter = -1;
        foreach (string prefix in notiPrefixes)
        {
            prefixCounter++;
            string prefixKey = prefixesPrefsKeyPrefix + prefixCounter.ToString();
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
        MauiPopup.PopupAction.DisplayPopup(new PrefixesPopupPage());
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
    }
}