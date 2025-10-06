#region usings & namespace
using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.CardImages;
using CollectaMundo.ApplicationServices.CardLists;
using CollectaMundo.ApplicationServices.EditCollection;
using CollectaMundo.ApplicationServices.Filtering;
using CollectaMundo.ApplicationServices.Filtering.CollectaMundo.ApplicationServices.Filtering;
using CollectaMundo.ApplicationServices.Import;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.EditCollection.Models;
using CollectaMundo.Presentation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using static CollectaMundo.DomainLogic.EditCollection.Models.CardChangeEventArgs;

namespace CollectaMundo.ViewModels
{
    #endregion
    public partial class MainWindowViewModel : ObservableObject, IUiBlockable, IAppRefresher
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

        // Mana keys for ColorIcons
        private readonly string[] ManaKeys = ["{W}", "{U}", "{B}", "{R}", "{G}", "{C}", "{X}"];

        #endregion

        #region child viewmodels (visible to XAML)
        public CardViewModel AllCardsVM { get; }
        public CardViewModel AllCardsForDecksVM { get; }
        public CardViewModel AllCardsInDecksVM { get; }
        public CardViewModel MyCollectionVM { get; }
        public CardViewModel ColorIcons { get; }
        public EditCollectionViewModel AddCardsVM { get; }
        public EditCollectionViewModel EditCardsVM { get; }
        public FilterViewModel FilterVM { get; }
        public CardImageViewModel CardImageVM { get; }
        public UpdateViewModel UpdateVM { get; }
        public PricesViewModel PricesVM { get; }
        #endregion

        #region ui state
        public Action? OnStartupComplete { get; set; }

        // What page are we on?
        [ObservableProperty]
        private Page currentPage = Page.SearchAndFilter;
        partial void OnCurrentPageChanged(Page oldValue, Page newValue)
        {
            if (oldValue == newValue)
                return;

            _statusVM.HideStatusOverlay();

            if (newValue == Page.MyCollection)
            {
                AddCardsVM.StatusMessage = string.Empty;
                SideMenuFilterVisibility = Visibility.Visible;
                SideMenuUtilsVisibility = Visibility.Collapsed;
                CardViewSectionVisibility = Visibility.Visible;
                MyCollectionResizeToken++;
            }
            else if (newValue == Page.SearchAndFilter)
            {
                EditCardsVM.StatusMessage = string.Empty;
                SideMenuFilterVisibility = Visibility.Visible;
                SideMenuUtilsVisibility = Visibility.Collapsed;
                CardViewSectionVisibility = Visibility.Visible;
            }
            else if (newValue == Page.Utilities)
            {
                SideMenuFilterVisibility = Visibility.Collapsed;
                SideMenuUtilsVisibility = Visibility.Visible;
                CardViewSectionVisibility = Visibility.Collapsed;
            }

            OnPropertyChanged(nameof(MiniLogoVisibility));
        }

        // Column resize
        [ObservableProperty]
        private int myCollectionResizeToken;
        public ObservableCollection<ObservableCollection<double>> ColumnWidths { get; } = new([[50, 50], [50, 50], [50]]);

        // Enable/disable top menu 
        [ObservableProperty]
        private bool isTopMenuEnabled = true;

        private void MiniLogoVisibilityFlipper()
        {
            AddCardsVM.PropertyChanged += (_, e) => { if (e.PropertyName == "StatusVisibility") { OnPropertyChanged(nameof(MiniLogoVisibility)); } };
            EditCardsVM.PropertyChanged += (_, e) => { if (e.PropertyName == "StatusVisibility") { OnPropertyChanged(nameof(MiniLogoVisibility)); } };
        }

        #region Visibility properties

        // Main sections visibility
        [ObservableProperty]
        private Visibility mainGridVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private Visibility contentSectionVisibility = Visibility.Hidden;

        // Side menu visibility
        //[ObservableProperty]
        //private Visibility sideMenuVisibility = Visibility.Hidden;

        // Side menu visibility
        private Visibility _sideMenuVisibility = Visibility.Hidden;
        public Visibility SideMenuVisibility
        {
            get => _sideMenuVisibility;
            set
            {
                _sideMenuVisibility = value;
                Debug.WriteLine($"[DEBUG] SideMenuVisibility set to {value}. DataContext is: {Application.Current.MainWindow.DataContext?.GetType().Name}");
                OnPropertyChanged();
            }

        }

        // Side menu subsections visibility properties
        [ObservableProperty]
        private Visibility sideMenuFilterVisibility = Visibility.Hidden;

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
                bool isLogoPage = CurrentPage == Page.MyCollection || CurrentPage == Page.SearchAndFilter;

                return (addBusy || editBusy || !isLogoPage)
                  ? Visibility.Collapsed
                  : Visibility.Visible;
            }
        }

        #endregion

        // Status overlay vm (owned by main window)
        private readonly StatusViewModel _statusVM;

        #endregion

        #region Constructor and factory method
        // Constructor
        private MainWindowViewModel(
            IFilteringService filteringService,
            IEditCollectionService editService,
            ICardImageService cardImageService,
            IImportService importExportService,
            ICardDatabaseManagementService cardDbManagementService,
            StatusViewModel statusVM,
            ICardListService cardListService,
            IAppSettings settings,
            IFacetUpdateScheduler? facetScheduler = null,
            IFacetUpdater? facetUpdater = null)
        {
            _statusVM = statusVM;

            _settings = settings;

            _filteringService = filteringService;
            _cardListService = cardListService;

            _facetScheduler = facetScheduler ?? new DispatcherDebounceScheduler(TimeSpan.FromMilliseconds(150));
            _facetUpdater = facetUpdater ?? new FacetUpdater();

            CurrentPage = Page.SearchAndFilter;

            // cardlist viewmodels
            AllCardsVM = new CardViewModel();
            MyCollectionVM = new CardViewModel();
            AllCardsForDecksVM = new CardViewModel();
            AllCardsInDecksVM = new CardViewModel();
            ColorIcons = new CardViewModel { Cards = [.. ManaKeys.Select(CardSet.FromManaKey)] };

            // edit collection viewmodels
            AddCardsVM = new EditCollectionViewModel(editService, removeCardWhenZero: true);
            EditCardsVM = new EditCollectionViewModel(editService, removeCardWhenZero: false);

            // filtering viewmodel
            FilterVM = new FilterViewModel(_filteringService);

            // card image viewmodel
            CardImageVM = new CardImageViewModel(cardImageService);

            // update viewmodel
            UpdateVM = new UpdateViewModel(cardDbManagementService, statusVM, this, this, () => MyCollectionVM.Cards.Count);

            // prices viewmodel
            PricesVM = new PricesViewModel(_settings, this);

            // event wiring
            SubscribeChildVmEvents();
            MiniLogoVisibilityFlipper();
        }
        public static async Task<MainWindowViewModel> CreateAsync(
            IFilteringService filteringService,
            IEditCollectionService editService,
            ICardImageService cardImageService,
            IImportService importExportService,
            ICardDatabaseManagementService prepService,
            StatusViewModel statusVM,
            ICardListService cardListService,
            IAppSettings settings,
            IFacetUpdateScheduler? facetScheduler = null,
            IFacetUpdater? facetUpdater = null,
            Action? onStartupComplete = null)
        {
            var vm = new MainWindowViewModel(filteringService, editService, cardImageService, importExportService, prepService, statusVM, cardListService, settings, facetScheduler, facetUpdater)
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
        [RelayCommand]
        private void ShowSearchAndFilter() => CurrentPage = Page.SearchAndFilter;

        [RelayCommand]
        private void ShowMyCollection() => CurrentPage = Page.MyCollection;

        [RelayCommand]
        private void ShowDecks() => CurrentPage = Page.Decks;

        [RelayCommand]
        private void ShowUtilities() => CurrentPage = Page.Utilities;

        #endregion

        #region event wiring (subscribe/unsubscribe)
        private void SubscribeChildVmEvents()
        {
            AddCardsVM.CardChanged += OnCardChanged;
            EditCardsVM.CardChanged += OnCardChanged;
            FilterVM.FilterChanged += OnFilterChanged;
        }
        private void UnsubscribeChildVmEvents()
        {
            AddCardsVM.CardChanged -= OnCardChanged;
            EditCardsVM.CardChanged -= OnCardChanged;
            FilterVM.FilterChanged -= OnFilterChanged;
        }
        #endregion

        #region event handlers (FilterChanged, CardChanged)
        // When a card is added/updated/deleted from collection
        private void OnCardChanged(object? sender, CardChangeEventArgs e)
        {
            // exactly your old MainWindow code, minus the Dispatcher.Invoke:
            switch (e.Type)
            {
                case ChangeType.Delete:
                    var dead = e.Removed.Single();
                    var toRm = MyCollectionVM.Cards.FirstOrDefault(c => c.CardId == dead);
                    if (toRm != null)
                    {
                        MyCollectionVM.Cards.Remove(toRm);
                    }

                    break;

                case ChangeType.Upsert:
                    var inc = e.Survivor!;
                    var exist = MyCollectionVM.Cards.FirstOrDefault(c => c.CardId == inc.CardId);
                    if (exist != null)
                    {
                        exist.CardsOwned = inc.CardsOwned;
                        exist.CardsForTrade = inc.CardsForTrade;
                        exist.SelectedCondition = inc.SelectedCondition;
                        exist.Language = inc.Language;
                        exist.SelectedFinish = inc.SelectedFinish;
                    }
                    else
                    {
                        MyCollectionVM.Cards.Add(inc);
                    }

                    foreach (var dupId in e.Removed)
                    {
                        var dup = MyCollectionVM.Cards.FirstOrDefault(c => c.CardId == dupId);
                        if (dup != null)
                        {
                            MyCollectionVM.Cards.Remove(dup);
                        }
                    }
                    break;
            }

            // reapply filters
            MyCollectionVM.FilteredCards = _filteringService.ApplyFilters(MyCollectionVM.Cards, FilterVM.Filters.Values);


            // debounce via scheduler (no direct DispatcherTimer usage here anymore)
            _facetScheduler.Cancel();
            _facetScheduler.Schedule(() => _facetUpdater.RefreshFromCollection(MyCollectionVM.Cards, FilterVM.Filters));
        }

        // When filters are updated
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
