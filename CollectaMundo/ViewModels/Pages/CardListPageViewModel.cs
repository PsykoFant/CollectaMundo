using CollectaMundo.ViewModels.Pages.SharedElements;
using CollectaMundo.Views.Pages.SharedElements;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace CollectaMundo.ViewModels.Pages
{
    public class CardListPageViewModel : ObservableObject
    {
        public CardViewModel CardsVM { get; }
        public CardImageViewModel CardImageVM { get; }
        public FilterViewModel FilterVM { get; }
        public EditCollectionViewModel? AddCardsVM { get; }
        public PricesViewModel? PricesVM { get; }
        public FilterHeaderModel? NameHeader { get; private set; }
        public FilterHeaderModel? SetNameHeader { get; private set; }
        public CardListPageViewModel(CardViewModel cardsVM,CardImageViewModel cardImageVM,FilterViewModel filterVM,PricesViewModel? pricesVM = null,EditCollectionViewModel? addOrEditCardsVM = null)
        {
            CardsVM = cardsVM;
            CardImageVM = cardImageVM;
            FilterVM = filterVM;
            PricesVM = pricesVM;
            AddCardsVM = addOrEditCardsVM;

            NameHeader = new FilterHeaderModel("Name");
            SetNameHeader = new FilterHeaderModel("Set Name");

            FilterVM.PropertyChanged += FilterVM_PropertyChanged;
        }
        private void FilterVM_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FilterViewModel.Filters))
                RebindHeaders();
        }

        private void RebindHeaders()
        {
            FilterVM.Filters.TryGetValue("Name", out var nameItem);
            FilterVM.Filters.TryGetValue("SetName", out var setItem);

            NameHeader.FilterItem = nameItem;
            SetNameHeader.FilterItem = setItem;
        }
    }
}
