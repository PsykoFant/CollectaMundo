using System.Data.SQLite;
using System.Diagnostics;

namespace CollectaMundo.Data
{
    public class DatabaseSchemaInitializer : IDatabaseSchemaInitializer
    {
        public async Task CreateTablesAsync(SQLiteConnection conn)
        {
            var tables = new Dictionary<string, string>
            {
                ["uniqueManaSymbols"] = "CREATE TABLE IF NOT EXISTS uniqueManaSymbols (uniqueManaSymbol TEXT PRIMARY KEY, manaSymbolImage BLOB);",
                ["uniqueManaCostImages"] = "CREATE TABLE IF NOT EXISTS uniqueManaCostImages (uniqueManaCost TEXT PRIMARY KEY, manaCostImage BLOB);",
                ["keyruneImages"] = "CREATE TABLE IF NOT EXISTS keyruneImages (setCode TEXT PRIMARY KEY, keyruneImage BLOB);",
                ["AggregatedCardKeywords"] = "CREATE TABLE IF NOT EXISTS AggregatedCardKeywords (uuid TEXT PRIMARY KEY, aggregatedKeywords TEXT);",
                ["myCollection"] = "CREATE TABLE IF NOT EXISTS myCollection (id INTEGER PRIMARY KEY AUTOINCREMENT, uuid TEXT, cardsOwned INTEGER, cardsForTrade INTEGER, condition TEXT, language TEXT, finish TEXT);",
                ["myDecks"] = "CREATE TABLE IF NOT EXISTS myDecks (id INTEGER PRIMARY KEY AUTOINCREMENT, deckName TEXT, deckDescription TEXT, targetFormat TEXT);",
                ["cardsInDecks"] = "CREATE TABLE IF NOT EXISTS cardsInDecks (id INTEGER PRIMARY KEY AUTOINCREMENT, deckId INTEGER, name TEXT, uuid TEXT, count INTEGER);",
                ["cardPrices"] = @"CREATE TABLE IF NOT EXISTS cardPrices (
                uuid TEXT UNIQUE PRIMARY KEY, 
                cardhoarderNormal DECIMAL(10, 2), cardhoarderFoil DECIMAL(10, 2), cardhoarderEtched DECIMAL(10, 2),
                cardkingdomNormal DECIMAL(10, 2), cardkingdomFoil DECIMAL(10, 2), cardkingdomEtched DECIMAL(10, 2),
                cardmarketNormal DECIMAL(10, 2), cardmarketFoil DECIMAL(10, 2), cardmarketEtched DECIMAL(10, 2),
                cardsphereNormal DECIMAL(10, 2), cardsphereFoil DECIMAL(10, 2), cardsphereEtched DECIMAL(10, 2),
                tcgplayerNormal DECIMAL(10, 2), tcgplayerFoil DECIMAL(10, 2), tcgplayerEtched DECIMAL(10, 2)
            );"
            };

            foreach (var (name, sql) in tables)
            {
                using var command = new SQLiteCommand(sql, conn);
                await command.ExecuteNonQueryAsync();
            }

            Debug.WriteLine("Custom tables created successfully.");
        }
    }

}
