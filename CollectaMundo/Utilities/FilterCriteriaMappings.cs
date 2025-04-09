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
        /// Maps each filter criteria to its corresponding list name,
        /// filter type, valid operators, and whether it should not split values.
        /// </summary>
        public static readonly Dictionary<string, (string? ReadableLabel, FilterType Type, OperatorType[]? Operators, bool ShouldNotSplit)> CriteriaMappings = new()
        {
            { "Name", ("Card Name", FilterType.Single, null, true) },
            { "SetName", ("Set Name", FilterType.Single, null, true) },
            { "Text", ("Rulestext", FilterType.Single, new[] { OperatorType.CONTAINS, OperatorType.DOES_NOT_CONTAIN }, false) },
            { "Colors", ("", FilterType.Multi, new[] { OperatorType.OR, OperatorType.AND, OperatorType.NOT }, false) },
            { "Rarity", ("", FilterType.Multi, new[] { OperatorType.OR, OperatorType.NOT }, false) },
            { "SuperTypes", ("Supertypes", FilterType.Multi, new[] { OperatorType.OR, OperatorType.AND, OperatorType.NOT }, false) },
            { "Types", ("Card type", FilterType.Multi, new[] { OperatorType.OR, OperatorType.AND, OperatorType.NOT }, false) },
            { "SubTypes", ("Subtypes", FilterType.Multi, new[] { OperatorType.OR, OperatorType.AND, OperatorType.NOT }, false) },
            { "Keywords", ("", FilterType.Multi, new[] { OperatorType.OR, OperatorType.AND, OperatorType.NOT }, false) },
            { "Finishes", ("", FilterType.Multi, new[] { OperatorType.OR, OperatorType.NOT }, false) },
            { "Language", ("", FilterType.Multi, new[] { OperatorType.OR, OperatorType.NOT }, false) },
            { "SelectedCondition", ("Condition", FilterType.Multi, new[] { OperatorType.OR, OperatorType.NOT }, false) },
            { "ManaValue", ("", FilterType.Numeric, new[] { OperatorType.GREATER_THAN, OperatorType.LESS_THAN, OperatorType.EQUALS, OperatorType.GREATER_THAN_OR_EQUALS, OperatorType.LESS_THAN_OR_EQUALS }, false) },
            { "CardsForTrade", ("", FilterType.Numeric, new[] { OperatorType.GREATER_THAN, OperatorType.EQUALS }, false) }
        };
    }
}
