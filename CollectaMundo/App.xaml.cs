using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ApplicationServices.Startup;
using CollectaMundo.ViewModels.Shared;
using CollectaMundo.Views;
using System.Diagnostics;
using System.Windows;

namespace CollectaMundo
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private StartupWindow? _statusWindow;
        protected override async void OnStartup(StartupEventArgs e)
        {

            base.OnStartup(e);

            var userPromptService = new UserPromptService();
            var operationOverlayVM = new OperationOverlayViewModel(userPromptService);
            _statusWindow = new StartupWindow { DataContext = operationOverlayVM };
            _statusWindow.Show();

            try
            {
                var rootVM = await StartupComposition.BuildAndStartAsync(operationOverlayVM, userPromptService);

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
