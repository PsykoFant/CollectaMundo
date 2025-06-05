using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.Data;
using CollectaMundo.Data.GenerateMissingPng;
using CollectaMundo.Data.ScryfallLookups;
using CollectaMundo.DomainLogic.GenerateMissingPng;
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
        public StartupService(Action closeStatusWindow)
        {
            _integrityService = new DatabaseIntegrityService();
            _closeStatusWindow = closeStatusWindow;

            var settings = new JsonAppSettings();
            var dbFactory = new DbConnectionFactory(settings);
            var scryfallLookups = new ScryfallLookups();
            var schemaInitializer = new DatabaseSchemaInitializer();
            var missingPngRepo = new GenerateMissingPngRepository();
            var missingPngLogic = new GenerateMissingPngLogic();
            var missingPngService = new GenerateMissingPngService(missingPngRepo, scryfallLookups, missingPngLogic);

            _prepService = new CardDatabasePreparationService(settings, dbFactory, schemaInitializer, missingPngService);
        }
        public async Task AppStartEntryPoint(StatusViewModel statusVm)
        {
            statusVm.Show("Checking database integrity…", false);
            await FlushUiAsync();

            var dbStatus = await _integrityService.GetDatabaseStatusAsync();

            /*
            if (dbStatus is DatabaseStatus.Missing or DatabaseStatus.Corrupt)
            {
                if (dbStatus == DatabaseStatus.Corrupt)
                {
                    string dbPath = Path.Combine(new JsonAppSettings().DatabaseSettings.SQLitePath, "AllPrintings.sqlite");
                    try
                    {
                        File.Delete(dbPath);
                        Debug.WriteLine("Deleted corrupted DB.");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Failed to delete corrupted DB: " + ex.Message);
                    }
                }

                await _prepService.FirstTimeDbSetup(statusVm);
            }
            */
            // test
            await _prepService.FirstTimeDbSetup(statusVm);


            statusVm.Show("Loading cards…", true);
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
                DataContext = new RootViewModel(mainVM, statusVm)
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
