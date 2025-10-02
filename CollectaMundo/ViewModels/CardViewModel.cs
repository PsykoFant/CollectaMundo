using CollectaMundo.DomainLogic.CardLists.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.ViewModels
{
    public partial class CardViewModel : ObservableObject
    {
        public List<CardSet> Cards { get; set; } = [];

        [ObservableProperty]
        private List<CardSet> filteredCards = [];

        public int FilteredCount => FilteredCards.Count;
        public int TotalCount => Cards.Count;

        partial void OnFilteredCardsChanged(List<CardSet>? oldValue, List<CardSet> newValue)
        {
            OnPropertyChanged(nameof(FilteredCount));
            OnPropertyChanged(nameof(TotalCount));
        }
    }
}
