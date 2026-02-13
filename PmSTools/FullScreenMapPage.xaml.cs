using System;
using Microsoft.Maui.Controls;

namespace PmSTools
{
    public partial class FullScreenMapPage : ContentPage
    {
        public FullScreenMapPage(string htmlContent)
        {
            InitializeComponent();

            if (!string.IsNullOrEmpty(htmlContent))
            {
                FullMapWebView.Source = new HtmlWebViewSource { Html = htmlContent };
                NoMapLabel.IsVisible = false;
            }
            else
            {
                NoMapLabel.IsVisible = true;
            }
        }

        private async void OnCloseClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }
    }
}