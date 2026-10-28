using CollectaMundo.DomainLogic.CardData.Models;
using CollectaMundo.DomainLogic.CardLegalities;
using CollectaMundo.Infrastructure.Shared.Database;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;

namespace CollectaMundo.Infrastructure.CardDatabaseManagement.CardData
{
    public sealed class CardDataRepo : ICardDataRepo
    {
        // Oracle cards
        public async Task<List<OracleFaceCandidate>> GetOracleFaceCandidatesAsync(SQLiteConnection connection, SQLiteTransaction? tx)
        {
            const string query = """
                     SELECT
                         c.uuid              AS Uuid,
                         ci.scryfallOracleId AS ScryfallOracleId,
                         c.side              AS Side,
                         1                   AS GamePlayCard,
                         c.name              AS Name,
                         c.manaCost          AS ManaCost,
                         c.manaValue         AS ManaValue,
                         c.colors            AS Colors,
                         c.keywords          AS Keywords,
                         c.otherFaceIds      AS OtherFaceIds,
                         c.text              AS RulesText,
                         c.supertypes        AS SuperTypes,
                         c.types             AS Types,
                         c.subtypes          AS SubTypes,
                         c.type              AS Type
                     FROM cards c
                     LEFT JOIN cardIdentifiers ci
                         ON ci.uuid = c.uuid

                     UNION ALL

                     SELECT
                         t.uuid              AS Uuid,
                         ti.scryfallOracleId AS ScryfallOracleId,
                         t.side              AS Side,
                         0                   AS GamePlayCard,
                         t.name              AS Name,
                         t.manaCost          AS ManaCost,
                         NULL                AS ManaValue,
                         t.colors            AS Colors,
                         t.keywords          AS Keywords,
                         t.otherFaceIds      AS OtherFaceIds,
                         t.text              AS RulesText,
                         t.supertypes        AS SuperTypes,
                         t.types             AS Types,
                         t.subtypes          AS SubTypes,
                         t.type              AS Type
                     FROM tokens t
                     LEFT JOIN tokenIdentifiers ti
                         ON ti.uuid = t.uuid
                     """;

            using var command = new SQLiteCommand(query, connection);

            using var reader = await command.ExecuteReaderAsync();

            var ordinals = OracleFaceCandidateOrdinals.FromReader(reader);
            var candidates = new List<OracleFaceCandidate>(capacity: 130_000);

            while (reader.Read())
            {
                var candidate = CreateCandidate(reader, ordinals);

                if (candidate is not null)
                {
                    candidates.Add(candidate);
                }
            }

            return candidates;
        }
        private static OracleFaceCandidate CreateCandidate(DbDataReader reader, OracleFaceCandidateOrdinals ordinals)
        {
            var uuid = DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.Uuid);
            var oracleId = DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.ScryfallOracleId);

            if (string.IsNullOrWhiteSpace(uuid))
            {
                throw new InvalidDataException("Card data row is missing UUID.");
            }

            if (string.IsNullOrWhiteSpace(oracleId))
            {
                throw new InvalidDataException($"Card data row '{uuid}' is missing ScryfallOracleId.");
            }

            return new OracleFaceCandidate(
                SourceUuid: uuid,
                ScryfallOracleId: oracleId,
                Side: DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.Side),
                GamePlayCard: reader.GetInt32(ordinals.GamePlayCard),

                Payload: new OracleFacePayload(
                    Name: DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.Name),
                    ManaCostRaw: DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.ManaCost),
                    ManaValue: DbDataReaderValueReader.GetFieldValue<double?>(reader, ordinals.ManaValue),
                    Colors: DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.Colors),
                    Keywords: DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.Keywords),
                    RulesText: DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.RulesText),
                    SuperTypes: DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.SuperTypes),
                    Types: DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.Types),
                    SubTypes: DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.SubTypes),
                    Type: DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.Type)),

                OtherFaceIds: DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.OtherFaceIds));
        }
        private readonly record struct OracleFaceCandidateOrdinals(
            int Uuid,
            int ScryfallOracleId,
            int Side,
            int GamePlayCard,
            int Name,
            int ManaCost,
            int ManaValue,
            int Colors,
            int Keywords,
            int OtherFaceIds,
            int RulesText,
            int SuperTypes,
            int Types,
            int SubTypes,
            int Type)
        {
            public static OracleFaceCandidateOrdinals FromReader(DbDataReader reader)
            {
                return new OracleFaceCandidateOrdinals(
                    reader.GetOrdinal("Uuid"),
                    reader.GetOrdinal("ScryfallOracleId"),
                    reader.GetOrdinal("Side"),
                    reader.GetOrdinal("GamePlayCard"),
                    reader.GetOrdinal("Name"),
                    reader.GetOrdinal("ManaCost"),
                    reader.GetOrdinal("ManaValue"),
                    reader.GetOrdinal("Colors"),
                    reader.GetOrdinal("Keywords"),
                    reader.GetOrdinal("OtherFaceIds"),
                    reader.GetOrdinal("RulesText"),
                    reader.GetOrdinal("SuperTypes"),
                    reader.GetOrdinal("Types"),
                    reader.GetOrdinal("SubTypes"),
                    reader.GetOrdinal("Type"));
            }
        }
        public async Task<int> RebuildOracleCardsAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyList<AggregatedOracleCard> cards)
        {
            const string deleteSql = "DELETE FROM oracleCards;";

            using (var deleteCommand = new SQLiteCommand(deleteSql, conn, tx))
            {
                await deleteCommand.ExecuteNonQueryAsync();
            }

            const string insertSql =
                """
                INSERT INTO oracleCards
                (
                    scryfallOracleId,
                    name,
                    gameplayCard,
                    manaCostRaw,
                    manaValue,
                    colors,
                    keywords,
                    rulesText,
                    superTypes,
                    types,
                    subTypes,
                    type
                )
                VALUES
                (
                    @scryfallOracleId,
                    @name,
                    @gameplayCard,
                    @manaCostRaw,
                    @manaValue,
                    @colors,
                    @keywords,
                    @rulesText,
                    @superTypes,
                    @types,
                    @subTypes,
                    @type
                );
                """;

            using var command = new SQLiteCommand(insertSql, conn, tx);

            var oracleId = command.Parameters.Add("@scryfallOracleId", DbType.String);
            var name = command.Parameters.Add("@name", DbType.String);
            var gamePlayCard = command.Parameters.Add("@gameplayCard", DbType.Int32);
            var manaCostRaw = command.Parameters.Add("@manaCostRaw", DbType.String);
            var manaValue = command.Parameters.Add("@manaValue", DbType.Double);
            var colors = command.Parameters.Add("@colors", DbType.String);
            var keywords = command.Parameters.Add("@keywords", DbType.String);
            var rulesText = command.Parameters.Add("@rulesText", DbType.String);
            var superTypes = command.Parameters.Add("@superTypes", DbType.String);
            var types = command.Parameters.Add("@types", DbType.String);
            var subTypes = command.Parameters.Add("@subTypes", DbType.String);
            var type = command.Parameters.Add("@type", DbType.String);

            command.Prepare();

            var inserted = 0;

            foreach (var card in cards)
            {
                oracleId.Value = card.ScryfallOracleId;
                name.Value = card.Name;
                gamePlayCard.Value = card.GamePlayCard;
                manaCostRaw.Value = (object?)card.ManaCostRaw ?? DBNull.Value;
                manaValue.Value = card.ManaValue is double value
                    ? value
                    : DBNull.Value;
                colors.Value = (object?)card.Colors ?? DBNull.Value;
                keywords.Value = (object?)card.Keywords ?? DBNull.Value;
                rulesText.Value = (object?)card.RulesText ?? DBNull.Value;
                superTypes.Value = (object?)card.SuperTypes ?? DBNull.Value;
                types.Value = (object?)card.Types ?? DBNull.Value;
                subTypes.Value = (object?)card.SubTypes ?? DBNull.Value;
                type.Value = (object?)card.Type ?? DBNull.Value;

                inserted += await command.ExecuteNonQueryAsync();
            }

            return inserted;
        }

        // Printing cards
        public async Task<int> RebuildPrintingCardsAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyDictionary<string, CardLegalityMasks> legalityMasksByUuid)
        {
            var totalSw = Stopwatch.StartNew();

            Debug.WriteLine("[PrintingRebuild] Starting");




            ArgumentNullException.ThrowIfNull(legalityMasksByUuid);

            // ---------------------------------------------------------
            // 1. Delete existing printing projection
            // ---------------------------------------------------------

            const string deleteSql = "DELETE FROM printingCards;";

            using (var deleteCommand = new SQLiteCommand(deleteSql, conn, tx))
            {
                await deleteCommand.ExecuteNonQueryAsync();
            }

            // ---------------------------------------------------------
            // 2. Insert standalone printings from cards and tokens
            //
            // Only primary faces are independently selectable: side NULL, empty/whitespace, or "a".
            //
            // Secondary faces remain in the original source tables. Legality masks initially default to zero.
            // ---------------------------------------------------------

            const string insertSql = """
                                    INSERT INTO printingCards
                                    (
                                        uuid,
                                        scryfallOracleId,
                                        side,
                                        setCode,
                                        language,
                                        finishes,
                                        availability,
                                        rarity,
                                        isPromo,
                                        playableFormatsMask,
                                        restrictedFormatsMask,
                                        otherFaceIds
                                    )

                                    SELECT
                                        c.uuid,
                                        ci.scryfallOracleId,
                                        COALESCE(c.side, ''),
                                        c.setCode,
                                        c.language,
                                        c.finishes,
                                        c.availability,
                                        c.rarity,
                                        CASE
                                            WHEN COALESCE(c.isPromo, 0) <> 0 THEN 1
                                            ELSE 0
                                        END,
                                        0,
                                        0,
                                        c.otherFaceIds
                                    FROM cards c
                                    LEFT JOIN cardIdentifiers ci
                                        ON ci.uuid = c.uuid
                                    WHERE
                                        COALESCE(TRIM(c.side), '') = ''
                                        OR c.side = 'a' COLLATE NOCASE

                                    UNION ALL

                                    SELECT
                                        t.uuid,
                                        ti.scryfallOracleId,
                                        COALESCE(t.side, ''),
                                        t.setCode,
                                        t.language,
                                        t.finishes,
                                        t.availability,
                                        NULL,
                                        CASE
                                            WHEN COALESCE(t.isPromo, 0) <> 0 THEN 1
                                            ELSE 0
                                        END,
                                        0,
                                        0,
                                        t.otherFaceIds
                                    FROM tokens t
                                    LEFT JOIN tokenIdentifiers ti
                                        ON ti.uuid = t.uuid
                                    WHERE
                                        COALESCE(TRIM(t.side), '') = ''
                                        OR t.side = 'a' COLLATE NOCASE;
                                    """;

            int inserted;

            Debug.WriteLine("[PrintingRebuild] Starting bulk INSERT");

            var insertSw = Stopwatch.StartNew();

            using (var insertCommand = new SQLiteCommand(insertSql, conn, tx))
            {
                inserted = await insertCommand.ExecuteNonQueryAsync();
            }

            Debug.WriteLine(
                $"[PrintingRebuild] Bulk INSERT completed: " +
                $"{inserted:N0} rows in {insertSw.ElapsedMilliseconds:N0} ms");

            // ---------------------------------------------------------
            // 3. Validate standalone-printing row count
            // ---------------------------------------------------------

            const string sourceCountSql = """
                                            SELECT
                                                (
                                                    SELECT COUNT(*)
                                                    FROM cards
                                                    WHERE
                                                        COALESCE(TRIM(side), '') = ''
                                                        OR side = 'a' COLLATE NOCASE
                                                )
                                                +
                                                (
                                                    SELECT COUNT(*)
                                                    FROM tokens
                                                    WHERE
                                                        COALESCE(TRIM(side), '') = ''
                                                        OR side = 'a' COLLATE NOCASE
                                                );
                                            """;

            long expected;

            using (var countCommand = new SQLiteCommand(sourceCountSql, conn, tx))
            {
                expected = Convert.ToInt64(await countCommand.ExecuteScalarAsync());
            }

            if (inserted != expected)
            {
                throw new InvalidOperationException($"Printing-card projection inserted {inserted:N0} rows, but {expected:N0} standalone source printings were expected.");
            }

            if (inserted == 0)
            {
                throw new InvalidOperationException("Printing-card projection produced no standalone printings.");
            }

            // ---------------------------------------------------------
            // 4. Validate Oracle references
            //
            // Every standalone printing must resolve to exactly one persisted Oracle card through ScryfallOracleId.
            // ---------------------------------------------------------

            const string unresolvedOracleSql = """
                                                SELECT COUNT(*)
                                                FROM printingCards p
                                                LEFT JOIN oracleCards o
                                                    ON o.scryfallOracleId = p.scryfallOracleId
                                                WHERE o.scryfallOracleId IS NULL;
                                                """;

            long unresolvedOracleCards;

            using (var validationCommand = new SQLiteCommand(unresolvedOracleSql, conn, tx))
            {
                unresolvedOracleCards = Convert.ToInt64(await validationCommand.ExecuteScalarAsync());
            }

            if (unresolvedOracleCards != 0)
            {
                throw new InvalidOperationException($"Printing-card projection contains {unresolvedOracleCards:N0} standalone printings that cannot be resolved to Oracle cards.");
            }

            // ---------------------------------------------------------
            // 5. Validate existing collection references
            //
            // Collection UUIDs must remain exact and resolvable. Never silently delete or remap collection records.
            // ---------------------------------------------------------

            const string unresolvedCollectionSql = """
                                                    SELECT
                                                        mc.id,
                                                        mc.uuid
                                                    FROM myCollection mc
                                                    LEFT JOIN printingCards p
                                                        ON p.uuid = mc.uuid
                                                    WHERE p.uuid IS NULL
                                                    LIMIT 1;
                                                    """;

            using (var collectionCommand = new SQLiteCommand(unresolvedCollectionSql, conn, tx))
            using (var reader = await collectionCommand.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    var collectionId = reader.GetInt64(0);
                    var printingUuid = reader.GetString(1);

                    throw new InvalidOperationException($"Collection entry '{collectionId}' references printing UUID '{printingUuid}', which is not a standalone printing in the rebuilt projection. Collection data was not modified.");
                }
            }

            // ---------------------------------------------------------
            // 6. Apply exact per-UUID legality masks
            //
            // The legality source can include secondary physical faces, which are deliberately absent from printingCards. Missing primary printings or completely unknown UUIDs must still cause an error.
            // ---------------------------------------------------------

            const string updateLegalitySql = """
                                            UPDATE printingCards
                                            SET
                                                playableFormatsMask = @playableFormatsMask,
                                                restrictedFormatsMask = @restrictedFormatsMask
                                            WHERE uuid = @uuid;
                                            """;

            const string secondarySourceSql = """
                                            SELECT EXISTS (
                                                SELECT 1
                                                FROM cards c
                                                WHERE c.uuid = @uuid
                                                  AND NOT (
                                                      COALESCE(TRIM(c.side), '') = ''
                                                      OR c.side = 'a' COLLATE NOCASE
                                                  )

                                                UNION ALL

                                                SELECT 1
                                                FROM tokens t
                                                WHERE t.uuid = @uuid
                                                  AND NOT (
                                                      COALESCE(TRIM(t.side), '') = ''
                                                      OR t.side = 'a' COLLATE NOCASE
                                                  )
                                            );
                                            """;

            using (var legalityCommand = new SQLiteCommand(updateLegalitySql, conn, tx))
            using (var secondaryCommand = new SQLiteCommand(secondarySourceSql, conn, tx))
            {
                var uuid = legalityCommand.Parameters.Add("@uuid", DbType.String);
                var playableFormatsMask = legalityCommand.Parameters.Add("@playableFormatsMask", DbType.Int64);
                var restrictedFormatsMask = legalityCommand.Parameters.Add("@restrictedFormatsMask", DbType.Int64);
                var secondaryUuid = secondaryCommand.Parameters.Add("@uuid", DbType.String);

                legalityCommand.Prepare();
                secondaryCommand.Prepare();

                Debug.WriteLine($"[PrintingRebuild] Starting legality updates: {legalityMasksByUuid.Count:N0} UUIDs");

                var updatedCount = 0;
                var secondaryCount = 0;

                foreach (var pair in legalityMasksByUuid)
                {
                    uuid.Value = pair.Key;
                    playableFormatsMask.Value = unchecked((long)pair.Value.PlayableFormatsMask);
                    restrictedFormatsMask.Value = unchecked((long)pair.Value.RestrictedFormatsMask);

                    var updated = await legalityCommand.ExecuteNonQueryAsync();

                    if (updated == 1)
                    {
                        updatedCount++;
                    }
                    else if (updated == 0)
                    {
                        secondaryUuid.Value = pair.Key;

                        var isSecondary = Convert.ToInt32(await secondaryCommand.ExecuteScalarAsync()) == 1;

                        if (!isSecondary)
                        {
                            throw new InvalidOperationException($"Legality UUID '{pair.Key}' is neither a standalone printing nor a recognized secondary source face.");
                        }

                        secondaryCount++;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Legality update for '{pair.Key}' affected {updated} rows.");
                    }
                }
            }

            return inserted;
        }

    }
}
