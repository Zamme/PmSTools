using System.Collections.ObjectModel;
using PmSTools.Models;
using PmSTools.Resources.Languages;

namespace PmSTools;

public partial class RouteCreationPage : ContentPage
{
    private readonly ObservableCollection<DeliveryRoute> _routes;
    private bool _isNavigating;
    public ObservableCollection<DeliveryRoute> Routes => _routes;
    public bool HasRoutes => _routes.Count > 0;

    public bool IsNavigating
    {
        get => _isNavigating;
        private set
        {
            if (_isNavigating == value)
                return;

            _isNavigating = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNotNavigating));
        }
    }

    public bool IsNotNavigating => !_isNavigating;

    public ObservableCollection<RouteListItem> RouteItems { get; }

    public RouteCreationPage()
    {
        InitializeComponent();

        _routes = SaveLoadData.TryGetSavedDeliveryRoutes(out var savedRoutes) && savedRoutes.Count > 0
            ? new ObservableCollection<DeliveryRoute>(savedRoutes)
            : new ObservableCollection<DeliveryRoute>();

        RouteItems = new ObservableCollection<RouteListItem>();

        BindingContext = this;
        SyncRouteItemsWithRoutes();
        UpdateEmptyStateVisibility();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        SyncRouteItemsWithRoutes();
        UpdateEmptyStateVisibility();
    }

    private void OnCreateNewRouteClicked(object? sender, EventArgs e)
    {
        var newRoute = new DeliveryRoute();

        _routes.Add(newRoute);
        SaveLoadData.SaveDeliveryRoutes(_routes);
        SyncRouteItemsWithRoutes();
        UpdateEmptyStateVisibility();
    }

    private void UpdateEmptyStateVisibility()
    {
        EmptyStateLabel.IsVisible = RouteItems.Count == 0;
        OnPropertyChanged(nameof(HasRoutes));
    }

    private async void OnRouteSelectedClicked(object? sender, EventArgs e)
    {
        if (!TryGetRouteItem(sender, out var routeItem))
            return;

        if (IsNavigating)
            return;

        try
        {
            IsNavigating = true;
            await Navigation.PushAsync(new RouteEditorPage(_routes, routeItem.Route, routeItem.Number));
        }
        finally
        {
            IsNavigating = false;
        }
    }

    private async void OnRenameRouteClicked(object? sender, EventArgs e)
    {
        if (!TryGetRouteItem(sender, out var routeItem))
            return;

        var currentName = routeItem.Route.Name ?? string.Empty;
        var newName = await DisplayPromptAsync(LangResources.RenameRouteTitleText, LangResources.RenameRoutePromptText,
            initialValue: currentName);

        if (newName == null)
            return;

        routeItem.Route.Name = string.IsNullOrWhiteSpace(newName) ? null : newName.Trim();
        SaveLoadData.SaveDeliveryRoutes(_routes);
        SyncRouteItemsWithRoutes();
    }

    private async void OnDeleteRouteClicked(object? sender, EventArgs e)
    {
        if (!TryGetRouteItem(sender, out var routeItem))
            return;

        var confirm = await DisplayAlertAsync(
            LangResources.DeleteRouteTitleText,
            string.Format(LangResources.DeleteRouteMessageFormatText, routeItem.DisplayName),
            LangResources.DeleteText,
            LangResources.CancelText);

        if (!confirm)
            return;

        _routes.Remove(routeItem.Route);
        SaveLoadData.SaveDeliveryRoutes(_routes);
        SyncRouteItemsWithRoutes();
        UpdateEmptyStateVisibility();
    }

    private async void OnDeleteAllRoutesClicked(object? sender, EventArgs e)
    {
        if (_routes.Count == 0)
            return;

        var confirm = await DisplayAlertAsync(
            LangResources.DeleteAllRoutesTitleText,
            LangResources.DeleteAllRoutesMessageText,
            LangResources.DeleteAllText,
            LangResources.CancelText);

        if (!confirm)
            return;

        _routes.Clear();
        SaveLoadData.ClearSavedDeliveryRoute();
        SyncRouteItemsWithRoutes();
        UpdateEmptyStateVisibility();
    }

    private void OnMoveRouteUpClicked(object? sender, EventArgs e)
    {
        if (!TryGetRouteItem(sender, out var routeItem))
            return;

        var index = _routes.IndexOf(routeItem.Route);
        if (index <= 0)
            return;

        _routes.Move(index, index - 1);
        SaveLoadData.SaveDeliveryRoutes(_routes);
        SyncRouteItemsWithRoutes();
    }

    private void OnMoveRouteDownClicked(object? sender, EventArgs e)
    {
        if (!TryGetRouteItem(sender, out var routeItem))
            return;

        var index = _routes.IndexOf(routeItem.Route);
        if (index < 0 || index >= _routes.Count - 1)
            return;

        _routes.Move(index, index + 1);
        SaveLoadData.SaveDeliveryRoutes(_routes);
        SyncRouteItemsWithRoutes();
    }

    private void SyncRouteItemsWithRoutes()
    {
        RouteItems.Clear();

        for (var routeIndex = 0; routeIndex < _routes.Count; routeIndex++)
        {
            var number = routeIndex + 1;
            var route = _routes[routeIndex];
            RouteItems.Add(new RouteListItem(
                route,
                number,
                BuildRouteDisplayName(route, number),
                routeIndex > 0,
                routeIndex < _routes.Count - 1));
        }
    }

    private static string BuildRouteDisplayName(DeliveryRoute route, int number)
    {
        if (route == null)
            return string.Format(LangResources.RouteDisplayNameFormatText, number);

        return string.IsNullOrWhiteSpace(route.Name)
            ? string.Format(LangResources.RouteDisplayNameFormatText, number)
            : string.Format(LangResources.RouteDisplayNameWithNameFormatText, number, route.Name.Trim());
    }

    private static bool TryGetRouteItem(object? sender, out RouteListItem routeItem)
    {
        routeItem = null!;

        if (sender is not BindableObject bindable)
            return false;

        var parameter = bindable.GetValue(Button.CommandParameterProperty)
            ?? bindable.GetValue(ImageButton.CommandParameterProperty);

        if (parameter is not RouteListItem item)
            return false;

        routeItem = item;
        return true;
    }

}
