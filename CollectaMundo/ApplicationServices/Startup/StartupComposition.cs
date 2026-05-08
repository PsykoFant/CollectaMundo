#region using directives
using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.CardImages;
using CollectaMundo.ApplicationServices.CardLists;
using CollectaMundo.ApplicationServices.CardLocations;
using CollectaMundo.ApplicationServices.CardPrices;
using CollectaMundo.ApplicationServices.CollectionMaterialization;
using CollectaMundo.ApplicationServices.CollectionMutations;
using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.ApplicationServices.Import;
using CollectaMundo.ApplicationServices.KeyedDataProvider;
using CollectaMundo.ApplicationServices.ModifyCollection;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ApplicationServices.Shared.Progress;
using CollectaMundo.Data.Filtering;
using CollectaMundo.DomainLogic.CardImages;
using CollectaMundo.DomainLogic.CardLists.Aggregation;
using CollectaMundo.DomainLogic.CardLocations;
using CollectaMundo.DomainLogic.CollectionMutations;
using CollectaMundo.DomainLogic.GenerateMissingPng;
using CollectaMundo.DomainLogic.Import;
using CollectaMundo.DomainLogic.ModifyCollection;
using CollectaMundo.Infrastructure.CardDatabaseManagement;
using CollectaMundo.Infrastructure.CardImages;
using CollectaMundo.Infrastructure.CardLists;
using CollectaMundo.Infrastructure.CardLocations;
using CollectaMundo.Infrastructure.CardPrices;
using CollectaMundo.Infrastructure.CollectionMutations;
using CollectaMundo.Infrastructure.GenerateMissingPng;
using CollectaMundo.Infrastructure.Import;
using CollectaMundo.Infrastructure.KeyedDataProvider;
using CollectaMundo.Infrastructure.ModifyCollection;
using CollectaMundo.Infrastructure.RemoteLookups;
using CollectaMundo.Infrastructure.Shared;
using CollectaMundo.Presentation;
using CollectaMundo.ViewModels;
using CollectaMundo.ViewModels.Shared;
using System.Diagnostics;
#endregion
namespace CollectaMundo.ApplicationServices.Startup
{
    public static class StartupComposition
    {
        public static async Task<RootViewModel> BuildAndStartAsync(OperationOverlayViewModel operationOverlayViewModel, IUserPromptService userPromptService)
        {
            try
            {
                // Infrastructure
                var settings = new AppSettings();
                var operationOverlayController = new OperationOverlayController(operationOverlayViewModel);

                // Delegate for retailer access from view models                
                string getRetailer() => settings.PriceInfo.Retailer;

                var remoteLookups = new RemoteLookups();
                var dbFactory = new DbConnectionFactory(settings);

                // Card DB prep (repos + services)
                var missingPngService = new GenerateMissingPngService(new GenerateMissingPngRepo(), remoteLookups, new GenerateMissingPngLogic());
                var priceService = new CardPriceService(settings, new CardPriceRepository());

                var progressSinks = CreateProgressSinks(operationOverlayController);
                var cardDbManagementService = new CardDatabaseManagementService(settings, dbFactory, progressSinks, new CardDatabaseManagementRepo(), priceService, missingPngService, remoteLookups);

                var integrityService = new DatabaseIntegrityService(dbFactory, settings);

                // Status overlay
                operationOverlayController.Show(string.Empty);
                operationOverlayController.SetDetail("Checking database integrity…");
                await UIHelper.ForceRenderAsync();

                // First-time setup / repair if needed
                var dbStatus = await integrityService.GetDatabaseStatusAsync();

                if (dbStatus is DatabaseStatus.Missing or DatabaseStatus.Corrupt)
                {
                    var prepResult = await cardDbManagementService.FirstTimeDbPrepOrchestrator();
                    if (prepResult.Code != OperationResultCode.Success)
                    {
                        ShowStartupFailure(operationOverlayController, prepResult);
                        throw new InvalidOperationException("Database preparation did not complete successfully.");
                    }
                }

                // Main app services (feature layer)
                operationOverlayController.Reset();
                operationOverlayController.SetDetail("Loading ALL the cards…");
                await UIHelper.ForceRenderAsync();

                var collectionMutationsLogic = new CollectionMutationsLogic();
                var collectionMutationsRepo = new CollectionMutationsRepo();
                var collectionMaterializer = new CollectionMaterializer();

                var collectionMutationsService = new CollectionMutationsService(collectionMutationsRepo);                                
                var collectionChangeSetApplier = new CollectionChangeSetApplier(collectionMaterializer);

                var modifyService = new ModifyCollectionService(dbFactory, new ModifyCollectionLogic(), new ModifyCollectionRepo(), collectionMutationsService, collectionMutationsLogic);
                var fileSystemPicker = new FileSystemPicker();

                var cardImageDownloader = new CardImageDownloader(settings);
                var cardImageService = new CardImageService(dbFactory, remoteLookups, new CardImageLogic(), new CardImageRepo(), cardImageDownloader);

                var keyedDataProviderService = new KeyedDataProviderService(dbFactory, new KeyedDataProviderRepo(), getRetailer);
                var cardListService = new CardListService(dbFactory, new CardListRepo(), new FilterDefaultsLogic(), keyedDataProviderService, new CardCoreAggregator(), collectionMaterializer);
                var cardLocationLookupStore = new CardLocationLookupStore();
                var cardLocationService = new CardLocationService(dbFactory,new CardLocationRepo(),new CardLocationLogic(),cardLocationLookupStore,collectionMutationsLogic,collectionMutationsService);

                var importService = new ImportService(dbFactory, new ImportRepo(), fileSystemPicker, new ImportLogic(), cardLocationService);

                // CreateCollectionChangeSetFromEdits view model off UI thread
                var mainVM = await Task.Run(() => MainWindowViewModel.CreateAsync(modifyService, cardImageService, cardDbManagementService, importService, operationOverlayController, userPromptService, fileSystemPicker, cardListService, collectionMaterializer, collectionChangeSetApplier, cardLocationService, cardLocationLookupStore, settings));

                mainVM.FilterVM.NotifyFilterChanged();
                operationOverlayController.Hide();
                return new RootViewModel(mainVM, operationOverlayViewModel);
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

            static void ShowStartupFailure(IOperationOverlayController operationOverlayController, OperationResult result)
            {
                // Title/above/below mapping
                string headline = result.Code switch
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

                operationOverlayController.Show(headline);
                operationOverlayController.SetStep(above);
                operationOverlayController.SetDetail(below);
                operationOverlayController.ShowProgress(false);
                operationOverlayController.ShowLogo(false);
                operationOverlayController.ShowSetupFailure(true);
                operationOverlayController.SetProgress(0);
            }
        }
    }
}

