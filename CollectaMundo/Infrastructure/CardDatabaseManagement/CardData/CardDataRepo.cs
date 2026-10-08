using CollectaMundo.DomainLogic.CardData.Models;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using static CollectaMundo.Infrastructure.Shared.DbDataReaderValueReader;

namespace CollectaMundo.Infrastructure.CardDatabaseManagement.CardData
{
    public sealed class CardDataRepo : ICardDataRepo
    {
        public async Task<List<OracleFaceCandidate>> GetOracleFaceCandidatesAsync(SQLiteConnection connection, SQLiteTransaction? tx)
        {
            const string query = """
                     SELECT
                         c.uuid              AS Uuid,
                         ci.scryfallOracleId AS ScryfallOracleId,
                         c.side              AS Side,
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
            var uuid = GetFieldValue<string>(reader, ordinals.Uuid);
            var oracleId = GetFieldValue<string>(reader, ordinals.ScryfallOracleId);

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
                Side: GetFieldValue<string>(reader, ordinals.Side),
                Payload: new OracleFacePayload(
                    Name: GetFieldValue<string>(reader, ordinals.Name),
                    ManaCostRaw: GetFieldValue<string>(reader, ordinals.ManaCost),
                    ManaValue: GetFieldValue<double?>(reader, ordinals.ManaValue),
                    Colors: GetFieldValue<string>(reader, ordinals.Colors),
                    Keywords: GetFieldValue<string>(reader, ordinals.Keywords),
                    RulesText: GetFieldValue<string>(reader, ordinals.RulesText),
                    SuperTypes: GetFieldValue<string>(reader, ordinals.SuperTypes),
                    Types: GetFieldValue<string>(reader, ordinals.Types),
                    SubTypes: GetFieldValue<string>(reader, ordinals.SubTypes),
                    Type: GetFieldValue<string>(reader, ordinals.Type)));
        }
        private readonly record struct OracleFaceCandidateOrdinals(int Uuid, int ScryfallOracleId, int Side, int Name, int ManaCost, int ManaValue, int Colors, int Keywords, int RulesText, int SuperTypes, int Types, int SubTypes, int Type)
        {
            public static OracleFaceCandidateOrdinals FromReader(
                DbDataReader reader)
            {
                return new OracleFaceCandidateOrdinals(
                    reader.GetOrdinal("Uuid"),
                    reader.GetOrdinal("ScryfallOracleId"),
                    reader.GetOrdinal("Side"),
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
        public async Task<int> RebuildCanonicalOracleFacesAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyList<CanonicalOracleFace> faces)
        {
            const string deleteSql = "DELETE FROM canonicalOracleFaces;";

            using (var deleteCommand = new SQLiteCommand(deleteSql, conn, tx))
            {
                await deleteCommand.ExecuteNonQueryAsync();
            }

            const string insertSql = """
                INSERT INTO canonicalOracleFaces
                (
                    scryfallOracleId,
                    side,
                    name,
                    manaCostRaw,
                    manaValue,
                    colors,
                    keywords,
                    rulesText,
                    superTypes,
                    types,
                    subTypes,
                    type,
                    selectedSourceUuid,
                    sourceRowCount,
                    winningRowCount,
                    variantCount,
                    isAmbiguous
                )
                VALUES
                (
                    @scryfallOracleId,
                    @side,
                    @name,
                    @manaCostRaw,
                    @manaValue,
                    @colors,
                    @keywords,
                    @rulesText,
                    @superTypes,
                    @types,
                    @subTypes,
                    @type,
                    @selectedSourceUuid,
                    @sourceRowCount,
                    @winningRowCount,
                    @variantCount,
                    @isAmbiguous
                );
                """;

            using var command = new SQLiteCommand(insertSql, conn, tx);

            var oracleId = command.Parameters.Add("@scryfallOracleId", DbType.String);
            var side = command.Parameters.Add("@side", DbType.String);
            var name = command.Parameters.Add("@name", DbType.String);
            var manaCostRaw = command.Parameters.Add("@manaCostRaw", DbType.String);
            var manaValue = command.Parameters.Add("@manaValue", DbType.Double);
            var colors = command.Parameters.Add("@colors", DbType.String);
            var keywords = command.Parameters.Add("@keywords", DbType.String);
            var rulesText = command.Parameters.Add("@rulesText", DbType.String);
            var superTypes = command.Parameters.Add("@superTypes", DbType.String);
            var types = command.Parameters.Add("@types", DbType.String);
            var subTypes = command.Parameters.Add("@subTypes", DbType.String);
            var type = command.Parameters.Add("@type", DbType.String);
            var selectedSourceUuid = command.Parameters.Add("@selectedSourceUuid", DbType.String);
            var sourceRowCount = command.Parameters.Add("@sourceRowCount", DbType.Int32);
            var winningRowCount = command.Parameters.Add("@winningRowCount", DbType.Int32);
            var variantCount = command.Parameters.Add("@variantCount", DbType.Int32);
            var isAmbiguous = command.Parameters.Add("@isAmbiguous", DbType.Int32);

            command.Prepare();

            var inserted = 0;

            foreach (var face in faces)
            {
                oracleId.Value = face.Key.ScryfallOracleId;
                side.Value = face.Key.Side;
                name.Value = (object?)face.Payload.Name ?? DBNull.Value;
                manaCostRaw.Value = (object?)face.Payload.ManaCostRaw ?? DBNull.Value;
                manaValue.Value = face.Payload.ManaValue is double value ? value : DBNull.Value;
                colors.Value = (object?)face.Payload.Colors ?? DBNull.Value;
                keywords.Value = (object?)face.Payload.Keywords ?? DBNull.Value; rulesText.Value = (object?)face.Payload.RulesText ?? DBNull.Value;
                superTypes.Value = (object?)face.Payload.SuperTypes ?? DBNull.Value;
                types.Value = (object?)face.Payload.Types ?? DBNull.Value;
                subTypes.Value = (object?)face.Payload.SubTypes ?? DBNull.Value;
                type.Value = (object?)face.Payload.Type ?? DBNull.Value;
                selectedSourceUuid.Value = face.SelectedSourceUuid;
                sourceRowCount.Value = face.SourceRowCount;
                winningRowCount.Value = face.WinningRowCount;
                variantCount.Value = face.VariantCount;
                isAmbiguous.Value = face.IsAmbiguous ? 1 : 0;

                inserted += await command.ExecuteNonQueryAsync();
            }

            return inserted;
        }
    }
}
