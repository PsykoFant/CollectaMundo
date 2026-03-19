#region usings & namespace
using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.CardImages;
using CollectaMundo.ApplicationServices.CardLists;
using CollectaMundo.ApplicationServices.Filtering;
using CollectaMundo.ApplicationServices.Import;
using CollectaMundo.ApplicationServices.Import.Models;
using CollectaMundo.ApplicationServices.ModifyCollection;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ApplicationServices.Shell;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.Infrastructure.Shared;
using CollectaMundo.Presentation;
using CollectaMundo.ViewModels.Import;
using CollectaMundo.ViewModels.Pages;
using CollectaMundo.ViewModels.Pages.SharedElements;
using CollectaMundo.ViewModels.Shell;
using CollectaMundo.ViewModels.SideMenuLeft;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Diagnostics;
using System.Windows;

namespace CollectaMundo.ViewModels
{
    #endregion
    public partial class MainWindowViewModel : ObservableObject, ICardCollectionHost, IShellUiState
    {
        #region class: MainWindowViewModel (fields, ctor, factory)

        #region readonly dependencies
        // App settings
        private readonly IAppSettings _settings;

        // Overlay controllers
        private readonly IOperationOverlayController _operationOverlayController;
        private readonly IImportOverlayController _importOverlayController;

        // Card list / card collection management services
        private readonly IModifyCollectionService _modifyService;
        private readonly ICardListService _cardListService;

        // Filtering infrastructure
        private readonly FilteringService _filteringService;
        private readonly IFacetUpdateScheduler _facetScheduler;
        private readonly IFacetUpdater _facetUpdater;

        // User prompt service
        private readonly IUserPromptService _userPromptService;

        // File system picker
        private readonly FileSystemPicker _filesystemPicker;

        private readonly NavigationCleanupService _navigationCleanupService;

        #endregion

        #region child viewmodels
        // Shell
        public TopMenuViewModel TopMenuVM { get; }

        // Pages
        public CardListPageViewModel SearchAndFilterPageVM { get; }
        public CardListPageViewModel MyCollectionPageVM { get; }
        public PagesUtilitiesViewModel UtilitiesPageVM { get; }

        // Menus
        public SideMenuFilteringViewModel FilteringSideMenuVM { get; }
        public SideMenuUtilitiesViewModel UtilitiesSideMenuVM { get; }

        public CardViewModel AllCardsVM { get; }
        public CardViewModel AllCardsForDecksVM { get; }
        public CardViewModel AllCardsInDecksVM { get; }
        public CardViewModel MyCollectionVM { get; }
        public CardViewModel ColorIconsViewModel { get; }
        public ModifyCollectionViewModel AddCardsVM { get; }
        public ModifyCollectionViewModel EditCardsVM { get; }
        public FilterViewModel FilterVM { get; }
        public CardImageViewModel CardImageVM { get; }
        public UtilitiesViewModel UtilitiesVM { get; }
        public ImportViewModel ImportVM { get; }
        public PricesViewModel PricesVM { get; }
        #endregion

        #region ui state
        public Action? OnStartupComplete { get; set; }

        [ObservableProperty]
        private object? currentPageViewModel;

        [ObservableProperty]
        private object? currentSideMenuViewModel;

        // Shell UI state properties
        [ObservableProperty]
        private bool isSideMenuLeftVisible = true;

        [ObservableProperty]
        private bool isTopMenuEnabled = true;

        public void SetUiBusy(bool isBusy)
        {
            IsTopMenuEnabled = !isBusy;
            IsSideMenuLeftVisible = !isBusy;
            CardViewSectionVisibility = isBusy ? Visibility.Collapsed : Visibility.Visible;
        }

        #region Visibility properties

        // Side menu subsections visibility properties - we will eventually refactor this as well
        [ObservableProperty]
        private Visibility sideMenuFilterVisibility = Visibility.Visible;

        [ObservableProperty] //  - we will eventually refactor this as well
        private Visibility sideMenuUtilsVisibility = Visibility.Hidden;

        // Card view visibility  - we will eventually refactor this as well
        [ObservableProperty]
        private Visibility cardViewSectionVisibility = Visibility.Visible;

        #endregion

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
            FileSystemPicker fileSystemPicker,
            ICardListService cardListService,
            IAppSettings settings,
            IFacetUpdateScheduler? facetScheduler = null,
            IFacetUpdater? facetUpdater = null)
        {
            _modifyService = modifyService;
            _settings = settings;
            _operationOverlayController = operationOverlayController;
            _filteringService = new FilteringService();
            _cardListService = cardListService;
            _facetScheduler = facetScheduler ?? new DispatcherDebounceScheduler(TimeSpan.FromMilliseconds(150));
            _facetUpdater = facetUpdater ?? new FacetUpdater();
            _userPromptService = userPromptService;
            _filesystemPicker = fileSystemPicker;

            // cardlist viewmodels
            AllCardsVM = new CardViewModel();
            MyCollectionVM = new CardViewModel();
            AllCardsForDecksVM = new CardViewModel();
            AllCardsInDecksVM = new CardViewModel();
            List<string> manaKeys = ["{W}", "{U}", "{B}", "{R}", "{G}", "{C}", "{X}"];
            ColorIconsViewModel = new CardViewModel { Cards = [.. manaKeys.Select(CardSet.FromManaKey)] };

            // edit collection viewmodels
            AddCardsVM = new ModifyCollectionViewModel(_modifyService, this, removeCardWhenZero: true);
            EditCardsVM = new ModifyCollectionViewModel(_modifyService, this, removeCardWhenZero: false);

            // filtering viewmodel
            FilterVM = new FilterViewModel(_filteringService);

            // card image viewmodel
            CardImageVM = new CardImageViewModel(cardImageService);

            var cardCollectionHost = this;
            var shellUiState = this;

            // import viewmodel
            ImportVM = new ImportViewModel(importService, shellUiState, _userPromptService);
            _importOverlayController = new ImportOverlayController(ImportVM);

            // Utility section viewmodel
            UtilitiesVM = new UtilitiesViewModel(shellUiState, cardDbManagementService, _operationOverlayController, _importOverlayController, _userPromptService, cardCollectionHost, () => MyCollectionVM.Cards.Count, _filesystemPicker);

            // prices viewmodel
            PricesVM = new PricesViewModel(_settings, cardCollectionHost);

            // Pages viewmodels
            SearchAndFilterPageVM = new PagesSearchAndFilterViewModel(cardsVM: AllCardsVM, cardImageVM: CardImageVM, filterVM: FilterVM, pageTitle: "Search and Filter Cards", primarySubmitButtonText: "Submit these cards to my collection", primarySubmitCommand: AddCardsVM.SubmitNewCardsCommand, pricesVM: PricesVM, modifyCollectionVM: AddCardsVM);
            MyCollectionPageVM = new PagesMyCollectionViewModel(cardsVM: MyCollectionVM, cardImageVM: CardImageVM, filterVM: FilterVM, pageTitle: "My Collection", primarySubmitButtonText: "Update selected cards", primarySubmitCommand: EditCardsVM.SubmitCardEditsCommand, pricesVM: PricesVM, modifyCollectionVM: EditCardsVM);
            UtilitiesPageVM = new PagesUtilitiesViewModel();

            // Side menu viewmodels
            FilteringSideMenuVM = new SideMenuFilteringViewModel(FilterVM, ColorIconsViewModel);
            UtilitiesSideMenuVM = new SideMenuUtilitiesViewModel(UtilitiesVM, PricesVM);

            // Set initial page and menu
            CurrentPageViewModel = SearchAndFilterPageVM;
            CurrentSideMenuViewModel = FilteringSideMenuVM;

            // Navigation cleanup service
            _navigationCleanupService = new NavigationCleanupService(_userPromptService, _operationOverlayController, _importOverlayController);

            // Set up top menu with references to page VMs
            TopMenuVM = new TopMenuViewModel(shellUIState: this, _navigationCleanupService, filteringSideMenuViewModel: FilteringSideMenuVM, utilitiesSideMenuViewModel: UtilitiesSideMenuVM, allCardsPageViewModel: SearchAndFilterPageVM, myCollectionPageViewModel: MyCollectionPageVM, utilitiesPageViewModel: UtilitiesPageVM);

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
            FileSystemPicker fileSystemPicker,
            ICardListService cardListService,
            IAppSettings settings,
            IFacetUpdateScheduler? facetScheduler = null,
            IFacetUpdater? facetUpdater = null,
            Action? onStartupComplete = null)
        {
            var vm = new MainWindowViewModel(editService, cardImageService, prepService, importService, operationOverlayController, userPromptService, fileSystemPicker, cardListService, settings, facetScheduler, facetUpdater)
            {
                OnStartupComplete = onStartupComplete
            };

            await vm.ReloadAllCardListsAndFiltersAsync();

            vm.OnStartupComplete?.Invoke();
            return vm;
        }
        #endregion

        #endregion

        #region event wiring (subscribe/unsubscribe)
        private void SubscribeChildVmEvents()
        {
            ImportVM.CollectionMutationRequested += OnImportCollectionMutationRequested;
            ImportVM.CardImageSelectionRequested += OnCardImageSelectionRequested;
            AddCardsVM.CollectionChanged += OnCollectionChanged;
            EditCardsVM.CollectionChanged += OnCollectionChanged;
            FilterVM.FilterChanged += OnFilterChanged;
        }
        private void UnsubscribeChildVmEvents()
        {
            ImportVM.CollectionMutationRequested -= OnImportCollectionMutationRequested;
            ImportVM.CardImageSelectionRequested -= OnCardImageSelectionRequested;
            AddCardsVM.CollectionChanged -= OnCollectionChanged;
            EditCardsVM.CollectionChanged -= OnCollectionChanged;
            FilterVM.FilterChanged -= OnFilterChanged;
        }

        #endregion

        #region event handlers (FilterChanged, CardChanged, CollectionChanged)

        private void OnImportCollectionMutationRequested(object? sender, CollectionMutation mutation)
        {
            var changeSet = _modifyService.BuildCollectionChangeSet(mutation, MyCollectionVM, AllCardsVM);
            OnCollectionChanged(sender, changeSet);
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
            _modifyService.ApplyMyCollectionChanges(MyCollectionVM.Cards, changeSet);

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
