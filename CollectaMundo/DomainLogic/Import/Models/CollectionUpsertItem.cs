namespace CollectaMundo.DomainLogic.Import.Models
{
    public sealed record CollectionUpsertItem(string Uuid, string Language, string Finish, string Condition, int CardsOwned, int CardsForTrade, int? LocationId, string? Comment);
}
