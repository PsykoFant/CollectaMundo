using CollectaMundo.Models;
using static CollectaMundo.MainWindow;

namespace CollectaMundo.Utilities
{
    public static class FilterCriteriaMappings
    {
        public static readonly Dictionary<string, (string Property, OperatorType[] Operators, bool ShouldNotSplit)> CriteriaMappings = new()
    {
        { "Name", (nameof(CardViewModel.allCards), [OperatorType.OR, OperatorType.NOT], true) },
        { "SetName", (nameof(CardViewModel.allCards), [OperatorType.OR, OperatorType.NOT], true) },
        { "Colors", (nameof(CardViewModel.allCards), [OperatorType.OR, OperatorType.AND, OperatorType.NOT], false) },
        { "ManaValue", (nameof(CardViewModel.allCards), [OperatorType.GREATER_THAN, OperatorType.LESS_THAN, OperatorType.EQUALS, OperatorType.GREATER_THAN_OR_EQUALS, OperatorType.LESS_THAN_OR_EQUALS], false) },
        { "Rarity", (nameof(CardViewModel.allCards), [OperatorType.OR, OperatorType.NOT], false) },
        { "SuperTypes", (nameof(CardViewModel.allCards), [OperatorType.OR, OperatorType.AND, OperatorType.NOT], false) },
        { "Types", (nameof(CardViewModel.allCards), [OperatorType.OR, OperatorType.AND, OperatorType.NOT], false) },
        { "SubTypes", (nameof(CardViewModel.allCards), [OperatorType.OR, OperatorType.AND, OperatorType.NOT], false) },
        { "Keywords", (nameof(CardViewModel.allCards), [OperatorType.OR, OperatorType.AND, OperatorType.NOT], false) },
        { "Text", (nameof(CardViewModel.allCards), [OperatorType.CONTAINS, OperatorType.DOES_NOT_CONTAIN], false) },
        { "Finishes", (nameof(CardViewModel.allCards), [OperatorType.OR, OperatorType.NOT], false) },
        { "Language", (nameof(CardViewModel.myCards), [OperatorType.OR, OperatorType.NOT], false) },
        { "SelectedCondition", (nameof(CardViewModel.myCards), [OperatorType.OR, OperatorType.NOT], false) },
        { "CardsForTrade", (nameof(CardViewModel.myCards), [OperatorType.GREATER_THAN, OperatorType.LESS_THAN, OperatorType.EQUALS], false) }
    };
    }

}
