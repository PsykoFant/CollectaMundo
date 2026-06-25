using CollectaMundo.ApplicationServices.Decks;
using CollectaMundo.ApplicationServices.Decks.Models;
using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.ViewModels.CardLists;
using CollectaMundo.ViewModels.Decks.Models;
using CollectaMundo.ViewModels.Filtering;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace CollectaMundo.ViewModels.Decks
{
    public partial class DeckBuilderViewModel(IDeckBuilderService deckBuilderService, CardListViewModel<OracleCard> oracleCardsVM, FilterPanelViewModel filterPanelViewModel) : ObservableObject
    {
        private readonly IDeckBuilderService _deckBuilderService = deckBuilderService;

        public event EventHandler? ExitEditorRequested;
        public event EventHandler<OracleCardImageSelectionRequest?>? CardImageSelectionRequested;
        public CardListViewModel<OracleCard> CardsVM { get; } = oracleCardsVM;
        public ObservableCollection<DeckCardEntryViewModel> DeckCards { get; } = [];
        public FilterPanelViewModel FilterVM { get; } = filterPanelViewModel;

        // Bindable pass-through properties for the filters 
        public FilterItemViewModel? NameFilter => FilterVM.Filters.TryGetValue("Name", out var f) ? f : null;


        [ObservableProperty]
        private OracleCard? selectedOracleCard;

        [ObservableProperty]
        private DeckCardEntryViewModel? selectedDeckCard;

        [ObservableProperty]
        private int? deckLocationId;

        [ObservableProperty]
        private string deckName = string.Empty;

        [ObservableProperty]
        private string? deckFormat;

        [ObservableProperty]
        private string? deckDescription;
        public async Task BeginEditAsync(DeckManagementRecord deck)
        {
            var entries = await _deckBuilderService.LoadDeckAsync(deck.LocationId);

            var rows = new List<DeckCardEntryViewModel>();

            foreach (var entry in entries)
            {
                var oracleCard = CardsVM.Cards.FirstOrDefault(c => c.ScryfallOracleId == entry.OracleId);

                if (oracleCard is null)
                {
                    continue;
                }

                rows.Add(new DeckCardEntryViewModel
                {
                    OracleCard = oracleCard,
                    DesiredQuantity = entry.DesiredQuantity,
                    Section = entry.Section
                });
            }

            DeckLocationId = deck.LocationId;
            DeckName = deck.Name;
            DeckFormat = deck.Format;
            DeckDescription = deck.Description;

            DeckCards.Clear();

            foreach (var row in rows)
            {
                DeckCards.Add(row);
            }
        }

        // Selecting a card in OracleCard datagrid
        partial void OnSelectedOracleCardChanged(OracleCard? value)
        {
            if (value is null)
            {
                CardImageSelectionRequested?.Invoke(this, new OracleCardImageSelectionRequest());
                return;
            }

            CardImageSelectionRequested?.Invoke(this, new OracleCardImageSelectionRequest(OracleId: value.ScryfallOracleId, Name: value.Name));
        }

        // Selecting a card in deck datagrid
        partial void OnSelectedDeckCardChanged(DeckCardEntryViewModel? value)
        {
            if (value is null)
            {
                CardImageSelectionRequested?.Invoke(this, new OracleCardImageSelectionRequest());
                return;
            }

            CardImageSelectionRequested?.Invoke(
                this,
                new OracleCardImageSelectionRequest(
                    OracleId: value.OracleId,
                    Name: value.CardName));
        }

        [RelayCommand]
        private async Task AddOracleCardToDeckAsync(object? param)
        {
            var cards = GetOracleCardsFromCommandParameter(param).ToList();

            if (cards.Count == 0)
            {
                return;
            }

            foreach (var card in cards)
            {
                await AddOracleCardQuantityToDeckAsync(card, quantityToAdd: 1);
            }
        }

        [RelayCommand]
        private async Task AddOracleCardPlaySetToDeckAsync(object? param)
        {
            var cards = GetOracleCardsFromCommandParameter(param).ToList();

            if (cards.Count == 0)
            {
                return;
            }

            foreach (var card in cards)
            {
                await AddOracleCardQuantityToDeckAsync(card, quantityToAdd: 4);
            }
        }

        // Add OracleCard helpers
        private static IEnumerable<OracleCard> GetOracleCardsFromCommandParameter(object? param)
        {
            if (param is OracleCard singleCard)
            {
                yield return singleCard;
                yield break;
            }

            if (param is IEnumerable selectedItems)
            {
                foreach (var item in selectedItems)
                {
                    if (item is OracleCard card)
                    {
                        yield return card;
                    }
                }
            }
        }
        private async Task AddOracleCardQuantityToDeckAsync(OracleCard? card, int quantityToAdd)
        {
            if (card is null || DeckLocationId is null || quantityToAdd <= 0)
            {
                return;
            }

            var existing = DeckCards.FirstOrDefault(x => x.OracleId == card.ScryfallOracleId && x.Section == DeckSection.Mainboard);

            DeckCardEntryViewModel? addedRow = null;
            var previousQuantity = existing?.DesiredQuantity;

            try
            {
                if (existing is not null)
                {
                    existing.DesiredQuantity += quantityToAdd;
                }
                else
                {
                    addedRow = new DeckCardEntryViewModel
                    {
                        OracleCard = card,
                        Section = DeckSection.Mainboard,
                        DesiredQuantity = quantityToAdd
                    };

                    DeckCards.Add(addedRow);
                }

                await PersistDeckAsync();
            }
            catch (Exception ex)
            {
                if (existing is not null && previousQuantity is not null)
                {
                    existing.DesiredQuantity = previousQuantity.Value;
                }

                if (addedRow is not null)
                {
                    DeckCards.Remove(addedRow);
                }

                Debug.WriteLine($"Failed to add card to deck: {ex}");
                throw;
            }
        }

        // Persist in db
        private Task PersistDeckAsync()
        {
            if (DeckLocationId is null)
            {
                return Task.CompletedTask;
            }

            var entries = DeckCards.Where(x => x.OracleCard is not null).Select(x => new DeckCardEntry
            {
                DeckLocationId = DeckLocationId.Value,
                OracleId = x.OracleId,
                CardName = x.CardName,
                DesiredQuantity = x.DesiredQuantity,
                Section = x.Section
            })
                .ToList();

            return _deckBuilderService.SaveDeckAsync(DeckLocationId.Value, entries);
        }

        [RelayCommand]
        private void BackToDeckManagement()
        {
            SelectedOracleCard = null;
            ExitEditorRequested?.Invoke(this, EventArgs.Empty);
        }


    }
}
