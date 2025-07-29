using CollectaMundo.ApplicationServices.CardPrices;
using CollectaMundo.ApplicationServices.EditCollection;
using CollectaMundo.ApplicationServices.Filtering;
using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.ApplicationServices.ImportExport;
using CollectaMundo.ApplicationServices.UpdateDB;
using CollectaMundo.ApplicationServices.Utilities;
using CollectaMundo.Data;
using CollectaMundo.Data.CardPrices;
using CollectaMundo.Data.GenerateMissingPng;
using CollectaMundo.Data.ImportExport;
using CollectaMundo.Data.ScryfallLookups;
using CollectaMundo.Data.UpdateDB;
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

                var missingPngRepo = new GenerateMissingPngRepository();
                var missingPngLogic = new GenerateMissingPngLogic();
                var missingPngService = new GenerateMissingPngService(missingPngRepo, scryfallLookups, missingPngLogic, statusVM);

                var cardPriceRepo = new CardPriceRepository();
                var priceService = new CardPriceService(settings, cardPriceRepo, statusVM);

                var schemaInitializer = new DatabaseSchemaRepository();
                var prepService = new CardDatabasePreparationService(settings, schemaInitializer, priceService, missingPngService, statusVM);
                var integrityService = new DatabaseIntegrityService(settings);

                statusVM.ShowStatusOverlay("Checking database integrity…");
                await UIHelper.ForceRenderAsync();

                var dbStatus = await integrityService.GetDatabaseStatusAsync();

                //if (dbStatus is DatabaseStatus.Missing or DatabaseStatus.Corrupt)
                //{
                //    await prepService.FirstTimeDbPrepOrchetrator();
                //}

                // debug
                await prepService.FirstTimeDbPrepOrchetrator();


                statusVM.StatusLabel1 = string.Empty;
                statusVM.StatusLabel2 = string.Empty;
                statusVM.StatusLabel3 = "Loading ALL the cards…";
                await UIHelper.ForceRenderAsync();

                // Construct your new DI services
                var filteringService = new FilteringService();
                var editService = new EditCollectionService();
                var importExportService = new ImportExportService(new ImportExportRepo());
                var updateService = new UpdateService(settings, AppGlobals.DbFactory, new UpdateDbRepo(), new UpdateDbRemoteData());

                var mainVM = await MainWindowViewModel.CreateAsync(filteringService, editService, importExportService, updateService, statusVM);

                mainVM.FilterVM.NotifyFilterChanged();
                mainVM.SideMenuVisibility = Visibility.Visible;
                mainVM.ContenSectionVisibility = Visibility.Visible;
                mainVM.MainGridVisibility = Visibility.Visible;

                statusVM.HideStatusOverlay();

                return new RootViewModel(mainVM, statusVM);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup failed: {ex.Message}");
                throw;
            }
        }

    }

}
