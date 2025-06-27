using CollectaMundo.ApplicationServices.Startup;
using CollectaMundo.ViewModels;
using System.Windows;

namespace CollectaMundo
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private StatusWindow? _statusWindow;
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var statusVM = new StatusViewModel();
            _statusWindow = new StatusWindow
            {
                DataContext = statusVM
            };
            _statusWindow.Show();

            var startupService = StartupComposition.Build(statusVM, () => _statusWindow!.Close());
            await startupService.AppStartEntryPoint();
        }
    }
}
