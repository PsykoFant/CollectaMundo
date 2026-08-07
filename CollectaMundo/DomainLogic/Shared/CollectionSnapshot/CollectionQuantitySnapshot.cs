namespace CollectaMundo.DomainLogic.Shared.CollectionSnapshot
{
    public sealed class CollectionQuantitySnapshot
        : ICollectionQuantitySnapshot
    {
        private readonly IReadOnlyDictionary<string, int> _ownedByOracleId;
        private readonly IReadOnlyDictionary<OracleLocationIdentity, int> _allocatedByOracleAndLocation;
        private readonly IReadOnlyDictionary<string, int> _allocatedByOracleId;
        internal CollectionQuantitySnapshot(IReadOnlyDictionary<string, int> ownedByOracleId, IReadOnlyDictionary<OracleLocationIdentity, int> allocatedByOracleAndLocation, IReadOnlyDictionary<string, int> allocatedByOracleId)
        {
            _ownedByOracleId = ownedByOracleId;
            _allocatedByOracleAndLocation = allocatedByOracleAndLocation;
            _allocatedByOracleId = allocatedByOracleId;
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
        public int GetAvailableQuantity(string oracleId, int locationId)
        {
            var owned = GetOwnedQuantity(oracleId);

            var allocatedTotal = _allocatedByOracleId.GetValueOrDefault(oracleId);

            var allocatedHere = GetAllocatedQuantity(oracleId, locationId);

            return owned - allocatedTotal + allocatedHere;
        }
    }
}
