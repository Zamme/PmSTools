using System;
using System.Diagnostics;
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
            await NavigateWithBusyOverlayAsync(
                Code2BarcodeButton,
                () => Navigation.PushAsync(new Code2Bar()));
        }

        protected async void ShowNotAvailable()
        {
            await DisplayAlertAsync(LangResources.OopsTitleText, LangResources.NotAvailableYet, LangResources.OkText);
        }

        private async void OnFindPlaceButtonClicked(object? sender, EventArgs e)
        {
            await NavigateWithBusyOverlayAsync(
                FindPlaceButton,
                () => Navigation.PushAsync(new FindPlacePage()));
        }

        private async void OnRouteCreationButtonClicked(object? sender, EventArgs e)
        {
            await NavigateWithBusyOverlayAsync(
                RouteCreationButton,
                () => Navigation.PushAsync(new RouteCreationPage()));
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

        private async Task NavigateWithBusyOverlayAsync(Button button, Func<Task> navigateAction)
        {
            try
            {
                await PlayMenuClickAsync();
                button.IsEnabled = false;
                BusyOverlay.IsVisible = true;
                BusyIndicator.IsRunning = true;
                await Task.Yield();

                await navigateAction();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                var message = string.IsNullOrWhiteSpace(ex.Message)
                    ? "Unexpected error. Please try again."
                    : ex.Message;
                await DisplayAlertAsync(LangResources.ErrorTitleText, message, LangResources.OkText);
            }
            finally
            {
                BusyIndicator.IsRunning = false;
                BusyOverlay.IsVisible = false;
                button.IsEnabled = true;
            }
        }
    
    }
}
