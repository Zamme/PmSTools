using MauiPopup.Views;

namespace PmSTools;
 
public partial class PrefixesPopupPage : BasePopupPage
{
    public PrefixesPopupPage(List<string> prefixesList)
    {
        InitializeComponent();
        FillPrefixes(prefixesList);
    }

    private void FillPrefixes(List<string> prefixesList)
    {
        foreach (var prefix in prefixesList)
        {
            Frame newFrame = new Frame();
            HorizontalStackLayout newStack = new HorizontalStackLayout();
            newStack.Spacing = 20;
            newStack.VerticalOptions = LayoutOptions.Center;
            newStack.HorizontalOptions = LayoutOptions.Center;
            CheckBox newCheckBox = new CheckBox();
            newCheckBox.IsChecked = true;
            newStack.Children.Add(newCheckBox);
            Label newLabel = new Label();
            newLabel.Text = prefix;
            newLabel.FontSize = 25;
            newLabel.FontAttributes = FontAttributes.Bold;
            newLabel.HorizontalOptions = LayoutOptions.Center;
            newLabel.VerticalOptions = LayoutOptions.Center;
            newStack.Children.Add(newLabel);
            Button newButton = new Button();
            newButton.Text = "X";
            newButton.FontSize = 20;
            newButton.FontAttributes = FontAttributes.Bold;
            newButton.TextColor = Colors.Red;
            newStack.Children.Add(newButton);
            newFrame.Content = newStack;
            PrefixesStack.Add(newFrame);
        }
    }
}