using CollectaMundo.ApplicationServices.CardPrices;
using CollectaMundo.ApplicationServices.DownloadResourceFiles;
using CollectaMundo.ApplicationServices.EditCollection;
using CollectaMundo.ApplicationServices.Filtering;
using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.ApplicationServices.ImportExport;
using CollectaMundo.ApplicationServices.UpdateDB;
using CollectaMundo.ApplicationServices.Utilities;
using CollectaMundo.ApplicationServices.Utilities.InternetCheck;
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
                var dbFactory = AppGlobals.DbFactory = new DbConnectionFactory(settings);

                var missingPngRepo = new GenerateMissingPngRepository();
                var missingPngLogic = new GenerateMissingPngLogic();
                var missingPngService = new GenerateMissingPngService(missingPngRepo, scryfallLookups, missingPngLogic);

                var cardPriceRepo = new CardPriceRepository();
                var priceService = new CardPriceService(settings, cardPriceRepo);

                var downloadService = new DownloadService();
                var internetCheckService = new InternetConnectivityService();

                var schemaInitializer = new DatabaseSchemaRepository();
                var prepService = new CardDatabasePreparationService(settings, dbFactory, schemaInitializer, priceService, missingPngService, downloadService, internetCheckService);
                var integrityService = new DatabaseIntegrityService(settings);

                statusVM.ShowStatusOverlay("Checking database integrity…");
                await UIHelper.ForceRenderAsync();

                var dbStatus = await integrityService.GetDatabaseStatusAsync();

                if (dbStatus is DatabaseStatus.Missing or DatabaseStatus.Corrupt)
                {

                    var progress = new Progress<SetupProgress>(u =>
                    {
                        if (u.Headline is not null) statusVM.StatusLabel1 = u.Headline;
                        if (u.Detail is not null) statusVM.StatusLabel2 = u.Detail;
                        if (u.Step is not null) statusVM.StatusLabel3 = u.Step;
                        if (u.Percent is not null) statusVM.ProgressValue = u.Percent.Value;
                        if (u.IsProgressVisible is not null)
                            statusVM.ProgressVisibility = u.IsProgressVisible.Value ? Visibility.Visible : Visibility.Collapsed;
                    });

                    prepService.SetProgress(progress);

                    var prepResult = await prepService.FirstTimeDbPrepOrchetrator();
                    if (prepResult.Code != OperationResultCode.Success)
                    {
                        switch (prepResult.Code)
                        {
                            case OperationResultCode.Error:
                                ShowStartupFatal(
                                    statusVM,
                                    main: "First time setup failed! CollectaMundo cannot continue",
                                    above: prepResult.Message,
                                    below: "CollectaMundo will close down shortly.");
                                break;

                            case OperationResultCode.NoInternet:
                                ShowStartupFatal(
                                    statusVM,
                                    main: "No internet connection! Internet connection is required to continue.",
                                    above: prepResult.Message,
                                    below: "CollectaMundo will close down shortly.");
                                break;

                            case OperationResultCode.DownloadFailed:
                                ShowStartupFatal(
                                    statusVM,
                                    main: "First time setup failed! Could not download necessary resource files.",
                                    above: prepResult.Message,
                                    below: "CollectaMundo will close down shortly.");
                                break;

                            default:
                                ShowStartupFatal(
                                    statusVM,
                                    main: "An unknown error occurred during database preparation.",
                                    above: "Unknown error",
                                    below: "CollectaMundo will close down shortly.");
                                break;
                        }
                        throw new InvalidOperationException("Database preparation did not complete successfully.");
                    }
                }

                statusVM.StatusLabel3 = "Loading ALL the cards…";
                await UIHelper.ForceRenderAsync();

                // Construct your new DI services
                var filteringService = new FilteringService();
                var editService = new EditCollectionService();
                var importExportService = new ImportExportService(new ImportExportRepo());
                var updateService = new UpdateService(settings, AppGlobals.DbFactory, downloadService, internetCheckService, new UpdateDbRepo(), new UpdateDbRemoteData());

                var mainVM = await Task.Run(() => MainWindowViewModel.CreateAsync(filteringService, editService, importExportService, updateService, downloadService, statusVM));

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
        private static void ShowStartupFatal(StatusViewModel vm, string main, string above, string below)
        {
            vm.ShowStatusOverlay(main);           // your existing overlay method
            vm.StatusLabel2 = above;                // details/error
            vm.StatusLabel3 = below;               // “The app will close shortly…” etc.
            vm.ProgressVisibility = Visibility.Collapsed;
            vm.ProgressVisibility = Visibility.Collapsed;
            vm.LogoVisibility = Visibility.Collapsed;
            vm.SetupFailVisibility = Visibility.Visible;
        }
    }

}
