using CollectaMundo.ApplicationServices;
using CollectaMundo.Data;
using CollectaMundo.DomainLogic;
using CollectaMundo.DomainLogic.Models;
using CollectaMundo.UICoordinators;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using static CollectaMundo.DomainLogic.Models.CardChangeEventArgs;

namespace CollectaMundo.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        // 1) INotifyPropertyChanged boilerplate
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // 2) The same properties your bindings refer to:
        public CardViewModel AllCardsVM { get; }
        public CardViewModel AllCardsForDecksVM { get; }
        public CardViewModel AllCardsInDecksVM { get; }
        public CardViewModel MyCollectionVM { get; }
        public CardViewModel ColorIcons { get; }
        public EditCollectionViewModel AddCardsVM { get; }
        public EditCollectionViewModel EditCardsVM { get; }
        public FilterViewModel FilterVM { get; }
        public ObservableCollection<ObservableCollection<double>> ColumnWidths { get; set; } = [[50, 50], [50, 50], [50]];

        private readonly IFilteringService _filteringService;
        private readonly IFilteringService _filterCoordinator = new FilteringService(new FilterDefaultsRepository());


        public MainWindowViewModel(SQLiteConnection connection)
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

        }

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
            MyCollectionVM.FilteredCards =
                _filteringService.ApplyFilters(MyCollectionVM.Cards, FilterVM.Filters.Values);
        }
        private void OnFilterChanged(object? sender, EventArgs e)
        {
            AllCardsVM.FilteredCards = _filteringService.ApplyFilters(AllCardsVM.Cards, FilterVM.Filters.Values);
            MyCollectionVM.FilteredCards = _filteringService.ApplyFilters(MyCollectionVM.Cards, FilterVM.Filters.Values);
            AllCardsForDecksVM.FilteredCards = _filteringService.ApplyFilters(AllCardsForDecksVM.Cards, FilterVM.Filters.Values);
        }

    }

}
