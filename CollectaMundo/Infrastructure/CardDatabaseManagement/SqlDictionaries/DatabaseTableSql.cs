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
                    ");",

                ["myDecks"] =
                    "CREATE TABLE myDecks ( " +
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
                    "section TEXT NOT NULL CHECK (section IN ('Mainboard', 'Sideboard', 'Commander', 'Companion', 'Maybeboard')), " +
                    "PRIMARY KEY (locationId, oracleId, section), " +
                    "FOREIGN KEY (locationId) REFERENCES cardLocations(id) ON DELETE CASCADE" +
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
