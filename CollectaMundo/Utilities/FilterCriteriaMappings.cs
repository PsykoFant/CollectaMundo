using CollectaMundo.Models;
using static CollectaMundo.Models.CardSet;

namespace CollectaMundo.Utilities
{
    public static class FilterCriteriaMappings
    {
        public static readonly Dictionary<string, string> CriteriaKeyToPropertyMap = new()
        {
            { "Name", nameof(CardSet.Name) },
            { "SetName", nameof(CardSet.SetName) },
            { "Colors", nameof(CardSet.Colors) },
            { "ManaValue", nameof(CardSet.ManaValue) },
            { "Rarity", nameof(CardSet.Rarity) },
            { "SuperTypes", nameof(CardSet.SuperTypes) },
            { "Types", nameof(CardSet.Types) },
            { "SubTypes", nameof(CardSet.SubTypes) },
            { "Keywords", nameof(CardSet.Keywords) },
            { "Text", nameof(CardSet.Text) },
            { "Finishes", nameof(CardSet.Finishes) },
            { "Language", nameof(CardInCollection.Language) },
            { "SelectedCondition", nameof(CardInCollection.SelectedCondition) },
            { "CardsForTrade", nameof(CardInCollection.CardsForTrade) }
        };
    }
}
