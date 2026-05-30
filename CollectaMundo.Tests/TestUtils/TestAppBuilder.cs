using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.CardImages;
using CollectaMundo.ApplicationServices.CardLists;
using CollectaMundo.ApplicationServices.CardLocations;
using CollectaMundo.ApplicationServices.CardPrices;
using CollectaMundo.ApplicationServices.CollectionMaterialization;
using CollectaMundo.ApplicationServices.CollectionMutations;
using CollectaMundo.ApplicationServices.Decks;
using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.ApplicationServices.Import;
using CollectaMundo.ApplicationServices.KeyedDataProvider;
using CollectaMundo.ApplicationServices.ModifyCollection;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ApplicationServices.Shared.Progress;
using CollectaMundo.Data.Filtering;
using CollectaMundo.DomainLogic.CardImages;
using CollectaMundo.DomainLogic.CardLists.Aggregation;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CardLocations;
using CollectaMundo.DomainLogic.CollectionMutations;
using CollectaMundo.DomainLogic.Filtering;
using CollectaMundo.DomainLogic.Filtering.Enums;
using CollectaMundo.DomainLogic.GenerateMissingPng;
using CollectaMundo.DomainLogic.Import;
using CollectaMundo.DomainLogic.ModifyCollection;
using CollectaMundo.DomainLogic.Shared.Models;
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
using CollectaMundo.Tests.ScenarioTests;
using CollectaMundo.ViewModels;
using CollectaMundo.ViewModels.Shared;

namespace CollectaMundo.Tests.TestUtils;

public static class TestAppBuilder
{
    public static async Task<(MainWindowViewModel VM, OperationOverlayViewModel OperationOverlayVM)> BuildAsync(
        InMemoryDatabaseFixture fixture,
        IDbConnectionFactory dbFactory,
        List<CollectionChangeSet<CardSet>>? eventSink = null,
        IUserPromptService? promptOverride = null,
        IFileSystemPicker? filePickerOverride = null)
    {
        await fixture.InitializeAsync();

        var uowRunner = new UnitOfWorkRunner(dbFactory);

        var userPromptService = promptOverride ?? new UserPromptService();
        var operationOverlayViewModel = new OperationOverlayViewModel(userPromptService);

        var operationOverlayController = new OperationOverlayController(operationOverlayViewModel);
        var settings = new AppSettings();

        string getRetailer() => settings.PriceInfo.Retailer;

        var remoteLookups = new RemoteLookups();

        var missingPngSvc = new GenerateMissingPngService(
            new GenerateMissingPngRepo(),
            remoteLookups,
            new GenerateMissingPngLogic());

        var priceService = new CardPriceService(
            settings,
            new CardPriceRepository());

        var prepService = new CardDatabaseManagementService(
            settings,
            dbFactory,
            CreateProgressSinks(operationOverlayController),
            new CardDatabaseManagementRepo(),
            priceService,
            missingPngSvc,
            remoteLookups);

        var keyedDataProviderService = new KeyedDataProviderService(
            uowRunner,
            new KeyedDataProviderRepo(),
            getRetailer);

        var collectionMaterializer = new CollectionMaterializer();

        var cardListService = new CardListService(
            dbFactory,
            new CardListRepo(),
            new FilterDefaultsLogic(),
            keyedDataProviderService,
            new CardCoreAggregator(),
            collectionMaterializer);

        var collectionMutationsLogic = new CollectionMutationsLogic();
        var collectionMutationsRepo = new CollectionMutationsRepo();
        var collectionMutationsService = new CollectionMutationsService(collectionMutationsRepo);
        var collectionChangeSetApplier = new CollectionChangeSetApplier(collectionMaterializer);

        var cardLocationLookupStore = new CardLocationLookupStore();
        var cardLocationRepo = new CardLocationRepo();
        var cardLocationService = new CardLocationService(uowRunner, cardLocationRepo, new CardLocationLogic(), cardLocationLookupStore, collectionMutationsLogic, collectionMutationsService);
        var deckManagementStore = new DeckManagementStore(cardLocationService);

        var modifyService = new ModifyCollectionService(
            uowRunner,
            new ModifyCollectionLogic(),
            new ModifyCollectionRepo(),
            collectionMutationsService,
            collectionMutationsLogic);

        var cardImageService = new CardImageService(
            uowRunner,
            remoteLookups,
            new CardImageLogic(),
            new CardImageRepo(),
            new CardImageDownloader(settings));

        var picker = filePickerOverride ?? new FileSystemPicker();

        var importService = new ImportService(
            uowRunner,
            new ImportRepo(),
            picker,
            new ImportLogic(),
            cardLocationService);

        var scheduler = new ImmediateScheduler();

        var mainVM = await MainWindowViewModel.CreateAsync(
            modifyService,
            cardImageService,
            prepService,
            importService,
            operationOverlayController,
            userPromptService,
            picker,
            cardListService,
            collectionMaterializer,
            collectionChangeSetApplier,
            cardLocationService,
            cardLocationLookupStore,
            deckManagementStore,
            settings,
            scheduler);

        //(modifyService, cardImageService, cardDbManagementService, importService, operationOverlayController, userPromptService, fileSystemPicker, cardListService, collectionMaterializer, collectionChangeSetApplier, cardLocationService, cardLocationLookupStore, deckManagementService, deckManagementStore, settings)

        if (eventSink is not null)
        {
            mainVM.AddCardsVM.CollectionChanged += (_, e) => eventSink.Add(e);
            mainVM.EditCardsVM.CollectionChanged += (_, e) => eventSink.Add(e);
        }

        var searchLogic = new FilterItemSearchLogic();

        foreach (var kvp in mainVM.FilterVM.Filters.ToList())
        {
            var old = kvp.Value;

            if (old.FilterCategory == FilterType.Single)
            {
                var testable = new TestableFilterItemViewModel(
                    old.CriteriaKey,
                    old.FilterOptions,
                    old.DefaultText,
                    old.ReadableLabel ?? old.CriteriaKey,
                    mainVM.FilterVM,
                    searchLogic,
                    numericOptions: null)
                {
                    OperatorSelection = old.OperatorSelection
                };

                mainVM.FilterVM.Filters[kvp.Key] = testable;
            }
        }

        mainVM.FilterVM.NotifyFilterChanged();

        SpinWait.SpinUntil(() =>
            mainVM.AllCardsVM.Cards.Count >= 61 &&
            mainVM.MyCollectionVM.Cards.Count >= 22,
            millisecondsTimeout: 500);

        return (mainVM, operationOverlayViewModel);
    }

    private static ProgressSinks CreateProgressSinks(IOperationOverlayController operationOverlayController) => new()
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
}
