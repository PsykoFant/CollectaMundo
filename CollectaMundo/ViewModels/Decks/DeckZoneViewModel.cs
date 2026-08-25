using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.ViewModels.Decks.Models.RowViewModels;
using System.Collections.ObjectModel;

namespace CollectaMundo.ViewModels.Decks
{
    public sealed class DeckZoneViewModel
    {
        public required DeckSection Section { get; init; }
        public required string DisplayName { get; init; }

        public ObservableCollection<DeckCardEntryViewModel> Cards { get; } = [];
    }
}
