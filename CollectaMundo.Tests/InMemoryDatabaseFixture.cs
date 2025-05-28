using CsvHelper;
using CsvHelper.Configuration;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace CollectaMundo.Tests
{
    // A fixture class to set up an in‑memory SQLite database and seed it from CSV strings.
    public class InMemoryDatabaseFixture : IDisposable
    {
        private readonly SQLiteConnection _masterConnection;
        private static bool _hasSeeded = false;
        public InMemoryDatabaseFixture()
        {
            _masterConnection = new SQLiteConnection("Data Source=file:SharedTestDb?mode=memory&cache=shared;Version=3;");
            _masterConnection.Open();

            EnsureSchemaAndSeed(); // replaces SetupSchema + _hasSeeded
        }


        private void EnsureSchemaAndSeed()
        {
            using var checkCmd = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name='cards';", _masterConnection);
            var result = checkCmd.ExecuteScalar();

            if (result == null)
            {
                SetupSchema();
                SeedDataAsync().GetAwaiter().GetResult();
            }
        }


        public static async Task<SQLiteConnection> CreateClonedConnectionAsync()
        {
            var clone = new SQLiteConnection("Data Source=file:SharedTestDb?mode=memory&cache=shared;Version=3;");
            await clone.OpenAsync();
            return clone;
        }

        public void Dispose() => _masterConnection.Dispose();
        private void SetupSchema()
        {
            using var command = new SQLiteCommand(_masterConnection);
            // CREATE TABLE IF NOT EXISTS: cards
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS cards (
                    artist TEXT,
                    artistIds TEXT,
                    asciiName TEXT,
                    attractionLights TEXT,
                    availability TEXT,
                    boosterTypes TEXT,
                    borderColor TEXT,
                    cardParts TEXT,
                    colorIdentity TEXT,
                    colorIndicator TEXT,
                    colors TEXT,
                    defense TEXT,
                    duelDeck TEXT,
                    edhrecRank INTEGER,
                    edhrecSaltiness FLOAT,
                    faceConvertedManaCost FLOAT,
                    faceFlavorName TEXT,
                    faceManaValue FLOAT,
                    faceName TEXT,
                    finishes TEXT,
                    flavorName TEXT,
                    flavorText TEXT,
                    frameEffects TEXT,
                    frameVersion TEXT,
                    hand TEXT,
                    hasAlternativeDeckLimit BOOLEAN,
                    hasContentWarning BOOLEAN,
                    hasFoil BOOLEAN,
                    hasNonFoil BOOLEAN,
                    isAlternative BOOLEAN,
                    isFullArt BOOLEAN,
                    isFunny BOOLEAN,
                    isOnlineOnly BOOLEAN,
                    isOversized BOOLEAN,
                    isPromo BOOLEAN,
                    isRebalanced BOOLEAN,
                    isReprint BOOLEAN,
                    isReserved BOOLEAN,
                    isStarter BOOLEAN,
                    isStorySpotlight BOOLEAN,
                    isTextless BOOLEAN,
                    isTimeshifted BOOLEAN,
                    keywords TEXT,
                    language TEXT,
                    layout TEXT,
                    leadershipSkills TEXT,
                    life TEXT,
                    loyalty TEXT,
                    manaCost TEXT,
                    manaValue FLOAT,
                    name TEXT,
                    number TEXT,
                    originalPrintings TEXT,
                    originalReleaseDate TEXT,
                    originalText TEXT,
                    originalType TEXT,
                    otherFaceIds TEXT,
                    power TEXT,
                    printings TEXT,
                    promoTypes TEXT,
                    rarity TEXT,
                    rebalancedPrintings TEXT,
                    relatedCards TEXT,
                    securityStamp TEXT,
                    setCode TEXT,
                    side TEXT,
                    signature TEXT,
                    sourceProducts TEXT,
                    subsets TEXT,
                    subtypes TEXT,
                    supertypes TEXT,
                    text TEXT,
                    toughness TEXT,
                    type TEXT,
                    types TEXT,
                    uuid VARCHAR(36) NOT NULL,
                    variations TEXT,
                    watermark TEXT
                );
            ";
            command.ExecuteNonQuery();

            // CREATE TABLE IF NOT EXISTS: tokens
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS tokens (
                    artist TEXT,
                    artistIds TEXT,
                    asciiName TEXT,
                    availability TEXT,
                    boosterTypes TEXT,
                    borderColor TEXT,
                    colorIdentity TEXT,
                    colors TEXT,
                    edhrecSaltiness FLOAT,
                    faceName TEXT,
                    finishes TEXT,
                    flavorName TEXT,
                    flavorText TEXT,
                    frameEffects TEXT,
                    frameVersion TEXT,
                    hasFoil BOOLEAN,
                    hasNonFoil BOOLEAN,
                    isFullArt BOOLEAN,
                    isFunny BOOLEAN,
                    isOversized BOOLEAN,
                    isPromo BOOLEAN,
                    isReprint BOOLEAN,
                    isTextless BOOLEAN,
                    keywords TEXT,
                    language TEXT,
                    layout TEXT,
                    manaCost TEXT,
                    name TEXT,
                    number TEXT,
                    orientation TEXT,
                    originalText TEXT,
                    originalType TEXT,
                    otherFaceIds TEXT,
                    power TEXT,
                    promoTypes TEXT,
                    relatedCards TEXT,
                    reverseRelated TEXT,
                    securityStamp TEXT,
                    setCode TEXT,
                    side TEXT,
                    signature TEXT,
                    subtypes TEXT,
                    supertypes TEXT,
                    text TEXT,
                    toughness TEXT,
                    type TEXT,
                    types TEXT,
                    uuid VARCHAR(36) NOT NULL,
                    watermark TEXT
                );
            ";
            command.ExecuteNonQuery();

            // CREATE TABLE IF NOT EXISTS: sets
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS sets (
                    baseSetSize INTEGER,
                    block TEXT,
                    cardsphereSetId INTEGER,
                    code VARCHAR(8) UNIQUE NOT NULL,
                    isFoilOnly BOOLEAN,
                    isForeignOnly BOOLEAN,
                    isNonFoilOnly BOOLEAN,
                    isOnlineOnly BOOLEAN,
                    isPartialPreview BOOLEAN,
                    keyruneCode TEXT,
                    languages TEXT,
                    mcmId INTEGER,
                    mcmIdExtras INTEGER,
                    mcmName TEXT,
                    mtgoCode TEXT,
                    name TEXT,
                    parentCode TEXT,
                    releaseDate TEXT,
                    tcgplayerGroupId INTEGER,
                    tokenSetCode TEXT,
                    totalSetSize INTEGER,
                    type TEXT
                );
            ";
            command.ExecuteNonQuery();

            // CREATE TABLE IF NOT EXISTS: cardForeignData
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS cardForeignData (
	            faceName TEXT,
	            flavorText TEXT,
	            identifiers TEXT,
	            language TEXT,
	            multiverseId INTEGER,
	            name TEXT,
	            text TEXT,
	            type TEXT,
	            uuid TEXT
            )
            ";
            command.ExecuteNonQuery();

            // CREATE TABLE IF NOT EXISTS: myCollection
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS myCollection (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    uuid TEXT,
                    cardsOwned INTEGER,
                    cardsForTrade INTEGER,
                    condition TEXT,
                    language TEXT,
                    finish TEXT
                );
            ";
            command.ExecuteNonQuery();

            // CREATE TABLE IF NOT EXISTS: cardsInDecks
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS cardsInDecks (id INTEGER PRIMARY KEY AUTOINCREMENT, deckId INTEGER, name TEXT, uuid TEXT, count INTEGER)
            ";
            command.ExecuteNonQuery();

            // CREATE TABLE IF NOT EXISTS: uniqueManaSymbols
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS uniqueManaSymbols (uniqueManaSymbol TEXT PRIMARY KEY, manaSymbolImage BLOB)
            ";
            command.ExecuteNonQuery();

            // CREATE TABLE IF NOT EXISTS: uniqueManaCostImages
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS uniqueManaCostImages (uniqueManaCost TEXT PRIMARY KEY, manaCostImage BLOB)
            ";
            command.ExecuteNonQuery();

            // CREATE VIEW IF NOT EXISTS: view_allCards
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS view_allCards(
                    Name            TEXT,
                    SetName         TEXT,
                    ReleaseDate     TEXT,
                    KeyRuneImage    BLOB,
                    ManaCost        TEXT,
                    ManaCostImage   BLOB,
                    Types           TEXT,
                    Colors          TEXT,
                    SuperTypes      TEXT,
                    SubTypes        TEXT,
                    Type            TEXT,
                    Keywords        TEXT,
                    RulesText       TEXT,
                    ManaValue       REAL,
                    Language        TEXT,
                    Uuid            VARCHAR(36),
                    Finishes        TEXT,
                    Side            TEXT,
                    Rarity          TEXT,
                    NormalPrice     DECIMAL(10, 2),
                    FoilPrice       DECIMAL(10, 2),
                    EtchedPrice     DECIMAL(10, 2)
                );
            ";
            command.ExecuteNonQuery();

            // CREATE VIEW IF NOT EXISTS: view_myCollection
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS view_myCollection(
                    Name            TEXT,
                    SetName         TEXT,
                    ReleaseDate     TEXT,
                    KeyRuneImage    BLOB,
                    ManaCost        TEXT,
                    ManaCostImage   BLOB,
                    Types           TEXT,
                    Colors          TEXT,
                    SuperTypes      TEXT,
                    SubTypes        TEXT,
                    Type            TEXT,
                    Keywords        TEXT,
                    RulesText       TEXT,
                    ManaValue       REAL,
                    Finishes        TEXT,                    
                    Uuid            VARCHAR(36),
                    CardId          INTEGER,
                    CardsOwned      INTEGER,
                    CardsForTrade   INTEGER,
                    Condition       TEXT,
                    Language        TEXT,
                    Finish          TEXT,
                    Side            TEXT,
                    Rarity          TEXT,
                    NormalPrice     DECIMAL(10, 2),
                    FoilPrice       DECIMAL(10, 2),
                    EtchedPrice     DECIMAL(10, 2)
                );
            ";
            command.ExecuteNonQuery();

            // CREATE VIEW IF NOT EXISTS: view_allCardsForDecks
            command.CommandText = @"
                CREATE VIEW IF NOT EXISTS view_allCardsForDecks AS
                    SELECT * FROM (
                        SELECT 
                            DISTINCT c.name AS Name, 
                            c.manaCost AS ManaCost, 
                            u.manaCostImage AS ManaCostImage, 
                            c.types AS Types, 
                            c.colors AS Colors,
                            c.supertypes AS SuperTypes, 
                            c.subtypes AS SubTypes, 
                            c.type AS Type, 
                            COALESCE(cg.AggregatedKeywords, c.keywords) AS Keywords,
                            c.text AS RulesText, 
                            c.manaValue AS ManaValue, 
                            c.side AS Side
                        FROM cards c
                        JOIN sets s ON c.setCode = s.code
                        LEFT JOIN uniqueManaCostImages u ON c.manaCost = u.uniqueManaCost
                        LEFT JOIN (
                             SELECT 
                                cc.Name, 
                                GROUP_CONCAT(cc.keywords, ', ') AS AggregatedKeywords
                             FROM cards cc
                             GROUP BY cc.Name
                            ) cg ON c.Name = cg.Name
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
                        END
            ";
            command.ExecuteNonQuery();

            // CREATE VIEW IF NOT EXISTS: view_cardsInDecks
            command.CommandText = @"
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
                        LEFT JOIN 
                            (
                                SELECT name, colors, manaCost, manaValue, type
                                FROM cards
                                GROUP BY name
                            ) c
	                        ON cardsInDecks.name = c.name
                        LEFT JOIN uniqueManaCostImages u ON c.manaCost = u.uniqueManaCost
            ";
            command.ExecuteNonQuery();


        }
        private async Task SeedDataAsync()
        {
            // Path to test resources directory.
            string basePath = Path.Combine(AppContext.BaseDirectory, "TestResources");

            // Seed each table from its CSV seed string.
            await SeedTableAsync("cards", Path.Combine(basePath, "cards.csv"));
            await SeedTableAsync("tokens", Path.Combine(basePath, "tokens.csv"));
            await SeedTableAsync("sets", Path.Combine(basePath, "sets.csv"));
            await SeedTableAsync("cardForeignData", Path.Combine(basePath, "cardForeignData.csv"));
            await SeedTableAsync("myCollection", Path.Combine(basePath, "myCollection.csv"));
            await SeedTableAsync("view_myCollection", Path.Combine(basePath, "view_myCollection.csv"));
            await SeedTableAsync("view_allCards", Path.Combine(basePath, "view_allCards.csv"));
        }
        private async Task SeedTableAsync(string tableName, string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"CSV file for {tableName} not found at {filePath}");
            }

            string csvData = await File.ReadAllTextAsync(filePath);
            // Use CsvHelper to parse the CSV.
            using var reader = new StringReader(csvData);
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ";",
                HasHeaderRecord = true,
                Quote = '"',
                BadDataFound = args => Debug.WriteLine($"Bad data found: {args.RawRecord}")
            };
            using var csv = new CsvReader(reader, config);
            await csv.ReadAsync();
            csv.ReadHeader();
            // Using the null-forgiving operator because we know headers is not null from our check.
            var headers = csv.HeaderRecord!;
            if (headers.Length == 0)
            {
                throw new Exception("CSV file missing headers.");
            }

            // Build the INSERT command.
            var parameters = string.Join(", ", headers.Select((h, i) => $"@p{i}"));
            string insertSql = $"INSERT INTO {tableName} ({string.Join(", ", headers)}) VALUES ({parameters});";
            Debug.WriteLine($"Seeding table '{tableName}' using SQL: {insertSql}");

            using var transaction = _masterConnection.BeginTransaction();
            using var cmd = new SQLiteCommand(insertSql, _masterConnection, transaction);
            for (int i = 0; i < headers.Length; i++)
            {
                cmd.Parameters.Add(new SQLiteParameter($"@p{i}"));
            }

            int rowIndex = 1;
            while (await csv.ReadAsync())
            {
                for (int i = 0; i < headers.Length; i++)
                {
                    // Get the field and default to string.Empty if it is null.
                    string field = csv.GetField(headers[i]) ?? string.Empty;
                    cmd.Parameters[$"@p{i}"].Value = string.IsNullOrWhiteSpace(field) ? (object)DBNull.Value : field;
                }

                try
                {
                    cmd.ExecuteNonQuery();
                    rowIndex++;
                }
                catch (Exception ex)
                {
                    // Retrieve the raw record safely.
                    string rawRecord = csv.Context.Parser?.RawRecord ?? "[no raw record available]";
                    Debug.WriteLine($"Error inserting row {rowIndex} into table '{tableName}'. Raw row: {rawRecord}. Exception: {ex.Message}");
                    throw;
                }
            }
            transaction.Commit();
        }

    }

}
