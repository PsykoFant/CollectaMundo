using CollectaMundo.ViewModels.Pages;

namespace CollectaMundo.ApplicationServices.Navigation
{
    public sealed class UtilitiesNavigator : IUtilitiesNavigator
    {
        private UtilitiesRoute _currentRoute = UtilitiesRoute.Home;

        private TaskCompletionSource? _pendingNavigationTcs;

        public UtilitiesRoute CurrentRoute => _currentRoute;

        public event EventHandler<UtilitiesRoute>? RouteChanged;

        public void ShowHome()
        {
            SetRoute(UtilitiesRoute.Home);
        }

        public Task ShowImport()
        {
            // If already navigating to Import, reuse existing task
            if (_currentRoute == UtilitiesRoute.Import)
            {
                return _pendingNavigationTcs?.Task ?? Task.CompletedTask;
            }

            _pendingNavigationTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            SetRoute(UtilitiesRoute.Import);

            return _pendingNavigationTcs.Task;
        }
        public void CompletePendingNavigation()
        {
            _pendingNavigationTcs?.TrySetResult();
            _pendingNavigationTcs = null;
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
