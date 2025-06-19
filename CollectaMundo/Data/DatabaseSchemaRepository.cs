using CollectaMundo.DomainLogic.CardPrices;
using System.Data.SQLite;
using System.Diagnostics;

namespace CollectaMundo.Data
{
    public class DatabaseSchemaRepository : IDatabaseSchemaRepository
    {
        private static readonly string[] first = ["uuid TEXT UNIQUE PRIMARY KEY"];

        public async Task CreateTablesAsync(SQLiteConnection conn)
        {
            var finishes = CardPriceDefinitions.Finishes;
            var retailerColumns = CardPriceDefinitions.RetailersByFormat
                .SelectMany(kvp => kvp.Value.SelectMany(r => finishes.Select(f => $"{r}{f} DECIMAL(10, 2)")))
                .ToList();

            var cardPricesColumns = string.Join(", ", first.Concat(retailerColumns));
            var cardPricesSql = $"CREATE TABLE IF NOT EXISTS cardPrices ({cardPricesColumns});";

            var tables = new Dictionary<string, string>
            {
                ["uniqueManaSymbols"] = "CREATE TABLE IF NOT EXISTS uniqueManaSymbols (uniqueManaSymbol TEXT PRIMARY KEY, manaSymbolImage BLOB);",
                ["uniqueManaCostImages"] = "CREATE TABLE IF NOT EXISTS uniqueManaCostImages (uniqueManaCost TEXT PRIMARY KEY, manaCostImage BLOB);",
                ["keyruneImages"] = "CREATE TABLE IF NOT EXISTS keyruneImages (setCode TEXT PRIMARY KEY, keyruneImage BLOB);",
                ["AggregatedCardKeywords"] = "CREATE TABLE IF NOT EXISTS AggregatedCardKeywords (uuid TEXT PRIMARY KEY, aggregatedKeywords TEXT);",
                ["myCollection"] = "CREATE TABLE IF NOT EXISTS myCollection (id INTEGER PRIMARY KEY AUTOINCREMENT, uuid TEXT, cardsOwned INTEGER, cardsForTrade INTEGER, condition TEXT, language TEXT, finish TEXT);",
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
                {"idx_tokens_setcode_name_type", "CREATE INDEX IF NOT EXISTS idx_tokens_setcode_name_type ON tokens(setCode, name, type);"}
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
            string normalPriceColumn = $"p.{retailer}Normal AS NormalPrice";
            string foilPriceColumn = $"p.{retailer}Foil AS FoilPrice";
            string etchedPriceColumn = $"p.{retailer}Etched AS EtchedPrice";

            const string dropAllCardsViewQuery = "DROP VIEW IF EXISTS view_allCards;";
            const string dropMyCollectionViewQuery = "DROP VIEW IF EXISTS view_myCollection;";

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
            string createAllCardsViewQuery = $@"
                CREATE VIEW IF NOT EXISTS view_allCards AS
                SELECT * FROM (
                    SELECT 
                        c.name AS Name, 
                        s.name AS SetName, 
                        s.releaseDate AS ReleaseDate,
                        k.keyruneImage AS KeyRuneImage, 
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
                        c.language AS Language,
                        c.uuid AS Uuid, 
                        c.finishes AS Finishes, 
                        c.side AS Side,
                        c.rarity AS Rarity,
                        {normalPriceColumn},
                        {foilPriceColumn},
                        {etchedPriceColumn}
                    FROM cards c
                    JOIN sets s ON c.setCode = s.code
                    LEFT JOIN keyruneImages k ON c.setCode = k.setCode
                    LEFT JOIN uniqueManaCostImages u ON c.manaCost = u.uniqueManaCost
                    LEFT JOIN cardPrices p ON c.uuid = p.uuid
                    LEFT JOIN (
                        SELECT cc.Name, REPLACE(GROUP_CONCAT(DISTINCT cc.keywords), ',', ',') AS AggregatedKeywords
                        FROM cards cc GROUP BY cc.Name
                    ) cg ON c.Name = cg.Name
                    LEFT JOIN (
                        SELECT cc.Name, REPLACE(GROUP_CONCAT(DISTINCT cc.colors), ' ', '') AS AggregatedColors
                        FROM cards cc GROUP BY cc.Name
                    ) ccol ON c.Name = ccol.Name
                    WHERE c.side IS NULL OR c.side = 'a'

                    UNION ALL

                    SELECT 
                        t.name AS Name, 
                        s.name AS SetName, 
                        s.releaseDate AS ReleaseDate,
                        k.keyruneImage AS KeyRuneImage, 
                        t.manaCost AS ManaCost, 
                        u.manaCostImage AS ManaCostImage, 
                        t.types AS Types, 
                        t.colors AS Colors,
                        t.supertypes AS SuperTypes, 
                        t.subtypes AS SubTypes, 
                        t.type AS Type, 
                        t.keywords AS Keywords, 
                        t.text AS RulesText, 
                        NULL AS ManaValue, 
                        t.language AS Language,
                        t.uuid AS Uuid, 
                        t.finishes AS Finishes, 
                        t.side AS Side,
                        NULL AS Rarity,
                        {normalPriceColumn},
                        {foilPriceColumn},
                        {etchedPriceColumn}
                    FROM tokens t 
                    JOIN sets s ON t.setCode = s.tokenSetCode 
                    LEFT JOIN keyruneImages k ON t.setCode = k.setCode
                    LEFT JOIN uniqueManaCostImages u ON t.manaCost = u.uniqueManaCost
                    LEFT JOIN cardPrices p ON t.uuid = p.uuid
                    WHERE t.side IS NULL OR t.side = 'a'
                ) 
                ORDER BY ReleaseDate DESC, SetName, Types,
                    CASE Colors
                        WHEN 'W' THEN 1
                        WHEN 'U' THEN 2
                        WHEN 'B' THEN 3
                        WHEN 'R' THEN 4
                        WHEN 'G' THEN 5
                        ELSE 7
                    END;
            ";
            string createMyCollectionViewQuery = $@"
                CREATE VIEW IF NOT EXISTS view_myCollection AS
                SELECT * FROM (
                    SELECT                        
                        c.name AS Name,
                        s.name AS SetName,
                        s.releaseDate AS ReleaseDate,
                        k.keyruneImage AS KeyRuneImage,
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
                        c.finishes AS Finishes,
                        c.uuid AS Uuid,
                        m.id AS CardId,
                        m.cardsOwned AS CardsOwned,
                        m.cardsForTrade AS CardsForTrade,
                        m.condition AS Condition,
                        m.language AS Language,
                        m.finish AS Finish,
                        c.side AS Side,
                        c.rarity AS Rarity,
                        {normalPriceColumn},
                        {foilPriceColumn},
                        {etchedPriceColumn}
                    FROM myCollection m
                    JOIN cards c ON m.uuid = c.uuid
                    LEFT JOIN sets s ON c.setCode = s.code
                    LEFT JOIN keyruneImages k ON c.setCode = k.setCode
                    LEFT JOIN uniqueManaCostImages u ON c.manaCost = u.uniqueManaCost
                    LEFT JOIN cardPrices p ON m.uuid = p.uuid	
                    LEFT JOIN (
                        SELECT cc.Name, REPLACE(GROUP_CONCAT(DISTINCT cc.keywords), ',', ',') AS AggregatedKeywords
                        FROM cards cc GROUP BY cc.Name
                    ) cg ON c.Name = cg.Name
                    LEFT JOIN (
                        SELECT cc.Name, REPLACE(GROUP_CONCAT(DISTINCT cc.colors), ' ', '') AS AggregatedColors
                        FROM cards cc GROUP BY cc.Name
                    ) ccol ON c.Name = ccol.Name
                    WHERE EXISTS (SELECT 1 FROM cards WHERE uuid = m.uuid)

                    UNION ALL

                    SELECT
                        t.name AS Name,
                        s.name AS SetName,
                        s.releaseDate AS ReleaseDate,
                        k.keyruneImage AS KeyRuneImage,
                        t.manaCost AS ManaCost,
                        u.manaCostImage AS ManaCostImage,
                        t.types AS Types,
                        t.colors AS Colors,
                        t.supertypes AS SuperTypes,
                        t.subtypes AS SubTypes,
                        t.type AS Type,
                        t.keywords AS Keywords,
                        t.text AS RulesText,
                        NULL AS ManaValue,
                        t.finishes AS Finishes,
                        t.uuid AS Uuid,
                        m.id AS CardId,
                        m.cardsOwned AS CardsOwned,
                        m.cardsForTrade AS CardsForTrade,
                        m.condition AS Condition,
                        m.language AS Language,
                        m.finish AS Finish,
                        t.side AS Side,
                        NULL AS Rarity,
                        {normalPriceColumn},
                        {foilPriceColumn},
                        {etchedPriceColumn}
                    FROM myCollection m
                    JOIN tokens t ON m.uuid = t.uuid
                    LEFT JOIN sets s ON t.setCode = s.tokenSetCode
                    LEFT JOIN keyruneImages k ON t.setCode = k.setCode
                    LEFT JOIN uniqueManaCostImages u ON t.manaCost = u.uniqueManaCost
                    LEFT JOIN cardPrices p ON m.uuid = p.uuid
                    WHERE NOT EXISTS (SELECT 1 FROM cards WHERE uuid = m.uuid)
                ) ORDER BY ReleaseDate DESC, SetName, Types,
                    CASE Colors
                        WHEN 'W' THEN 1
                        WHEN 'U' THEN 2
                        WHEN 'B' THEN 3
                        WHEN 'R' THEN 4
                        WHEN 'G' THEN 5
                        ELSE 6
                    END;
            ";

            var viewSqls = new[]
            {
                createCardTokenViewQuery,
                createAllCardsForDecksViewQuery,
                createCardsInDecksViewQuery,
                dropAllCardsViewQuery,
                createAllCardsViewQuery,
                dropMyCollectionViewQuery,
                createMyCollectionViewQuery
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
            //throw new Exception("Test throw.");
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
        }
    }
}
