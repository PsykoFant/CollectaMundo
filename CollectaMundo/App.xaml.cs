using CollectaMundo.ApplicationServices.Startup;
using CollectaMundo.ViewModels;
using System.Diagnostics;
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
            try
            {
                base.OnStartup(e);

                var statusVM = new StatusViewModel();
                _statusWindow = new StatusWindow { DataContext = statusVM };
                _statusWindow.Show();

                var rootVM = await StartupComposition.BuildAndStartAsync(statusVM);

                var mainWindow = new MainWindow
                {
                    DataContext = rootVM
                };

                _statusWindow.Close();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Application startup failed: {ex.Message}");
            }
        }
    }
}
