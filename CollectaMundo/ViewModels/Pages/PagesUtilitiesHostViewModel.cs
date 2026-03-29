using CollectaMundo.ApplicationServices.Navigation;
using CollectaMundo.ViewModels.Import;
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

        public PagesUtilitiesHostViewModel(UtilitiesViewModel utilitiesVM, ImportViewModel importVM, IUtilitiesNavigator navigator)
        {
            UtilitiesVM = utilitiesVM;
            ImportVM = importVM;
            _navigator = navigator;

            currentUtilitiesContentViewModel = ResolveRoute(navigator.CurrentRoute);
            _navigator.RouteChanged += OnRouteChanged;
        }

        private async void OnRouteChanged(object? sender, UtilitiesRoute route)
        {
            CurrentUtilitiesContentViewModel = ResolveRoute(route);

            switch (route)
            {
                case UtilitiesRoute.Home:
                    ImportVM.EndImport();
                    break;

                case UtilitiesRoute.Import:
                    await ImportVM.Begin();
                    break;
            }
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
