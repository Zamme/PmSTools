using System.Collections.ObjectModel;
using MauiPopup.Views;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;
using PmSTools.Models;
using Xamarin.Google.MLKit.Vision.Text;

namespace PmSTools;
 
public partial class PopupPage : BasePopupPage
{
    ObservableCollection<BarcodeItem> barcodeItems = new ObservableCollection<BarcodeItem>();
    public ObservableCollection<BarcodeItem> BarcodeItems { get { return barcodeItems; } }

    public void ConstructPage(string _text)
    {
        char[] charSeparators = new char[] { ' ', '\n' };
        string[] notiPrefixes = ["NV", "NT", "NE", "NA", "C1", "CD", "PK", "PQ", "PS", "90"];
        string[] textParts = _text.Split(charSeparators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var textPart in textParts)
        {
            string modTextPart = new string(textPart.ToUpper());
            foreach (var notiPrefix in notiPrefixes)
            {
                modTextPart = modTextPart.Replace("O", "0");
                if (modTextPart.Length > 8)
                {
                    if (modTextPart.StartsWith(notiPrefix))
                    {
                        BarcodeItem newBarcodeItem = new BarcodeItem
                        {
                            Code = modTextPart,
                            BarcodeView = new BarcodeGeneratorView
                            {
                                Format = BarcodeFormat.Code39,
                                ForegroundColor = Colors.Black,
                                BackgroundColor = Colors.White,
                                WidthRequest = 400,
                                HeightRequest = 100,
                                Value = modTextPart
                            }
                        };
                        barcodeItems.Add(newBarcodeItem);

                        Label newLabel = new Label { Text = newBarcodeItem.Code,
                            VerticalOptions=LayoutOptions.Center, 
                            HorizontalOptions=LayoutOptions.Center,
                            BackgroundColor=Colors.White,
                            TextColor=Colors.Black };
                        VerticalStackLayout newVSL = new VerticalStackLayout
                        {
                            Spacing = 5,
                            BackgroundColor = Colors.White,
                            Padding = 5
                        };
                        newVSL.Add(newBarcodeItem.BarcodeView);
                        newVSL.Add(newLabel);
                        Border newBorder = new Border
                        {
                            Padding = 2,
                            BackgroundColor = Colors.White,
                            Content = newVSL
                        };
                        CodesStack.Add(newBorder);
                    }
                }
            }
        }

        if (barcodeItems.Count < 1)
        {
            Label newLabel = new Label { Text = "ERROR",
                VerticalOptions=LayoutOptions.Center, 
                HorizontalOptions=LayoutOptions.Center,
                BackgroundColor=Colors.White,
                TextColor=Colors.Black };
            CodesStack.Add(newLabel);
        }
        /*BarcodesCollectionView.ItemsSource = barcodeItems;*/
        /*string codeFound = new string(BarcodeItems[0].Code);
        BarcodeImage.Value = codeFound;
        CodiString.Text = codeFound;*/
    }
    public PopupPage()
    {
        InitializeComponent();
    }
    public PopupPage(string _text)
    {
        InitializeComponent();
        ConstructPage(_text);
    }
}