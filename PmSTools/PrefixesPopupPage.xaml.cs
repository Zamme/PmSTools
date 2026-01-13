using MauiPopup.Views;

namespace PmSTools;
 
public partial class PrefixesPopupPage : BasePopupPage
{
    private List<string> currentPrefixesList = new List<string>();
    
    public PrefixesPopupPage(List<string> prefixesList)
    {
        InitializeComponent();
        currentPrefixesList = new List<string>(prefixesList);
        FillPrefixes(currentPrefixesList);
    }

    private void FillPrefixes(List<string> prefixesList)
    {
        PrefixesStack.Clear();
        int currentPrefixIndex = 0;
        foreach (var prefix in prefixesList)
        {
            Border newBorder = new Border();
            HorizontalStackLayout newStack = new HorizontalStackLayout();
            newStack.Spacing = 20;
            newStack.VerticalOptions = LayoutOptions.Center;
            newStack.HorizontalOptions = LayoutOptions.Center;
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
            newButton.Clicked += async (sender, args) => OnPrefixDeleteButtonClick(sender, args, prefix);
            newStack.Children.Add(newButton);
            newBorder.Content = newStack;
            PrefixesStack.Add(newBorder);
            currentPrefixIndex++;
        }
    }

    private void OnPrefixDeleteButtonClick(object? sender, EventArgs args, string _prefix)
    {
        Console.WriteLine("Borrat " + _prefix);
        currentPrefixesList.Remove(_prefix);
        FillPrefixes(currentPrefixesList);
    }

    private void OnAddPrefixClicked(object? sender, EventArgs e)
    {
        /*throw new NotImplementedException();*/
    }

    private void OnSavePrefixesClicked(object? sender, EventArgs e)
    {
        // TODO : Save currentPrefixesList
        MauiPopup.PopupAction.ClosePopup();
    }
}