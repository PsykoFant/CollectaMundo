using CollectaMundo.DomainLogic.Shared.Factories;
using CollectaMundo.Infrastructure.CardLists.Models;
using CollectaMundo.Infrastructure.Shared.Database;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;

namespace CollectaMundo.Infrastructure.CardLists
{
    public class CardListRepo : ICardListRepo
    {
        // Read aggregated Oracle cards from the database
        public async Task<IReadOnlyList<OracleCardDbRow>> ReadOracleCardsAsync(SQLiteConnection conn)
        {
            const string sql = """
                                SELECT
                                    scryfallOracleId AS ScryfallOracleId,
                                    name             AS Name,
                                    gameplayCard     AS GamePlayCard,
                                    manaCostRaw      AS ManaCostRaw,
                                    manaValue        AS ManaValue,
                                    colors           AS Colors,
                                    keywords         AS Keywords,
                                    rulesText        AS RulesText,
                                    superTypes       AS SuperTypes,
                                    types            AS Types,
                                    subTypes         AS SubTypes,
                                    type             AS Type
                                FROM oracleCards;
                                """;

            using var command = new SQLiteCommand(sql, conn);
            using var reader = await command.ExecuteReaderAsync();

            var ordinals = OracleCardOrdinals.FromReader(reader);
            var rows = new List<OracleCardDbRow>(capacity: 45_000);

            while (await reader.ReadAsync())
            {
                rows.Add(OracleCardDbRowFromReader(reader, ordinals));
            }

            return rows;
        }
        private readonly record struct OracleCardOrdinals(int ScryfallOracleId, int Name, int GamePlayCard, int ManaCostRaw, int ManaValue, int Colors, int Keywords, int RulesText, int SuperTypes, int Types, int SubTypes, int Type)
        {
            public static OracleCardOrdinals FromReader(DbDataReader reader)
            {
                return new OracleCardOrdinals(
                    reader.GetOrdinal("ScryfallOracleId"),
                    reader.GetOrdinal("Name"),
                    reader.GetOrdinal("GamePlayCard"),
                    reader.GetOrdinal("ManaCostRaw"),
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
        private static OracleCardDbRow OracleCardDbRowFromReader(DbDataReader reader, OracleCardOrdinals o)
        {
            return new OracleCardDbRow(
                ScryfallOracleId: reader.GetString(o.ScryfallOracleId),
                Name: reader.GetString(o.Name),
                GamePlayCard: reader.GetInt32(o.GamePlayCard),
                ManaCostRaw: DbDataReaderValueReader.GetFieldValue<string>(reader, o.ManaCostRaw),
                ManaValue: DbDataReaderValueReader.GetFieldValue<double?>(reader, o.ManaValue),
                Colors: DbDataReaderValueReader.GetFieldValue<string>(reader, o.Colors),
                Keywords: DbDataReaderValueReader.GetFieldValue<string>(reader, o.Keywords),
                RulesText: DbDataReaderValueReader.GetFieldValue<string>(reader, o.RulesText),
                SuperTypes: DbDataReaderValueReader.GetFieldValue<string>(reader, o.SuperTypes),
                Types: DbDataReaderValueReader.GetFieldValue<string>(reader, o.Types),
                SubTypes: DbDataReaderValueReader.GetFieldValue<string>(reader, o.SubTypes),
                Type: DbDataReaderValueReader.GetFieldValue<string>(reader, o.Type));
        }

        // Read Printing Cards from the database
        public async Task<IReadOnlyList<PrintingCardDbRow>> ReadPrintingCardRowsAsync(SQLiteConnection conn)
        {
            const string sql = """
                       SELECT
                           uuid             AS Uuid,
                           scryfallOracleId AS ScryfallOracleId,
                           side             AS Side,
                           setCode          AS SetCode,
                           language         AS Language,
                           finishes         AS Finishes,
                           availability     AS Availability,
                           rarity           AS Rarity,
                           isPromo          AS IsPromo,
                           otherFaceIds     AS OtherFaceIds,
                           playableFormatsMask   AS PlayableFormatsMask,
                           restrictedFormatsMask AS RestrictedFormatsMask
                       FROM printingCards;
                       """;

            using var command = new SQLiteCommand(sql, conn);
            using var reader = await command.ExecuteReaderAsync();
            var ordinals = PrintingCardOrdinals.FromReader(reader);
            var rows = new List<PrintingCardDbRow>(capacity: 130_000);

            while (reader.Read())
            {
                rows.Add(PrintingCardDbRowFromReader(reader, ordinals));
            }

            return rows;
        }
        private readonly record struct PrintingCardOrdinals(
            int Uuid,
            int ScryfallOracleId,
            int Side,
            int SetCode,
            int Language,
            int Finishes,
            int Availability,
            int Rarity,
            int IsPromo,
            int OtherFaceIds,
            int PlayableFormatsMask,
            int RestrictedFormatsMask)
        {
            public static PrintingCardOrdinals FromReader(DbDataReader reader)
            {
                return new PrintingCardOrdinals(
                    reader.GetOrdinal("Uuid"),
                    reader.GetOrdinal("ScryfallOracleId"),
                    reader.GetOrdinal("Side"),
                    reader.GetOrdinal("SetCode"),
                    reader.GetOrdinal("Language"),
                    reader.GetOrdinal("Finishes"),
                    reader.GetOrdinal("Availability"),
                    reader.GetOrdinal("Rarity"),
                    reader.GetOrdinal("IsPromo"),
                    reader.GetOrdinal("OtherFaceIds"),
                    reader.GetOrdinal("PlayableFormatsMask"),
                    reader.GetOrdinal("RestrictedFormatsMask")
                    );
            }
        }
        private static PrintingCardDbRow PrintingCardDbRowFromReader(DbDataReader reader, PrintingCardOrdinals ordinals)
        {
            return new PrintingCardDbRow
            {
                Uuid = GetRequiredString(reader, ordinals.Uuid, "Uuid"),
                ScryfallOracleId = GetRequiredString(reader, ordinals.ScryfallOracleId, "ScryfallOracleId"),
                Side = GetRequiredString(reader, ordinals.Side, "Side"),
                SetCode = DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.SetCode),
                Language = DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.Language),
                Finishes = DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.Finishes),
                Availability = DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.Availability),
                Rarity = DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.Rarity),
                IsPromo = DbDataReaderValueReader.GetBooleanValue(reader, ordinals.IsPromo),
                OtherFaceIds = DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.OtherFaceIds),
                PlayableFormatsMask = unchecked((ulong)reader.GetInt64(ordinals.PlayableFormatsMask)),
                RestrictedFormatsMask = unchecked((ulong)reader.GetInt64(ordinals.RestrictedFormatsMask))
            };
        }

        // Read My Collection from the database
        public async Task<List<CollectionCardDbRow>> ReadMyCollectionAsync(SQLiteConnection conn)
        {
            const string sql = """
                       SELECT
                           id,
                           uuid,
                           cardsOwned,
                           cardsForTrade,
                           condition,
                           language,
                           finish,
                           locationId,
                           comment
                       FROM myCollection;
                       """;

            using var cmd = new SQLiteCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            var ordinals = CollectionCardOrdinals.FromReader(reader);
            var list = new List<CollectionCardDbRow>();

            while (await reader.ReadAsync())
            {
                list.Add(CollectionCardDbRowFromReader(reader, ordinals));
            }

            return list;
        }
        private readonly record struct CollectionCardOrdinals(int Id, int Uuid, int CardsOwned, int CardsForTrade, int Condition, int Language, int Finish, int LocationId, int Comment)
        {
            public static CollectionCardOrdinals FromReader(DbDataReader reader)
            {
                return new CollectionCardOrdinals(
                    reader.GetOrdinal("id"),
                    reader.GetOrdinal("uuid"),
                    reader.GetOrdinal("cardsOwned"),
                    reader.GetOrdinal("cardsForTrade"),
                    reader.GetOrdinal("condition"),
                    reader.GetOrdinal("language"),
                    reader.GetOrdinal("finish"),
                    reader.GetOrdinal("locationId"),
                    reader.GetOrdinal("comment"));
            }
        }
        private static CollectionCardDbRow CollectionCardDbRowFromReader(DbDataReader reader, CollectionCardOrdinals ordinals)
        {
            var uuid = GetRequiredFieldValue<string>(reader, ordinals.Uuid, "uuid");
            var condition = GetRequiredFieldValue<string>(reader, ordinals.Condition, "condition");
            var language = GetRequiredFieldValue<string>(reader, ordinals.Language, "language");
            var finish = GetRequiredFieldValue<string>(reader, ordinals.Finish, "finish");
            var locationId = DbDataReaderValueReader.GetFieldValue<int?>(reader, ordinals.LocationId);
            var comment = DbDataReaderValueReader.GetFieldValue<string>(reader, ordinals.Comment);

            return new CollectionCardDbRow
            {
                CardId = GetRequiredFieldValue<int>(reader, ordinals.Id, "id"),
                Identity = CollectionIdentityFactory.Create(uuid, condition, language, finish, locationId, comment),
                CardsOwned = GetRequiredFieldValue<int>(reader, ordinals.CardsOwned, "cardsOwned"),
                CardsForTrade = GetRequiredFieldValue<int>(reader, ordinals.CardsForTrade, "cardsForTrade")
            };
        }

        // Helper methods to read required values from the database reader
        private static T GetRequiredFieldValue<T>(DbDataReader reader, int ordinal, string columnName)
        {
            if (reader.IsDBNull(ordinal))
            {
                throw new InvalidOperationException($"Column '{columnName}' must not be null.");
            }

            var value = DbDataReaderValueReader.GetFieldValue<T>(reader, ordinal);

            return value is null
                ? throw new InvalidOperationException($"Column '{columnName}' must not be null.")
                : value;
        }
        private static string GetRequiredString(DbDataReader reader, int ordinal, string columnName)
        {
            return DbDataReaderValueReader.GetFieldValue<string>(reader, ordinal) ?? throw new InvalidDataException($"Required database column '{columnName}' was null.");
        }
    }
}
