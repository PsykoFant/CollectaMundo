namespace CollectaMundo.DomainLogic.Shared
{
    public static class CollectionIdentityFactory
    {
        public static CollectionIdentity Create(
            string? uuid,
            string? condition,
            string? language,
            string? finish)
        {
            return new CollectionIdentity(
                uuid ?? throw new InvalidOperationException("Uuid required"),
                condition ?? throw new InvalidOperationException("Condition required"),
                language ?? throw new InvalidOperationException("Language required"),
                finish ?? throw new InvalidOperationException("Finish required"));
        }
    }
}
