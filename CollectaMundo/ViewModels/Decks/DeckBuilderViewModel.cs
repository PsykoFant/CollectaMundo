using CollectaMundo.ApplicationServices.CardLocations.Models;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.ViewModels.CardLists;
using CollectaMundo.ViewModels.Decks.Models;
using CollectaMundo.ViewModels.Filtering;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CollectaMundo.ViewModels.Decks
{
    public partial class DeckBuilderViewModel(CardListViewModel<OracleCard> oracleCardsVM, FilterPanelViewModel filterPanelViewModel) : ObservableObject
    {
        public event EventHandler? ExitEditorRequested;
        public event EventHandler<OracleCardImageSelectionRequest?>? CardImageSelectionRequested;
        public CardListViewModel<OracleCard> CardsVM { get; } = oracleCardsVM;
        public ObservableCollection<DeckCardEntryViewModel> DeckCards { get; } = [];
        public FilterPanelViewModel FilterVM { get; } = filterPanelViewModel;

        [ObservableProperty]
        private OracleCard? selectedOracleCard;

        [ObservableProperty]
        private int? deckLocationId;

        [ObservableProperty]
        private string deckName = string.Empty;

        [ObservableProperty]
        private string? deckFormat;

        [ObservableProperty]
        private string? deckDescription;
        public Task BeginEditAsync(DeckManagementRecord deck)
        {
            DeckLocationId = deck.LocationId;
            DeckName = deck.Name;
            DeckFormat = deck.Format;
            DeckDescription = deck.Description;

            DeckCards.Clear();

            return Task.CompletedTask;
        }
        partial void OnSelectedOracleCardChanged(OracleCard? value)
        {
            if (value is null)
            {
                CardImageSelectionRequested?.Invoke(this, new OracleCardImageSelectionRequest());
                return;
            }

            CardImageSelectionRequested?.Invoke(this, new OracleCardImageSelectionRequest(OracleId: value.ScryfallOracleId, Name: value.Name));
        }

        // Bindable pass-through properties for the filters 
        public FilterItemViewModel? NameFilter => FilterVM.Filters.TryGetValue("Name", out var f) ? f : null;

        [RelayCommand]
        private void BackToDeckManagement()
        {
            SelectedOracleCard = null;
            ExitEditorRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void AddOracleCardToDeck(OracleCard? card)
        {
            if (card is null)
            {
                return;
            }

            var existing = DeckCards.FirstOrDefault(x => x.OracleId == card.ScryfallOracleId);

            if (existing is not null)
            {
                existing.DesiredQuantity++;
                return;
            }

            DeckCards.Add(new DeckCardEntryViewModel
            {
                OracleId = card.ScryfallOracleId,
                CardName = card.Name,
                ManaCostImage = card.ManaCostImage
            });
        }
    }
}
