using CommunityToolkit.Maui.Core;
using Plugin.Maui.OCR;

namespace PmSTools
{
    public partial class MainPage : ContentPage
    {
        private ICameraProvider cameraProvider;

        public MainPage(ICameraProvider cameraProvider)
        {
            InitializeComponent();

            /*this.cameraProvider = cameraProvider;*/
        }

        protected async override void OnAppearing()
        {
            base.OnAppearing();

            await OcrPlugin.Default.InitAsync();
            
            OnReadocrClicked();
        }
        
        // Implemented as a follow up video https://youtu.be/JUdfA7nFdWw
        /*protected async override void OnNavigatedTo(NavigatedToEventArgs args)
        {
            base.OnNavigatedTo(args);
            
            await cameraProvider.RefreshAvailableCameras(CancellationToken.None);
            MyCamera.SelectedCamera = cameraProvider.AvailableCameras
                .Where(c => c.Position == CameraPosition.Rear).FirstOrDefault();
        }*/

        // Implemented as a follow up video https://youtu.be/JUdfA7nFdWw
        /*protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
        {
            base.OnNavigatedFrom(args);

            MyCamera.MediaCaptured -= MyCamera_MediaCaptured;
            MyCamera.Handler?.DisconnectHandler();
        }*/

        /*private void MyCamera_MediaCaptured(object? sender, CommunityToolkit.Maui.Views.MediaCapturedEventArgs e)
        {
            if (Dispatcher.IsDispatchRequired)
            {
                Dispatcher.Dispatch(() => MyImage.Source = ImageSource.FromStream(() => e.Media));
                return;
            }

            MyImage.Source = ImageSource.FromStream(() => e.Media);
        }*/

        private async void OnReadocrClicked()
        {
            /*this.TakePhoto();*/
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

        /*private async void TakePhoto()
        {
            await MyCamera.CaptureImage(CancellationToken.None);
        }*/

        /*private void Button_Clicked_1(object sender, EventArgs e)
        {
            MyCamera.CameraFlashMode = MyCamera.CameraFlashMode == CameraFlashMode.Off ? 
                CameraFlashMode.On : CameraFlashMode.Off;
        }

        private void Button_Clicked_2(object sender, EventArgs e)
        {
            MyCamera.ZoomFactor += 0.1f;
        }*/
    }

}
