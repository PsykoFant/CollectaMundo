using CollectaMundo.ApplicationServices;
using CollectaMundo.Data;
using CollectaMundo.ViewModels;
using System.Windows;
using System.Windows.Threading;

namespace CollectaMundo
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private StatusWindow? _statusWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var statusVM = new StatusViewModel();
            _statusWindow = new StatusWindow
            {
                DataContext = statusVM
            };
            _statusWindow.Show();

            _ = StartAppAsync(statusVM);
        }
        private async Task StartAppAsync(StatusViewModel statusVM)
        {
            statusVM.Show("Checking database integrity…", false);
            await FlushUiAsync();

            await DownloadAndPrepDB.SystemIntegrityCheckAsync();
            statusVM.Show("Loading cards…", false);

            await FlushUiAsync();

            var dbFactory = new DbConnectionFactory(new JsonAppSettings());
            var mainVM = await MainWindowViewModel.CreateAsync(dbFactory);

            var mainWindow = new MainWindow
            {
                DataContext = new RootViewModel(mainVM, statusVM)
            };

            _statusWindow!.Close();
            mainWindow.Show();
        }

        private static async Task FlushUiAsync()
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(
                    DispatcherPriority.Render,
                    new DispatcherOperationCallback(f =>
                    {
                        ((DispatcherFrame)f).Continue = false;
                        return null!;
                    }), frame);
                Dispatcher.PushFrame(frame);
            });
        }


    }


}
