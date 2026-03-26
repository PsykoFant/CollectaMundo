namespace CollectaMundo.ViewModels.Pages
{
    public sealed class UtilitiesNavigator : IUtilitiesNavigator
    {
        private UtilitiesRoute _currentRoute = UtilitiesRoute.Home;

        public UtilitiesRoute CurrentRoute => _currentRoute;

        public event EventHandler<UtilitiesRoute>? RouteChanged;

        public void ShowHome() => SetRoute(UtilitiesRoute.Home);

        public async Task ShowImport() => SetRoute(UtilitiesRoute.Import);

        private void SetRoute(UtilitiesRoute route)
        {
            if (_currentRoute == route)
            {
                return;
            }

            _currentRoute = route;
            RouteChanged?.Invoke(this, route);
        }
    }
}
