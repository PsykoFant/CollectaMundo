#region using directives
using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.CardLists;
using CollectaMundo.ApplicationServices.CardLists.Lookups;
using CollectaMundo.ApplicationServices.CardPrices;
using CollectaMundo.ApplicationServices.DownloadResourceFiles;
using CollectaMundo.ApplicationServices.EditCollection;
using CollectaMundo.ApplicationServices.Filtering;
using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.ApplicationServices.ImportExport;
using CollectaMundo.ApplicationServices.Utilities;
using CollectaMundo.ApplicationServices.Utilities.Progress;
using CollectaMundo.Data;
using CollectaMundo.Data.CardDatabaseManagement;
using CollectaMundo.Data.CardLists;
using CollectaMundo.Data.CardPrices;
using CollectaMundo.Data.EditCollection;
using CollectaMundo.Data.Filtering;
using CollectaMundo.Data.GenerateMissingPng;
using CollectaMundo.Data.ImportExport;
using CollectaMundo.Data.RemoteLookups;
using CollectaMundo.DomainLogic.CardLists;
using CollectaMundo.DomainLogic.EditCollection;
using CollectaMundo.DomainLogic.GenerateMissingPng;
using CollectaMundo.ViewModels;
using System.Diagnostics;
using System.Windows;
#endregion
namespace CollectaMundo.ApplicationServices.Startup
{
    public static class StartupComposition
    {
        public static async Task<RootViewModel> BuildAndStartAsync(StatusViewModel statusVM)
        {
            try
            {
                // Infrastructure
                var settings = new JsonAppSettings();

                string getRetailer() => settings.PriceInfo.Retailer;
                void setRetailerAndPersist(string key)
                {
                    // persist to appsettings.json
                    settings.UpdatePriceInfo(updatedDate: null, retailer: key);
                }

                var RemoteLookups = new RemoteLookups();
                var dbFactory = AppGlobals.DbFactory = new DbConnectionFactory(settings);
                var downloadService = new DownloadService();

                // Card DB prep (repos + services)
                var missingPngRepo = new GenerateMissingPngRepository();
                var missingPngLogic = new GenerateMissingPngLogic();
                var missingPngSvc = new GenerateMissingPngService(missingPngRepo, RemoteLookups, missingPngLogic);

                var cardPriceRepo = new CardPriceRepository();
                var priceService = new CardPriceService(settings, cardPriceRepo);

                var prepRepo = new CardDatabasePreparationRepo();
                var progressSinks = CreateProgressSinks(statusVM);

                var prepService = new CardDatabasePreparationService(settings, dbFactory, progressSinks, prepRepo, priceService, missingPngSvc, downloadService, RemoteLookups);

                var integrityService = new DatabaseIntegrityService(settings);

                // Status overlay
                statusVM.ShowStatusOverlay("Checking database integrity…");
                await UIHelper.ForceRenderAsync();

                // First-time setup / repair if needed
                var dbStatus = await integrityService.GetDatabaseStatusAsync();

                if (dbStatus is DatabaseStatus.Missing or DatabaseStatus.Corrupt)
                {
                    var prepResult = await prepService.FirstTimeDbPrepOrchetrator();
                    if (prepResult.Code != OperationResultCode.Success)
                    {
                        ShowStartupFailure(statusVM, prepResult);
                        throw new InvalidOperationException("Database preparation did not complete successfully.");
                    }
                }

                // Main app services (feature layer)
                statusVM.ResetStatusOverlay();
                statusVM.StatusLabel3 = "Loading ALL the cards…";
                await UIHelper.ForceRenderAsync();

                var filteringService = new FilteringService();

                var editCollectionRepo = new EditCollectionRepository();
                var editService = new EditCollectionService(new EditCollectionLogic(editCollectionRepo));

                var importExportService = new ImportExportService(new ImportExportRepo());

                var cardLookupsRepo = new CardLookupsRepo();
                var cardLookupsService = new CardLookupsService(cardLookupsRepo, getRetailer);

                var cardListRepo = new CardListRepository();
                var filterDefaultsLogic = new FilterDefaultsLogic();
                var coreAggregator = new CardCoreAggregator();
                var cardListService = new CardListService(cardListRepo, filterDefaultsLogic, cardLookupsService, coreAggregator);

                // Build view model off UI thread
                var mainVM = await Task.Run(() => MainWindowViewModel.CreateAsync(filteringService, editService, importExportService, prepService, downloadService, statusVM, cardListService, getRetailer, setRetailerAndPersist));

                // Show initial UI
                mainVM.FilterVM.NotifyFilterChanged();
                mainVM.SideMenuVisibility = Visibility.Visible;
                mainVM.ContentSectionVisibility = Visibility.Visible;
                mainVM.MainGridVisibility = Visibility.Visible;

                statusVM.HideStatusOverlay();
                return new RootViewModel(mainVM, statusVM);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup failed: {ex.Message}");
                throw;
            }

            // --- local helpers ---

            static ProgressSinks CreateProgressSinks(StatusViewModel vm) => new()
            {
                Headline = new Progress<string>(s => vm.StatusLabel1 = s),
                Detail = new Progress<string>(s => vm.StatusLabel2 = s),
                Step = new Progress<string>(s => vm.StatusLabel3 = s),
                Percent = new Progress<int>(p => vm.ProgressValue = p),
                ProgressBarVisible = new Progress<bool>(v =>
                    vm.ProgressVisibility = v ? Visibility.Visible : Visibility.Collapsed)
            };

            static void ShowStartupFailure(StatusViewModel vm, OperationResult result)
            {
                // Title/above/below mapping
                string main = result.Code switch
                {
                    OperationResultCode.NoInternet => "No internet connection! Internet connection is required to continue.",
                    OperationResultCode.DownloadFailed => "First time setup failed! Could not download necessary resource files.",
                    OperationResultCode.Error => "First time setup failed! CollectaMundo cannot continue",
                    _ => "An unknown error occurred during database preparation."
                };

                string above = result.Code switch
                {
                    OperationResultCode.NoInternet => result.Message,
                    OperationResultCode.DownloadFailed => result.Message,
                    OperationResultCode.Error => result.Message,
                    _ => "Unknown error"
                };

                const string below = "CollectaMundo will close down shortly.";

                vm.ShowStatusOverlay(main);
                vm.StatusLabel2 = above;
                vm.StatusLabel3 = below;
                vm.ProgressVisibility = Visibility.Collapsed;
                vm.LogoVisibility = Visibility.Collapsed;
                vm.SetupFailVisibility = Visibility.Visible;
                vm.ProgressValue = 0;
            }
        }
    }
}

