using CollectaMundo.ViewModels.Pages;

namespace CollectaMundo.ApplicationServices.Navigation
{
    public interface IUtilitiesNavigator
    {
        UtilitiesRoute CurrentRoute { get; }
        event EventHandler<UtilitiesRoute>? RouteChanged;

        void ShowHome();
        Task ShowImport();
        Task ShowCardLocationManagement();

        void CompletePendingNavigation();
    }
}
