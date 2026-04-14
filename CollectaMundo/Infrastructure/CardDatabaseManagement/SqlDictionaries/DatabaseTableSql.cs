using CollectaMundo.DomainLogic.CardPrices;

namespace CollectaMundo.Infrastructure.CardDatabaseManagement.SqlDictionaries
{
    public static class DatabaseTableSql
    {
        // Fixed table create statements shared by prod and test
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
                    ");"
            };

        // Dynamic because price columns depend on retailer/finish definitions
        public static string BuildCardPricesCreateSql()
        {
            var first = new[]
            {
                "uuid TEXT UNIQUE PRIMARY KEY"
            };

            var finishes = CardPriceDefinitions.Finishes;

            var retailerIds = CardPriceDefinitions.RetailersByFormat
                .SelectMany(kvp => kvp.Value.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            var retailerColumns = retailerIds
                .SelectMany(id => finishes.Select(f => $"{id}{f} DECIMAL(10, 2)"))
                .ToList();

            var allColumns = string.Join(", ", first.Concat(retailerColumns));

            return $"CREATE TABLE IF NOT EXISTS cardPrices ({allColumns});";
        }

        // Combined ordered set for callers that want everything in one pass
        public static IReadOnlyDictionary<string, string> GetAllStatements()
        {
            var map = new Dictionary<string, string>(Statements)
            {
                ["cardPrices"] = BuildCardPricesCreateSql()
            };

            return map;
        }
    }
}
