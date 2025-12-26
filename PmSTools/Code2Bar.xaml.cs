using CommunityToolkit.Maui.Core;
using Plugin.Maui.OCR;

namespace PmSTools;

public partial class Code2Bar : ContentPage
{
    public Code2Bar()
    {
        InitializeComponent();
    }
    
    protected async override void OnAppearing()
    {
        base.OnAppearing();

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
                MauiPopup.PopupAction.DisplayPopup(new PopupPage(ocrResult.AllText));
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
}