using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Shared.CardModels;

namespace CollectaMundo.DomainLogic.Decks
{
    public static class DeckCardStateMapper
    {
        public static IReadOnlyList<DeckCardState> CreateStates(IEnumerable<DeckCardEntry> entries, IEnumerable<OracleCard> oracleCards)
        {
            var oracleCardsById = oracleCards.Where(card => !string.IsNullOrWhiteSpace(card.ScryfallOracleId)).ToDictionary(card => card.ScryfallOracleId, StringComparer.OrdinalIgnoreCase);
            var states = new List<DeckCardState>();

            foreach (var entry in entries)
            {
                if (!oracleCardsById.TryGetValue(entry.OracleId, out var oracleCard))
                {
                    continue;
                }

                states.Add(new DeckCardState
                {
                    Card = oracleCard,
                    DesiredQuantity = entry.DesiredQuantity,
                    Section = entry.Section
                });
            }

            return states;
        }
    }
}
