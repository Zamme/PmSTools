using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Plugin.Maui.OCR;
using System.IO;
using PmSTools.Models;
using PmSTools.Resources.Languages;

namespace PmSTools;

public partial class FindPlacePage : ContentPage
{
    public FindPlacePage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateLastSearchPlaces();
    }
    
    private async void OnReadocrClicked()
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
                await Navigation.PushAsync(new PlaceScanResultPage(ocrResult.AllText));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(LangResources.ErrorTitleText, ex.Message, LangResources.OkText);
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
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            var imageAsBytes = memoryStream.ToArray();
            var ocrResult = await OcrPlugin.Default.RecognizeTextAsync(imageAsBytes);

            if (!ocrResult.Success)
            {
                await DisplayAlertAsync(LangResources.NoSuccessTitleText, LangResources.NoOcrPossibleText, LangResources.OkText);
                return;
            }

            await Navigation.PushAsync(new PlaceScanResultPage(ocrResult.AllText));
        }
        else
        {
            await DisplayAlertAsync(LangResources.ResultErrorTitleText, LangResources.ResultIsNullText, LangResources.OkText);
        }
    }
    
    private void OnTakeAdressPhotoButtonClicked(object? sender, EventArgs e)
    {
        InitOcrAsync();
        OnReadocrClicked();
    }

    private void UpdateLastSearchPlaces()
    {
        var lastPlacesStack = new VerticalStackLayout
        {
            Spacing = 10,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Start
        };

        List<PlaceInfoItem> lastPlaces = SaveLoadData.GetLastPlaceInfos();
        ClearHistoryButton.IsEnabled = lastPlaces.Count > 0;

        if (lastPlaces.Count > 0)
        {
            foreach (PlaceInfoItem lastPlace in lastPlaces)
            {
                string title = string.IsNullOrWhiteSpace(lastPlace.Name) ? LangResources.UnknownNameText : lastPlace.Name;

                List<string> addressParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(lastPlace.Street))
                {
                    addressParts.Add(lastPlace.Street);
                }

                string cityLine = string.Join(" ", new[] { lastPlace.PostalCode, lastPlace.City }
                    .Where(part => !string.IsNullOrWhiteSpace(part)));
                if (!string.IsNullOrWhiteSpace(cityLine))
                {
                    addressParts.Add(cityLine);
                }

                if (!string.IsNullOrWhiteSpace(lastPlace.Country))
                {
                    addressParts.Add(lastPlace.Country);
                }

                var titleLabel = new Label
                {
                    Text = title,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 16,
                    TextColor = Colors.Black
                };

                var addressLabel = new Label
                {
                    Text = addressParts.Count > 0 ? string.Join("\n", addressParts) : LangResources.UnknownAddressText,
                    FontSize = 14,
                    TextColor = Colors.Black
                };

                var openButton = new Button
                {
                    Text = LangResources.OpenText,
                    HorizontalOptions = LayoutOptions.End
                };
                openButton.Clicked += async (_, _) => await OpenPlaceAsync(lastPlace);

                var contentStack = new VerticalStackLayout
                {
                    Spacing = 6,
                    HorizontalOptions = LayoutOptions.Fill
                };
                contentStack.Add(titleLabel);
                contentStack.Add(addressLabel);
                contentStack.Add(openButton);

                var border = new Border
                {
                    Padding = 10,
                    StrokeThickness = 1,
                    BackgroundColor = Colors.White,
                    HorizontalOptions = LayoutOptions.Fill,
                    Content = contentStack
                };

                lastPlacesStack.Add(border);
            }
        }
        else
        {
            var emptyLabel = new Label
            {
                Text = LangResources.NoLastPlaceInfoText,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center,
                BackgroundColor = Colors.White,
                TextColor = Colors.Black
            };
            lastPlacesStack.Add(emptyLabel);
        }

        LastSearchPlacesScroll.Content = lastPlacesStack;
    }

    private async Task OpenPlaceAsync(PlaceInfoItem placeInfo)
    {
        if (placeInfo != null)
        {
            await Navigation.PushAsync(new PlaceScanResultPage(placeInfo));
        }
        else
        {
            await DisplayAlertAsync(LangResources.NoDataTitleText, LangResources.NoLastPlaceInfoAvailableText, LangResources.OkText);
        }
    }

    private async void OnClearLastSearchPlacesClicked(object? sender, EventArgs e)
    {
        bool confirm = await DisplayAlertAsync(
            LangResources.ClearHistoryTitleText,
            LangResources.DeleteSavedAddressHistoryText,
            LangResources.YesText,
            LangResources.NoText);
        if (!confirm)
        {
            return;
        }

        SaveLoadData.ClearLastPlaceInfos();
        UpdateLastSearchPlaces();
    }
}