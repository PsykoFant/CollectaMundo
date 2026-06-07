using CollectaMundo.ApplicationServices.CardLocations.Models;

namespace CollectaMundo.ViewModels.Decks
{
    public sealed class DeckManagementRowViewModel
    {
        private readonly Func<string?, string> _formatDisplayNameResolver;

        public DeckManagementRowViewModel(
            DeckManagementRecord record,
            Func<string?, string> formatDisplayNameResolver)
        {
            Record = record;
            _formatDisplayNameResolver = formatDisplayNameResolver;
        }

        public DeckManagementRecord Record { get; }

        public int LocationId => Record.LocationId;
        public string Name => Record.Name;
        public string? Format => Record.Format;
        public string? Description => Record.Description;

        public string FormatDisplayName => _formatDisplayNameResolver(Format);
    }
}
