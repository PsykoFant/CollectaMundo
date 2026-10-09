using CollectaMundo.DomainLogic.CardPrices;

namespace CollectaMundo.Infrastructure.CardDatabaseManagement.SqlDictionaries
{
    public static class DatabaseTableSql
    {
        private const string CardPricesTableName = "cardPrices";

        // Fixed CollectaMundo-owned tables. These are created by CollectaMundo and must survive replacement of the upstream MTGJSON tables during a database update.
        public static IReadOnlyDictionary<string, string> Statements { get; } =
            new Dictionary<string, string>
            {
                ["uniqueManaSymbols"] =
                    "CREATE TABLE IF NOT EXISTS uniqueManaSymbols (" +
                    "uniqueManaSymbol TEXT PRIMARY KEY, " +
                    "manaSymbolImage BLOB" +
                    ");",

                ["uniqueManaCostImages"] =
                    "CREATE TABLE IF NOT EXISTS uniqueManaCostImages (" +
                    "uniqueManaCost TEXT PRIMARY KEY, " +
                    "manaCostImage BLOB" +
                    ");",

                ["keyruneImages"] =
                    "CREATE TABLE IF NOT EXISTS keyruneImages (" +
                    "setCode TEXT PRIMARY KEY, " +
                    "keyruneImage BLOB, " +
                    "defaultSvgUsed BOOLEAN" +
                    ");",

                ["canonicalOracleFaces"] =
                    "CREATE TABLE IF NOT EXISTS canonicalOracleFaces (" +
                    "id INTEGER PRIMARY KEY, " +
                    "scryfallOracleId TEXT NOT NULL, " +
                    "side TEXT NOT NULL, " +
                    "name TEXT NULL, " +
                    "manaCostRaw TEXT NULL, " +
                    "manaValue REAL NULL, " +
                    "colors TEXT NULL, " +
                    "keywords TEXT NULL, " +
                    "rulesText TEXT NULL, " +
                    "superTypes TEXT NULL, " +
                    "types TEXT NULL, " +
                    "subTypes TEXT NULL, " +
                    "type TEXT NULL, " +
                    "selectedSourceUuid TEXT NOT NULL, " +
                    "sourceRowCount INTEGER NOT NULL CHECK (sourceRowCount > 0), " +
                    "winningRowCount INTEGER NOT NULL CHECK (winningRowCount > 0), " +
                    "variantCount INTEGER NOT NULL CHECK (variantCount > 0), " +
                    "isAmbiguous INTEGER NOT NULL CHECK (isAmbiguous IN (0, 1)), " +
                    "UNIQUE (scryfallOracleId, side)" +
                    ");",

                ["myCollection"] =
                    "CREATE TABLE IF NOT EXISTS myCollection (" +
                    "id INTEGER PRIMARY KEY, " +
                    "uuid TEXT NOT NULL, " +
                    "condition TEXT NOT NULL, " +
                    "finish TEXT NOT NULL, " +
                    "language TEXT NOT NULL, " +
                    "locationId INTEGER NULL, " +
                    "comment TEXT NULL, " +
                    "cardsOwned INTEGER NOT NULL CHECK (cardsOwned >= 0), " +
                    "cardsForTrade INTEGER NOT NULL CHECK (cardsForTrade >= 0), " +
                    "FOREIGN KEY (locationId) REFERENCES cardLocations(id)" +
                    ");",

                ["cardLocations"] =
                    "CREATE TABLE IF NOT EXISTS cardLocations (" +
                    "id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                    "name TEXT NOT NULL COLLATE NOCASE UNIQUE, " +
                    "type TEXT NOT NULL CHECK (type IN ('Storage', 'Deck'))" +
                    ");",

                ["myDecks"] =
                    "CREATE TABLE IF NOT EXISTS myDecks (" +
                    "locationId INTEGER PRIMARY KEY, " +
                    "format TEXT NULL, " +
                    "description TEXT NULL, " +
                    "FOREIGN KEY (locationId) REFERENCES cardLocations(id)" +
                    ");",

                ["myDeckCards"] =
                    "CREATE TABLE IF NOT EXISTS myDeckCards (" +
                    "locationId INTEGER NOT NULL, " +
                    "oracleId TEXT NOT NULL, " +
                    "cardName TEXT NOT NULL, " +
                    "desiredQuantity INTEGER NOT NULL CHECK (desiredQuantity >= 0), " +
                    "section TEXT NOT NULL CHECK (" +
                    "section IN ('Mainboard', 'Sideboard', 'Commander', 'Companion', 'Maybeboard')" +
                    "), " +
                    "PRIMARY KEY (locationId, oracleId, section), " +
                    "FOREIGN KEY (locationId) REFERENCES cardLocations(id) ON DELETE CASCADE" +
                    ");"
            };

        // Complete set of tables owned by CollectaMundo.
        //
        // Any table in the downloaded MTGJSON database which is NOT in this set is considered upstream source data and may be replaced during update.
        public static IReadOnlySet<string> CollectaMundoOwnedTableNames { get; } = new HashSet<string>(Statements.Keys.Append(CardPricesTableName), StringComparer.OrdinalIgnoreCase);

        // All CollectaMundo-owned CREATE statements.
        // cardPrices is generated dynamically because its columns depend on the configured retailer/finish definitions.
        public static IReadOnlyDictionary<string, string> GetAllStatements()
        {
            var map = new Dictionary<string, string>(Statements, StringComparer.OrdinalIgnoreCase)
            {
                [CardPricesTableName] = BuildCardPricesCreateSql()
            };

            return map;
        }

        private static string BuildCardPricesCreateSql()
        {
            var fixedColumns = new[] { "uuid TEXT UNIQUE PRIMARY KEY" };
            var finishes = CardPriceDefinitions.Finishes;
            var retailerIds = CardPriceDefinitions.RetailersByFormat.SelectMany(kvp => kvp.Value.Keys).Distinct(StringComparer.OrdinalIgnoreCase);
            var retailerColumns = retailerIds.SelectMany(retailerId => finishes.Select(finish => $"{retailerId}{finish} DECIMAL(10, 2)"));
            var allColumns = string.Join(", ", fixedColumns.Concat(retailerColumns));

            return $"CREATE TABLE IF NOT EXISTS {CardPricesTableName} " + $"({allColumns});";
        }
    }
}
