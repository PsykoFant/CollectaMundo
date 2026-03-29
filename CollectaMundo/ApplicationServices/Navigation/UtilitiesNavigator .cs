using CollectaMundo.ViewModels.Pages;

namespace CollectaMundo.ApplicationServices.Navigation
{
    public sealed class UtilitiesNavigator : IUtilitiesNavigator
    {
        private UtilitiesRoute _currentRoute = UtilitiesRoute.Home;
        public UtilitiesRoute CurrentRoute => _currentRoute;

        public event EventHandler<UtilitiesRoute>? RouteChanged;
        public void ShowHome() => SetRoute(UtilitiesRoute.Home);
        public void ShowImport() => SetRoute(UtilitiesRoute.Import);
        //{
        //    SetRoute(UtilitiesRoute.Import);
        //    return Task.CompletedTask;
        //}
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
