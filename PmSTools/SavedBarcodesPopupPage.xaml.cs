using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MauiPopup.Views;
using PmSTools.Models;
using PmSTools.Resources.Languages;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace PmSTools;

public partial class SavedBarcodesPopupPage : BasePopupPage
{
    public SavedBarcodesPopupPage()
    {
        InitializeComponent();
        UpdateSavedBarcodesList();
    }

    private void UpdateSavedBarcodesList()
    {
        List<string> savedCodes = SaveLoadData.GetSavedCodes();
        SavedBarcodesStack.Clear();
        if (savedCodes.Count == 0)
        {
            Label noSavedCodesLabel = new Label();
            noSavedCodesLabel.Text = LangResources.NoSavedCodesText;
            SavedBarcodesStack.Add(noSavedCodesLabel);
        }
        else
        {
            if (savedCodes.Count == 1)
            {
                if (savedCodes[0] == "")
                {
                    Label noSavedCodesLabel = new Label();
                    noSavedCodesLabel.Text = LangResources.NoSavedCodesText;
                    SavedBarcodesStack.Add(noSavedCodesLabel);
                }
            }
            else
            {
                foreach (var savedCode in savedCodes)
                {
                    if (savedCode == "")
                    {
                        continue;
                    }
                    BarcodeItem newBarcodeItem = new BarcodeItem
                    {
                        Code = savedCode,
                        BarcodeView = new BarcodeGeneratorView
                        {
                            Format = BarcodeFormat.Code39,
                            ForegroundColor = Colors.Black,
                            BackgroundColor = Colors.White,
                            HorizontalOptions = LayoutOptions.Fill,
                            HeightRequest = 60,
                            Value = savedCode
                        }
                    };
                    Border newSavedCodeBorder = new Border
                    {
                        HorizontalOptions = LayoutOptions.Fill,
                        BackgroundColor = Colors.White,
                        StrokeThickness = 5
                    };
                    /*HorizontalStackLayout newSavedCodeHStack = new HorizontalStackLayout
                    {
                        VerticalOptions = LayoutOptions.Fill,
                        HorizontalOptions = LayoutOptions.Fill,
                        Spacing = 5
                    };*/
                    VerticalStackLayout newSavedCodeVStack = new VerticalStackLayout
                    {
                        HorizontalOptions = LayoutOptions.Fill
                    };
                    Label newCodeLabel = new Label();
                    newCodeLabel.Text = newBarcodeItem.Code;
                    newCodeLabel.FontSize = 16;
                    newCodeLabel.FontAttributes = FontAttributes.Bold;
                    newCodeLabel.TextColor = Colors.Black;
                    newCodeLabel.HorizontalOptions = LayoutOptions.Center;
                    newSavedCodeVStack.Add(newBarcodeItem.BarcodeView);
                    newSavedCodeVStack.Add(newCodeLabel);
                    Button newDeleteButton = new Button
                    {
                        Text = LangResources.DeleteText,
                        TextColor = Colors.Red,
                        FontAttributes = FontAttributes.Bold,
                        FontSize = 16,
                        HorizontalOptions = LayoutOptions.Fill,
                    };
                    newDeleteButton.Clicked += async (sender, args) => OnDeleteSavedCodeButtonClick(sender, args, savedCode);
                    newSavedCodeVStack.Add(newDeleteButton);
                    /*
                    newSavedCodeHStack.Add(newSavedCodeVStack);
                    */
                    newSavedCodeBorder.Content = newSavedCodeVStack;
                    SavedBarcodesStack.Add(newSavedCodeBorder);
                }
            }

            /*Label yesSavedCodesLabel = new Label();
            yesSavedCodesLabel.Text = "Saved codes = " + savedCodes.Count.ToString() + " - " + savedCodes.Last();
            SavedBarcodesStack.Add(yesSavedCodesLabel);*/
        }
    }

    private void OnDeleteSavedCodeButtonClick(object? sender, EventArgs args, string savedCode)
    {
        SaveLoadData.DeleteCode(savedCode);
        UpdateSavedBarcodesList();
    }

    private void OnClearAllSavedCodesClicked(object? sender, EventArgs e)
    {
        SaveLoadData.ClearAllSavedCodes();
        UpdateSavedBarcodesList();
    }
}