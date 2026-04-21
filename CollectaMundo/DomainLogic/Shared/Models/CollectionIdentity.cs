namespace CollectaMundo.DomainLogic.Shared.Models
{
    public sealed record CollectionIdentity(string Uuid, string Condition, string Language, string Finish, int? LocationId, string? Comment);
}
