using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.ViewModels.Pages
{
    public sealed class CardListPageViewModel : ObservableObject
    {
        public CardViewModel CardsVM { get; }
        public CardImageViewModel CardImageVM { get; }
        public FilterViewModel FilterVM { get; }
        public ModifyCollectionViewModel? ModifyCollectionViewModel { get; }
        public PricesViewModel? PricesVM { get; }

        public FilterItemViewModel? NameFilter => FilterVM.Filters.TryGetValue("Name", out var f) ? f : null;
        public FilterItemViewModel? SetNameFilter => FilterVM.Filters.TryGetValue("SetName", out var f) ? f : null;
        public CardListPageViewModel(CardViewModel cardsVM, CardImageViewModel cardImageVM, FilterViewModel filterVM, PricesViewModel? pricesVM = null, ModifyCollectionViewModel? modifyCollectionVM = null)
        {
            CardsVM = cardsVM;
            CardImageVM = cardImageVM;
            FilterVM = filterVM;
            PricesVM = pricesVM;
            ModifyCollectionViewModel = modifyCollectionVM;

            FilterVM.FiltersRebuilt += (_, _) =>
            {
                OnPropertyChanged(nameof(NameFilter));
                OnPropertyChanged(nameof(SetNameFilter));
            };
        }
    }
}
