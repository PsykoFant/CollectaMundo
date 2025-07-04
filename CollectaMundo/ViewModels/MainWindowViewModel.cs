using CollectaMundo.ApplicationServices.CardLists;
using CollectaMundo.ApplicationServices.EditCollection;
using CollectaMundo.ApplicationServices.Filtering;
using CollectaMundo.ApplicationServices.ImportExport;
using CollectaMundo.ApplicationServices.Startup;
using CollectaMundo.ApplicationServices.UpdateDB;
using CollectaMundo.ApplicationServices.Utilities;
using CollectaMundo.DomainLogic.EditCollection.Models;
using CollectaMundo.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using static CollectaMundo.DomainLogic.EditCollection.Models.CardChangeEventArgs;

namespace CollectaMundo.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        // INotifyPropertyChanged boilerplate
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        public Action? OnStartupComplete { get; set; }

        private readonly StatusViewModel _statusOverlayVM;
        private readonly IFilteringService _filteringService;
        private readonly IImportExportService _importExportService;
        private readonly IUpdateService _updateService;

        // Constructor
        private MainWindowViewModel(IFilteringService filteringService, IEditCollectionService editService, IImportExportService importExportService, IUpdateService updateService, StatusViewModel statusOverlayVM)
        {
            _statusOverlayVM = statusOverlayVM;

            _filteringService = filteringService;
            _importExportService = importExportService;
            _updateService = updateService;

            CurrentPage = Page.SearchAndFilter;

            AllCardsVM = new CardViewModel();
            MyCollectionVM = new CardViewModel();
            AllCardsForDecksVM = new CardViewModel();
            AllCardsInDecksVM = new CardViewModel();
            ColorIcons = new CardViewModel();

            AddCardsVM = new EditCollectionViewModel(editService, removeCardWhenZero: true);
            EditCardsVM = new EditCollectionViewModel(editService, removeCardWhenZero: false);
            AddCardsVM.CardChanged += OnCardChanged;
            EditCardsVM.CardChanged += OnCardChanged;

            FilterVM = new FilterViewModel(_filteringService);
            FilterVM.FilterChanged += OnFilterChanged;

            MiniLogoVisibilityFlipper();

            ShowSearchAndFilterCommand = new RelayCommand<object>(_ => { CurrentPage = Page.SearchAndFilter; });
            ShowMyCollectionCommand = new RelayCommand<object>(_ => { CurrentPage = Page.MyCollection; });
            ShowDecksCommand = new RelayCommand<object>(_ => CurrentPage = Page.Decks);
            ShowUtilitiesCommand = new RelayCommand<object>(_ => CurrentPage = Page.Utilities);
            BackupCollectionCommand = new RelayCommand<object>(async _ => await BackupCollectionAsync());
            CheckForDbUpdatesCommand = new RelayCommand<object>(async _ => await CheckForDbUpdatesAsync());
            UpdateDBCommand = new RelayCommand<object>(async _ => await UpdateDBAsync());
        }
        // Command methods
        private async Task BackupCollectionAsync()
        {
            var result = await _importExportService.ExportCollectionAsync();
            _statusOverlayVM.ShowBackupResult(result);
        }
        private async Task CheckForDbUpdatesAsync()
        {
            _statusOverlayVM.ShowStatusOverlay("One moment - checking for updates...", false);

            var result = await _updateService.CheckForDbUpdatesAsync();

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
            SideMenuUtilsUpdateDbVisibility = Visibility.Collapsed;
            _statusOverlayVM.ShowStatusOverlay("Updating database, please wait...", true);
            _statusOverlayVM.StatusLabel2 = "Step 1 / 4 - downloading new card database";

            // Step 1: Download the new database
            var statusProgress = new Progress<string>(msg => _statusOverlayVM.StatusLabel1 = msg);
            var percentProgress = new Progress<int>(percent => _statusOverlayVM.ProgressValue = percent);
            var result = await _updateService.UpdateDbAsync(statusProgress, percentProgress);

            if (result.Code == OperationResultCode.Error)
            {
                _statusOverlayVM.StatusLabel2 = string.Empty;
                _statusOverlayVM.StatusLabel3 = result.Message;
                _statusOverlayVM.AckButtonVisibility = Visibility.Visible;
                _statusOverlayVM.ProgressVisibility = Visibility.Collapsed;
                _statusOverlayVM.AckButtonText = "  OK!  ";
                return;
            }

            _statusOverlayVM.AckButtonVisibility = Visibility.Visible;
            _statusOverlayVM.AckButtonText = "  OK!  ";
            _statusOverlayVM.ShowStatusOverlay(result.Message);
        }



        // Page navigation
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

        // Viewmodels
        public CardViewModel AllCardsVM { get; }
        public CardViewModel AllCardsForDecksVM { get; }
        public CardViewModel AllCardsInDecksVM { get; }
        public CardViewModel MyCollectionVM { get; }
        public CardViewModel ColorIcons { get; }
        public EditCollectionViewModel AddCardsVM { get; }
        public EditCollectionViewModel EditCardsVM { get; }
        public FilterViewModel FilterVM { get; }

        // Misc. properties
        public ObservableCollection<ObservableCollection<double>> ColumnWidths { get; set; } = [[50, 50], [50, 50], [50]];
        // HideStatusOverlay mini logo at appropriate times
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

        // Grid visibility properties

        // Main Grids
        private Visibility _mainGridVisibility = Visibility.Collapsed;
        public Visibility MainGridVisibility
        {
            get => _mainGridVisibility;
            set { _mainGridVisibility = value; OnPropertyChanged(); }
        }

        private Visibility _contenSectionVisibility = Visibility.Hidden;
        public Visibility ContenSectionVisibility
        {
            get => _contenSectionVisibility;
            set { _contenSectionVisibility = value; OnPropertyChanged(); }
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

        //private Visibility _sideMenuUtilsUpdateDbVisibility = Visibility.Collapsed;
        // debug - always visible
        private Visibility _sideMenuUtilsUpdateDbVisibility = Visibility.Collapsed;

        public Visibility SideMenuUtilsUpdateDbVisibility
        {
            get => _sideMenuUtilsUpdateDbVisibility;
            set { _sideMenuUtilsUpdateDbVisibility = value; OnPropertyChanged(); }
        }

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

        // Commands to switch pages
        public ICommand ShowSearchAndFilterCommand { get; }
        public ICommand ShowMyCollectionCommand { get; }
        public ICommand ShowDecksCommand { get; }
        public ICommand ShowUtilitiesCommand { get; }

        // Utilities commands
        public ICommand BackupCollectionCommand { get; }
        public ICommand CheckForDbUpdatesCommand { get; }
        public ICommand UpdateDBCommand { get; }


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
        }

        // When filters are updated
        private void OnFilterChanged(object? sender, EventArgs e)
        {
            AllCardsVM.FilteredCards = _filteringService.ApplyFilters(AllCardsVM.Cards, FilterVM.Filters.Values);
            MyCollectionVM.FilteredCards = _filteringService.ApplyFilters(MyCollectionVM.Cards, FilterVM.Filters.Values);
            AllCardsForDecksVM.FilteredCards = _filteringService.ApplyFilters(AllCardsForDecksVM.Cards, FilterVM.Filters.Values);
        }
        private void MiniLogoVisibilityFlipper()
        {
            AddCardsVM.PropertyChanged += (_, e) => { if (e.PropertyName == "StatusVisibility") { OnPropertyChanged(nameof(MiniLogoVisibility)); } };
            EditCardsVM.PropertyChanged += (_, e) => { if (e.PropertyName == "StatusVisibility") { OnPropertyChanged(nameof(MiniLogoVisibility)); } };
        }


        // Factory method to create the ViewModel
        public static async Task<MainWindowViewModel> CreateAsync(IFilteringService filteringService, IEditCollectionService editService, IImportExportService importExportService, IUpdateService updateService, StatusViewModel statusVM, Action? onStartupComplete = null)
        {
            var vm = new MainWindowViewModel(filteringService, editService, importExportService, updateService, statusVM)
            {
                OnStartupComplete = onStartupComplete
            };

            await MainWindowInitializer.InitializeAsync(
                [
                    (vm.AllCardsVM, CardListQueryCatalog.AllCards),
                    (vm.MyCollectionVM, CardListQueryCatalog.MyCollection),
                    (vm.AllCardsForDecksVM, CardListQueryCatalog.AllCardsForDecks),
                    (vm.AllCardsInDecksVM, CardListQueryCatalog.AllCardsInDecks),
                    (vm.ColorIcons, CardListQueryCatalog.ColorIcons)
                ],
                vm.FilterVM.Filters, vm.FilterVM
            );

            vm.FilterVM.NotifyFilterChanged();
            vm.OnStartupComplete?.Invoke();
            return vm;
        }

    }
}
