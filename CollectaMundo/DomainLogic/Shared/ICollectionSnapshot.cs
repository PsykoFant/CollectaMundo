namespace CollectaMundo.DomainLogic.Shared
{
    public interface ICollectionSnapshot
    {
        bool TryGetById(int cardId, out MyCollectionRow row);
        bool TryGetByIdentity(CollectionIdentity identity, out MyCollectionRow row);
        IReadOnlyCollection<MyCollectionRow> Rows { get; }
    }

}
