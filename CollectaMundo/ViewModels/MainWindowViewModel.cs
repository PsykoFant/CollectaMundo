#region usings & namespace
using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.CardLists;
using CollectaMundo.ApplicationServices.EditCollection;
using CollectaMundo.ApplicationServices.Filtering;
using CollectaMundo.ApplicationServices.Filtering.CollectaMundo.ApplicationServices.Filtering;
using CollectaMundo.ApplicationServices.Import;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CardPrices;
using CollectaMundo.DomainLogic.EditCollection.Models;
using CollectaMundo.Presentation;
using CollectaMundo.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using static CollectaMundo.DomainLogic.EditCollection.Models.CardChangeEventArgs;

namespace CollectaMundo.ViewModels
{
    #endregion
    public class MainWindowViewModel : INotifyPropertyChanged, IUiBlockable, IAppRefresher
    {
        #region class: MainWindowViewModel (fields, ctor, factory)

        #region INotifyPropertyChanged boilerplate
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        #endregion

        #region readonly dependencies
        // Services
        private readonly IFilteringService _filteringService;
        private readonly IImportService _importService;
        private readonly ICardDatabaseManagementService _cardDbManagementService;
        private readonly ICardListService _cardListService;

        // Filtering infrastructure
        private readonly IFacetUpdateScheduler _facetScheduler;
        private readonly IFacetUpdater _facetUpdater;

        // Mana keys for ColorIcons
        private readonly string[] ManaKeys = ["{W}", "{U}", "{B}", "{R}", "{G}", "{C}", "{X}"];

        // Retailer selection
        private readonly Func<string> _getRetailer;
        private readonly Action<string> _setRetailerAndPersist;

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
        public UpdateViewModel UpdateVM { get; }
        #endregion

        #region ui state
        public Action? OnStartupComplete { get; set; }

        // What page are we on?
        private Page _currentPage = Page.SearchAndFilter;
        public Page CurrentPage
        {
            get => _currentPage;
            set
            {
                if (_currentPage == value)
                {
                    return;
                }

                _currentPage = value;

                _statusVM.HideStatusOverlay();

                if (_currentPage == Page.MyCollection)
                {
                    AddCardsVM.StatusMessage = string.Empty;
                    SideMenuFilterVisibility = Visibility.Visible;
                    SideMenuUtilsVisibility = Visibility.Collapsed;
                    CardViewSectionVisibility = Visibility.Visible;

                    // Nudge the second grid once it’s about to be shown
                    MyCollectionResizeToken++;
                }
                else if (_currentPage == Page.SearchAndFilter)
                {
                    EditCardsVM.StatusMessage = string.Empty;
                    SideMenuFilterVisibility = Visibility.Visible;
                    SideMenuUtilsVisibility = Visibility.Collapsed;
                    CardViewSectionVisibility = Visibility.Visible;
                }
                else if (_currentPage == Page.Utilities)
                {
                    SideMenuFilterVisibility = Visibility.Collapsed;
                    SideMenuUtilsVisibility = Visibility.Visible;
                    CardViewSectionVisibility = Visibility.Collapsed;
                }

                OnPropertyChanged();                  // CurrentPage
                OnPropertyChanged(nameof(MiniLogoVisibility));
            }
        }

        // Column resize
        private int _myCollectionResizeToken;
        public int MyCollectionResizeToken
        {
            get => _myCollectionResizeToken;
            private set { _myCollectionResizeToken = value; OnPropertyChanged(); }
        }
        public ObservableCollection<ObservableCollection<double>> ColumnWidths { get; set; } = [[50, 50], [50, 50], [50]];

        // Enable/disable top menu 
        private bool _isTopMenuEnabled = true;
        public bool IsTopMenuEnabled
        {
            get => _isTopMenuEnabled;
            set
            {
                if (_isTopMenuEnabled != value)
                {
                    _isTopMenuEnabled = value;
                    OnPropertyChanged(); // Required for WPF to update bindings
                }
            }
        }
        private void MiniLogoVisibilityFlipper()
        {
            AddCardsVM.PropertyChanged += (_, e) => { if (e.PropertyName == "StatusVisibility") { OnPropertyChanged(nameof(MiniLogoVisibility)); } };
            EditCardsVM.PropertyChanged += (_, e) => { if (e.PropertyName == "StatusVisibility") { OnPropertyChanged(nameof(MiniLogoVisibility)); } };
        }

        // Retailer options 
        public sealed record RetailerOption(string Key, string Display);
        public ObservableCollection<RetailerOption> Retailers { get; }

        private RetailerOption? _selectedRetailer;
        public RetailerOption? SelectedRetailer
        {
            get => _selectedRetailer;
            set { if (_selectedRetailer != value) { _selectedRetailer = value; OnPropertyChanged(); } }
        }

        // Price column headers (dynamic based on retailer)
        private string _priceHeader = "Price";
        public string PriceHeader
        {
            get => _priceHeader;
            private set { if (_priceHeader != value) { _priceHeader = value; OnPropertyChanged(); } }
        }

        private string _foilPriceHeader = "Foil Price";
        public string FoilPriceHeader
        {
            get => _foilPriceHeader;
            private set { if (_foilPriceHeader != value) { _foilPriceHeader = value; OnPropertyChanged(); } }
        }

        private string _etchedPriceHeader = "Etched Price";
        public string EtchedPriceHeader
        {
            get => _etchedPriceHeader;
            private set { if (_etchedPriceHeader != value) { _etchedPriceHeader = value; OnPropertyChanged(); } }
        }

        // simple currency mapping
        private static string GetCurrencyForRetailer(string key) => string.Equals(key, "cardmarket", StringComparison.OrdinalIgnoreCase) ? "EUR" : "USD";
        private void UpdatePriceHeaders()
        {
            var key = SelectedRetailer?.Key ?? "cardmarket";
            var currency = GetCurrencyForRetailer(key);
            PriceHeader = $"Price ({currency})";
            FoilPriceHeader = $"Foil Price ({currency})";
            EtchedPriceHeader = $"Etched Price ({currency})";
        }

        #region Visibility properties

        // Main sections visibility
        private Visibility _mainGridVisibility = Visibility.Collapsed;
        public Visibility MainGridVisibility
        {
            get => _mainGridVisibility;
            set { _mainGridVisibility = value; OnPropertyChanged(); }
        }

        private Visibility _contentSectionVisibility = Visibility.Hidden;
        public Visibility ContentSectionVisibility
        {
            get => _contentSectionVisibility;
            set { _contentSectionVisibility = value; OnPropertyChanged(); }
        }

        // Side menu visibility
        private Visibility _sideMenuVisibility = Visibility.Hidden;
        public Visibility SideMenuVisibility
        {
            get => _sideMenuVisibility;
            set { _sideMenuVisibility = value; OnPropertyChanged(); }
        }

        // Side menu subsections visibility properties
        private Visibility _sideMenuFilterVisibility = Visibility.Visible;
        public Visibility SideMenuFilterVisibility
        {
            get => _sideMenuFilterVisibility;
            set { _sideMenuFilterVisibility = value; OnPropertyChanged(); }
        }

        private Visibility _sideMenuUtilsVisibility = Visibility.Hidden;
        public Visibility SideMenuUtilsVisibility
        {
            get => _sideMenuUtilsVisibility;
            set { _sideMenuUtilsVisibility = value; OnPropertyChanged(); }
        }

        // Card view visibility
        private Visibility _cardViewSectionVisibility = Visibility.Visible;
        public Visibility CardViewSectionVisibility
        {
            get => _cardViewSectionVisibility;
            set { _cardViewSectionVisibility = value; OnPropertyChanged(); }
        }

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
            IImportService importExportService,
            ICardDatabaseManagementService cardDbManagementService,
            StatusViewModel statusVM,
            ICardListService cardListService,
            Func<string> getRetailer,
            Action<string> setRetailerAndPersist,
            IFacetUpdateScheduler? facetScheduler = null,
            IFacetUpdater? facetUpdater = null)
        {
            _statusVM = statusVM;

            _filteringService = filteringService;
            _importService = importExportService;
            _cardDbManagementService = cardDbManagementService;
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

            // update viewmodel
            UpdateVM = new UpdateViewModel(cardDbManagementService, statusVM, this, this, () => MyCollectionVM.Cards.Count);

            // retailers
            _getRetailer = getRetailer;
            _setRetailerAndPersist = setRetailerAndPersist;

            // build retailer list (purely static definitions)
            Retailers = new ObservableCollection<RetailerOption>(CardPriceDefinitions.RetailersByFormat["paper"].Select(kv => new RetailerOption(kv.Key, kv.Value)));

            // pick initial from settings via delegate
            var savedKey = _getRetailer();
            SelectedRetailer = Retailers.FirstOrDefault(r => string.Equals(r.Key, savedKey, StringComparison.OrdinalIgnoreCase)) ?? Retailers.First();

            // event wiring
            SubscribeChildVmEvents();
            BuildCommands();
            MiniLogoVisibilityFlipper();
            UpdatePriceHeaders();
        }
        public static async Task<MainWindowViewModel> CreateAsync(
            IFilteringService filteringService,
            IEditCollectionService editService,
            IImportService importExportService,
            ICardDatabaseManagementService prepService,
            StatusViewModel statusVM,
            ICardListService cardListService,
            Func<string> getRetailer,
            Action<string> setRetailerAndPersist,
            IFacetUpdateScheduler? facetScheduler = null,
            IFacetUpdater? facetUpdater = null,
            Action? onStartupComplete = null)
        {
            var vm = new MainWindowViewModel(filteringService, editService, importExportService, prepService, statusVM, cardListService, getRetailer, setRetailerAndPersist, facetScheduler, facetUpdater)
            {
                OnStartupComplete = onStartupComplete
            };

            await vm.ReloadAllCardListsAndFiltersAsync();

            vm.OnStartupComplete?.Invoke();
            return vm;
        }
        #endregion

        #endregion

        #region commands (construction + handlers)
        // Commands to switch pages
        public ICommand ShowSearchAndFilterCommand { get; private set; } = null!;
        public ICommand ShowMyCollectionCommand { get; private set; } = null!;
        public ICommand ShowDecksCommand { get; private set; } = null!;
        public ICommand ShowUtilitiesCommand { get; private set; } = null!;
        public ICommand ChangeRetailerCommand { get; private set; } = null!;
        private void BuildCommands()
        {
            ShowSearchAndFilterCommand = new RelayCommand<object>(_ => { CurrentPage = Page.SearchAndFilter; });
            ShowMyCollectionCommand = new RelayCommand<object>(_ => { CurrentPage = Page.MyCollection; });
            ShowDecksCommand = new RelayCommand<object>(_ => CurrentPage = Page.Decks);
            ShowUtilitiesCommand = new RelayCommand<object>(_ => CurrentPage = Page.Utilities);

            ChangeRetailerCommand = new RelayCommand<object>(async _ => await ChangeRetailerAsync());
        }

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

        // When retailer is changed, refresh prices on all cards
        void RefreshAllPrices()
        {
            foreach (var c in AllCardsVM.Cards)
            {
                c.RefreshPricesFromProvider();
            }

            foreach (var c in MyCollectionVM.Cards)
            {
                c.RefreshPricesFromProvider();
            }
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

            sw.Stop();
            Debug.WriteLine($"[ReloadAllCardListsAsync] M1 finished in {sw.ElapsedMilliseconds} ms ({sw.Elapsed}).");
        }

        #endregion

        #region Command methods - status overlay / maintenance tasks (backup, update db)       


        private async Task ChangeRetailerAsync()
        {
            if (SelectedRetailer is null)
            {
                return;
            }
            _setRetailerAndPersist(SelectedRetailer.Key);
            await _cardListService.ReloadPriceLookupsAsync(SelectedRetailer.Key);
            RefreshAllPrices();
            UpdatePriceHeaders();
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
