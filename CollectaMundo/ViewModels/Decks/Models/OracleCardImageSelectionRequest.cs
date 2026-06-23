namespace CollectaMundo.ViewModels.Decks.Models
{
    public sealed record OracleCardImageSelectionRequest(
    string? Uuid = null,
    string? OracleId = null,
    string? Name = null);
}
