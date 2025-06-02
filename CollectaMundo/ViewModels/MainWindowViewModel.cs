using CollectaMundo.ApplicationServices;
using CollectaMundo.ApplicationServices.CardLists;
using CollectaMundo.Data;
using CollectaMundo.DomainLogic.EditCollection.Models;
using CollectaMundo.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
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

        // Page navigation

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

                if (_currentPage == Page.MyCollection)
                {
                    AddCardsVM.StatusMessage = string.Empty;
                }
                else if (_currentPage == Page.SearchAndFilter)
                {
                    EditCardsVM.StatusMessage = string.Empty;
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
        // Hide mini logo at appropriate times
        public Visibility MiniLogoVisibility
        {
            get
            {
                // if *either* status box is Visible, hide our logo
                bool addBusy = AddCardsVM.StatusVisibility == Visibility.Visible;
                bool editBusy = EditCardsVM.StatusVisibility == Visibility.Visible;
                return (addBusy || editBusy)
                  ? Visibility.Collapsed
                  : Visibility.Visible;
            }
        }

        // Grid visibility properties

        private Visibility _mainGridVisibility = Visibility.Collapsed;
        public Visibility MainGridVisibility
        {
            get => _mainGridVisibility;
            set { _mainGridVisibility = value; OnPropertyChanged(); }
        }
        private Visibility _sideMenuVisibility = Visibility.Hidden;
        public Visibility SideMenuVisibility
        {
            get => _sideMenuVisibility;
            set { _sideMenuVisibility = value; OnPropertyChanged(); }
        }

        private Visibility _contenSectionVisibility = Visibility.Hidden;
        public Visibility ContenSectionVisibility
        {
            get => _contenSectionVisibility;
            set { _contenSectionVisibility = value; OnPropertyChanged(); }
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


        // Backing fields
        private readonly IDbConnectionFactory _dbFactory;
        private readonly IFilteringService _filteringService;

        // Commands to switch pages
        public ICommand ShowSearchAndFilterCommand { get; }
        public ICommand ShowMyCollectionCommand { get; }
        public ICommand ShowDecksCommand { get; }
        public ICommand ShowUtilitiesCommand { get; }

        // Constructor
        private MainWindowViewModel(IDbConnectionFactory dbFactory)
        {
            CurrentPage = Page.SearchAndFilter;

            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));

            AllCardsVM = new CardViewModel();
            MyCollectionVM = new CardViewModel();
            AllCardsForDecksVM = new CardViewModel();
            AllCardsInDecksVM = new CardViewModel();
            ColorIcons = new CardViewModel();

            // Edit collection stack
            var editRepo = new EditCollectionRepository(new SQLiteConnection());
            var editUow = new UnitOfWork(_dbFactory);
            var editService = new EditCollectionService(editUow);
            AddCardsVM = new EditCollectionViewModel(editService, removeCardWhenZero: true);
            EditCardsVM = new EditCollectionViewModel(editService, removeCardWhenZero: false);
            AddCardsVM.CardChanged += OnCardChanged;
            EditCardsVM.CardChanged += OnCardChanged;

            // Filtering stack
            var filterUow = new UnitOfWork(_dbFactory);
            _filteringService = new FilteringService();
            FilterVM = new FilterViewModel(_filteringService);
            FilterVM.FilterChanged += OnFilterChanged;

            HookUpStatusChanged();

            ShowSearchAndFilterCommand = new RelayCommand<object>(_ => { CurrentPage = Page.SearchAndFilter; });
            ShowMyCollectionCommand = new RelayCommand<object>(_ => { CurrentPage = Page.MyCollection; });
            ShowDecksCommand = new RelayCommand<object>(_ => CurrentPage = Page.Decks);
            ShowUtilitiesCommand = new RelayCommand<object>(_ => CurrentPage = Page.Utilities);
        }
        public static async Task<MainWindowViewModel> CreateAsync(IDbConnectionFactory dbFactory, Action? onStartupComplete = null)
        {
            var vm = new MainWindowViewModel(dbFactory)
            {
                OnStartupComplete = onStartupComplete
            };
            await vm.InitializeListsAsync();
            return vm;
        }
        private async Task InitializeListsAsync()
        {
            var init = new MainWindowInitializer(_dbFactory);
            await init.InitializeAsync(
                new List<(CardViewModel, CardListQuerySpec)>
                {
                    (AllCardsVM, CardListQueryCatalog.AllCards),
                    (MyCollectionVM, CardListQueryCatalog.MyCollection),
                    (AllCardsForDecksVM, CardListQueryCatalog.AllCardsForDecks),
                    (AllCardsInDecksVM, CardListQueryCatalog.AllCardsInDecks),
                    (ColorIcons, CardListQueryCatalog.ColorIcons)
                },
                FilterVM.Filters, FilterVM
            );

            FilterVM.NotifyFilterChanged();
            OnStartupComplete?.Invoke();
        }


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

        private void HookUpStatusChanged()
        {
            AddCardsVM.PropertyChanged += (_, e) => { if (e.PropertyName == "StatusVisibility") { OnPropertyChanged(nameof(MiniLogoVisibility)); } };
            EditCardsVM.PropertyChanged += (_, e) => { if (e.PropertyName == "StatusVisibility") { OnPropertyChanged(nameof(MiniLogoVisibility)); } };
        }

    }

}
