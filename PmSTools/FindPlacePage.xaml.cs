using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Plugin.Maui.OCR;

namespace PmSTools;

public partial class FindPlacePage : ContentPage
{
    public FindPlacePage()
    {
        InitializeComponent();
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
                MauiPopup.PopupAction.DisplayPopup(new PlaceScanResultPopupPage());
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    protected async void InitOcrAsync()
    {
        await OcrPlugin.Default.InitAsync();
    }

    private async void OnOpenAdressPictureButtonClicked(object? sender, EventArgs e)
    {
        var result = await FilePicker.Default.PickAsync(PickOptions.Images);
        if (result != null)
        {
            await OcrPlugin.Default.InitAsync();
            using var stream = await result.OpenReadAsync();
            var imageAsBytes = new byte[stream.Length];
            await stream.ReadAsync(imageAsBytes);
            var ocrResult = await OcrPlugin.Default.RecognizeTextAsync(imageAsBytes);

            if (!ocrResult.Success)
            {
                await DisplayAlertAsync("No success", "No OCR possible", "OK");
                return;
            }

            await MauiPopup.PopupAction.DisplayPopup(new PlaceScanResultPopupPage());
        }
        else
        {
            await DisplayAlertAsync("Result error", "Result is null", "OK");
        }
    }
    
    private void OnTakeAdressPhotoButtonClicked(object? sender, EventArgs e)
    {
        InitOcrAsync();
        OnReadocrClicked();
    }
}