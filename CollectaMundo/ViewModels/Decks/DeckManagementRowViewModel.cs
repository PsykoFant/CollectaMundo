using CollectaMundo.ApplicationServices.Decks.Models;

namespace CollectaMundo.ViewModels.Decks
{
    public sealed class DeckManagementRowViewModel(DeckManagementRecord record, Func<string?, string> formatDisplayNameResolver)
    {
        private readonly Func<string?, string> _formatDisplayNameResolver = formatDisplayNameResolver;

        public DeckManagementRecord Record { get; } = record;

        public int LocationId => Record.LocationId;
        public string Name => Record.Name;
        public string? Format => Record.Format;
        public string? Description => Record.Description;

        public string FormatDisplayName => _formatDisplayNameResolver(Format);
    }
}
