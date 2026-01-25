using MauiPopup.Views;

namespace PmSTools;
 
public partial class AboutPopupPage : BasePopupPage
{
    public AboutPopupPage()
    {
        InitializeComponent();
    }

    private void OnOkButtonClicked(object? sender, EventArgs e)
    {
        MauiPopup.PopupAction.ClosePopup();
    }
}