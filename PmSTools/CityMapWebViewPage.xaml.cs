using System;
using Microsoft.Maui.Controls;

namespace PmSTools
{
    public partial class CityMapWebViewPage : ContentPage
    {
        public CityMapWebViewPage()
        {
            InitializeComponent();

            var html = @"<!doctype html>
<html>
<head>
  <meta name='viewport' content='width=device-width, initial-scale=1.0'>
  <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>
  <style>html,body,#map{height:100%;margin:0;padding:0}</style>
</head>
<body>
  <div id='map'></div>
  <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
  <script>
    // Centered on Tàrrega, Spain
    var map = L.map('map').setView([41.6496,1.0850], 13);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',{maxZoom:19}).addTo(map);
    L.marker([41.6496,1.0850]).addTo(map).bindPopup('Tàrrega, Spain');
  </script>
</body>
</html>";

            mapWebView.Source = new HtmlWebViewSource { Html = html };
        }
    }
}
