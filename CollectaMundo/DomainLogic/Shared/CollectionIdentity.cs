namespace CollectaMundo.DomainLogic.Shared
{
    public sealed record CollectionIdentity(
        string Uuid,
        string Condition,
        string Language,
        string Finish);
}
