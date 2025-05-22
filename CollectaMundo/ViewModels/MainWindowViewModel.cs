using CollectaMundo.ApplicationServices;
using CollectaMundo.Data;
using CollectaMundo.DomainLogic;
using CollectaMundo.DomainLogic.Models;
using CollectaMundo.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using static CollectaMundo.DomainLogic.Models.CardChangeEventArgs;

namespace CollectaMundo.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        // INotifyPropertyChanged boilerplate
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // Page navigation

        private Page _currentPage = Page.SearchAndFilter;
        public Page CurrentPage
        {
            get => _currentPage;
            set
            {
                if (_currentPage == value) return;
                _currentPage = value;

                // clear the “old” page's status
                if (_currentPage == Page.MyCollection)
                    AddCardsVM.StatusMessage = string.Empty;
                else if (_currentPage == Page.SearchAndFilter)
                    EditCardsVM.StatusMessage = string.Empty;

                OnPropertyChanged(nameof(CurrentPage));
                // make sure IdleVisibility re-evaluates now that one status has been cleared
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


        // Misc. properties and fields
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

        private readonly IFilteringService _filteringService;
        private readonly IFilteringService _filterCoordinator = new FilteringService(new FilterDefaultsRepository());

        // Commands to switch pages
        public ICommand ShowSearchAndFilterCommand { get; }
        public ICommand ShowMyCollectionCommand { get; }
        public ICommand ShowDecksCommand { get; }
        public ICommand ShowUtilitiesCommand { get; }

        // Constructor
        public MainWindowViewModel(
    IAppSettings settings,
    IDbConnectionFactory dbFactory,
    ICardListService cardListSvc,
    IFilterDefaultsService filterDefaultsSvc,
    IFilteringService filterSvc,
    IEditCollectionService editCollSvc
)
        {
            AllCardsVM = new CardViewModel();
            MyCollectionVM = new CardViewModel();
            AllCardsForDecksVM = new CardViewModel();
            AllCardsInDecksVM = new CardViewModel();
            FilterVM = new FilterViewModel(_filterCoordinator);
            ColorIcons = new CardViewModel();

            var editRepo = new EditCollectionRepository(connection);
            var editLogic = new EditCollectionLogic(editRepo);
            var editUow = new UnitOfWork(connection);
            var editCoordinator = new EditCollectionService(editUow, editLogic);
            AddCardsVM = new EditCollectionViewModel(editCoordinator, removeCardWhenZero: true);
            EditCardsVM = new EditCollectionViewModel(editCoordinator, removeCardWhenZero: false);
            AddCardsVM.CardChanged += OnCardChanged;
            EditCardsVM.CardChanged += OnCardChanged;

            // 3) "Filtering" stack: defaults repo --> filtering coordinator --> view-model
            var filterDefaultsRepo = new FilterDefaultsRepository();
            var filteringCoordinator = new FilteringService(filterDefaultsRepo);
            _filteringService = filteringCoordinator;
            FilterVM = new FilterViewModel(filteringCoordinator);
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
            AddCardsVM.PropertyChanged += (_, e) => { if (e.PropertyName == "StatusVisibility") OnPropertyChanged(nameof(IdleVisibility)); };
            EditCardsVM.PropertyChanged += (_, e) => { if (e.PropertyName == "StatusVisibility") OnPropertyChanged(nameof(IdleVisibility)); };
        }

    }

}
