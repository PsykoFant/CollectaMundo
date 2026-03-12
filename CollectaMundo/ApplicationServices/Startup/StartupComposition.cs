#region using directives
using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.CardImages;
using CollectaMundo.ApplicationServices.CardLists;
using CollectaMundo.ApplicationServices.CardLists.CardLookups;
using CollectaMundo.ApplicationServices.CardPrices;
using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.ApplicationServices.Import;
using CollectaMundo.ApplicationServices.ModifyCollection;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ApplicationServices.Shared.Progress;
using CollectaMundo.Data.Filtering;
using CollectaMundo.DomainLogic.CardImages;
using CollectaMundo.DomainLogic.CardLists.Aggregation;
using CollectaMundo.DomainLogic.GenerateMissingPng;
using CollectaMundo.DomainLogic.Import;
using CollectaMundo.DomainLogic.ModifyCollection;
using CollectaMundo.Infrastructure.CardDatabaseManagement;
using CollectaMundo.Infrastructure.CardImages;
using CollectaMundo.Infrastructure.CardLists;
using CollectaMundo.Infrastructure.CardPrices;
using CollectaMundo.Infrastructure.GenerateMissingPng;
using CollectaMundo.Infrastructure.Import;
using CollectaMundo.Infrastructure.ModifyCollection;
using CollectaMundo.Infrastructure.RemoteLookups;
using CollectaMundo.Infrastructure.Shared;
using CollectaMundo.Presentation;
using CollectaMundo.ViewModels;
using CollectaMundo.ViewModels.Shared;
using System.Diagnostics;
using System.Windows;
#endregion
namespace CollectaMundo.ApplicationServices.Startup
{
    public static class StartupComposition
    {
        public static async Task<RootViewModel> BuildAndStartAsync(IOperationOverlayController operationOverlayController,IUserPromptService userPromptService)
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
                var progressSinks = CreateProgressSinks(operationOverlayController);

                var cardDbManagementService = new CardDatabaseManagementService(settings, dbFactory, progressSinks, cardDbManagementRepo, priceService, missingPngSvc, remoteLookups);

                var integrityService = new DatabaseIntegrityService(dbFactory, settings);

                // Status overlay
                operationOverlayController.Show(string.Empty);
                operationOverlayController.("Checking database integrity…");
                await UIHelper.ForceRenderAsync();

                // First-time setup / repair if needed
                var dbStatus = await integrityService.GetDatabaseStatusAsync();

                if (dbStatus is DatabaseStatus.Missing or DatabaseStatus.Corrupt)
                {
                    var prepResult = await cardDbManagementService.FirstTimeDbPrepOrchestrator();
                    if (prepResult.Code != OperationResultCode.Success)
                    {
                        ShowStartupFailure(operationOverlayVM, prepResult);
                        throw new InvalidOperationException("Database preparation did not complete successfully.");
                    }
                }

                // Main app services (feature layer)
                operationOverlayVM.ResetStatusOverlay();
                operationOverlayVM.StatusLabel3 = "Loading ALL the cards…";
                await UIHelper.ForceRenderAsync();

                var modifyService = new ModifyCollectionService(dbFactory, new ModifyCollectionLogic(), new ModifyCollectionRepo());

                var fileSystemPicker = new FileSystemPicker();
                var importService = new ImportService(dbFactory, new ImportRepo(), fileSystemPicker, new ImportLogic());

                var cardImageDownloader = new CardImageDownloader(settings);
                var cardImageService = new CardImageService(dbFactory, remoteLookups, new CardImageLogic(), new CardImageRepo(), cardImageDownloader);

                var cardListRepo = new CardListRepo();
                var filterDefaultsLogic = new FilterDefaultsLogic();
                var coreAggregator = new CardCoreAggregator();
                var cardLookupsService = new CardLookupsService(dbFactory, new CardLookupsRepo(), getRetailer);
                var cardListService = new CardListService(dbFactory, cardListRepo, filterDefaultsLogic, cardLookupsService, coreAggregator);

                // CreateCollectionChangeSetFromEdits view model off UI thread
                var mainVM = await Task.Run(() => MainWindowViewModel.CreateAsync(modifyService, cardImageService, cardDbManagementService, importService, operationOverlayVM, userPromptService, fileSystemPicker, cardListService, settings));

                mainVM.FilterVM.NotifyFilterChanged();
                operationOverlayVM.HideStatusOverlay();
                return new RootViewModel(mainVM, operationOverlayVM);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup failed: {ex.Message}");
                throw;
            }

            // --- local helpers ---

            static ProgressSinks CreateProgressSinks(IOperationOverlayController operationOverlayController) => new()
            {
                Headline = new Progress<string>(s => operationOverlayController.SetHeadline(s)),
                Detail = new Progress<string>(s => operationOverlayController.SetDetail(s)),
                Step = new Progress<string>(s => operationOverlayController.SetStep(s)),
                Percent = new Progress<int>(p => operationOverlayController.SetProgress(p)),
                ProgressBarVisible = new Progress<bool>(v => operationOverlayController.ShowProgress(v)),
                CancelEnabled = new Progress<bool>(enabled =>
                {
                    if (enabled)
                    {
                        operationOverlayController.ShowPrimaryButton(
                            "   Cancel   ",
                            _ => operationOverlayController.SetDetail("Cancelling..."));
                    }
                    else
                    {
                        operationOverlayController.HidePrimaryButton();
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

