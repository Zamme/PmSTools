using ZXing.Net.Maui.Controls;

namespace PmSTools.Models;

public class BarcodeItem
{
    public BarcodeItem()
    {
        Code = string.Empty;
        BarcodeView = new BarcodeGeneratorView();
    }

    public string Code { get; set; } = string.Empty;
    public BarcodeGeneratorView BarcodeView { get; set; } = new BarcodeGeneratorView();

}