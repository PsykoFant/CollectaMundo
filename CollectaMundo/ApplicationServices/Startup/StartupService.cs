using CollectaMundo.ViewModels;
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

            //await UIHelper.ForceRenderAsync();
            //var dbStatus = await _integrityService.GetDatabaseStatusAsync();

            //// If the database is missing or corrupt, we need to set it up
            //if (dbStatus is DatabaseStatus.Missing or DatabaseStatus.Corrupt)
            //{
            //    await _prepService.FirstTimeDbSetup();
            //}

            // temp test
            await _prepService.FirstTimeDbSetup();

            // Now we can proceed to load the main window
            _statusVM.StatusLabelMain = "Loading ALL the cards…";

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
