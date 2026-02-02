using CsvHelper;
using CsvHelper.Configuration;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace CollectaMundo.Tests.TestUtils
{
    // A fixture class to set up an in‑memory SQLite database and seed it from CSV strings.
    public class InMemoryDatabaseFixture : IAsyncLifetime, IDisposable
    {
        // Unique DB name per fixture instance
        public string DbName { get; } = $"MasterDb_{Guid.NewGuid():N}";

        // CreateCollectionChangeSetFromEdits a connection string that points to this instance's DB name
        private string MasterConnectionString => $"Data Source=file:{DbName}?mode=memory&cache=shared;Version=3;URI=True;";

        // Instance fields 
        private SQLiteConnection? _masterConnection;
        private Task? _seedingTask;

        // ---- IAsyncLifetime ----
        public async ValueTask InitializeAsync()
        {
            try
            {
                if (_masterConnection == null)
                {
                    _masterConnection = new SQLiteConnection(MasterConnectionString);
                    await _masterConnection.OpenAsync();
                }

                if (_seedingTask == null)
                {
                    _seedingTask = InitializeSchemaAndSeedAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing in-memory database: {ex.Message}");
                throw;
            }

            await _seedingTask; // wait outside any lock
        }
        private async Task InitializeSchemaAndSeedAsync()
        {
            SetupSchema();
            await SeedDataAsync();
        }
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
	            edhrecSaltiness REAL, 
	            faceConvertedManaCost REAL, 
	            faceFlavorName TEXT, 
	            faceManaValue REAL, 
	            faceName TEXT, 
	            facePrintedName TEXT, 
	            finishes TEXT, 
	            flavorName TEXT, 
	            flavorText TEXT, 
	            frameEffects TEXT, 
	            frameVersion TEXT, 
	            hand TEXT, 
	            hasAlternativeDeckLimit INTEGER, 
	            hasContentWarning INTEGER, 
	            hasFoil INTEGER, 
	            hasNonFoil INTEGER, 
	            isAlternative INTEGER, 
	            isFullArt INTEGER, 
	            isFunny INTEGER, 
	            isGameChanger INTEGER, 
	            isOnlineOnly INTEGER, 
	            isOversized INTEGER, 
	            isPromo INTEGER, 
	            isRebalanced INTEGER, 
	            isReprint INTEGER, 
	            isReserved INTEGER, 
	            isStorySpotlight INTEGER, 
	            isTextless INTEGER, 
	            isTimeshifted INTEGER, 
	            keywords TEXT, 
	            language TEXT, 
	            layout TEXT, 
	            leadershipSkills TEXT, 
	            life TEXT, 
	            loyalty TEXT, 
	            manaCost TEXT, 
	            manaValue REAL, 
	            name TEXT, 
	            number TEXT, 
	            originalPrintings TEXT, 
	            originalReleaseDate TEXT, 
	            originalText TEXT, 
	            otherFaceIds TEXT, 
	            power TEXT, 
	            printedName TEXT, 
	            printedText TEXT, 
	            printedType TEXT, 
	            printings TEXT, 
	            producedMana TEXT, 
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
	            uuid TEXT, 
	            variations TEXT, 
	            watermark TEXT);
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
	            colorIndicator TEXT, 
	            colors TEXT, 
	            edhrecSaltiness REAL, 
	            faceName TEXT, 
	            finishes TEXT, 
	            flavorName TEXT, 
	            flavorText TEXT, 
	            frameEffects TEXT, 
	            frameVersion TEXT, 
	            hasFoil INTEGER, 
	            hasNonFoil INTEGER, 
	            isFullArt INTEGER, 
	            isFunny INTEGER, 
	            isOversized INTEGER, 
	            isPromo INTEGER, 
	            isReprint INTEGER, 
	            isTextless INTEGER, 
	            keywords TEXT, 
	            language TEXT, 
	            layout TEXT, 
	            manaCost TEXT, 
	            name TEXT, 
	            number TEXT, 
	            orientation TEXT, 
	            originalText TEXT, 
	            otherFaceIds TEXT, 
	            power TEXT, 
	            printedType TEXT, 
	            producedMana TEXT, 
	            promoTypes TEXT, 
	            relatedCards TEXT, 
	            reverseRelated TEXT, 
	            securityStamp TEXT, 
	            setCode TEXT, 
	            side TEXT, 
	            signature TEXT, 
	            sourceProducts TEXT, 
	            subtypes TEXT, 
	            supertypes TEXT, 
	            text TEXT, 
	            toughness TEXT, 
	            type TEXT, 
	            types TEXT, 
	            uuid TEXT, 
	            watermark TEXT);
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

            // CREATE TABLE IF NOT EXISTS: keyruneImages
            command.CommandText = @"CREATE TABLE IF NOT EXISTS keyruneImages(setCode TEXT PRIMARY KEY, keyruneImage BLOB);";
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
	            uuid TEXT)
            ";
            command.ExecuteNonQuery();

            // CREATE TABLE IF NOT EXISTS: cardIdentifiers
            command.CommandText = @"
                CREATE TABLE cardIdentifiers (
	                cardKingdomEtchedId TEXT,
	                cardKingdomFoilId TEXT,
	                cardKingdomId TEXT,
	                cardsphereFoilId TEXT,
	                cardsphereId TEXT,
	                deckboxId TEXT,
	                mcmId TEXT,
	                mcmMetaId TEXT,
	                mtgArenaId TEXT,
	                mtgjsonFoilVersionId TEXT,
	                mtgjsonNonFoilVersionId TEXT,
	                mtgjsonV4Id TEXT,
	                mtgoFoilId TEXT,
	                mtgoId TEXT,
	                multiverseId TEXT,
	                scryfallCardBackId TEXT,
	                scryfallId TEXT,
	                scryfallIllustrationId TEXT,
	                scryfallOracleId TEXT,
	                tcgplayerEtchedProductId TEXT,
	                tcgplayerProductId TEXT,
	                uuid TEXT
                )
            ";
            command.ExecuteNonQuery();

            // CREATE TABLE IF NOT EXISTS: tokenIdentifiers
            command.CommandText = @"
                CREATE TABLE tokenIdentifiers (
	                cardKingdomEtchedId TEXT,
	                cardKingdomFoilId TEXT,
	                cardKingdomId TEXT,
	                cardsphereFoilId TEXT,
	                cardsphereId TEXT,
	                deckboxId TEXT,
	                mcmId TEXT,
	                mcmMetaId TEXT,
	                mtgArenaId TEXT,
	                mtgjsonFoilVersionId TEXT,
	                mtgjsonNonFoilVersionId TEXT,
	                mtgjsonV4Id TEXT,
	                mtgoFoilId TEXT,
	                mtgoId TEXT,
	                multiverseId TEXT,
	                scryfallCardBackId TEXT,
	                scryfallId TEXT,
	                scryfallIllustrationId TEXT,
	                scryfallOracleId TEXT,
	                tcgplayerEtchedProductId TEXT,
	                tcgplayerProductId TEXT,
	                uuid TEXT
                )
            ";
            command.ExecuteNonQuery();

            // CREATE TABLE IF NOT EXISTS: cardPrices
            command.CommandText = @"CREATE TABLE cardPrices (uuid TEXT UNIQUE PRIMARY KEY, cardkingdomNormal DECIMAL(10, 2), cardkingdomFoil DECIMAL(10, 2), cardkingdomEtched DECIMAL(10, 2), cardmarketNormal DECIMAL(10, 2), cardmarketFoil DECIMAL(10, 2), cardmarketEtched DECIMAL(10, 2), cardsphereNormal DECIMAL(10, 2), cardsphereFoil DECIMAL(10, 2), cardsphereEtched DECIMAL(10, 2), tcgplayerNormal DECIMAL(10, 2), tcgplayerFoil DECIMAL(10, 2), tcgplayerEtched DECIMAL(10, 2), cardhoarderNormal DECIMAL(10, 2), cardhoarderFoil DECIMAL(10, 2), cardhoarderEtched DECIMAL(10, 2))";
            command.ExecuteNonQuery();

            // CREATE TABLE IF NOT EXISTS: myCollection
            command.CommandText = @"CREATE TABLE myCollection (id INTEGER PRIMARY KEY,uuid TEXT NOT NULL,language TEXT NOT NULL,finish TEXT NOT NULL,condition TEXT NOT NULL,cardsOwned INTEGER NOT NULL CHECK (cardsOwned >= 0),cardsForTrade INTEGER NOT NULL CHECK (cardsForTrade >= 0),UNIQUE (uuid, language, finish, condition));";
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

            // CREATE VIEW IF NOT EXISTS: view_cardToken
            command.CommandText = @"
                CREATE VIEW view_cardToken AS
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
                    t.side IS NULL OR t.side = 'a'
            ";
            command.ExecuteNonQuery();
        }
        private async Task SeedDataAsync()
        {
            // Check if seed is already there
            var cmd = new SQLiteCommand("SELECT COUNT(*) FROM sets", _masterConnection);
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            if (count > 0)
            {
                Debug.WriteLine("Skipping seed: 'sets' table already populated.");
                return;
            }

            string basePath = Path.Combine(AppContext.BaseDirectory, "TestResources");

            try
            {
                await SeedTableAsync("cards", Path.Combine(basePath, "cards.csv"));
                await SeedTableAsync("tokens", Path.Combine(basePath, "tokens.csv"));
                await SeedTableAsync("sets", Path.Combine(basePath, "sets.csv"));
                await SeedTableAsync("keyruneImages", Path.Combine(basePath, "keyruneImages.csv"));
                await SeedTableAsync("uniqueManaCostImages", Path.Combine(basePath, "uniqueManaCostImages.csv"));
                await SeedTableAsync("cardForeignData", Path.Combine(basePath, "cardForeignData.csv"));
                await SeedTableAsync("cardIdentifiers", Path.Combine(basePath, "cardIdentifiers.csv"));
                await SeedTableAsync("tokenIdentifiers", Path.Combine(basePath, "tokenIdentifiers.csv"));
                await SeedTableAsync("cardPrices", Path.Combine(basePath, "cardPrices.csv"));
                await SeedTableAsync("myCollection", Path.Combine(basePath, "myCollection.csv"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error seeding tables: {ex.Message}");
                throw;
            }
        }
        private async ValueTask SeedTableAsync(string tableName, string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"CSV file for {tableName} not found at {filePath}");
            }

            if (_masterConnection is null)
            {
                throw new InvalidOperationException("_masterConnection is not initialized.");
            }

            Debug.WriteLine($"Seeding database {tableName} with initial data from CSV files.");

            string csvData = await File.ReadAllTextAsync(filePath);
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
            var headers = csv.HeaderRecord!;
            if (headers.Length == 0)
            {
                throw new Exception("CSV file missing headers.");
            }

            // Discover which columns are BLOBs in this table
            var blobColumns = await GetBlobColumnsAsync(tableName);

            // CreateCollectionChangeSetFromEdits the INSERT command.
            var parameters = string.Join(", ", headers.Select((h, i) => $"@p{i}"));
            string insertSql = $"INSERT INTO {tableName} ({string.Join(", ", headers)}) VALUES ({parameters});";
            Debug.WriteLine($"Seeding table '{tableName}' using SQL: {insertSql}");

            using var transaction = _masterConnection.BeginTransaction();
            using var cmd = new SQLiteCommand(insertSql, _masterConnection, transaction);

            // Create parameters with correct DbType for blobs
            for (int i = 0; i < headers.Length; i++)
            {
                var p = new SQLiteParameter($"@p{i}");
                if (blobColumns.Contains(headers[i]))
                {
                    p.DbType = System.Data.DbType.Binary; // << important for BLOB
                }

                cmd.Parameters.Add(p);
            }

            int rowIndex = 1;
            while (await csv.ReadAsync())
            {
                for (int i = 0; i < headers.Length; i++)
                {
                    string field = csv.GetField(headers[i]) ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(field))
                    {
                        cmd.Parameters[$"@p{i}"].Value = DBNull.Value;
                        continue;
                    }

                    if (blobColumns.Contains(headers[i]))
                    {
                        try
                        {
                            // Your CSV stores the image as base64; convert to real bytes for BLOB
                            cmd.Parameters[$"@p{i}"].Value = Convert.FromBase64String(field.Trim());
                        }
                        catch (FormatException fe)
                        {
                            Debug.WriteLine($"Base64 decode failed for table '{tableName}', column '{headers[i]}', row {rowIndex}: {fe.Message}");
                            throw;
                        }
                    }
                    else
                    {
                        cmd.Parameters[$"@p{i}"].Value = field;
                    }
                }

                try
                {
                    cmd.ExecuteNonQuery();
                    rowIndex++;
                }
                catch (Exception ex)
                {
                    string rawRecord = csv.Context.Parser?.RawRecord ?? "[no raw record available]";
                    Debug.WriteLine($"Error inserting row {rowIndex} into table '{tableName}'. Raw row: {rawRecord}. Exception: {ex.Message}");
                    throw;
                }
            }

            transaction.Commit();
            Debug.WriteLine("Seeding completed!");
        }
        private async ValueTask<HashSet<string>> GetBlobColumnsAsync(string tableName)
        {
            var blobCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using var cmd = new SQLiteCommand($"PRAGMA table_info({tableName});", _masterConnection);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                var name = rdr["name"]?.ToString();
                var type = rdr["type"]?.ToString() ?? string.Empty;

                // SQLite is loose with types, but your schema uses "BLOB" for image columns.
                if (!string.IsNullOrWhiteSpace(name) &&
                    type.IndexOf("BLOB", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    blobCols.Add(name);
                }
            }
            return blobCols;
        }

        public ValueTask DisposeAsync()
        {
            _masterConnection?.Dispose();
            return ValueTask.CompletedTask;
        }
        public void Dispose()
        {
            _masterConnection?.Dispose();
        }
    }

}
