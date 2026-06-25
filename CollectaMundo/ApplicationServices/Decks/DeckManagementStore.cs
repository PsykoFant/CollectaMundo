using CollectaMundo.ApplicationServices.CardLocations;
using CollectaMundo.ApplicationServices.Decks.Models;
using CollectaMundo.ViewModels.Decks.Models;
using System.Collections.ObjectModel;

namespace CollectaMundo.ApplicationServices.Decks
{
    public sealed class DeckManagementStore(ICardLocationService cardLocationService) : IDeckManagementStore
    {
        private readonly ICardLocationService _cardLocationService = cardLocationService;
        public ObservableCollection<DeckManagementRecord> Decks { get; } = [];
        public ObservableCollection<DeckFormatOption> DeckFormats { get; } = [];
        public async Task LoadAsync()
        {
            var loadedDecks = await _cardLocationService.GetAllDecksAsync();
            var loadedFormats = await _cardLocationService.GetDeckFormatsAsync();

            Decks.Clear();
            foreach (var deck in loadedDecks)
            {
                Decks.Add(deck);
            }

            DeckFormats.Clear();

            DeckFormats.Add(new DeckFormatOption(string.Empty, "(No format)"));
            DeckFormats.Add(new DeckFormatOption("casual", "Casual/kitchen table"));

            foreach (var format in loadedFormats)
            {
                DeckFormats.Add(CreateDeckFormatOption(format));
            }
        }
        private static DeckFormatOption CreateDeckFormatOption(string value)
        {
            return new DeckFormatOption(value, char.ToUpperInvariant(value[0]) + value[1..]);
        }
        public void Upsert(DeckManagementRecord deck)
        {
            int existingIndex = -1;

            for (int i = 0; i < Decks.Count; i++)
            {
                if (Decks[i].LocationId == deck.LocationId)
                {
                    existingIndex = i;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                Decks[existingIndex] = deck;
                return;
            }

            Decks.Add(deck);
        }
        public void Remove(int locationId)
        {
            var existingDeck = Decks.FirstOrDefault(deck => deck.LocationId == locationId);

            if (existingDeck is not null)
            {
                Decks.Remove(existingDeck);
            }
        }
    }
}
