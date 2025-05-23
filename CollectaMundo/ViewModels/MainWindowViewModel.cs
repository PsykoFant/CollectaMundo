using CollectaMundo.ApplicationServices;
using CollectaMundo.Data;
using CollectaMundo.DomainLogic;
using CollectaMundo.DomainLogic.Models;
using CollectaMundo.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using static CollectaMundo.DomainLogic.Models.CardChangeEventArgs;

namespace CollectaMundo.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        // INotifyPropertyChanged boilerplate
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // Page navigation

        private Page _currentPage = Page.SearchAndFilter;
        public Page CurrentPage
        {
            get => _currentPage;
            set
            {
                if (_currentPage == value) return;
                _currentPage = value;

                if (_currentPage == Page.MyCollection)
                    AddCardsVM.StatusMessage = string.Empty;
                else if (_currentPage == Page.SearchAndFilter)
                    EditCardsVM.StatusMessage = string.Empty;

                // 1) CurrentPage changed
                OnPropertyChanged();

                // 2) the top‐menu’s enabledness depends on CurrentPage
                OnPropertyChanged(nameof(IsTopMenuEnabled));

                // 3) your little logo’s IdleVisibility also depends on CurrentPage
                OnPropertyChanged(nameof(IdleVisibility));
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

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        private bool _isProgressVisible;
        public bool IsProgressVisible
        {
            get => _isProgressVisible;
            set { _isProgressVisible = value; OnPropertyChanged(); }
        }

        private string _firstTimeSetupText = "";
        public string FirstTimeSetupText
        {
            get => _firstTimeSetupText;
            set { _firstTimeSetupText = value; OnPropertyChanged(); }
        }
        // Misc. properties
        public ObservableCollection<ObservableCollection<double>> ColumnWidths { get; set; } = [[50, 50], [50, 50], [50]];
        // Hide mini logo at appropriate times
        public Visibility IdleVisibility
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

        public bool IsTopMenuEnabled => CurrentPage != Page.StatusScreen;


        // Backing fields
        private readonly IDbConnectionFactory _dbFactory;
        private readonly IFilteringService _filteringService;

        // Commands to switch pages
        public ICommand ShowSearchAndFilterCommand { get; }
        public ICommand ShowMyCollectionCommand { get; }
        public ICommand ShowDecksCommand { get; }
        public ICommand ShowUtilitiesCommand { get; }

        // Constructor
        public MainWindowViewModel(IDbConnectionFactory dbFactory)
        {
            CurrentPage = Page.StatusScreen;

            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));

            AllCardsVM = new CardViewModel();
            MyCollectionVM = new CardViewModel();
            AllCardsForDecksVM = new CardViewModel();
            AllCardsInDecksVM = new CardViewModel();
            ColorIcons = new CardViewModel();

            var editRepo = new EditCollectionRepository(_dbFactory);
            var editLogic = new EditCollectionLogic(editRepo);
            var editUow = new UnitOfWork(_dbFactory);
            var editService = new EditCollectionService(editUow, editLogic);
            AddCardsVM = new EditCollectionViewModel(editService, removeCardWhenZero: true);
            EditCardsVM = new EditCollectionViewModel(editService, removeCardWhenZero: false);
            AddCardsVM.CardChanged += OnCardChanged;
            EditCardsVM.CardChanged += OnCardChanged;

            // 3) "Filtering" stack: defaults repo --> filtering coordinator --> view-model
            var filterDefaultsRepo = new FilterDefaultsRepository(_dbFactory);
            _filteringService = new FilteringService(filterDefaultsRepo);
            // now hand it off to your FilterVM
            FilterVM = new FilterViewModel(_filteringService);
            _ = InitializeFiltersAsync(filterDefaultsRepo);
            FilterVM.FilterChanged += OnFilterChanged;

            HookUpStatusChanged();

            ShowSearchAndFilterCommand = new RelayCommand<object>(_ =>
            {
                CurrentPage = Page.SearchAndFilter;
            });
            ShowMyCollectionCommand = new RelayCommand<object>(_ =>
            {
                CurrentPage = Page.MyCollection;
            });
            ShowDecksCommand = new RelayCommand<object>(_ => CurrentPage = Page.Decks);
            ShowUtilitiesCommand = new RelayCommand<object>(_ => CurrentPage = Page.Utilities);
        }

        private async Task InitializeFiltersAsync(IFilterDefaultsRepository defaultsRepo)
        {
            var uow = new UnitOfWork(_dbFactory);
            await FilterVM.InitializeAsync(defaultsRepo, uow);
        }

        public void ShowStatusScreen(bool show, string? message = null, bool progress = false, string? firstTimeText = null)
        {
            CurrentPage = show ? Page.StatusScreen : Page.SearchAndFilter;
            if (message != null) StatusMessage = message;
            if (firstTimeText != null) FirstTimeSetupText = firstTimeText;
            IsProgressVisible = progress;
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
            AddCardsVM.PropertyChanged += (_, e) => { if (e.PropertyName == "StatusVisibility") OnPropertyChanged(); };
            EditCardsVM.PropertyChanged += (_, e) => { if (e.PropertyName == "StatusVisibility") OnPropertyChanged(); };
        }

    }

}
