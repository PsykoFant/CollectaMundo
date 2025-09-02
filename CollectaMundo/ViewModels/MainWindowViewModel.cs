#region usings & namespace
using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.CardLists;
using CollectaMundo.ApplicationServices.DownloadResourceFiles;
using CollectaMundo.ApplicationServices.EditCollection;
using CollectaMundo.ApplicationServices.Filtering;
using CollectaMundo.ApplicationServices.Filtering.CollectaMundo.ApplicationServices.Filtering;
using CollectaMundo.ApplicationServices.ImportExport;
using CollectaMundo.ApplicationServices.Utilities;
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
    public sealed class MainWindowViewModel : INotifyPropertyChanged
    {
        #region class: MainWindowViewModel (fields, ctor, factory)

        #region INotifyPropertyChanged boilerplate
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        #endregion

        #region readonly dependencies
        // Services
        private readonly IFilteringService _filteringService;
        private readonly IImportExportService _importExportService;
        private readonly ICardDatabasePreparationService _prepService;
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
                // If we are on the same page, do nothing
                if (_currentPage == value)
                {
                    return;
                }

                _currentPage = value;

                // Reset and hide the status overlay
                _statusOverlayVM.HideStatusOverlay();

                if (_currentPage == Page.MyCollection)
                {
                    AddCardsVM.StatusMessage = string.Empty;
                    SideMenuFilterVisibility = Visibility.Visible;
                    SideMenuUtilsVisibility = Visibility.Collapsed;
                }
                else if (_currentPage == Page.SearchAndFilter)
                {
                    EditCardsVM.StatusMessage = string.Empty;
                    SideMenuFilterVisibility = Visibility.Visible;
                    SideMenuUtilsVisibility = Visibility.Collapsed;
                }
                else if (_currentPage == Page.Utilities)
                {
                    SideMenuFilterVisibility = Visibility.Collapsed;
                    SideMenuUtilsVisibility = Visibility.Visible;
                }
                // CurrentPage changed
                OnPropertyChanged();

                // MiniLogoVisibility depends on CurrentPage
                OnPropertyChanged(nameof(MiniLogoVisibility));
            }
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

        #region Visibility properties
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

        private Visibility _sideMenuVisibility = Visibility.Hidden;
        public Visibility SideMenuVisibility
        {
            get => _sideMenuVisibility;
            set { _sideMenuVisibility = value; OnPropertyChanged(); }
        }

        // Side menu visibility properties
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

        private Visibility _sideMenuUtilsCheckForUpdatesVisibility = Visibility.Visible;
        public Visibility SideMenuUtilsCheckForUpdatesVisibility
        {
            get => _sideMenuUtilsCheckForUpdatesVisibility;
            set { _sideMenuUtilsCheckForUpdatesVisibility = value; OnPropertyChanged(); }
        }

        private Visibility _sideMenuUtilsUpdateDbVisibility = Visibility.Collapsed;
        public Visibility SideMenuUtilsUpdateDbVisibility
        {
            get => _sideMenuUtilsUpdateDbVisibility;
            set { _sideMenuUtilsUpdateDbVisibility = value; OnPropertyChanged(); }
        }
        #endregion

        // Status overlay vm (owned by main window)
        private readonly StatusViewModel _statusOverlayVM;

        #endregion

        #region Constructor and factory method
        // Constructor
        private MainWindowViewModel(
            IFilteringService filteringService,
            IEditCollectionService editService,
            IImportExportService importExportService,
            ICardDatabasePreparationService prepService,
            StatusViewModel statusOverlayVM,
            ICardListService cardListService,
            Func<string> getRetailer,
            Action<string> setRetailerAndPersist,
            IFacetUpdateScheduler? facetScheduler = null,
            IFacetUpdater? facetUpdater = null)
        {
            _statusOverlayVM = statusOverlayVM;

            _filteringService = filteringService;
            _importExportService = importExportService;
            _prepService = prepService;
            _cardListService = cardListService;

            _facetScheduler = facetScheduler ?? new DispatcherDebounceScheduler(TimeSpan.FromMilliseconds(150));
            _facetUpdater = facetUpdater ?? new FacetUpdater();

            CurrentPage = Page.SearchAndFilter;

            // card lists
            AllCardsVM = new CardViewModel();
            MyCollectionVM = new CardViewModel();
            AllCardsForDecksVM = new CardViewModel();
            AllCardsInDecksVM = new CardViewModel();
            ColorIcons = new CardViewModel { Cards = [.. ManaKeys.Select(CardSet.FromManaKey)] };

            // edit collection
            AddCardsVM = new EditCollectionViewModel(editService, removeCardWhenZero: true);
            EditCardsVM = new EditCollectionViewModel(editService, removeCardWhenZero: false);

            // filtering
            FilterVM = new FilterViewModel(_filteringService);

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
        }
        public static async Task<MainWindowViewModel> CreateAsync(
            IFilteringService filteringService,
            IEditCollectionService editService,
            IImportExportService importExportService,
            ICardDatabasePreparationService prepService,
            IDownloadService downloadService,
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

            await vm.ReloadAllCardListsAsync();

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
        public ICommand BackupCollectionCommand { get; private set; } = null!;
        public ICommand CheckForDbUpdatesCommand { get; private set; } = null!;
        public ICommand UpdateDBCommand { get; private set; } = null!;
        public ICommand ChangeRetailerCommand { get; private set; } = null!;
        private void BuildCommands()
        {
            ShowSearchAndFilterCommand = new RelayCommand<object>(_ => { CurrentPage = Page.SearchAndFilter; });
            ShowMyCollectionCommand = new RelayCommand<object>(_ => { CurrentPage = Page.MyCollection; });
            ShowDecksCommand = new RelayCommand<object>(_ => CurrentPage = Page.Decks);
            ShowUtilitiesCommand = new RelayCommand<object>(_ => CurrentPage = Page.Utilities);

            BackupCollectionCommand = new RelayCommand<object>(async _ => await BackupCollectionAsync());
            CheckForDbUpdatesCommand = new RelayCommand<object>(async _ => await CheckForDbUpdatesAsync());
            UpdateDBCommand = new RelayCommand<object>(async _ => await UpdateDBAsync());

            ChangeRetailerCommand = new RelayCommand<object>(async _ =>
            {
                if (SelectedRetailer is null)
                {
                    return;
                }

                _setRetailerAndPersist(SelectedRetailer.Key);

                var sample = AllCardsVM.FilteredCards.FirstOrDefault();
                Debug.WriteLine($"Before: {sample?.Uuid} -> {sample?.NormalPrice}");

                await _cardListService.ReloadPriceLookupsAsync(SelectedRetailer.Key);

                RefreshAllPrices();
                Debug.WriteLine($"After: {sample?.Uuid} -> {sample?.NormalPrice}");
            });

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
        private async Task ReloadAllCardListsAsync()
        {
            var sw = Stopwatch.StartNew();
            Debug.WriteLine("[ReloadAllCardListsAsync] Initializing card lists");

            await _cardListService.InitializeAsync(AllCardsVM, MyCollectionVM, FilterVM.Filters, FilterVM);

            FilterVM.NotifyFilterChanged();

            sw.Stop();
            Debug.WriteLine($"[ReloadAllCardListsAsync] M1 finished in {sw.ElapsedMilliseconds} ms ({sw.Elapsed}).");
        }
        #endregion

        #region status overlay / maintenance tasks (backup, update db)
        // Command methods
        private async Task BackupCollectionAsync()
        {
            var result = await _importExportService.ExportCollectionAsync();
            _statusOverlayVM.ShowBackupResult(result);
        }
        private async Task CheckForDbUpdatesAsync()
        {
            _statusOverlayVM.ShowStatusOverlay("One moment - checking for updates...", false);

            var result = await _prepService.CheckForDbUpdatesAsync();

            switch (result.Code)
            {
                case OperationResultCode.UpToDate:
                    _statusOverlayVM.StatusLabel3 = result.Message;
                    _statusOverlayVM.AckButtonVisibility = Visibility.Visible;
                    _statusOverlayVM.AckButtonText = "Got it!";

                    break;

                case OperationResultCode.NeedsUpdate:
                    SideMenuUtilsUpdateDbVisibility = Visibility.Visible;
                    _statusOverlayVM.StatusLabel3 = result.Message;
                    break;

                case OperationResultCode.Error:
                    _statusOverlayVM.AckButtonVisibility = Visibility.Visible;
                    _statusOverlayVM.AckButtonText = "OK";
                    _statusOverlayVM.StatusLabel3 = result.Message;
                    break;
            }
        }
        private async Task UpdateDBAsync()
        {
            _statusOverlayVM.ShowStatusOverlay("Updating database, please wait...", true);
            IsTopMenuEnabled = false; // Disable top menu during update
            SideMenuUtilsVisibility = Visibility.Collapsed; // Hide utilities menu during update


            // Create progress handlers for status updates
            var statusLabel2Progress = new Progress<string>(msg => _statusOverlayVM.StatusLabel2 = msg);
            var statusLabel3Progress = new Progress<string>(msg => _statusOverlayVM.StatusLabel3 = msg);
            var percentProgress = new Progress<int>(percent => _statusOverlayVM.ProgressValue = percent);

            // Start the update process
            var result = await _prepService.UpdateDbPrepOrchetrator();

            _statusOverlayVM.ResetStatusOverlay();


            // Handle the result of the update
            if (result.Code != OperationResultCode.Success)
            {
                _statusOverlayVM.StatusLabel1 = "Update failed!";
                _statusOverlayVM.StatusLabel3 = result.Message;
            }
            else
            {
                _statusOverlayVM.StatusLabel1 = "Database updated successfully!";
                _statusOverlayVM.StatusLabel3 = "Reloading card lists…";
                await Task.Run(() => ReloadAllCardListsAsync());
            }

            _statusOverlayVM.AckButtonVisibility = Visibility.Visible;
            IsTopMenuEnabled = true; // Re-enable top menu after update
            SideMenuUtilsVisibility = Visibility.Visible; // Show utilities menu again
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
