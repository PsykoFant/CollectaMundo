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

            var settings = new JsonAppSettings();
            var dbFactory = new DbConnectionFactory(settings);
            var healthRepo = new DatabaseHealthRepository();
            var downloader = new ResourceDownloader();

            var startupService = new StartupService(settings, dbFactory, healthRepo, downloader);
            _ = StartAppAsync(statusVM, startupService);
        }
        private async Task StartAppAsync(StatusViewModel statusVM, IStartupService startupService)
        {
            statusVM.Show("Checking database integrity…", false);
            await FlushUiAsync();

            await startupService.EnsureDatabaseIntegrityAsync(statusVM);
            await FlushUiAsync();

            statusVM.Show("Loading cards…", true);
            await FlushUiAsync();

            var dbFactory = new DbConnectionFactory(new ApplicationServices.JsonAppSettings());
            var mainVM = await MainWindowViewModel.CreateAsync(dbFactory);

            // Set all your visibility toggles BEFORE showing the window
            mainVM.FilterVM.NotifyFilterChanged();
            mainVM.SideMenuVisibility = Visibility.Visible;
            mainVM.ContenSectionVisibility = Visibility.Visible;
            mainVM.MainGridVisibility = Visibility.Visible;

            var mainWindow = new MainWindow
            {
                DataContext = new RootViewModel(mainVM, statusVM)
            };

            await FlushUiAsync();

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
