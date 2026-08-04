namespace CollectaMundo.DomainLogic.Shared.CollectionSnapshot
{
    public interface ICollectionQuantitySnapshot
    {
        int GetOwnedQuantity(string oracleId);
        int GetAllocatedQuantity(string oracleId, int locationId);
    }
}
