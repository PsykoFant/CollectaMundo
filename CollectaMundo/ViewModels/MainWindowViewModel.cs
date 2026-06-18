#region usings & namespace
using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.CardImages;
using CollectaMundo.ApplicationServices.CardLists;
using CollectaMundo.ApplicationServices.CardLocations;
using CollectaMundo.ApplicationServices.CollectionMutations;
using CollectaMundo.ApplicationServices.Decks;
using CollectaMundo.ApplicationServices.Filtering;
using CollectaMundo.ApplicationServices.Import;
using CollectaMundo.ApplicationServices.Import.Models;
using CollectaMundo.ApplicationServices.KeyedDataProvider.Providers;
using CollectaMundo.ApplicationServices.ModifyCollection;
using CollectaMundo.ApplicationServices.Navigation;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ApplicationServices.Shared.Operation;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.DomainLogic.KeyedDataProvider;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.DomainLogic.Shared.Factories;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.Infrastructure.Shared;
using CollectaMundo.Infrastructure.Shared.Models;
using CollectaMundo.Presentation;
using CollectaMundo.ViewModels.CardLists;
using CollectaMundo.ViewModels.Decks;
using CollectaMundo.ViewModels.Filtering;
using CollectaMundo.ViewModels.Import;
using CollectaMundo.ViewModels.ModifyCollection;
using CollectaMundo.ViewModels.Pages;
using CollectaMundo.ViewModels.Pages.SharedElements;
using CollectaMundo.ViewModels.Shell;
using CollectaMundo.ViewModels.SideMenuLeft;
using CollectaMundo.ViewModels.SideMenuRight;
using CollectaMundo.ViewModels.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Diagnostics;

namespace CollectaMundo.ViewModels
{
    #endregion
    public partial class MainWindowViewModel : ObservableObject, ICardCollectionHost, IShellNavigationHost
    {
        #region readonly dependencies
        // App settings
        private readonly IAppSettings _settings;

        // Overlay controllers
        private readonly IOperationOverlayController _operationOverlayController;

        // Card list / card collection management services
        private readonly IModifyCollectionService _modifyService;
        private readonly ICardListService _cardListService;
        private readonly IImportService _importService;
        private readonly ICardLocationService _cardLocationService;
        private readonly ICardLocationLookupStore _cardLocationLookupStore;
        private IKeyedDataProvider<int, CardLocation>? _cardLocationProvider;

        // Deck management service
        private readonly IDeckManagementStore _deckManagementStore;

        // Filtering infrastructure
        private readonly FilteringService _filteringService;
        private readonly IFacetUpdateScheduler _facetScheduler;
        private readonly IFacetUpdater _facetUpdater;

        // User prompt service
        private readonly IUserPromptService _userPromptService;

        // File system picker
        private readonly IFileSystemPicker _filesystemPicker;

        private readonly NavigationCleanupService _navigationCleanupService;

        #endregion

        #region child viewmodels
        // Shell
        public TopMenuViewModel TopMenuVM { get; }

        // Pages
        public CardListPageViewModel<PrintingCard> SearchAndFilterPageVM { get; }
        public CardListPageViewModel<CollectionCard> MyCollectionPageVM { get; }
        public PagesDecksHostViewModel PagesDecksHostVM { get; }
        public PagesUtilitiesHostViewModel PagesUtilitiesHostVM { get; }


        // Menus
        public SideMenuFilteringViewModel FilteringSideMenuVM { get; }
        public SideMenuUtilitiesViewModel UtilitiesSideMenuVM { get; }

        // Content
        public CardListViewModel<PrintingCard> AllCardsVM { get; }
        public CardListViewModel<CollectionCard> MyCollectionVM { get; }
        public CardListViewModel<OracleCard> OracleCardsVM { get; }
        public CardListViewModel<ManaSymbolViewModel> ColorIconsViewModel { get; }
        public ModifyCollectionViewModel AddCardsVM { get; }
        public ModifyCollectionViewModel EditCardsVM { get; }
        public DeckManagementViewModel DeckManagementVM { get; }
        public DeckEditorViewModel DeckEdititorVM { get; }
        public FilterPanelViewModel FilterPanelVM { get; }
        public CardImageViewModel CardImageVM { get; }
        public UtilitiesViewModel UtilitiesVM { get; }
        public ImportViewModel ImportVM { get; }
        public CardLocationViewModel CardLocationVM { get; }
        public PricesViewModel PricesVM { get; }
        #endregion

        #region ui state
        public Action? OnStartupComplete { get; set; }

        [ObservableProperty]
        private object? currentPageViewModel;

        [ObservableProperty]
        private ShellPageEnum currentPage;

        [ObservableProperty]
        private object? currentSideMenuLeftViewModel;

        [ObservableProperty]
        private object? currentSideMenuRightViewModel;

        // Shell UI state properties
        [ObservableProperty]
        private bool isSideMenuLeftVisible = true;

        [ObservableProperty]
        private bool isSideMenuRightVisible = true;

        [ObservableProperty]
        private bool isTopMenuEnabled = true;

        public void SetUiBusy(bool isBusy)
        {
            IsTopMenuEnabled = !isBusy;
            IsSideMenuLeftVisible = !isBusy;
            IsSideMenuRightVisible = !isBusy;
        }

        #endregion

        #region Constructor and factory method
        // Constructor
        private MainWindowViewModel(
            IModifyCollectionService modifyService,
            ICardImageService cardImageService,
            ICardDatabaseManagementService cardDbManagementService,
            IImportService importService,
            IOperationOverlayController operationOverlayController,
            IUserPromptService userPromptService,
            IFileSystemPicker fileSystemPicker,
            ICardListService cardListService,
            ICardLocationService cardLocationService,
            ICardLocationLookupStore cardLocationLookupStore,
            IDeckManagementStore deckManagementStore,
            IAppSettings settings,
            IFacetUpdateScheduler? facetScheduler = null,
            IFacetUpdater? facetUpdater = null)
        {
            _modifyService = modifyService;
            _settings = settings;
            _operationOverlayController = operationOverlayController;
            _filteringService = new FilteringService();
            _cardListService = cardListService;
            _importService = importService;
            _cardLocationService = cardLocationService;
            _cardLocationLookupStore = cardLocationLookupStore;
            _deckManagementStore = deckManagementStore;
            _facetScheduler = facetScheduler ?? new DispatcherDebounceScheduler(TimeSpan.FromMilliseconds(150));
            _facetUpdater = facetUpdater ?? new FacetUpdater();
            _userPromptService = userPromptService;
            _filesystemPicker = fileSystemPicker;

            // cardlist viewmodels
            AllCardsVM = new CardListViewModel<PrintingCard>();
            MyCollectionVM = new CardListViewModel<CollectionCard>();
            OracleCardsVM = new CardListViewModel<OracleCard>();

            List<string> manaKeys = ["{W}", "{U}", "{B}", "{R}", "{G}", "{C}", "{X}"];
            var manaSymbols = manaKeys.Select(key => new ManaSymbolViewModel { ManaCostRaw = key }).ToList();
            ColorIconsViewModel = new CardListViewModel<ManaSymbolViewModel> { Cards = manaSymbols, FilteredCards = manaSymbols };

            // Modify collection viewmodels
            AddCardsVM = new ModifyCollectionViewModel(_modifyService, this, removeCardWhenZero: true);
            EditCardsVM = new ModifyCollectionViewModel(_modifyService, this, removeCardWhenZero: false);

            // filtering viewmodel
            FilterPanelVM = new FilterPanelViewModel(_filteringService);

            // card image viewmodel
            CardImageVM = new CardImageViewModel(cardImageService);

            // Deck management viewmodels
            DeckManagementVM = new DeckManagementViewModel(_cardLocationService, _deckManagementStore);
            DeckEdititorVM = new DeckEditorViewModel(OracleCardsVM, CardImageVM, FilterPanelVM);

            var cardCollectionHost = this;
            var shellUiState = this;
            var utilitiesNavigator = new UtilitiesNavigator();


            // Utility viewmodels
            UtilitiesVM = new UtilitiesViewModel(shellUiState, cardDbManagementService, _operationOverlayController, utilitiesNavigator, _userPromptService, cardCollectionHost, () => MyCollectionVM.Cards.Count, _filesystemPicker);
            ImportVM = new ImportViewModel(_importService, shellUiState, utilitiesNavigator, _userPromptService);
            CardLocationVM = new CardLocationViewModel(_cardLocationService);

            // prices viewmodel
            PricesVM = new PricesViewModel(_settings, cardCollectionHost);

            // Pages viewmodels
            SearchAndFilterPageVM = new PagesSearchAndFilterViewModel(cardsVM: AllCardsVM, cardImageVM: CardImageVM, filterVM: FilterPanelVM, pageTitle: "Search and Filter Cards", cardListPage: ShellPageEnum.SearchAndFilter, primarySubmitButtonText: "Submit these cards to my collection", primarySubmitCommand: AddCardsVM.SubmitNewCardsCommand, pricesVM: PricesVM, modifyCollectionVM: AddCardsVM);
            MyCollectionPageVM = new PagesMyCollectionViewModel(cardsVM: MyCollectionVM, cardImageVM: CardImageVM, filterVM: FilterPanelVM, pageTitle: "My Collection", cardListPage: ShellPageEnum.MyCollection, primarySubmitButtonText: "Update selected cards", primarySubmitCommand: EditCardsVM.SubmitCardEditsCommand, pricesVM: PricesVM, modifyCollectionVM: EditCardsVM);
            PagesDecksHostVM = new PagesDecksHostViewModel(DeckManagementVM, DeckEdititorVM);
            PagesUtilitiesHostVM = new PagesUtilitiesHostViewModel(UtilitiesVM, ImportVM, CardLocationVM, utilitiesNavigator);

            // Side menu viewmodels
            FilteringSideMenuVM = new SideMenuFilteringViewModel(FilterPanelVM, ColorIconsViewModel, shellUiState);
            UtilitiesSideMenuVM = new SideMenuUtilitiesViewModel(UtilitiesVM, PricesVM);

            // Set initial page and menu
            CurrentPageViewModel = SearchAndFilterPageVM;
            CurrentSideMenuLeftViewModel = FilteringSideMenuVM;
            CurrentSideMenuRightViewModel = CardImageVM;
            CurrentPage = ShellPageEnum.SearchAndFilter;

            // Navigation cleanup service
            _navigationCleanupService = new NavigationCleanupService(_userPromptService, _operationOverlayController, utilitiesNavigator);

            // Set up top menu with references to page VMs
            TopMenuVM = new TopMenuViewModel(shellNavigationHost: this, _navigationCleanupService, filteringSideMenuViewModel: FilteringSideMenuVM, utilitiesSideMenuViewModel: UtilitiesSideMenuVM, allCardsPageViewModel: SearchAndFilterPageVM, myCollectionPageViewModel: MyCollectionPageVM, pagesDecksHostViewModel: PagesDecksHostVM, pagesUtilitiesHostVM: PagesUtilitiesHostVM);

            // event wiring
            SubscribeChildVmEvents();
        }

        public static async Task<MainWindowViewModel> CreateAsync(
            IModifyCollectionService editService,
            ICardImageService cardImageService,
            ICardDatabaseManagementService prepService,
            IImportService importService,
            IOperationOverlayController operationOverlayController,
            IUserPromptService userPromptService,
            IFileSystemPicker fileSystemPicker,
            ICardListService cardListService,
            ICardLocationService cardLocationService,
            ICardLocationLookupStore cardLocationLookupStore,
            IDeckManagementStore deckManagementStore,
            IAppSettings settings,
            IFacetUpdateScheduler? facetScheduler = null,
            IFacetUpdater? facetUpdater = null,
            Action? onStartupComplete = null)
        {
            var vm = new MainWindowViewModel(editService, cardImageService, prepService, importService, operationOverlayController, userPromptService, fileSystemPicker, cardListService, cardLocationService, cardLocationLookupStore, deckManagementStore, settings, facetScheduler, facetUpdater)
            {
                OnStartupComplete = onStartupComplete
            };

            await vm.ReloadAllCardListsAndFiltersAsync();
            await vm.ReloadAvailableLocationsAsync();

            vm.OnStartupComplete?.Invoke();
            return vm;
        }

        #endregion

        #region event wiring (subscribe/unsubscribe)
        private void SubscribeChildVmEvents()
        {
            ImportVM.CollectionMutationRequested += OnImportCollectionMutationRequested;
            ImportVM.CardImageSelectionRequested += OnCardImageSelectionRequested;

            AddCardsVM.CollectionChanged += OnCollectionRowsChanged;
            EditCardsVM.CollectionChanged += OnCollectionRowsChanged;
            CardLocationVM.CollectionChanged += OnCollectionRowsChanged;
            DeckManagementVM.CollectionChanged += OnCollectionRowsChanged;

            FilterPanelVM.FilterChanged += OnFilterChanged;
            _cardLocationLookupStore.LocationsChanged += OnLocationsChanged;
        }
        private void UnsubscribeChildVmEvents()
        {
            ImportVM.CollectionMutationRequested -= OnImportCollectionMutationRequested;
            ImportVM.CardImageSelectionRequested -= OnCardImageSelectionRequested;

            AddCardsVM.CollectionChanged -= OnCollectionRowsChanged;
            EditCardsVM.CollectionChanged -= OnCollectionRowsChanged;
            CardLocationVM.CollectionChanged -= OnCollectionRowsChanged;
            DeckManagementVM.CollectionChanged -= OnCollectionRowsChanged;

            FilterPanelVM.FilterChanged -= OnFilterChanged;
            _cardLocationLookupStore.LocationsChanged -= OnLocationsChanged;
        }

        #endregion

        #region event handlers (FilterChanged, CardChanged, CollectionChanged)
        private void OnImportCollectionMutationRequested(object? sender, ImportCollectionUpsertResult mutation)
        {
            var changeSet = BuildCollectionChangeSetFromMutation(mutation);
            OnCollectionChanged(sender, changeSet);
        }
        private CollectionChangeSet<CollectionCard> BuildCollectionChangeSetFromMutation(ImportCollectionUpsertResult mutation)
        {
            var addedOrUpdated = new List<CollectionCard>();

            // Existing in-memory collection cards, keyed by DB collection row id. If an import row updates an existing row, we mutate that existing object so current bindings keep working.
            var cardById = MyCollectionVM.Cards.ToDictionary(c => c.CardId);

            // AllCardsVM is the hydrated printing catalog.
            var printingByUuid = AllCardsVM.Cards.Where(c => !string.IsNullOrWhiteSpace(c.Uuid)).ToDictionary(c => c.Uuid, StringComparer.OrdinalIgnoreCase);

            foreach (var row in mutation.UpsertedRows)
            {
                if (cardById.TryGetValue(row.CardId, out var existingCard))
                {
                    // Import mutation rows contain only collection identity/quantity data. The existing CollectionCard already has the hydrated PrintingCard, so for existing rows we only need to apply quantity deltas.
                    existingCard.CardsOwned += row.CardsOwned;
                    existingCard.CardsForTrade += row.CardsForTrade;
                    existingCard.RecomputeCollectionPrice();

                    addedOrUpdated.Add(existingCard);
                    continue;
                }

                // New row: hydrate collection identity data with the richer PrintingCard so the collection list has name, mana cost, set icon, prices, etc.
                //var card = _collectionMaterializer.MaterializeFromRow(row, printingByUuid);
                if (!printingByUuid.TryGetValue(row.Identity.Uuid, out var printing))
                {
                    throw new InvalidOperationException($"Cannot materialize collection card. Printing not found for UUID '{row.Identity.Uuid}'.");
                }

                var card = CollectionCardFactory.FromPrintingAndDbRow(printing, row);
                addedOrUpdated.Add(card);

                addedOrUpdated.Add(card);
            }

            return new CollectionChangeSet<CollectionCard>
            {
                RemovedIds = [],
                AddedOrUpdated = addedOrUpdated
            };
        }
        private void OnCardImageSelectionRequested(object? sender, string? uuid)
        {
            if (string.IsNullOrWhiteSpace(uuid))
            {
                CardImageVM.SelectedCard = null;
                return;
            }

            CardImageVM.SelectedCard = AllCardsVM.Cards.FirstOrDefault(card => string.Equals(card.Uuid, uuid, StringComparison.OrdinalIgnoreCase));
        }
        private void OnLocationsChanged(object? sender, EventArgs e)
        {
            var locations = _cardLocationLookupStore.GetAll();

            AddCardsVM.SetAvailableLocations(locations);
            EditCardsVM.SetAvailableLocations(locations);

            _cardLocationProvider = new ValueProvider<int, CardLocation>(
                locations.ToDictionary(x => x.Id));

            // Refresh all collection cards with new location provider and updated location info.
            foreach (var card in MyCollectionVM.Cards)
            {
                AttachCardLocationProvider(card);
            }

            // Refresh all open add/edit rows with new location provider and updated location info.
            foreach (var row in AddCardsVM.CardsToAddOrEdit)
            {
                row.CardToAddOrEdit.CardLocationProvider = _cardLocationProvider;
                row.CardToAddOrEdit.RefreshLocationsFromProvider();
            }

            // Refresh all open add/edit rows with new location provider and updated location info.
            foreach (var row in EditCardsVM.CardsToAddOrEdit)
            {
                row.CardToAddOrEdit.CardLocationProvider = _cardLocationProvider;
                row.CardToAddOrEdit.RefreshLocationsFromProvider();
            }

            // Rebuild collection-backed filter options after location display names changed.
            _facetUpdater.RefreshFromCollection(MyCollectionVM.Cards, FilterPanelVM.Filters);

            // Reapply active filters because selected/display values may have changed.
            OnFilterChanged(this, EventArgs.Empty);
        }
        private void OnFilterChanged(object? sender, EventArgs e)
        {
            AllCardsVM.FilteredCards = _filteringService.ApplyFilters(AllCardsVM.Cards, FilterPanelVM.Filters.Values);
            MyCollectionVM.FilteredCards = _filteringService.ApplyFilters(MyCollectionVM.Cards, FilterPanelVM.Filters.Values);
            OracleCardsVM.FilteredCards = _filteringService.ApplyFilters(OracleCardsVM.Cards, FilterPanelVM.Filters.Values);
        }
        private void OnCollectionChanged(object? sender, CollectionChangeSet<CollectionCard> changeSet)
        {
            // Newly materialized CollectionCards need access to location lookups
            // for SelectedLocationName / SelectedLocationType / SelectedLocationDisplayName.

            foreach (var card in changeSet.AddedOrUpdated)
            {
                AttachCardLocationProvider(card);
            }

            CollectionChangeSetApplier.Apply(MyCollectionVM.Cards, changeSet);

            // Apply DB-truth changes to the in-memory collection list.
            CollectionChangeSetApplier.Apply(MyCollectionVM.Cards, changeSet);

            // Open add/edit staging rows may now be stale.
            // Reconcile them against the updated in-memory collection:
            // - remove draft rows whose source CardId no longer exists
            // - refresh draft rows whose source CardId still exists
            AddCardsVM.ReconcileOpenRowsWithCollection(MyCollectionVM.Cards);
            EditCardsVM.ReconcileOpenRowsWithCollection(MyCollectionVM.Cards);

            // Reapply active filters to the updated collection list.
            MyCollectionVM.FilteredCards = _filteringService.ApplyFilters(MyCollectionVM.Cards, FilterPanelVM.Filters.Values);

            // Refresh collection-derived filter facets after mutation.
            _facetScheduler.Cancel();
            _facetScheduler.Schedule(() => _facetUpdater.RefreshFromCollection(MyCollectionVM.Cards, FilterPanelVM.Filters));
        }
        private void OnCollectionRowsChanged(object? sender, CollectionChangeSet<CollectionCardDbRow> rowChangeSet)
        {
            var printingByUuid = AllCardsVM.Cards.Where(c => !string.IsNullOrWhiteSpace(c.Uuid)).ToDictionary(c => c.Uuid, StringComparer.OrdinalIgnoreCase);

            var hydratedChangeSet = new CollectionChangeSet<CollectionCard>
            {
                RemovedIds = rowChangeSet.RemovedIds,
                AddedOrUpdated =
                [
                    .. rowChangeSet.AddedOrUpdated.Select(row =>
                    {
                        if (!printingByUuid.TryGetValue(row.Identity.Uuid, out var printing))
                        {
                            throw new InvalidOperationException($"Cannot materialize collection card. Printing not found for UUID '{row.Identity.Uuid}'.");
                        }

                        return CollectionCardFactory.FromPrintingAndDbRow(printing, row);
                    })
                ]
            };

            OnCollectionChanged(sender, hydratedChangeSet);
        }
        private void AttachCardLocationProvider(CollectionCard card)
        {
            card.CardLocationProvider = _cardLocationProvider;
            card.RefreshLocationsFromProvider();
        }

        #endregion

        #region startup / reload
        public async Task ReloadAllCardListsAndFiltersAsync()
        {
            var sw = Stopwatch.StartNew();

            Debug.WriteLine("[ReloadAllCardListsAsync] Initializing card lists");
            await _cardListService.InitializeCardListsAsync(AllCardsVM, MyCollectionVM, OracleCardsVM, FilterPanelVM.Filters, FilterPanelVM);
            FilterPanelVM.NotifyFiltersRebuilt();
            FilterPanelVM.NotifyFilterChanged();
            Debug.WriteLine($"latest price date from settings: {_settings.PriceInfo.PricesUpdatedDate}");
            PricesVM.RefreshLatestPriceDate();
            sw.Stop();
            Debug.WriteLine($"[ReloadAllCardListsAsync] M1 finished in {sw.ElapsedMilliseconds} ms ({sw.Elapsed}).");
        }
        public async Task ReloadAvailableLocationsAsync()
        {
            var sw = Stopwatch.StartNew();

            Debug.WriteLine("[ReloadAvailableLocationsAsync] Loading card locations");

            var locations = await _cardLocationService.GetAllLocationsAsync();

            _cardLocationLookupStore.ReplaceAll(locations);

            sw.Stop();
            Debug.WriteLine($"[ReloadAvailableLocationsAsync] Finished in {sw.ElapsedMilliseconds} ms ({sw.Elapsed}).");
        }

        // When retailer is changed, refresh prices on all cards
        public async Task RefreshAllPrices()
        {
            await _cardListService.ReloadPriceLookupsAsync(_settings.PriceInfo.Retailer);

            // Force grids to re-read computed price properties.
            AllCardsVM.FilteredCards = [.. AllCardsVM.FilteredCards];
            MyCollectionVM.FilteredCards = [.. MyCollectionVM.FilteredCards];

            Debug.WriteLine($"latest price date from settings: {_settings.PriceInfo.PricesUpdatedDate}");
            PricesVM.RefreshLatestPriceDate();
        }
        public ICollectionSnapshot CreateMyCollectionSnapshot()
        {
            return CollectionSnapshot.From(MyCollectionVM.Cards);
        }

        #endregion

        #region disposal
        public void Dispose()
        {
            UnsubscribeChildVmEvents();
            _facetScheduler.Cancel(); // \ safety
        }
        #endregion
    }
}
