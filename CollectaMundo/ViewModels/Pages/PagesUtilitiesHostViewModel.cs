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
        public CardLocationViewModel CardLocationVM { get; }

        [ObservableProperty]
        private object currentUtilitiesContentViewModel;

        public PagesUtilitiesHostViewModel(UtilitiesViewModel utilitiesVM, ImportViewModel importVM, CardLocationViewModel cardLocationViewModel, IUtilitiesNavigator navigator)
        {
            UtilitiesVM = utilitiesVM;
            ImportVM = importVM;
            CardLocationVM = cardLocationViewModel;
            _navigator = navigator;

            currentUtilitiesContentViewModel = ResolveRoute(navigator.CurrentRoute);
            _navigator.RouteChanged += OnRouteChanged;
        }

        private void OnRouteChanged(object? sender, UtilitiesRoute route)
        {
            switch (route)
            {
                case UtilitiesRoute.Home:
                    CurrentUtilitiesContentViewModel = UtilitiesVM;
                    break;

                case UtilitiesRoute.Import:
                    CurrentUtilitiesContentViewModel = ImportVM;
                    ImportVM.Begin();
                    break;

                case UtilitiesRoute.CardLocations:
                    CurrentUtilitiesContentViewModel = CardLocationVM;
                    break;
            }
        }

        private object ResolveRoute(UtilitiesRoute route) => route switch
        {
            UtilitiesRoute.Home => UtilitiesVM,
            UtilitiesRoute.Import => ImportVM,
            UtilitiesRoute.CardLocations => CardLocationVM,
            _ => UtilitiesVM
        };
    }

    public enum UtilitiesRoute
    {
        Home,
        Import,
        CardLocations
    }
}
