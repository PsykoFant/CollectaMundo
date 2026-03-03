#region usings & namespace
using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.CardImages;
using CollectaMundo.ApplicationServices.CardLists;
using CollectaMundo.ApplicationServices.EditCollection;
using CollectaMundo.ApplicationServices.Filtering;
using CollectaMundo.ApplicationServices.Import;
using CollectaMundo.ApplicationServices.Import.Models;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.Infrastructure.Shared;
using CollectaMundo.Presentation;
using CollectaMundo.ViewModels.Import;
using CollectaMundo.ViewModels.Pages;
using CollectaMundo.Views.Pages.SharedElements;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Diagnostics;
using System.Windows;

namespace CollectaMundo.ViewModels
{
    #endregion
    public partial class MainWindowViewModel : ObservableObject, IParentViewModelContext
    {
        #region class: MainWindowViewModel (fields, ctor, factory)

        #region readonly dependencies
        // App settings
        private readonly IAppSettings _settings;

        // Services
        private readonly IFilteringService _filteringService;
        private readonly ICardListService _cardListService;

        // Filtering infrastructure
        private readonly IFacetUpdateScheduler _facetScheduler;
        private readonly IFacetUpdater _facetUpdater;

        // User prompt service
        private readonly IUserPromptService _userPromptService;

        // File system picker
        private readonly FileSystemPicker _filesystemPicker;

        // Mana keys for ColorIcons
        private readonly string[] ManaKeys = ["{W}", "{U}", "{B}", "{R}", "{G}", "{C}", "{X}"];

        #endregion

        #region child viewmodels
        // Pages
        public CardListPageViewModel SearchAndFilterPageVM { get; }

        public StatusViewModel StatusVM { get; }
        public CardViewModel AllCardsVM { get; }
        public CardViewModel AllCardsForDecksVM { get; }
        public CardViewModel AllCardsInDecksVM { get; }
        public CardViewModel MyCollectionVM { get; }
        public CardViewModel ColorIcons { get; }
        public EditCollectionViewModel AddCardsVM { get; }
        public EditCollectionViewModel EditCardsVM { get; }
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

        // Column resize
        [ObservableProperty]
        private int myCollectionResizeToken;
        public void SetUiBusy(bool isBusy)
        {
            IsTopMenuEnabled = !isBusy;
            SideMenuVisibility = isBusy ? Visibility.Collapsed : Visibility.Visible;
            CardViewSectionVisibility = isBusy ? Visibility.Collapsed : Visibility.Visible;
        }

        // Enable/disable top menu 
        [ObservableProperty]
        private bool isTopMenuEnabled = true;

        private void MiniLogoVisibilityFlipper()
        {
            AddCardsVM.PropertyChanged += (_, e) => { if (e.PropertyName == "StatusVisibility") { OnPropertyChanged(nameof(MiniLogoVisibility)); } };
            EditCardsVM.PropertyChanged += (_, e) => { if (e.PropertyName == "StatusVisibility") { OnPropertyChanged(nameof(MiniLogoVisibility)); } };
        }

        #region Visibility properties

        // Side menu visibility
        [ObservableProperty]
        private Visibility sideMenuVisibility = Visibility.Visible;

        // Side menu subsections visibility properties
        [ObservableProperty]
        private Visibility sideMenuFilterVisibility = Visibility.Visible;

        [ObservableProperty]
        private Visibility sideMenuUtilsVisibility = Visibility.Hidden;

        // Card view visibility
        [ObservableProperty]
        private Visibility cardViewSectionVisibility = Visibility.Visible;

        // Miscellaneous visibility properties
        public Visibility MiniLogoVisibility
        {
            get
            {
                // if *either* status box is Visible, hide our logo
                bool addBusy = AddCardsVM.StatusVisibility == Visibility.Visible;
                bool editBusy = EditCardsVM.StatusVisibility == Visibility.Visible;
                //bool isLogoPage = CurrentPage == Page.MyCollection || CurrentPage == Page.SearchAndFilter;
                bool isLogoPage = true;

                return (addBusy || editBusy || !isLogoPage)
                  ? Visibility.Collapsed
                  : Visibility.Visible;
            }
        }

        #endregion

        // Status overlay vm (owned by main window)
        //private readonly StatusViewModel _statusVM;

        #endregion

        #region Constructor and factory method
        // Constructor
        private MainWindowViewModel(
            IEditCollectionService editService,
            ICardImageService cardImageService,
            ICardDatabaseManagementService cardDbManagementService,
            IImportService importService,
            StatusViewModel statusVM,
            IUserPromptService userPromptService,
            FileSystemPicker fileSystemPicker,
            ICardListService cardListService,
            IAppSettings settings,
            IFacetUpdateScheduler? facetScheduler = null,
            IFacetUpdater? facetUpdater = null)
        {
            StatusVM = statusVM;

            _settings = settings;
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
            ColorIcons = new CardViewModel { Cards = [.. ManaKeys.Select(CardSet.FromManaKey)] };

            // edit collection viewmodels
            AddCardsVM = new EditCollectionViewModel(editService, this, removeCardWhenZero: true);
            EditCardsVM = new EditCollectionViewModel(editService, this, removeCardWhenZero: false);

            // filtering viewmodel
            FilterVM = new FilterViewModel(_filteringService);

            // card image viewmodel
            CardImageVM = new CardImageViewModel(cardImageService);

            var parentContext = this;

            // import viewmodel
            ImportVM = new ImportViewModel(importService, parentContext, _userPromptService);

            // Utility section viewmodel
            UtilitiesVM = new UtilitiesViewModel(cardDbManagementService, statusVM, ImportVM, _userPromptService, parentContext, () => MyCollectionVM.Cards.Count, _filesystemPicker);

            // prices viewmodel
            PricesVM = new PricesViewModel(_settings, parentContext);

            // Pages viewmodels
            SearchAndFilterPageVM = new CardListPageViewModel(cardsVM: AllCardsVM, cardImageVM: CardImageVM, resizeSpec: ColumnResizeSpec.ForSearchAndFilter(), pricesVM: PricesVM, filterVM: FilterVM, addOrEditCardsVM: AddCardsVM);

            CurrentPageViewModel = SearchAndFilterPageVM; // default page

            // event wiring
            SubscribeChildVmEvents();
            MiniLogoVisibilityFlipper();
        }
        public static async Task<MainWindowViewModel> CreateAsync(
            IEditCollectionService editService,
            ICardImageService cardImageService,
            ICardDatabaseManagementService prepService,
            IImportService importService,
            StatusViewModel statusVM,
            IUserPromptService userPromptService,
            FileSystemPicker fileSystemPicker,
            ICardListService cardListService,
            IAppSettings settings,
            IFacetUpdateScheduler? facetScheduler = null,
            IFacetUpdater? facetUpdater = null,
            Action? onStartupComplete = null)
        {
            var vm = new MainWindowViewModel(editService, cardImageService, prepService, importService, statusVM, userPromptService, fileSystemPicker, cardListService, settings, facetScheduler, facetUpdater)
            {
                OnStartupComplete = onStartupComplete
            };

            await vm.ReloadAllCardListsAndFiltersAsync();

            vm.OnStartupComplete?.Invoke();
            return vm;
        }
        #endregion

        #endregion

        #region commands
        // Commands to switch pages


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
            var changeSet = _cardListService.BuildCollectionChangeSet(mutation, MyCollectionVM, AllCardsVM);
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
            _cardListService.ApplyMyCollectionChanges(MyCollectionVM.Cards, changeSet);

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
