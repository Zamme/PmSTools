namespace PmSTools.Models
{
    public class RouteListItem
    {
        public RouteListItem(DeliveryRoute route, int number, string displayName, bool canMoveUp, bool canMoveDown)
        {
            Route = route;
            Number = number;
            DisplayName = displayName;
            CanMoveUp = canMoveUp;
            CanMoveDown = canMoveDown;
        }

        public DeliveryRoute Route { get; }
        public int Number { get; }
        public string DisplayName { get; }
        public bool CanMoveUp { get; }
        public bool CanMoveDown { get; }
    }
}
