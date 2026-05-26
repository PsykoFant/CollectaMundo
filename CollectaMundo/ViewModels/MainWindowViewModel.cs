#region usings & namespace
using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.CardImages;
using CollectaMundo.ApplicationServices.CardLists;
using CollectaMundo.ApplicationServices.CardLocations;
using CollectaMundo.ApplicationServices.CollectionMaterialization;
using CollectaMundo.ApplicationServices.CollectionMutations;
using CollectaMundo.ApplicationServices.Decks;
using CollectaMundo.ApplicationServices.Filtering;
using CollectaMundo.ApplicationServices.Import;
using CollectaMundo.ApplicationServices.Import.Models;
using CollectaMundo.ApplicationServices.KeyedDataProvider.Providers;
using CollectaMundo.ApplicationServices.ModifyCollection;
using CollectaMundo.ApplicationServices.Navigation;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.Infrastructure.Shared;
using CollectaMundo.Presentation;
using CollectaMundo.ViewModels.Decks;
using CollectaMundo.ViewModels.Filtering;
using CollectaMundo.ViewModels.Import;
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
        private readonly ICollectionMaterializer _collectionMaterializer;
        private readonly ICollectionChangeSetApplier _collectionChangeSetApplier;
        private readonly IImportService _importService;
        private readonly ICardLocationService _cardLocationService;
        private readonly ICardLocationLookupStore _cardLocationLookupStore;

        // Deck management service
        private readonly IDeckManagementService _deckManagementService;
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
        public CardListPageViewModel SearchAndFilterPageVM { get; }
        public CardListPageViewModel MyCollectionPageVM { get; }
        public PagesDecksHostViewModel PagesDecksHostVM { get; }
        public PagesUtilitiesHostViewModel PagesUtilitiesHostVM { get; }


        // Menus
        public SideMenuFilteringViewModel FilteringSideMenuVM { get; }
        public SideMenuUtilitiesViewModel UtilitiesSideMenuVM { get; }

        public CardListViewModel AllCardsVM { get; }
        public CardListViewModel AllCardsForDecksVM { get; }
        public CardListViewModel AllCardsInDecksVM { get; }
        public CardListViewModel MyCollectionVM { get; }
        public CardListViewModel ColorIconsViewModel { get; }
        public ModifyCollectionViewModel AddCardsVM { get; }
        public ModifyCollectionViewModel EditCardsVM { get; }
        public DeckManagementViewModel DeckManagementVM { get; }
        public DeckEditorViewModel DeckEdititorVM { get; }
        public FilterViewModel FilterVM { get; }
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
            ICollectionMaterializer collectionMaterializer,
            ICollectionChangeSetApplier collectionChangeSetApplier,
            ICardLocationService cardLocationService,
            ICardLocationLookupStore cardLocationLookupStore,
            IDeckManagementService deckManagementService,
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
            _collectionMaterializer = collectionMaterializer;
            _collectionChangeSetApplier = collectionChangeSetApplier;
            _importService = importService;
            _cardLocationService = cardLocationService;
            _cardLocationLookupStore = cardLocationLookupStore;
            _deckManagementService = deckManagementService;
            _deckManagementStore = deckManagementStore;
            _facetScheduler = facetScheduler ?? new DispatcherDebounceScheduler(TimeSpan.FromMilliseconds(150));
            _facetUpdater = facetUpdater ?? new FacetUpdater();
            _userPromptService = userPromptService;
            _filesystemPicker = fileSystemPicker;

            // cardlist viewmodels
            AllCardsVM = new CardListViewModel();
            MyCollectionVM = new CardListViewModel();
            AllCardsForDecksVM = new CardListViewModel();
            AllCardsInDecksVM = new CardListViewModel();
            List<string> manaKeys = ["{W}", "{U}", "{B}", "{R}", "{G}", "{C}", "{X}"];
            ColorIconsViewModel = new CardListViewModel { Cards = [.. manaKeys.Select(CardSet.FromManaKey)] };

            // Modify collection viewmodels
            AddCardsVM = new ModifyCollectionViewModel(_modifyService, this, removeCardWhenZero: true);
            EditCardsVM = new ModifyCollectionViewModel(_modifyService, this, removeCardWhenZero: false);

            // Deck management viewmodels
            DeckManagementVM = new DeckManagementViewModel(_deckManagementService, _deckManagementStore);
            DeckEdititorVM = new DeckEditorViewModel();

            // filtering viewmodel
            FilterVM = new FilterViewModel(_filteringService);

            // card image viewmodel
            CardImageVM = new CardImageViewModel(cardImageService);

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
            SearchAndFilterPageVM = new PagesSearchAndFilterViewModel(cardsVM: AllCardsVM, cardImageVM: CardImageVM, filterVM: FilterVM, pageTitle: "Search and Filter Cards", primarySubmitButtonText: "Submit these cards to my collection", primarySubmitCommand: AddCardsVM.SubmitNewCardsCommand, pricesVM: PricesVM, modifyCollectionVM: AddCardsVM);
            MyCollectionPageVM = new PagesMyCollectionViewModel(cardsVM: MyCollectionVM, cardImageVM: CardImageVM, filterVM: FilterVM, pageTitle: "My Collection", primarySubmitButtonText: "Update selected cards", primarySubmitCommand: EditCardsVM.SubmitCardEditsCommand, pricesVM: PricesVM, modifyCollectionVM: EditCardsVM);
            PagesDecksHostVM = new PagesDecksHostViewModel(DeckManagementVM, DeckEdititorVM);
            PagesUtilitiesHostVM = new PagesUtilitiesHostViewModel(UtilitiesVM, ImportVM, CardLocationVM, utilitiesNavigator);

            // Side menu viewmodels
            FilteringSideMenuVM = new SideMenuFilteringViewModel(FilterVM, ColorIconsViewModel, shellUiState);
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
            ICollectionMaterializer collectionMaterializer,
            ICollectionChangeSetApplier collectionChangeSetApplier,
            ICardLocationService cardLocationService,
            ICardLocationLookupStore cardLocationLookupStore,
            IDeckManagementService deckManagementService,
            IDeckManagementStore deckManagementStore,
            IAppSettings settings,
            IFacetUpdateScheduler? facetScheduler = null,
            IFacetUpdater? facetUpdater = null,
            Action? onStartupComplete = null)
        {
            var vm = new MainWindowViewModel(editService, cardImageService, prepService, importService, operationOverlayController, userPromptService, fileSystemPicker, cardListService, collectionMaterializer, collectionChangeSetApplier, cardLocationService, cardLocationLookupStore, deckManagementService, deckManagementStore, settings, facetScheduler, facetUpdater)
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
            AddCardsVM.CollectionChanged += OnCollectionChanged;
            EditCardsVM.CollectionChanged += OnCollectionChanged;
            FilterVM.FilterChanged += OnFilterChanged;
            _cardLocationLookupStore.LocationsChanged += OnLocationsChanged;
            CardLocationVM.CollectionChanged += OnCollectionChanged;
        }
        private void UnsubscribeChildVmEvents()
        {
            ImportVM.CollectionMutationRequested -= OnImportCollectionMutationRequested;
            ImportVM.CardImageSelectionRequested -= OnCardImageSelectionRequested;
            AddCardsVM.CollectionChanged -= OnCollectionChanged;
            EditCardsVM.CollectionChanged -= OnCollectionChanged;
            FilterVM.FilterChanged -= OnFilterChanged;
            _cardLocationLookupStore.LocationsChanged -= OnLocationsChanged;
            CardLocationVM.CollectionChanged -= OnCollectionChanged;
        }

        #endregion

        #region event handlers (FilterChanged, CardChanged, CollectionChanged)
        private void OnImportCollectionMutationRequested(object? sender, ImportCollectionUpsertResult mutation)
        {
            var changeSet = BuildCollectionChangeSetFromMutation(mutation);
            OnCollectionChanged(sender, changeSet);
        }
        private CollectionChangeSet<CardSet> BuildCollectionChangeSetFromMutation(ImportCollectionUpsertResult mutation)
        {
            var addedOrUpdated = new List<CardSet>();

            var cardById = MyCollectionVM.Cards.Where(c => c.CardId.HasValue).ToDictionary(c => c.CardId!.Value);
            var coreByUuid = AllCardsVM.Cards.Where(c => c.Core is not null).Select(c => c.Core!).ToDictionary(c => c.Uuid, StringComparer.OrdinalIgnoreCase);

            foreach (var row in mutation.UpsertedRows)
            {
                if (cardById.TryGetValue(row.CardId, out var existingCard))
                {
                    existingCard.CardsOwned += row.CardsOwned;
                    existingCard.CardsForTrade += row.CardsForTrade;
                    existingCard.RecomputeCollectionPrice();

                    addedOrUpdated.Add(existingCard);
                    continue;
                }

                var card = _collectionMaterializer.MaterializeFromRow(row, coreByUuid);
                addedOrUpdated.Add(card);
            }

            return new CollectionChangeSet<CardSet>
            {
                RemovedIds = [],
                AddedOrUpdated = addedOrUpdated
            };
        }
        private void OnCardImageSelectionRequested(object? sender, string? uuid)
        {
            if (string.IsNullOrWhiteSpace(uuid))
            {
                CardImageVM.SelectedCard = null; // reset UI
            }
            else
            {
                CardImageVM.SelectedCard = new CardSet { Uuid = uuid };
            }
        }
        private void OnCollectionChanged(object? sender, CollectionChangeSet<CardSet> changeSet)
        {
            // Apply add/update
            _collectionChangeSetApplier.Apply(MyCollectionVM.Cards, changeSet);

            // External collection mutations can make open add/edit rows stale. Reconcile draft rows against the updated in-memory collection:
            // - remove rows whose source CardId no longer exists
            // - refresh rows whose source CardId still exists
            AddCardsVM.ReconcileOpenRowsWithCollection(MyCollectionVM.Cards);
            EditCardsVM.ReconcileOpenRowsWithCollection(MyCollectionVM.Cards);

            // Reapply filters
            MyCollectionVM.FilteredCards = _filteringService.ApplyFilters(MyCollectionVM.Cards, FilterVM.Filters.Values);

            // Debounced facet refresh
            _facetScheduler.Cancel();
            _facetScheduler.Schedule(() => _facetUpdater.RefreshFromCollection(MyCollectionVM.Cards, FilterVM.Filters));
        }
        private void OnFilterChanged(object? sender, EventArgs e)
        {
            AllCardsVM.FilteredCards = _filteringService.ApplyFilters(AllCardsVM.Cards, FilterVM.Filters.Values);
            MyCollectionVM.FilteredCards = _filteringService.ApplyFilters(MyCollectionVM.Cards, FilterVM.Filters.Values);
            AllCardsForDecksVM.FilteredCards = _filteringService.ApplyFilters(AllCardsForDecksVM.Cards, FilterVM.Filters.Values);
        }
        private void OnLocationsChanged(object? sender, EventArgs e)
        {
            var locations = _cardLocationLookupStore.GetAll();

            AddCardsVM.SetAvailableLocations(locations);
            EditCardsVM.SetAvailableLocations(locations);

            CardSet.CardLocationProvider = new ValueProvider<int, CardLocation>(locations.ToDictionary(x => x.Id));

            foreach (var c in MyCollectionVM.Cards)
            {
                c.RefreshLocationsFromProvider();
            }

            foreach (var c in AllCardsVM.Cards)
            {
                c.RefreshLocationsFromProvider();
            }

            // Rebuild collection-backed filter options after location display names changed
            _facetUpdater.RefreshFromCollection(MyCollectionVM.Cards, FilterVM.Filters);

            // Reapply active filters because selected/display values may have changed
            OnFilterChanged(this, EventArgs.Empty);
        }

        #endregion

        #region startup / reload
        public async Task ReloadAllCardListsAndFiltersAsync()
        {
            var sw = Stopwatch.StartNew();

            Debug.WriteLine("[ReloadAllCardListsAsync] Initializing card lists");
            await _cardListService.InitializeCardListsAsync(AllCardsVM, MyCollectionVM, FilterVM.Filters, FilterVM);
            FilterVM.NotifyFiltersRebuilt();
            FilterVM.NotifyFilterChanged();
            Debug.WriteLine($"latest price date from settings: {_settings.PriceInfo.PricesUpdatedDate}");
            PricesVM.RefreshLatestPriceDate();
            sw.Stop();
            Debug.WriteLine($"[ReloadAllCardListsAsync] M1 finished in {sw.ElapsedMilliseconds} ms ({sw.Elapsed}).");
        }
        public async Task ReloadAvailableLocationsAsync()
        {
            var sw = Stopwatch.StartNew();

            Debug.WriteLine("[ReloadAvailableLocationsAsync] Loading card locations");

            var locations = await _cardLocationService.GetAllAsync();

            _cardLocationLookupStore.ReplaceAll(locations);

            sw.Stop();
            Debug.WriteLine($"[ReloadAvailableLocationsAsync] Finished in {sw.ElapsedMilliseconds} ms ({sw.Elapsed}).");
        }

        // When retailer is changed, refresh prices on all cards
        public void RefreshAllPrices()
        {
            // Reset price dictionary
            _cardListService.ReloadPriceLookupsAsync(_settings.PriceInfo.Retailer);

            // Refresh prices on all cards in all lists
            foreach (var c in AllCardsVM.Cards)
            {
                c.RefreshPricesFromProvider();
            }

            foreach (var c in MyCollectionVM.Cards)
            {
                c.RefreshPricesFromProvider();
            }

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
