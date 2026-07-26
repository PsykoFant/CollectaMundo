namespace CollectaMundo.DomainLogic.Decks.Models
{
    public sealed class DeckActionAvailability
    {
        public bool CanSetAsCommander { get; init; }
        public bool CanSetAsCompanion { get; init; }
    }
}
