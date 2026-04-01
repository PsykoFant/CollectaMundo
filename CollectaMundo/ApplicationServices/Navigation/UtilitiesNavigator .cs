using CollectaMundo.ViewModels.Pages;

namespace CollectaMundo.ApplicationServices.Navigation
{
    public sealed class UtilitiesNavigator : IUtilitiesNavigator
    {
        private UtilitiesRoute _currentRoute = UtilitiesRoute.Home;

        private TaskCompletionSource? _activeFeatureSessionTcs;

        public UtilitiesRoute CurrentRoute => _currentRoute;

        public event EventHandler<UtilitiesRoute>? RouteChanged;

        public void ShowHome()
        {
            CompletePendingNavigation();
            SetRoute(UtilitiesRoute.Home);
        }

        public Task ShowImport()
        {
            if (_currentRoute == UtilitiesRoute.Import)
            {
                return _activeFeatureSessionTcs?.Task ?? Task.CompletedTask;
            }

            CompletePendingNavigation(); // resolve any stale/in-flight pending navigation

            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _activeFeatureSessionTcs = tcs;

            SetRoute(UtilitiesRoute.Import);

            return tcs.Task;
        }
        public Task ShowCardLocationManagement()
        {
            if (_currentRoute == UtilitiesRoute.CardLocations)
            {
                return _activeFeatureSessionTcs?.Task ?? Task.CompletedTask;
            }

            CompletePendingNavigation(); // resolve any stale/in-flight pending navigation

            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _activeFeatureSessionTcs = tcs;

            SetRoute(UtilitiesRoute.CardLocations);

            return tcs.Task;
        }
        public void CompletePendingNavigation()
        {
            _activeFeatureSessionTcs?.TrySetResult();
            _activeFeatureSessionTcs = null;
        }
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
