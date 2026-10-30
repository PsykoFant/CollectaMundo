namespace CollectaMundo.Infrastructure.CardDatabaseManagement.SqlDictionaries
{
    public static class DatabaseIndexSql
    {
        // Additional indexes managed by CollectaMundo. 
        // Primary-key, unique-constraint and upstream MTGJSON indexes are defined or created elsewhere.
        public static IReadOnlyDictionary<string, string> Statements { get; } = new Dictionary<string, string>
        {
            { "idx_myCollection_locationId", "CREATE INDEX IF NOT EXISTS idx_myCollection_locationId ON myCollection(locationId);"},
            { "idx_cardIdentifiers_mcmId", "CREATE INDEX IF NOT EXISTS idx_cardIdentifiers_mcmId ON cardIdentifiers(mcmId);" },
            { "idx_tokenIdentifiers_mcmId", "CREATE INDEX IF NOT EXISTS idx_tokenIdentifiers_mcmId ON tokenIdentifiers(mcmId);" },
            { "idx_cardIdentifiers_scryfallId", "CREATE INDEX IF NOT EXISTS idx_cardIdentifiers_scryfallId ON cardIdentifiers(scryfallId);" },
            { "idx_tokenIdentifiers_scryfallId", "CREATE INDEX IF NOT EXISTS idx_tokenIdentifiers_scryfallId ON tokenIdentifiers(scryfallId);" },
            { "ux_myCollection_identity", "CREATE UNIQUE INDEX IF NOT EXISTS ux_myCollection_identity ON myCollection (uuid, language, finish, condition, COALESCE(locationId, -1), COALESCE(comment, ''));" }
        };
    }
}
