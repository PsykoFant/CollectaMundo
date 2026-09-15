using CollectaMundo.ApplicationServices.Decks.Models;

namespace CollectaMundo.ViewModels.Decks.Models.RowViewModels
{
    public sealed class DeckManagementRowViewModel(DeckManagementRecord record, DeckFormatOption? formatOption)
    {
        public DeckManagementRecord Record { get; } = record;
        public int LocationId => Record.LocationId;
        public string Name => Record.Name;
        public string? Format => Record.Format;
        public string? Description => Record.Description;
        public DeckFormatOption? FormatOption { get; } = formatOption;
        public string FormatDisplayName => FormatOption?.DisplayName ?? Format ?? string.Empty;
    }
}
