using CommunityToolkit.Maui.Core;

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
            await DisplayAlertAsync("Ups!", "Encara no esta disponible", "OK");
        }

        private void OnFindPlaceButtonClicked(object? sender, EventArgs e)
        {
            ShowNotAvailable();      
        }
    }
}
