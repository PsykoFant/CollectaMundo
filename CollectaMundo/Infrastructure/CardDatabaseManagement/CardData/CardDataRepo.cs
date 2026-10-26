using CollectaMundo.DomainLogic.CardData.Models;
using CollectaMundo.DomainLogic.CardLegalities;
using CollectaMundo.Infrastructure.Shared.Database;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
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
                Payload: new OracleFacePayload(Name: DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.Name),
                ManaCostRaw: DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.ManaCost),
                ManaValue: DbDataReaderValueReader.GetFieldValue<double?>(reader, ordinals.ManaValue),
                Colors: DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.Colors),
                Keywords: DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.Keywords),
                RulesText: DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.RulesText),
                SuperTypes: DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.SuperTypes),
                Types: DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.Types),
                SubTypes: DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.SubTypes),
                Type: DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.Type)));
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
            // 1. DELETE existing projection
            const string deleteSql = "DELETE FROM printingCards;";

            using (var deleteCommand = new SQLiteCommand(deleteSql, conn, tx))
            {
                await deleteCommand.ExecuteNonQueryAsync();
            }

            // 2. Bulk INSERT all card + token printing rows
            //    Masks start at 0/0.
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
                    ON ti.uuid = t.uuid;
                """;

            int inserted;

            using (var insertCommand = new SQLiteCommand(insertSql, conn, tx))
            {
                inserted = await insertCommand.ExecuteNonQueryAsync();
            }

            // 3. Validate that every physical printing resolves to an Oracle card.
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
                unresolvedOracleCards = Convert.ToInt64(
                    await validationCommand.ExecuteScalarAsync());
            }

            if (unresolvedOracleCards != 0)
            {
                throw new InvalidOperationException($"Printing-card projection contains {unresolvedOracleCards:N0} rows that cannot be resolved to aggregated Oracle cards.");
            }

            // 4. Apply exact per-UUID legality masks to gameplay printings
            const string updateLegalitySql = """
                                            UPDATE printingCards
                                            SET
                                                playableFormatsMask = @playableFormatsMask,
                                                restrictedFormatsMask = @restrictedFormatsMask
                                            WHERE uuid = @uuid;
                                            """;

            using (var legalityCommand = new SQLiteCommand(updateLegalitySql, conn, tx))
            {
                var uuid = legalityCommand.Parameters.Add("@uuid", DbType.String);
                var playableFormatsMask = legalityCommand.Parameters.Add("@playableFormatsMask", DbType.Int64);
                var restrictedFormatsMask = legalityCommand.Parameters.Add("@restrictedFormatsMask", DbType.Int64);

                legalityCommand.Prepare();

                foreach (var pair in legalityMasksByUuid)
                {
                    uuid.Value = pair.Key;

                    playableFormatsMask.Value = unchecked((long)pair.Value.PlayableFormatsMask);
                    restrictedFormatsMask.Value = unchecked((long)pair.Value.RestrictedFormatsMask);

                    var updated = await legalityCommand.ExecuteNonQueryAsync();

                    if (updated != 1)
                    {
                        throw new InvalidOperationException($"Could not persist legality masks for printing '{pair.Key}'. Expected exactly one printing row, but updated {updated:N0}.");
                    }
                }
            }

            return inserted;
        }
    }
}
