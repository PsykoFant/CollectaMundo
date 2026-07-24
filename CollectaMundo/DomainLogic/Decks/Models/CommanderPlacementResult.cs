namespace CollectaMundo.DomainLogic.Decks.Models
{
    public sealed class CommanderPlacementResult
    {
        public CommanderPlacementAction Action { get; init; }
        public bool IsAllowed => Action != CommanderPlacementAction.NotAllowed;
        public string? Message { get; init; }
    }
}
