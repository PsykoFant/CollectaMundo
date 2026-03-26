namespace CollectaMundo.ViewModels.Pages
{
    public interface IUtilitiesNavigator
    {
        UtilitiesRoute CurrentRoute { get; }
        event EventHandler<UtilitiesRoute>? RouteChanged;

        void ShowHome();
        Task ShowImport();
    }
}
