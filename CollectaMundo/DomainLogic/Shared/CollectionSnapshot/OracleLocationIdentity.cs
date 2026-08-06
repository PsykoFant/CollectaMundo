namespace CollectaMundo.DomainLogic.Shared.CollectionSnapshot
{
    public readonly record struct OracleLocationIdentity
    {
        public OracleLocationIdentity(string oracleId, int locationId)
        {
            OracleId = oracleId.ToUpperInvariant();
            LocationId = locationId;
        }
        public string OracleId { get; }
        public int LocationId { get; }
    }
}
