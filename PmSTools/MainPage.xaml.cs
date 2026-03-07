using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Core;
using Plugin.Maui.Audio;
using PmSTools.Resources.Languages;

namespace PmSTools
{
    public partial class MainPage : ContentPage
    {
        private const string MenuClickSoundFileName = "menu_click.wav";
        private readonly IAudioManager _audioManager;
        private IAudioPlayer? _menuClickPlayer;
        private Stream? _menuClickStream;

        public MainPage(ICameraProvider cameraProvider, IAudioManager audioManager)
        {
            _audioManager = audioManager;
            InitializeComponent();
        }

        protected async override void OnAppearing()
        {
            base.OnAppearing();

        }

        private async void OnCode2BarcodeButtonClicked(object? sender, EventArgs e)
        {
            await PlayMenuClickAsync();
            Navigation.PushAsync(new Code2Bar());
        }

        protected async void ShowNotAvailable()
        {
            await DisplayAlertAsync(LangResources.OopsTitleText, LangResources.NotAvailableYet, LangResources.OkText);
        }

        private async void OnFindPlaceButtonClicked(object? sender, EventArgs e)
        {
            await PlayMenuClickAsync();
            Navigation.PushAsync(new FindPlacePage());      
        }

        private async void OnRouteCreationButtonClicked(object? sender, EventArgs e)
        {
            try
            {
                await PlayMenuClickAsync();
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

        private async Task PlayMenuClickAsync()
        {
            if (_menuClickPlayer == null)
            {
                _menuClickStream = await FileSystem.OpenAppPackageFileAsync(MenuClickSoundFileName);
                _menuClickPlayer = _audioManager.CreatePlayer(_menuClickStream);
            }

            if (_menuClickPlayer.IsPlaying)
            {
                _menuClickPlayer.Stop();
            }

            _menuClickPlayer.Play();
        }
    
    }
}
