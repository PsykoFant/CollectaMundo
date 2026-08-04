namespace CollectaMundo.DomainLogic.Shared.CollectionSnapshot
{
    public sealed class CollectionQuantitySnapshot
        : ICollectionQuantitySnapshot
    {
        private readonly IReadOnlyDictionary<string, int> _ownedByOracleId;
        private readonly IReadOnlyDictionary<OracleLocationIdentity, int> _allocatedByOracleAndLocation;

        internal CollectionQuantitySnapshot(IReadOnlyDictionary<string, int> ownedByOracleId, IReadOnlyDictionary<OracleLocationIdentity, int> allocatedByOracleAndLocation)
        {
            _ownedByOracleId = ownedByOracleId;
            _allocatedByOracleAndLocation = allocatedByOracleAndLocation;
        }
        public int GetOwnedQuantity(string oracleId)
        {
            if (string.IsNullOrWhiteSpace(oracleId))
            {
                return 0;
            }

            return _ownedByOracleId.GetValueOrDefault(oracleId);
        }

        public int GetAllocatedQuantity(string oracleId, int locationId)
        {
            if (string.IsNullOrWhiteSpace(oracleId))
            {
                return 0;
            }

            return _allocatedByOracleAndLocation.GetValueOrDefault(new OracleLocationIdentity(oracleId, locationId));
        }
    }
}
