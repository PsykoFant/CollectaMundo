using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.ViewModels.CardLists
{
    public partial class CardListViewModel<TCard> : ObservableObject
    {
        public List<TCard> Cards { get; set; } = [];

        [ObservableProperty]
        private List<TCard> filteredCards = [];

        public int FilteredCount => FilteredCards.Count;
        public int TotalCount => Cards.Count;

        partial void OnFilteredCardsChanged(List<TCard>? oldValue, List<TCard> newValue)
        {
            OnPropertyChanged(nameof(FilteredCount));
            OnPropertyChanged(nameof(TotalCount));
        }
    }
}
