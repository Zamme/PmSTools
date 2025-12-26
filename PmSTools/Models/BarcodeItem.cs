using ZXing.Net.Maui.Controls;

namespace PmSTools.Models;

public class BarcodeItem
{
    public string Code {get; set;}
    public BarcodeGeneratorView BarcodeView { get; set; }

}