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
        public static readonly Dictionary<string, (string? ReadableLabel, string Property, FilterType Type, OperatorType[]? Operators, bool ShouldNotSplit)> CriteriaMappings = new()
        {
            // Single-Criteria Filters (Selection replaces existing selection, no operators)
            { "Name", ("Card Name", nameof(CardViewModel.AllCards), FilterType.Single, null, true) },
            { "SetName", ("Set Name", nameof(CardViewModel.AllCards), FilterType.Single, null, true) },
            { "Text", ("Rulestext", nameof(CardViewModel.AllCards), FilterType.Single,[OperatorType.CONTAINS, OperatorType.DOES_NOT_CONTAIN], false) },

            // Multi-Criteria Filters (Checkbox selections, OR/AND/NOT logic)
            { "Colors", ("", nameof(CardViewModel.AllCards), FilterType.Multi,[OperatorType.OR, OperatorType.AND, OperatorType.NOT], false) },
            { "Rarity", ("", nameof(CardViewModel.AllCards), FilterType.Multi,[OperatorType.OR, OperatorType.NOT], false) },
            { "SuperTypes", ("Supertypes", nameof(CardViewModel.AllCards), FilterType.Multi,[OperatorType.OR, OperatorType.AND, OperatorType.NOT], false) },
            { "Types", ("Card type", nameof(CardViewModel.AllCards), FilterType.Multi,[OperatorType.OR, OperatorType.AND, OperatorType.NOT], false) },
            { "SubTypes", ("Subtypes", nameof(CardViewModel.AllCards), FilterType.Multi,[OperatorType.OR, OperatorType.AND, OperatorType.NOT], false) },
            { "Keywords", ("", nameof(CardViewModel.AllCards), FilterType.Multi,[OperatorType.OR, OperatorType.AND, OperatorType.NOT], false) },
            { "Finishes", ("", nameof(CardViewModel.AllCards), FilterType.Multi,[OperatorType.OR, OperatorType.NOT], false) },
            { "Language", ("", nameof(CardViewModel.MyCollection), FilterType.Multi,[OperatorType.OR, OperatorType.NOT], false) },
            { "SelectedCondition", ("Condition", nameof(CardViewModel.MyCollection), FilterType.Multi,[OperatorType.OR, OperatorType.NOT], false) },

            // Numeric Filters (Greater/Less/Equal comparisons)
            { "ManaValue", ("", nameof(CardViewModel.AllCards), FilterType.Numeric,[OperatorType.GREATER_THAN, OperatorType.LESS_THAN, OperatorType.EQUALS, OperatorType.GREATER_THAN_OR_EQUALS, OperatorType.LESS_THAN_OR_EQUALS], false) },
            { "CardsForTrade", ("", nameof(CardViewModel.MyCollection), FilterType.Numeric,[OperatorType.GREATER_THAN, OperatorType.EQUALS], false) }
        };
    }
}
