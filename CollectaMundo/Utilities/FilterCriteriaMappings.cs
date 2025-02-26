using CollectaMundo.Models;
using static CollectaMundo.MainWindow;

namespace CollectaMundo.Utilities
{
    /// <summary>
    /// Defines the type of filtering applicable to a criteria.
    /// </summary>
    public enum FilterType
    {
        Single,  // A single-selection filter (e.g., Name, SetName)
        Multi,   // A multi-selection filter (e.g., Keywords, Colors, Types)
        Numeric  // A numeric-based filter (e.g., ManaValue, CardsForTrade)
    }

    public static class FilterCriteriaMappings
    {
        /// <summary>
        /// Maps each filter criteria to its corresponding property, filter type, valid operators, and whether it should not split values.
        /// </summary>
        public static readonly Dictionary<string, (string Property, FilterType Type, OperatorType[]? Operators, bool ShouldNotSplit)> CriteriaMappings = new()
        {
            // Single-Criteria Filters (Selection replaces existing selection, no operators)
            { "Name", (nameof(CardViewModel.allCards), FilterType.Single, null, true) },
            { "SetName", (nameof(CardViewModel.allCards), FilterType.Single, null, true) },
            { "Text", (nameof(CardViewModel.allCards), FilterType.Single, [OperatorType.CONTAINS, OperatorType.DOES_NOT_CONTAIN], false) },

            // Multi-Criteria Filters (Checkbox selections, OR/AND/NOT logic)
            { "Colors", (nameof(CardViewModel.allCards), FilterType.Multi, [OperatorType.OR, OperatorType.AND, OperatorType.NOT], false) },
            { "Rarity", (nameof(CardViewModel.allCards), FilterType.Multi, [OperatorType.OR, OperatorType.NOT], false) },
            { "SuperTypes", (nameof(CardViewModel.allCards), FilterType.Multi, [OperatorType.OR, OperatorType.AND, OperatorType.NOT], false) },
            { "Types", (nameof(CardViewModel.allCards), FilterType.Multi, [OperatorType.OR, OperatorType.AND, OperatorType.NOT], false) },
            { "SubTypes", (nameof(CardViewModel.allCards), FilterType.Multi, [OperatorType.OR, OperatorType.AND, OperatorType.NOT], false) },
            { "Keywords", (nameof(CardViewModel.allCards), FilterType.Multi, [OperatorType.OR, OperatorType.AND, OperatorType.NOT], false) },
            { "Finishes", (nameof(CardViewModel.allCards), FilterType.Multi, [OperatorType.OR, OperatorType.NOT], false) },
            { "Language", (nameof(CardViewModel.myCards), FilterType.Multi, [OperatorType.OR, OperatorType.NOT], false) },
            { "SelectedCondition", (nameof(CardViewModel.myCards), FilterType.Multi, [OperatorType.OR, OperatorType.NOT], false) },

            // Numeric Filters (Greater/Less/Equal comparisons)
            { "ManaValue", (nameof(CardViewModel.allCards), FilterType.Numeric, [OperatorType.GREATER_THAN, OperatorType.LESS_THAN, OperatorType.EQUALS, OperatorType.GREATER_THAN_OR_EQUALS, OperatorType.LESS_THAN_OR_EQUALS], false) },
            { "CardsForTrade", (nameof(CardViewModel.myCards), FilterType.Numeric, [OperatorType.GREATER_THAN, OperatorType.LESS_THAN, OperatorType.EQUALS], false) }
        };
    }
}
