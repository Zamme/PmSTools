using Java.Lang;
using MauiPopup.Views;
using PmSTools.Models;
using Exception = System.Exception;

namespace PmSTools;
 
public partial class PrefixesPopupPage : BasePopupPage
{
    private List<string> currentPrefixesList = new List<string>();
    private List<bool> currentActivePrefixesList = new List<bool>();
    private int prefixesCount = 0;

    public PrefixesPopupPage()
    {
        InitializeComponent();
        UpdateFromPrefixesPrefs();
        FillPrefixes(currentPrefixesList, currentActivePrefixesList);
    }

    private void FillPrefixes(List<string> prefixesList, List<bool> activePrefixesList)
    {
        PrefixesVSL.Clear();
        for (int currentPrefixIndex = 0; currentPrefixIndex < prefixesList.Count; currentPrefixIndex++)
        {
            int cPI = currentPrefixIndex;
            Border newBorder = new Border();
            newBorder.HorizontalOptions = LayoutOptions.Fill;
            newBorder.VerticalOptions = LayoutOptions.Fill;
            Grid newGrid = new Grid
            {
                Padding = 5,
                RowDefinitions =
                {
                    new RowDefinition
                    {
                        Height = GridLength.Star
                    },
                },
                ColumnDefinitions =
                {
                    new ColumnDefinition                    
                    {
                        Width = GridLength.Star
                    },
                    new ColumnDefinition                    
                    {
                        Width = GridLength.Star
                    },
                    new ColumnDefinition
                    {
                        Width = GridLength.Star
                    }
                }
            };
            /*
            HorizontalStackLayout newGrid = new HorizontalStackLayout();
            */
            newGrid.VerticalOptions = LayoutOptions.Center;
            newGrid.HorizontalOptions = LayoutOptions.Fill;
            CheckBox newCheckBox = new CheckBox();
            newCheckBox.IsChecked = activePrefixesList[cPI];
            newCheckBox.Margin = new Thickness(5, 0, 5, 0);
            newCheckBox.HorizontalOptions = LayoutOptions.Start;
            newCheckBox.VerticalOptions = LayoutOptions.Center;
            newCheckBox.CheckedChanged += (s, e) => OnPrefixCheckBoxChanged(s, e, cPI);
            newGrid.Add(newCheckBox, 0, 0);
            Label newLabel = new Label();
            newLabel.Text = currentPrefixesList[cPI];
            newLabel.FontSize = 25;
            newLabel.FontAttributes = FontAttributes.Bold;
            newLabel.HorizontalOptions = LayoutOptions.Fill;
            newLabel.VerticalOptions = LayoutOptions.Center;
            newGrid.Add(newLabel, 1, 0);
            Button newButton = new Button();
            newButton.Text = "X";
            newButton.FontSize = 20;
            newButton.FontAttributes = FontAttributes.Bold;
            newButton.TextColor = Colors.Red;
            newButton.Margin = new Thickness(5, 0, 5, 0);
            newButton.VerticalOptions = LayoutOptions.Center;
            newButton.HorizontalOptions = LayoutOptions.End;
            newButton.Clicked += async (sender, args) => OnPrefixDeleteButtonClick(sender, args, cPI);
            newGrid.Add(newButton, 2, 0);
            newBorder.Content = newGrid;
            PrefixesVSL.Add(newBorder);
        }
    }

    private void OnPrefixCheckBoxChanged(object? sender, CheckedChangedEventArgs checkedChangedEventArgs, int index)
    {
        currentActivePrefixesList[index] = checkedChangedEventArgs.Value;
        FillPrefixes(currentPrefixesList, currentActivePrefixesList);
    }

    private void OnPrefixDeleteButtonClick(object? sender, EventArgs args, int _prefixIndex)
    {
        /*Console.WriteLine("Borrat " + _prefix);*/
        /*string prefixKey = SaveLoadData.PrefixesPrefsKeyPrefix + _prefixIndex.ToString();
        string activePrefixKey = SaveLoadData.ActivePrefixesPrefsKeyPrefix + _prefixIndex.ToString();*/
        Console.WriteLine("Esborrant " + _prefixIndex.ToString());
        if (_prefixIndex < currentPrefixesList.Count)
        {
            currentPrefixesList.RemoveAt(_prefixIndex);
            Console.WriteLine("Esborrat " + _prefixIndex.ToString());
        }

        if (_prefixIndex < currentActivePrefixesList.Count)
        {
            currentActivePrefixesList.RemoveAt(_prefixIndex);
            Console.WriteLine("Esborrat active " + _prefixIndex.ToString());
        }

        FillPrefixes(currentPrefixesList, currentActivePrefixesList);
    }

    private void OnAddPrefixClicked(object? sender, EventArgs e)
    {
        string newPrefixAdded = "";
        try
        {
            newPrefixAdded = PrefixEditor.Text.ToString();
            currentPrefixesList.Add(newPrefixAdded);
            currentActivePrefixesList.Add(true);
            FillPrefixes(currentPrefixesList, currentActivePrefixesList);
        }
        catch (Exception exception)
        {
            Console.WriteLine("No prefix written");
        }
    }

    private void OnSavePrefixesClicked(object? sender, EventArgs e)
    {
        SaveLoadData.CleanPrefixesPrefs();
        SaveLoadData.CleanActivePrefixesPrefs();
        SaveLoadData.SavePrefixesPrefs(currentPrefixesList);
        SaveLoadData.SaveActivePrefixesPrefs(currentActivePrefixesList);
        MauiPopup.PopupAction.ClosePopup();
    }

    private void UpdateFromPrefixesPrefs()
    {
        prefixesCount = Preferences.Get(SaveLoadData.PrefixesCountPrefName, 0);
        currentPrefixesList.Clear();
        currentActivePrefixesList.Clear();
        if (prefixesCount < 1)
        {
            DisplayAlertAsync("Error", "Prefixes count is empty", "OK");
        }
        else
        {
            for (int prefixCounter = 0; prefixCounter < prefixesCount; prefixCounter++)
            {
                string currentPrefixKey = SaveLoadData.PrefixesPrefsKeyPrefix + prefixCounter.ToString();
                string currentPrefix = Preferences.Get(currentPrefixKey, "null");
                string currentActivePrefixKey = SaveLoadData.ActivePrefixesPrefsKeyPrefix + prefixCounter.ToString();
                bool currentActivePrefix = Preferences.Get(currentActivePrefixKey, true);
                if (currentPrefix == "null")
                {
                    // TODO: What to do if it doesn't find prefix pref key
                }
                else
                {
                    currentPrefixesList.Add(currentPrefix);
                    currentActivePrefixesList.Add(currentActivePrefix);
                }
            }
        }
    }
    
    private void UpdatePrefixes()
    {
    }
}