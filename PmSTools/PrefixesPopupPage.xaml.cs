using MauiPopup.Views;

namespace PmSTools;
 
public partial class PrefixesPopupPage : BasePopupPage
{
    private List<string> currentPrefixesList = new List<string>();
    
    public PrefixesPopupPage(List<string> prefixesList)
    {
        InitializeComponent();
        FillPrefixes(prefixesList);
    }

    private void FillPrefixes(List<string> prefixesList)
    {
        int currentPrefixIndex = 0;
        foreach (var prefix in prefixesList)
        {
            Border newBorder = new Border();
            HorizontalStackLayout newStack = new HorizontalStackLayout();
            newStack.Spacing = 20;
            newStack.VerticalOptions = LayoutOptions.Center;
            CheckBox newCheckBox = new CheckBox();
            newCheckBox.IsChecked = true;
            newCheckBox.Margin = new Thickness(5, 0, 5, 0);
            newCheckBox.HorizontalOptions = LayoutOptions.Start;
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
            newButton.Margin = new Thickness(5, 0, 5, 0);
            newButton.VerticalOptions = LayoutOptions.Center;
            newButton.HorizontalOptions = LayoutOptions.End;
            /*newButton.Clicked += async (sender, args) => await OnPrefixDeleteButtonClick(sender, args);*/
            newStack.Children.Add(newButton);
            newBorder.Content = newStack;
            PrefixesStack.Add(newBorder);
            currentPrefixIndex++;
        }
    }

    private async void OnPrefixDeleteButtonClick(object? sender, EventArgs args)
    {
        throw new NotImplementedException();
    }
}