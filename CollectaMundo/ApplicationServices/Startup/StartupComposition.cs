#region using directives
using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.CardImages;
using CollectaMundo.ApplicationServices.CardLists;
using CollectaMundo.ApplicationServices.CardLists.CardLookups;
using CollectaMundo.ApplicationServices.CardPrices;
using CollectaMundo.ApplicationServices.EditCollection;
using CollectaMundo.ApplicationServices.Filtering;
using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.ApplicationServices.Import;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ApplicationServices.Shared.Progress;
using CollectaMundo.Data.Filtering;
using CollectaMundo.DomainLogic.CardImages;
using CollectaMundo.DomainLogic.CardLists;
using CollectaMundo.DomainLogic.EditCollection;
using CollectaMundo.DomainLogic.GenerateMissingPng;
using CollectaMundo.Infrastructure.CardDatabaseManagement;
using CollectaMundo.Infrastructure.CardImages;
using CollectaMundo.Infrastructure.CardLists;
using CollectaMundo.Infrastructure.CardPrices;
using CollectaMundo.Infrastructure.Common;
using CollectaMundo.Infrastructure.EditCollection;
using CollectaMundo.Infrastructure.GenerateMissingPng;
using CollectaMundo.Infrastructure.Import;
using CollectaMundo.Infrastructure.RemoteLookups;
using CollectaMundo.Presentation;
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
                var settings = new AppSettings();


                // Delegate for retailer access from view models                
                string getRetailer() => settings.PriceInfo.Retailer;

                var remoteLookups = new RemoteLookups();
                var dbFactory = new DbConnectionFactory(settings);

                // Card DB prep (repos + services)
                var missingPngRepo = new GenerateMissingPngRepo();
                var missingPngLogic = new GenerateMissingPngLogic();
                var missingPngSvc = new GenerateMissingPngService(missingPngRepo, remoteLookups, missingPngLogic);

                var cardPriceRepo = new CardPriceRepository();
                var priceService = new CardPriceService(settings, cardPriceRepo);

                var cardDbManagementRepo = new CardDatabaseManagementRepo();
                var progressSinks = CreateProgressSinks(statusVM);

                var cardDbManagementService = new CardDatabaseManagementService(settings, dbFactory, progressSinks, cardDbManagementRepo, priceService, missingPngSvc, remoteLookups);

                var integrityService = new DatabaseIntegrityService(dbFactory, settings);

                // Status overlay
                statusVM.ShowStatusOverlay(string.Empty);
                statusVM.StatusLabel3 = "Checking database integrity…";
                await UIHelper.ForceRenderAsync();

                // First-time setup / repair if needed
                var dbStatus = await integrityService.GetDatabaseStatusAsync();

                if (dbStatus is DatabaseStatus.Missing or DatabaseStatus.Corrupt)
                {
                    var prepResult = await cardDbManagementService.FirstTimeDbPrepOrchetrator();
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

                var editCollectionRepo = new EditCollectionRepo();
                var editService = new EditCollectionService(dbFactory, new EditCollectionLogic(editCollectionRepo));

                var importService = new ImportService(new ImportRepo(), settings);

                var cardLookupsRepo = new CardLookupsRepo();
                var cardLookupsService = new CardLookupsService(dbFactory, cardLookupsRepo, getRetailer);

                var cardImageDownloader = new CardImageDownloader(settings);
                var cardImageService = new CardImageService(dbFactory, remoteLookups, new CardImageLogic(), new CardImageRepo(), cardImageDownloader);

                var cardListRepo = new CardListRepo();
                var filterDefaultsLogic = new FilterDefaultsLogic();
                var coreAggregator = new CardCoreAggregator();
                var cardListService = new CardListService(dbFactory, cardListRepo, filterDefaultsLogic, cardLookupsService, coreAggregator);

                // Build view model off UI thread
                var mainVM = await Task.Run(() => MainWindowViewModel.CreateAsync(filteringService, editService, cardImageService, importService, cardDbManagementService, statusVM, cardListService, settings));

                mainVM.FilterVM.NotifyFilterChanged();
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
                    vm.ProgressVisibility = v ? Visibility.Visible : Visibility.Collapsed),

                CancelEnabled = new Progress<bool>(enabled =>
                {
                    if (enabled)
                    {
                        vm.SetPrimaryAction(_ => vm.StatusLabel2 = "Cancelling...");
                    }
                    else
                    {
                        vm.SetPrimaryAction(null);
                        vm.PrimaryButtonVisibility = Visibility.Collapsed;
                    }
                })
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

