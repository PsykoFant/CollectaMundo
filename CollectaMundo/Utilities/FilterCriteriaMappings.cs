using CollectaMundo.Models;
using static CollectaMundo.MainWindow;

namespace CollectaMundo.Utilities
{
    public static class FilterCriteriaMappings
    {
        public static readonly Dictionary<string, (string Property, OperatorType[] Operators)> CriteriaMappings = new()
        {
            { "Name", (nameof(CardViewModel.allCards), [OperatorType.OR, OperatorType.NOT]) },
            { "SetName", (nameof(CardViewModel.allCards), [OperatorType.OR, OperatorType.NOT]) },
            { "Colors", (nameof(CardViewModel.allCards), [OperatorType.OR, OperatorType.AND, OperatorType.NOT]) },
            { "ManaValue", (nameof(CardViewModel.allCards), [OperatorType.GREATER_THAN, OperatorType.LESS_THAN, OperatorType.EQUALS, OperatorType.GREATER_THAN_OR_EQUALS, OperatorType.LESS_THAN_OR_EQUALS]) },
            { "Rarity", (nameof(CardViewModel.allCards), [OperatorType.OR, OperatorType.NOT]) },
            { "SuperTypes", (nameof(CardViewModel.allCards), [OperatorType.OR, OperatorType.AND, OperatorType.NOT]) },
            { "Types", (nameof(CardViewModel.allCards), [OperatorType.OR, OperatorType.AND, OperatorType.NOT]) },
            { "SubTypes", (nameof(CardViewModel.allCards), [OperatorType.OR, OperatorType.AND, OperatorType.NOT]) },
            { "Keywords", (nameof(CardViewModel.allCards), [OperatorType.OR, OperatorType.AND, OperatorType.NOT]) },
            { "Text", (nameof(CardViewModel.allCards), [OperatorType.CONTAINS, OperatorType.DOES_NOT_CONTAIN]) },
            { "Finishes", (nameof(CardViewModel.allCards), [OperatorType.OR, OperatorType.NOT]) },
            { "Language", (nameof(CardViewModel.myCards), [OperatorType.OR, OperatorType.NOT]) },
            { "SelectedCondition", (nameof(CardViewModel.myCards), [OperatorType.OR, OperatorType.NOT]) },
            { "CardsForTrade", (nameof(CardViewModel.myCards), [OperatorType.GREATER_THAN, OperatorType.LESS_THAN, OperatorType.EQUALS]) }
        };
    }
}
