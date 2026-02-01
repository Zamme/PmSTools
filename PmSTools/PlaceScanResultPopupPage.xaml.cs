using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MauiPopup.Views;
using SkiaSharp.Views.Maui.Controls.Hosting;

/*
using Microsoft.Maui.Controls.Maps;

using Microsoft.Maui.Maps;
*/
/*
using Map = Microsoft.Maui.Controls.Maps.Map;
*/

namespace PmSTools;

public partial class PlaceScanResultPopupPage : BasePopupPage
{
    public PlaceScanResultPopupPage()
    {
        InitializeComponent();
        
        var mapControl = new Mapsui.UI.Maui.MapControl();
        mapControl.Map?.Layers.Add(Mapsui.Tiling.OpenStreetMap.CreateTileLayer());
        PlaceScanResultContainer.Add(mapControl);
        
        /*MapPage();*/
    }
    
    /*public void MapPage()
    {
        var map = new Map(MapSpan.FromCenterAndRadius(
            new Location(37.7749, -122.4194), Distance.FromMiles(5)))
        {
            IsShowingUser = false,
            VerticalOptions = LayoutOptions.FillAndExpand
        };

        Content = new StackLayout
        {
            Children = { map }
        };
    }*/
}