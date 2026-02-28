using System.Threading.Tasks;
using CommunityToolkit.Maui.Core;
using PmSTools.Resources.Languages;

namespace PmSTools
{
    public partial class MainPage : ContentPage
    {

        public MainPage(ICameraProvider cameraProvider)
        {
            InitializeComponent();
        }

        protected async override void OnAppearing()
        {
            base.OnAppearing();

        }

        private void OnCode2BarcodeButtonClicked(object? sender, EventArgs e)
        {
            Navigation.PushAsync(new Code2Bar());
        }

        protected async void ShowNotAvailable()
        {
            await DisplayAlertAsync("Ups!", LangResources.NotAvailableYet, "OK");
        }

        private void OnFindPlaceButtonClicked(object? sender, EventArgs e)
        {
            Navigation.PushAsync(new FindPlacePage());      
        }

        private async void OnRouteCreationButtonClicked(object? sender, EventArgs e)
        {
            try
            {
                RouteCreationButton.IsEnabled = false;
                BusyOverlay.IsVisible = true;
                BusyIndicator.IsRunning = true;
                await Task.Yield();

                await Navigation.PushAsync(new RouteCreationPage());
            }
            finally
            {
                BusyIndicator.IsRunning = false;
                BusyOverlay.IsVisible = false;
                RouteCreationButton.IsEnabled = true;
            }
        }

        private void ConfigMenuItem_OnClicked(object? sender, EventArgs e)
        {
            ShowNotAvailable();
        }

        private void SettingsMenuItem_OnClicked(object? sender, EventArgs e)
        {
            ShowNotAvailable();
        }

        private void HelpMenuItem_OnClicked(object? sender, EventArgs e)
        {
            ShowNotAvailable();
        }

        private void AboutMenuItem_OnClicked(object? sender, EventArgs e)
        {
            MauiPopup.PopupAction.DisplayPopup(new AboutPopupPage());
        }
    
    }
}
