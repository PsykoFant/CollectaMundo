using CollectaMundo.ViewModels.Pages.Models;

namespace CollectaMundo.ApplicationServices.Navigation
{
    public interface IUtilitiesNavigator
    {
        UtilitiesRoute CurrentRoute { get; }
        event EventHandler<UtilitiesRoute>? RouteChanged;

        void ShowHome();
        Task ShowCollectionExport();
        Task ShowImport();
        Task ShowCardLocationManagement();
    }
}
