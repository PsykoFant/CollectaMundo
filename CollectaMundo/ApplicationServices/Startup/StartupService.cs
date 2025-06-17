using CollectaMundo.ApplicationServices.Utilities;
using CollectaMundo.ViewModels;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace CollectaMundo.ApplicationServices.Startup
{
    public class StartupService(IDatabaseIntegrityService integrityService, ICardDatabasePreparationService prepService, Action closeStatusWindow, StatusViewModel statusVM) : IStartupService
    {
        private readonly IDatabaseIntegrityService _integrityService = integrityService;
        private readonly ICardDatabasePreparationService _prepService = prepService;
        private readonly Action _closeStatusWindow = closeStatusWindow;
        private readonly StatusViewModel _statusVM = statusVM;

        public async Task AppStartEntryPoint()
        {

            // Check database integrity
            _statusVM.Show("Checking database integrity…");

            await UIHelper.ForceRenderAsync();
            var dbStatus = await _integrityService.GetDatabaseStatusAsync();

            // If the database is missing or corrupt, we need to set it up
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

                await _prepService.FirstTimeDbSetup();
            }

            // Now we can proceed to load the main window
            _statusVM.StatusMessage = "Loading ALL the cards…";

            var mainVM = await MainWindowViewModel.CreateAsync();

            // Set all your visibility toggles BEFORE showing the window
            mainVM.FilterVM.NotifyFilterChanged();
            mainVM.SideMenuVisibility = Visibility.Visible;
            mainVM.ContenSectionVisibility = Visibility.Visible;
            mainVM.MainGridVisibility = Visibility.Visible;

            var mainWindow = new MainWindow
            {
                DataContext = new RootViewModel(mainVM, _statusVM)
            };

            _closeStatusWindow();
            mainWindow.Show();
        }
    }
}
