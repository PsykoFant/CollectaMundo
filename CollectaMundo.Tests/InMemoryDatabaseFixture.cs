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
        public SQLiteConnection Connection { get; private set; }

        // CSV data for each table.
        // Replace these sample rows with the full CSV data from your files.
        public InMemoryDatabaseFixture()
        {
            // Create an in-memory SQLite database.
            Connection = new SQLiteConnection("Data Source=:memory:;Version=3;");
            Connection.Open();

            // Create tables.
            SetupSchema();

            // Seed tables with CSV data.
            // Synchronously seed tables with CSV data.
            SeedDataAsync().GetAwaiter().GetResult();
        }
        private void SetupSchema()
        {
            using var command = new SQLiteCommand(Connection);
            // Create table: cards
            command.CommandText = @"
                CREATE TABLE cards (
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

            // Create table: tokens
            command.CommandText = @"
                CREATE TABLE tokens (
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

            // Create table: sets
            command.CommandText = @"
                CREATE TABLE sets (
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

            // Create table: cardForeignData
            command.CommandText = @"
                CREATE TABLE cardForeignData (
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

            // Create table: myCollection
            command.CommandText = @"
                CREATE TABLE myCollection (
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

            // Create table: view_allCards
            command.CommandText = @"
                CREATE TABLE view_allCards(
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

            // Create table: view_myCollection
            command.CommandText = @"
                CREATE TABLE view_myCollection(
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

        // A helper method to seed a table from CSV data.
        // Assumes semicolon ';' as delimiter.
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

            using var transaction = Connection.BeginTransaction();
            using var cmd = new SQLiteCommand(insertSql, Connection, transaction);
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
        public void Dispose()
        {
            Connection?.Close();
            Connection?.Dispose();
        }
    }
}
