using CollectaMundo.Models;

namespace CollectaMundo.Utilities
{
    public static class FilterCriteriaMappings
    {
        public static readonly Dictionary<string, string> CriteriaKeyToPropertyMap = new()
        {
            { "Name", nameof(CardViewModel.allCards) },
            { "SetName", nameof(CardViewModel.allCards) },
            { "Colors", nameof(CardViewModel.allCards) },
            { "ManaValue", nameof(CardViewModel.allCards) },
            { "Rarity", nameof(CardViewModel.allCards) },
            { "SuperTypes", nameof(CardViewModel.allCards) },
            { "Types", nameof(CardViewModel.allCards) },
            { "SubTypes", nameof(CardViewModel.allCards) },
            { "Keywords", nameof(CardViewModel.allCards) },
            { "Text", nameof(CardViewModel.allCards) },
            { "Finishes", nameof(CardViewModel.allCards) },
            { "Language", nameof(CardViewModel.myCards) },
            { "SelectedCondition", nameof(CardViewModel.myCards) },
            { "CardsForTrade", nameof(CardViewModel.myCards) }
        };
    }
}
