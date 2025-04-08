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
        public static readonly Dictionary<string, (string? ReadableLabel, string ListName, FilterType Type, OperatorType[]? Operators, bool ShouldNotSplit)> CriteriaMappings = new()
        {
            { "Name", ("Card Name", "AllCards", FilterType.Single, null, true) },
            { "SetName", ("Set Name", "AllCards", FilterType.Single, null, true) },
            { "Text", ("Rulestext", "AllCards", FilterType.Single, new[] { OperatorType.CONTAINS, OperatorType.DOES_NOT_CONTAIN }, false) },
            { "Colors", ("", "AllCards", FilterType.Multi, new[] { OperatorType.OR, OperatorType.AND, OperatorType.NOT }, false) },
            { "Rarity", ("", "AllCards", FilterType.Multi, new[] { OperatorType.OR, OperatorType.NOT }, false) },
            { "SuperTypes", ("Supertypes", "AllCards", FilterType.Multi, new[] { OperatorType.OR, OperatorType.AND, OperatorType.NOT }, false) },
            { "Types", ("Card type", "AllCards", FilterType.Multi, new[] { OperatorType.OR, OperatorType.AND, OperatorType.NOT }, false) },
            { "SubTypes", ("Subtypes", "AllCards", FilterType.Multi, new[] { OperatorType.OR, OperatorType.AND, OperatorType.NOT }, false) },
            { "Keywords", ("", "AllCards", FilterType.Multi, new[] { OperatorType.OR, OperatorType.AND, OperatorType.NOT }, false) },
            { "Finishes", ("", "AllCards", FilterType.Multi, new[] { OperatorType.OR, OperatorType.NOT }, false) },
            { "Language", ("", "MyCollection", FilterType.Multi, new[] { OperatorType.OR, OperatorType.NOT }, false) },
            { "SelectedCondition", ("Condition", "MyCollection", FilterType.Multi, new[] { OperatorType.OR, OperatorType.NOT }, false) },
            { "ManaValue", ("", "AllCards", FilterType.Numeric, new[] { OperatorType.GREATER_THAN, OperatorType.LESS_THAN, OperatorType.EQUALS, OperatorType.GREATER_THAN_OR_EQUALS, OperatorType.LESS_THAN_OR_EQUALS }, false) },
            { "CardsForTrade", ("", "MyCollection", FilterType.Numeric, new[] { OperatorType.GREATER_THAN, OperatorType.EQUALS }, false) }
        };
    }
}
