using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ApplicationServices.Startup;
using CollectaMundo.ViewModels;
using CollectaMundo.Views.Shell;
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

            base.OnStartup(e);

            var userPromptService = new UserPromptService();
            var statusVM = new StatusViewModel(userPromptService);
            _statusWindow = new StatusWindow { DataContext = statusVM };
            _statusWindow.Show();

            try
            {
                var rootVM = await StartupComposition.BuildAndStartAsync(statusVM, userPromptService);

                var mainWindow = new MainWindow
                {
                    DataContext = rootVM
                };

                _statusWindow.Close();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                await Task.Delay(10000);
                Shutdown(-1);
            }
        }
    }
}
