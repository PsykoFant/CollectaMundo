using CollectaMundo.DomainLogic.Shared.Factories;
using CollectaMundo.Infrastructure.CardLists.Models;
using CollectaMundo.Infrastructure.Shared.Database;
using System.Data.Common;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardLists
{
    public class CardListRepo : ICardListRepo
    {
        public async Task<IReadOnlyList<PrintingCardDbRow>> ReadAllCardPrintingDbRowsAsync(SQLiteConnection conn)
        {
            const string query = """
                                 SELECT 
                                     ci.scryfallOracleId AS ScryfallOracleId,
                                     c.name              AS Name,
                                     c.setCode           AS SetCode,
                                     c.manaCost          AS ManaCost,
                                     c.types             AS Types,
                                     c.colors            AS Colors,
                                     c.supertypes        AS SuperTypes,
                                     c.subtypes          AS SubTypes,
                                     c.type              AS Type,
                                     c.keywords          AS Keywords,
                                     c.text              AS RulesText,
                                     c.manaValue         AS ManaValue,
                                     c.language          AS Language,
                                     c.uuid              AS Uuid,
                                     c.otherFaceIds      AS OtherFaceIds,
                                     c.availability      AS Availability,
                                     1                   AS GameplayCard,
                                     c.finishes          AS Finishes,
                                     c.side              AS Side,
                                     c.isPromo           AS IsPromo,
                                     c.rarity            AS Rarity
                                 FROM cards c
                                 LEFT JOIN cardIdentifiers ci
                                     ON ci.uuid = c.uuid

                                 UNION ALL

                                 SELECT 
                                     ti.scryfallOracleId AS ScryfallOracleId,
                                     t.name              AS Name,
                                     t.setCode           AS SetCode,
                                     t.manaCost          AS ManaCost,
                                     t.types             AS Types,
                                     t.colors            AS Colors,
                                     t.supertypes        AS SuperTypes,
                                     t.subtypes          AS SubTypes,
                                     t.type              AS Type,
                                     t.keywords          AS Keywords,
                                     t.text              AS RulesText,
                                     NULL                AS ManaValue,
                                     t.language          AS Language,
                                     t.uuid              AS Uuid,
                                     t.otherFaceIds      AS OtherFaceIds,
                                     t.availability      AS Availability,
                                     0                   AS GameplayCard,
                                     t.finishes          AS Finishes,
                                     t.side              AS Side,
                                     t.isPromo           AS IsPromo,
                                     NULL                AS Rarity
                                 FROM tokens t
                                 LEFT JOIN tokenIdentifiers ti
                                     ON ti.uuid = t.uuid
                                 """;

            using var cmd = new SQLiteCommand(query, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            var ordinals = PrintingCardOrdinals.FromReader(reader);
            var list = new List<PrintingCardDbRow>(capacity: 130000);

            while (await reader.ReadAsync())
            {
                list.Add(CardPrintingDbRowFromReader(reader, ordinals));
            }

            return list;
        }
        private readonly record struct PrintingCardOrdinals(
            int ScryfallOracleId,
            int Name,
            int SetCode,
            int ManaCost,
            int Types,
            int Colors,
            int SuperTypes,
            int SubTypes,
            int Type,
            int Keywords,
            int RulesText,
            int ManaValue,
            int Language,
            int Uuid,
            int OtherFaceIds,
            int Availability,
            int GameplayCard,
            int Finishes,
            int Side,
            int IsPromo,
            int Rarity)
        {
            public static PrintingCardOrdinals FromReader(DbDataReader reader)
            {
                return new PrintingCardOrdinals(
                    reader.GetOrdinal("ScryfallOracleId"),
                    reader.GetOrdinal("Name"),
                    reader.GetOrdinal("SetCode"),
                    reader.GetOrdinal("ManaCost"),
                    reader.GetOrdinal("Types"),
                    reader.GetOrdinal("Colors"),
                    reader.GetOrdinal("SuperTypes"),
                    reader.GetOrdinal("SubTypes"),
                    reader.GetOrdinal("Type"),
                    reader.GetOrdinal("Keywords"),
                    reader.GetOrdinal("RulesText"),
                    reader.GetOrdinal("ManaValue"),
                    reader.GetOrdinal("Language"),
                    reader.GetOrdinal("Uuid"),
                    reader.GetOrdinal("OtherFaceIds"),
                    reader.GetOrdinal("Availability"),
                    reader.GetOrdinal("GameplayCard"),
                    reader.GetOrdinal("Finishes"),
                    reader.GetOrdinal("Side"),
                    reader.GetOrdinal("IsPromo"),
                    reader.GetOrdinal("Rarity"));
            }
        }
        private static PrintingCardDbRow CardPrintingDbRowFromReader(DbDataReader r, PrintingCardOrdinals o)
        {
            return new PrintingCardDbRow
            {
                ScryfallOracleId = DbDataReaderValueReader.GetFieldValue<string>(r, o.ScryfallOracleId),
                Name = DbDataReaderValueReader.GetFieldValue<string>(r, o.Name),
                ManaCostRaw = DbDataReaderValueReader.GetFieldValue<string>(r, o.ManaCost),
                Colors = DbDataReaderValueReader.GetFieldValue<string>(r, o.Colors),
                Type = DbDataReaderValueReader.GetFieldValue<string>(r, o.Type),
                Types = DbDataReaderValueReader.GetFieldValue<string>(r, o.Types),
                SuperTypes = DbDataReaderValueReader.GetFieldValue<string>(r, o.SuperTypes),
                SubTypes = DbDataReaderValueReader.GetFieldValue<string>(r, o.SubTypes),
                Keywords = DbDataReaderValueReader.GetFieldValue<string>(r, o.Keywords),
                RulesText = DbDataReaderValueReader.GetFieldValue<string>(r, o.RulesText),
                Side = DbDataReaderValueReader.GetFieldValue<string>(r, o.Side),
                IsPromo = DbDataReaderValueReader.GetBooleanValue(r, o.IsPromo),
                OtherFaceIds = DbDataReaderValueReader.GetFieldValue<string>(r, o.OtherFaceIds),
                Availability = DbDataReaderValueReader.GetFieldValue<string>(r, o.Availability),
                GamePlayCard = DbDataReaderValueReader.GetFieldValue<int>(r, o.GameplayCard),
                ManaValue = DbDataReaderValueReader.GetFieldValue<double?>(r, o.ManaValue),
                Uuid = DbDataReaderValueReader.GetFieldValue<string>(r, o.Uuid),
                Language = DbDataReaderValueReader.GetFieldValue<string>(r, o.Language),
                SetCode = DbDataReaderValueReader.GetFieldValue<string>(r, o.SetCode),
                Rarity = DbDataReaderValueReader.GetFieldValue<string>(r, o.Rarity),
                Finishes = DbDataReaderValueReader.GetFieldValue<string>(r, o.Finishes)
            };
        }
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
    }
}
