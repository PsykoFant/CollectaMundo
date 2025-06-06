using CollectaMundo.Data;
using CollectaMundo.ViewModels;
using System.Windows;
using System.Windows.Threading;

namespace CollectaMundo.ApplicationServices
{
    public class StartupService : IStartupService
    {
        private readonly IDatabaseIntegrityService _integrityService;
        private readonly ICardDatabasePreparationService _prepService;
        private readonly Action _closeStatusWindow;
        private readonly StatusViewModel _statusVM;

        public StartupService(IDatabaseIntegrityService integrityService, ICardDatabasePreparationService prepService, Action closeStatusWindow, StatusViewModel statusVM)
        {
            _integrityService = integrityService;
            _prepService = prepService;
            _closeStatusWindow = closeStatusWindow;
            _statusVM = statusVM;
        }
        public async Task AppStartEntryPoint()
        {
            _statusVM.Show("Checking database integrity…", false);
            await FlushUiAsync();

            // Check database integrity
            //var dbStatus = await _integrityService.GetDatabaseStatusAsync();

            //// If the database is missing or corrupt, we need to set it up
            //if (dbStatus is DatabaseStatus.Missing or DatabaseStatus.Corrupt)
            //{
            //    if (dbStatus == DatabaseStatus.Corrupt)
            //    {
            //        string dbPath = Path.Combine(new JsonAppSettings().DatabaseSettings.SQLitePath, "AllPrintings.sqlite");
            //        try
            //        {
            //            File.Delete(dbPath);
            //            Debug.WriteLine("Deleted corrupted DB.");
            //        }
            //        catch (Exception ex)
            //        {
            //            Debug.WriteLine("Failed to delete corrupted DB: " + ex.Message);
            //        }
            //    }

            //    await _prepService.FirstTimeDbSetup();
            //}

            // test call - remove later
            await _prepService.FirstTimeDbSetup();


            // Now we can proceed to load the main window
            _statusVM.Show("Loading cards…", true);
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
                DataContext = new RootViewModel(mainVM, _statusVM)
            };

            await FlushUiAsync();

            _closeStatusWindow();
            mainWindow.Show();
        }

        private async Task FlushUiAsync()
        {
            await Task.Delay(50);
            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        }
    }
}
