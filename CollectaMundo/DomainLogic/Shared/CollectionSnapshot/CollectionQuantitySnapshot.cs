namespace CollectaMundo.DomainLogic.Shared.CollectionSnapshot
{
    public sealed class CollectionQuantitySnapshot
        : ICollectionQuantitySnapshot
    {
        private readonly IReadOnlyDictionary<string, int> _ownedByOracleId;
        private readonly IReadOnlyDictionary<OracleLocationIdentity, int> _deckAllocatedByOracleAndLocation;
        private readonly IReadOnlyDictionary<string, int> _deckAllocatedByOracleId;
        internal CollectionQuantitySnapshot(IReadOnlyDictionary<string, int> ownedByOracleId, IReadOnlyDictionary<OracleLocationIdentity, int> deckAllocatedByOracleAndLocation, IReadOnlyDictionary<string, int> deckAllocatedByOracleId)
        {
            _ownedByOracleId = ownedByOracleId;
            _deckAllocatedByOracleAndLocation = deckAllocatedByOracleAndLocation;
            _deckAllocatedByOracleId = deckAllocatedByOracleId;
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

            return _deckAllocatedByOracleAndLocation.GetValueOrDefault(new OracleLocationIdentity(oracleId, locationId));
        }
        public int GetAvailableQuantity(string oracleId, int locationId)
        {
            var owned = GetOwnedQuantity(oracleId);

            var allocatedTotal = _deckAllocatedByOracleId.GetValueOrDefault(oracleId);

            var allocatedHere = GetAllocatedQuantity(oracleId, locationId);

            return owned - allocatedTotal + allocatedHere;
        }
    }
}
