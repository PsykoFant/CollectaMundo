using CollectaMundo.DomainLogic.CardPrices;
using CollectaMundo.Infrastructure.Shared;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CollectaMundo.Infrastructure.CardDatabaseManagement
{
    public class CardDatabaseManagementRepo : ICardDatabaseManagementRepo
    {
        // Create
        private static readonly string[] first = ["uuid TEXT UNIQUE PRIMARY KEY"];
        public async Task CreateTablesAsync(SQLiteConnection conn)
        {
            var finishes = CardPriceDefinitions.Finishes;

            // Use KEYS (canonical ids) across all formats
            var retailerIds = CardPriceDefinitions.RetailersByFormat
                .SelectMany(kvp => kvp.Value.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            // CreateCollectionChangeSetFromEdits columns like: cardmarketNormal, cardmarketFoil, cardmarketEtched, ...
            var retailerColumns = retailerIds
                .SelectMany(id => finishes.Select(f => $"{id}{f} DECIMAL(10, 2)"))
                .ToList();

            var cardPricesColumns = string.Join(", ", first.Concat(retailerColumns));
            var cardPricesSql = $"CREATE TABLE IF NOT EXISTS cardPrices ({cardPricesColumns});";

            Debug.WriteLine(cardPricesSql);


            var tables = new Dictionary<string, string>
            {
                ["uniqueManaSymbols"] = "CREATE TABLE IF NOT EXISTS uniqueManaSymbols (uniqueManaSymbol TEXT PRIMARY KEY, manaSymbolImage BLOB);",
                ["uniqueManaCostImages"] = "CREATE TABLE IF NOT EXISTS uniqueManaCostImages (uniqueManaCost TEXT PRIMARY KEY, manaCostImage BLOB);",
                ["keyruneImages"] = "CREATE TABLE IF NOT EXISTS keyruneImages (setCode TEXT PRIMARY KEY, keyruneImage BLOB, defaultSvgUsed BOOLEAN);",
                ["myCollection"] = "CREATE TABLE myCollection(id INTEGER PRIMARY KEY AUTOINCREMENT,uuid TEXT NOT NULL,language TEXT NOT NULL,finish TEXT NOT NULL,condition TEXT NOT NULL,cardsOwned INTEGER NOT NULL,cardsForTrade INTEGER NOT NULL);",
                ["myDecks"] = "CREATE TABLE IF NOT EXISTS myDecks (id INTEGER PRIMARY KEY AUTOINCREMENT, deckName TEXT, deckDescription TEXT, targetFormat TEXT);",
                ["cardsInDecks"] = "CREATE TABLE IF NOT EXISTS cardsInDecks (id INTEGER PRIMARY KEY AUTOINCREMENT, deckId INTEGER, name TEXT, uuid TEXT, count INTEGER);",
                ["cardPrices"] = cardPricesSql
            };

            foreach (var (name, sql) in tables)
            {
                using var command = new SQLiteCommand(sql, conn);
                await command.ExecuteNonQueryAsync();
            }

            Debug.WriteLine("Custom tables created successfully.");
        }
        public async Task CreateIndicesAsync(SQLiteConnection conn)
        {
            var indices = new Dictionary<string, string>
            {
                {"idx_uniquemanasymbols_uniquemanasymbol", "CREATE INDEX IF NOT EXISTS idx_uniquemanasymbols_uniquemanasymbol ON uniqueManaSymbols(uniqueManaSymbol);"},
                {"idx_uniquemanaCostimages_uniquemanaCost", "CREATE INDEX IF NOT EXISTS idx_uniquemanaCostimages_uniquemanaCost ON uniqueManaCostImages(uniqueManaCost);"},
                {"idx_keyruneimages_setcode", "CREATE INDEX IF NOT EXISTS idx_keyruneimages_setcode ON keyruneImages(setCode);"},
                {"idx_cardprices_uuid", "CREATE INDEX IF NOT EXISTS idx_cardprices_uuid ON cardPrices(uuid);"},
                {"idx_cardidentifiers_uuid", "CREATE INDEX IF NOT EXISTS idx_cardidentifiers_uuid ON cardIdentifiers(uuid);"},
                {"idx_cardforeigndata_uuid", "CREATE INDEX IF NOT EXISTS idx_cardforeigndata_uuid ON cardForeignData(uuid);"},
                {"idx_cardlegalities_uuid", "CREATE INDEX IF NOT EXISTS idx_cardlegalities_uuid ON cardLegalities(uuid);"},
                {"idx_cards_uuid", "CREATE INDEX IF NOT EXISTS idx_cards_uuid ON cards(uuid);"},
                {"idx_cards_setcode_name", "CREATE INDEX IF NOT EXISTS idx_cards_setcode_name ON cards(setCode, name);"},
                {"idx_tokens_setcode_name", "CREATE INDEX IF NOT EXISTS idx_tokens_setcode_name ON tokens(setCode, name);"},
                {"idx_cards_keywords", "CREATE INDEX IF NOT EXISTS idx_cards_keywords ON cards(keywords);"},
                {"idx_sets_tokenSetcode", "CREATE INDEX IF NOT EXISTS idx_sets_tokenSetcode ON sets(tokenSetCode);"},
                {"idx_tokenidentifiers_uuid", "CREATE INDEX IF NOT EXISTS idx_tokenidentifiers_uuid ON tokenIdentifiers(uuid);"},
                {"idx_tokens_uuid", "CREATE INDEX IF NOT EXISTS idx_tokens_uuid ON tokens(uuid);"},
                {"idx_tokens_name", "CREATE INDEX IF NOT EXISTS idx_tokens_name ON tokens(name);"},
                {"idx_tokens_facename", "CREATE INDEX IF NOT EXISTS idx_tokens_facename ON tokens(faceName);"},
                {"idx_mycollection_uuid", "CREATE INDEX IF NOT EXISTS idx_mycollection_uuid ON myCollection(uuid);"},
                {"idx_cards_side_uuid", "CREATE INDEX IF NOT EXISTS idx_cards_side_uuid ON cards(side, uuid);"},
                {"idx_tokens_side_uuid", "CREATE INDEX IF NOT EXISTS idx_tokens_side_uuid ON tokens(side, uuid);"},
                {"idx_sets_code_tokensetcode", "CREATE INDEX IF NOT EXISTS idx_sets_code_tokensetcode ON sets(code, tokenSetCode);"},
                {"idx_cards_setcode_name_type", "CREATE INDEX IF NOT EXISTS idx_cards_setcode_name_type ON cards(setCode, name, type);"},
                {"idx_tokens_setcode_name_type", "CREATE INDEX IF NOT EXISTS idx_tokens_setcode_name_type ON tokens(setCode, name, type);"},
                {"ux_myCollection_identity", "CREATE UNIQUE INDEX ux_myCollection_identity ON myCollection (uuid, language, finish, condition);"}
            };

            foreach (var (_, sql) in indices)
            {
                using var command = new SQLiteCommand(sql, conn);
                await command.ExecuteNonQueryAsync();
            }

            Debug.WriteLine("Indices created successfully.");
        }
        public async Task CreateViewsAsync(SQLiteConnection conn, string retailer)
        {
            string createCardTokenViewQuery = @"
                CREATE VIEW IF NOT EXISTS view_cardToken AS
                SELECT 
                    c.uuid,
                    c.name,
                    s.name AS setName,
                    c.setCode,
                    NULL AS tokenSetCode,
                    NULL AS faceName
                FROM 
                    cards c
                JOIN 
                    sets s ON c.setCode = s.code
                WHERE 
                    c.side IS NULL OR c.side = 'a'
                UNION ALL
                SELECT 
                    t.uuid,
                    t.name,
                    s.name AS setName,
                    s.code AS setCode,
                    s.tokenSetCode,
                    t.faceName
                FROM 
                    tokens t
                JOIN 
                    sets s ON t.setCode = s.tokenSetCode
                WHERE 
                    t.side IS NULL OR t.side = 'a';
            ";
            string createAllCardsForDecksViewQuery = $@"
                CREATE VIEW IF NOT EXISTS view_allCardsForDecks AS
                SELECT * FROM (
                    SELECT 
                        DISTINCT c.name AS Name, 
                        c.manaCost AS ManaCost, 
                        u.manaCostImage AS ManaCostImage, 
                        c.types AS Types, 
                        CAST(COALESCE(ccol.AggregatedColors, c.colors) AS TEXT) AS Colors,
                        c.supertypes AS SuperTypes, 
                        c.subtypes AS SubTypes, 
                        c.type AS Type, 
                        CAST(COALESCE(cg.AggregatedKeywords, c.keywords) AS TEXT) AS Keywords,
                        c.text AS RulesText, 
                        c.manaValue AS ManaValue, 
                        c.side AS Side
                    FROM cards c
                    JOIN sets s ON c.setCode = s.code
                    LEFT JOIN uniqueManaCostImages u ON c.manaCost = u.uniqueManaCost
                    LEFT JOIN (
                        SELECT cc.Name, GROUP_CONCAT(cc.keywords, ', ') AS AggregatedKeywords
                        FROM cards cc GROUP BY cc.Name
                    ) cg ON c.Name = cg.Name
                    LEFT JOIN (
                        SELECT cc.Name, REPLACE(GROUP_CONCAT(DISTINCT cc.colors), ' ', '') AS AggregatedColors
                        FROM cards cc GROUP BY cc.Name
                    ) ccol ON c.Name = ccol.Name
                    WHERE c.side IS NULL OR c.side = 'a'
                ) 
                ORDER BY Types,
                    CASE Colors
                        WHEN 'W' THEN 1
                        WHEN 'U' THEN 2
                        WHEN 'B' THEN 3
                        WHEN 'R' THEN 4
                        WHEN 'G' THEN 5
                        ELSE 7
                    END;
            ";
            string createCardsInDecksViewQuery = @"
                CREATE VIEW IF NOT EXISTS view_cardsInDecks AS
                SELECT 
                    cardsInDecks.id AS CardId,
                    cardsInDecks.name AS Name,
                    cardsInDecks.deckId AS DeckId,
                    cardsInDecks.uuid AS Uuid,
                    cardsInDecks.count AS Count,
                    c.manaCost AS ManaCost,
                    c.colors AS Colors,
                    c.manaValue AS ManaValue, 
                    u.manaCostImage AS ManaCostImage, 
                    c.type AS Type
                FROM 
                    cardsInDecks
                LEFT JOIN (
                    SELECT name, colors, manaCost, manaValue, type
                    FROM cards
                    GROUP BY name
                ) c ON cardsInDecks.name = c.name
                LEFT JOIN uniqueManaCostImages u ON c.manaCost = u.uniqueManaCost;
            ";

            var viewSqls = new[]
            {
                createCardTokenViewQuery,
                createAllCardsForDecksViewQuery,
                createCardsInDecksViewQuery,
            };

            foreach (var sql in viewSqls)
            {
                using var cmd = new SQLiteCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();
            }

            Debug.WriteLine("Views created successfully.");
        }
        public async Task OptimizeAsync(SQLiteConnection conn)
        {
            var commands = new[]
            {
                "VACUUM;",
                "ANALYZE;",
                "PRAGMA optimize;"
            };

            foreach (var cmdText in commands)
            {
                using var command = new SQLiteCommand(cmdText, conn);
                await command.ExecuteNonQueryAsync();
            }
            Debug.WriteLine("Database optimization completed.");
        }


        // Update
        public async Task<int> GetNumberOfSetsAsync(SQLiteConnection conn, CancellationToken ct = default)
        {
            var sets = await DbHelpers.GetUniqueValuesAsync(conn, "sets", "code", ct);
            return sets.Count;
        }
        public async Task AttachTempDbAsync(SQLiteConnection conn, string newDbPath, IProgress<string> progress)
        {
            var attachSql = $"ATTACH DATABASE '{newDbPath}' AS tempDb;";
            await new SQLiteCommand(attachSql, conn).ExecuteNonQueryAsync();
            progress.Report("Attached temp DB.");
        }
        public async Task DropTablesAsync(SQLiteConnection conn, IProgress<string> progress)
        {
            var tables = TablesToCopy;

            Debug.WriteLine("Dropping old tables...");
            foreach (var item in tables)
            {
                using var dropCommand = new SQLiteCommand(item.Value, conn);
                await dropCommand.ExecuteNonQueryAsync();
                progress.Report($"Dropped {item.Key}");
            }
        }
        public async Task CopyTablesAsync(SQLiteConnection conn, IProgress<string> progress)
        {
            var tables = TablesToCopy;

            Debug.WriteLine("Copying tables...");
            foreach (var item in tables)
            {
                var copySql = $"CREATE TABLE {item.Key} AS SELECT * FROM tempDb.{item.Key};";
                using var copyCommand = new SQLiteCommand(copySql, conn);
                await copyCommand.ExecuteNonQueryAsync();
                progress.Report($"Copied {item.Key}");
                Debug.WriteLine($"Copied {item.Key}");
            }

            progress.Report("Copy complete...");

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        public async Task DetachTempDbAsync(SQLiteConnection conn, IProgress<string> progress)
        {
            Debug.WriteLine("Detaching new DB...");
            var detachSql = "DETACH DATABASE tempDb;";
            await new SQLiteCommand(detachSql, conn).ExecuteNonQueryAsync();
            progress.Report("Detached temp DB.");
        }

        // Export
        public async Task<string?> ExportCollectionAsync(SQLiteConnection conn, string backupFolderPath, CancellationToken ct = default)
        {
            Directory.CreateDirectory(backupFolderPath);

            using var command = new SQLiteCommand("SELECT * FROM myCollection", conn);
            using var reader = await command.ExecuteReaderAsync(ct);

            if (!reader.HasRows)
            {
                return null;
            }

            string filePath = Path.Combine(backupFolderPath, $"MyCollection_backup_{DateTime.Now:yyyyMMdd}.csv");
            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

            // Write header
            for (int i = 0; i < reader.FieldCount; i++)
            {
                ct.ThrowIfCancellationRequested();

                writer.Write(reader.GetName(i));
                if (i < reader.FieldCount - 1)
                {
                    writer.Write(";");
                }
            }
            writer.WriteLine();

            // Write rows
            while (await reader.ReadAsync(ct))
            {
                ct.ThrowIfCancellationRequested();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string value = reader[i]?.ToString()?.Replace(";", ",") ?? string.Empty;
                    writer.Write(value);
                    if (i < reader.FieldCount - 1)
                    {
                        writer.Write(";");
                    }
                }
                writer.WriteLine();
            }

            return filePath;
        }

        // Helper
        private static readonly Dictionary<string, string> TablesToCopy = new()
            {
                {"cardForeignData", "DROP TABLE IF EXISTS cardForeignData;" },
                {"cardIdentifiers", "DROP TABLE IF EXISTS cardIdentifiers;" },
                {"cardLegalities", "DROP TABLE IF EXISTS cardLegalities;" },
                {"cardPurchaseUrls", "DROP TABLE IF EXISTS cardPurchaseUrls;" },
                {"cardRulings", "DROP TABLE IF EXISTS cardRulings;" },
                {"cards", "DROP TABLE IF EXISTS cards;" },
                {"meta", "DROP TABLE IF EXISTS meta;" },
                {"setBoosterContentWeights", "DROP TABLE IF EXISTS setBoosterContentWeights;" },
                {"setBoosterContents", "DROP TABLE IF EXISTS setBoosterContents;" },
                {"setBoosterSheetCards", "DROP TABLE IF EXISTS setBoosterSheetCards;" },
                {"setBoosterSheets", "DROP TABLE IF EXISTS setBoosterSheets;" },
                {"setTranslations", "DROP TABLE IF EXISTS setTranslations;" },
                {"sets", "DROP TABLE IF EXISTS sets;" },
                {"tokenIdentifiers", "DROP TABLE IF EXISTS tokenIdentifiers;" },
                {"tokens", "DROP TABLE IF EXISTS tokens;" },
            };

    }
}
