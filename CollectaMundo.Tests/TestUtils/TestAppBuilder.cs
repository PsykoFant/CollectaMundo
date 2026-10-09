using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.CardImages;
using CollectaMundo.ApplicationServices.CardLegalities;
using CollectaMundo.ApplicationServices.CardLists;
using CollectaMundo.ApplicationServices.CardLocations;
using CollectaMundo.ApplicationServices.CardPrices;
using CollectaMundo.ApplicationServices.CollectionMutations;
using CollectaMundo.ApplicationServices.Decks;
using CollectaMundo.ApplicationServices.Decks.DeckExport;
using CollectaMundo.ApplicationServices.Decks.Shared;
using CollectaMundo.ApplicationServices.Filtering;
using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.ApplicationServices.Import;
using CollectaMundo.ApplicationServices.KeyedDataProvider;
using CollectaMundo.ApplicationServices.ModifyCollection;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ApplicationServices.Shared.Operation;
using CollectaMundo.ApplicationServices.Shared.Progress;
using CollectaMundo.ApplicationServices.Shared.UnitOfWork;
using CollectaMundo.Data.Filtering;
using CollectaMundo.DomainLogic.CardImages;
using CollectaMundo.DomainLogic.CardLocations;
using CollectaMundo.DomainLogic.CollectionMutations;
using CollectaMundo.DomainLogic.Decks;
using CollectaMundo.DomainLogic.Filtering;
using CollectaMundo.DomainLogic.Filtering.Enums;
using CollectaMundo.DomainLogic.GenerateMissingPng;
using CollectaMundo.DomainLogic.Import;
using CollectaMundo.DomainLogic.ModifyCollection;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.Infrastructure.CardDatabaseManagement;
using CollectaMundo.Infrastructure.CardDatabaseManagement.CardData;
using CollectaMundo.Infrastructure.CardImages;
using CollectaMundo.Infrastructure.CardLegalities;
using CollectaMundo.Infrastructure.CardLists;
using CollectaMundo.Infrastructure.CardLists.Models;
using CollectaMundo.Infrastructure.CardLocations;
using CollectaMundo.Infrastructure.CardPrices;
using CollectaMundo.Infrastructure.CollectionMutations;
using CollectaMundo.Infrastructure.Decks;
using CollectaMundo.Infrastructure.GenerateMissingPng;
using CollectaMundo.Infrastructure.Import;
using CollectaMundo.Infrastructure.KeyedDataProvider;
using CollectaMundo.Infrastructure.ModifyCollection;
using CollectaMundo.Infrastructure.RemoteLookups;
using CollectaMundo.Infrastructure.Shared.Database;
using CollectaMundo.Infrastructure.Shared.Desktop;
using CollectaMundo.Infrastructure.Shared.Files;
using CollectaMundo.Infrastructure.Shared.FileTransfers;
using CollectaMundo.ViewModels;
using CollectaMundo.ViewModels.Shared;
using System.Net.Http;

namespace CollectaMundo.Tests.TestUtils;

public static class TestAppBuilder
{
    public static async Task<(MainWindowViewModel VM, OperationOverlayViewModel OperationOverlayVM)> BuildAsync(InMemoryDatabaseFixture fixture, IDbConnectionFactory dbFactory, List<CollectionChangeSet<CollectionCardDbRow>>? eventSink = null, IUserPromptService? promptOverride = null, IFileSystemPicker? filePickerOverride = null)
    {
        await fixture.InitializeAsync();

        var uowRunner = new UnitOfWorkRunner(dbFactory);

        var userPromptService = promptOverride ?? new UserPromptService();
        var operationOverlayViewModel = new OperationOverlayViewModel(userPromptService);

        var operationOverlayController = new OperationOverlayController(operationOverlayViewModel);
        var settings = new AppSettings();

        string getRetailer() => settings.PriceInfo.Retailer;

        var remoteLookups = new RemoteLookups();

        var missingPngService = new GenerateMissingPngService(
            uowRunner,
            new GenerateMissingPngRepo(),
            remoteLookups,
            new GenerateMissingPngLogic());

        var priceService = new CardPriceService(new CardPriceRepository());
        var progressSinks = CreateProgressSinks(operationOverlayController);

        var cardDbManagementService = new CardDatabaseManagementService(settings, dbFactory, uowRunner, progressSinks, new CardDatabaseManagementRepo(new CsvFileWriter()), new CardDataRepo(), priceService, missingPngService, remoteLookups, new RemoteFileDownloader(new HttpClient()), new GzipFileDecompressor());

        var keyedDataProviderService = new KeyedDataProviderService(uowRunner, new KeyedDataProviderRepo(), getRetailer);

        var cardLegalityProviderService = new CardLegalityProviderService(uowRunner, new CardLegalityRepo());

        var cardListService = new CardListService(uowRunner, new CardListRepo(), new FilterDefaultsLogic(cardLegalityProviderService), keyedDataProviderService, cardLegalityProviderService);

        var collectionMutationsLogic = new CollectionMutationsLogic();
        var collectionMutationsRepo = new CollectionMutationsRepo();
        var collectionMutationsService = new CollectionMutationsService(collectionMutationsLogic, collectionMutationsRepo);

        var cardLocationLookupStore = new CardLocationLookupStore();
        var cardLocationRepo = new CardLocationRepo();
        var cardLocationService = new CardLocationService(uowRunner, cardLocationRepo, new CardLocationLogic(), cardLocationLookupStore, collectionMutationsService);
        var deckManagementStore = new DeckManagementStore(cardLocationService, cardLegalityProviderService);
        var deckBuilderService = new DeckBuilderService(uowRunner, new DeckCardReader(uowRunner, new DeckBuilderRepo()), cardLegalityProviderService, new DeckBuilderLogic(), new DeckBuilderRepo());
        var deckExportService = new DeckExportService(new DeckCardReader(uowRunner, new DeckBuilderRepo()), new DeckExportLogic(), new CsvFileWriter());

        var modifyService = new ModifyCollectionService(uowRunner, new ModifyCollectionLogic(), new ModifyCollectionRepo(), collectionMutationsService);

        var cardImageService = new CardImageService(uowRunner, remoteLookups, new CardImageLogic(), new CardImageRepo(), new CardImageDownloader(settings));

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
            cardDbManagementService,
            importService,
            operationOverlayController,
            userPromptService,
            picker,
            cardListService,
            cardLocationService,
            cardLocationLookupStore,
            deckManagementStore,
            deckBuilderService,
            deckExportService,
            settings,
            scheduler);

        if (eventSink is not null)
        {
            mainVM.AddCardsVM.CollectionChanged += (_, e) => eventSink.Add(e);
            mainVM.EditCardsVM.CollectionChanged += (_, e) => eventSink.Add(e);
        }

        var searchLogic = new FilterItemSearchLogic();

        foreach (var kvp in mainVM.FilterPanelVM.Filters.ToList())
        {
            var old = kvp.Value;

            if (old.FilterCategory == FilterType.Single)
            {
                var testable = new TestableFilterItemViewModel(
                    old.CriteriaKey,
                    old.FilterOptions,
                    old.DefaultText,
                    old.ReadableLabel ?? old.CriteriaKey,
                    mainVM.FilterPanelVM,
                    searchLogic,
                    numericOptions: null)
                {
                    OperatorSelection = old.OperatorSelection
                };

                mainVM.FilterPanelVM.Filters[kvp.Key] = testable;
            }
        }

        mainVM.FilterPanelVM.NotifyFilterChanged();

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
        ProgressBarIndeterminate = new Progress<bool>(operationOverlayController.SetProgressIndeterminate),
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
    private sealed class ImmediateScheduler : IFacetUpdateScheduler
    {
        public void Schedule(Action run) => run();
        public void Cancel() { }
    }
}
