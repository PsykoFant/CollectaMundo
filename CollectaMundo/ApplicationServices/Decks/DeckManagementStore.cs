using CollectaMundo.ApplicationServices.CardLocations;
using CollectaMundo.DomainLogic.Decks.Models;
using System.Collections.ObjectModel;

namespace CollectaMundo.ApplicationServices.Decks
{
    public sealed class DeckManagementStore(ICardLocationService cardLocationService) : IDeckManagementStore
    {
        private readonly ICardLocationService _cardLocationService = cardLocationService;

        public ObservableCollection<DeckManagementRecord> Decks { get; } = [];
        public async Task LoadAsync()
        {
            var loadedDecks = await _cardLocationService.GetAllDecksAsync();

            Decks.Clear();

            foreach (var deck in loadedDecks)
            {
                Decks.Add(deck);
            }
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
