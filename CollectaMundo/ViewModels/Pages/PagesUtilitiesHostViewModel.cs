using CollectaMundo.ApplicationServices.Navigation;
using CollectaMundo.ViewModels.Import;
using CollectaMundo.ViewModels.Shell;
using CollectaMundo.ViewModels.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.ViewModels.Pages
{
    public partial class PagesUtilitiesHostViewModel : ObservableObject
    {
        private readonly IUtilitiesNavigator _navigator;
        public UtilitiesViewModel UtilitiesVM { get; }
        public ImportViewModel ImportVM { get; }

        [ObservableProperty]
        private object currentUtilitiesContentViewModel;
        public PagesUtilitiesHostViewModel(UtilitiesViewModel utilitiesVM,ImportViewModel importVM,IUtilitiesNavigator navigator)
        {
            UtilitiesVM = utilitiesVM;
            ImportVM = importVM;
            _navigator = navigator;

            currentUtilitiesContentViewModel = ResolveRoute(navigator.CurrentRoute);
            _navigator.RouteChanged += OnRouteChanged;
        }
        private void OnRouteChanged(object? sender, UtilitiesRoute route)
        {
            CurrentUtilitiesContentViewModel = ResolveRoute(route);
        }

        private object ResolveRoute(UtilitiesRoute route) => route switch
        {
            UtilitiesRoute.Home => UtilitiesVM,
            UtilitiesRoute.Import => ImportVM,
            _ => UtilitiesVM
        };
    }

    public enum UtilitiesRoute
    {
        Home,
        Import
    }
}
