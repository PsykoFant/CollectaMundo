using CollectaMundo.ApplicationServices.CardPrices;
using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.ApplicationServices.Utilities;
using CollectaMundo.Data;
using CollectaMundo.Data.CardPrices;
using CollectaMundo.Data.GenerateMissingPng;
using CollectaMundo.Data.ScryfallLookups;
using CollectaMundo.DomainLogic.GenerateMissingPng;
using CollectaMundo.ViewModels;
using System.Diagnostics;
using System.Windows;

namespace CollectaMundo.ApplicationServices.Startup
{
    public static class StartupComposition
    {
        public static async Task<RootViewModel> BuildAndStartAsync(StatusViewModel statusVM)
        {
            try
            {
                var settings = new JsonAppSettings();
                var scryfallLookups = new ScryfallLookups();

                AppGlobals.DbFactory = new DbConnectionFactory(settings);

                // Prep services
                var missingPngRepo = new GenerateMissingPngRepository();
                var missingPngLogic = new GenerateMissingPngLogic();
                var missingPngService = new GenerateMissingPngService(missingPngRepo, scryfallLookups, missingPngLogic, statusVM);

                var cardPriceRepo = new CardPriceRepository();
                var priceService = new CardPriceService(settings, cardPriceRepo, statusVM);

                var schemaInitializer = new DatabaseSchemaRepository();
                var prepService = new CardDatabasePreparationService(settings, schemaInitializer, priceService, missingPngService, statusVM);
                var integrityService = new DatabaseIntegrityService(settings);

                // Do DB checks
                statusVM.ShowStatusOverlay("Checking database integrity…");
                await UIHelper.ForceRenderAsync();

                var dbStatus = await integrityService.GetDatabaseStatusAsync();
                if (dbStatus is DatabaseStatus.Missing or DatabaseStatus.Corrupt)
                {
                    await prepService.FirstTimeDbPrepOrchetrator();
                }

                // Create main window VM
                statusVM.StatusLabel3 = "Loading ALL the cards…";

                var mainVM = await MainWindowViewModel.CreateAsync();
                mainVM.FilterVM.NotifyFilterChanged();
                mainVM.SideMenuVisibility = Visibility.Visible;
                mainVM.ContenSectionVisibility = Visibility.Visible;
                mainVM.MainGridVisibility = Visibility.Visible;

                // Done with splash overlay
                statusVM.HideStatusOverlay();

                return new RootViewModel(mainVM, statusVM);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup failed: {ex.Message}");
                throw; // Re-throw to let the application handle it
            }
        }
    }

}
