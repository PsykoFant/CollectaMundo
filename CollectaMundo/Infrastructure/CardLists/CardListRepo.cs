using CollectaMundo.DomainLogic.Shared.Factories;
using CollectaMundo.Infrastructure.Shared.Models;
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
            var list = new List<PrintingCardDbRow>(capacity: 122000);

            while (reader.Read())
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
        private static PrintingCardDbRow CardPrintingDbRowFromReader(DbDataReader reader, PrintingCardOrdinals ordinals)
        {
            return new PrintingCardDbRow
            {
                ScryfallOracleId = GetFieldValue<string>(reader, ordinals.ScryfallOracleId),
                Name = GetFieldValue<string>(reader, ordinals.Name),
                ManaCostRaw = GetFieldValue<string>(reader, ordinals.ManaCost),
                Colors = GetFieldValue<string>(reader, ordinals.Colors),
                Type = GetFieldValue<string>(reader, ordinals.Type),
                Types = GetFieldValue<string>(reader, ordinals.Types),
                SuperTypes = GetFieldValue<string>(reader, ordinals.SuperTypes),
                SubTypes = GetFieldValue<string>(reader, ordinals.SubTypes),
                Keywords = GetFieldValue<string>(reader, ordinals.Keywords),
                RulesText = GetFieldValue<string>(reader, ordinals.RulesText),
                Side = GetFieldValue<string>(reader, ordinals.Side),
                IsPromo = GetBooleanValue(reader, ordinals.IsPromo),
                OtherFaceIds = GetFieldValue<string>(reader, ordinals.OtherFaceIds),
                Availability = GetFieldValue<string>(reader, ordinals.Availability),
                GamePlayCard = GetFieldValue<int>(reader, ordinals.GameplayCard),
                ManaValue = GetFieldValue<double?>(reader, ordinals.ManaValue),
                Uuid = GetFieldValue<string>(reader, ordinals.Uuid),
                Language = GetFieldValue<string>(reader, ordinals.Language),
                SetCode = GetFieldValue<string>(reader, ordinals.SetCode),
                Rarity = GetFieldValue<string>(reader, ordinals.Rarity),
                Finishes = GetFieldValue<string>(reader, ordinals.Finishes)
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

            var list = new List<CollectionCardDbRow>();
            using var rdr = await cmd.ExecuteReaderAsync();

            while (await rdr.ReadAsync())
            {
                var uuid = rdr["uuid"]?.ToString() ?? throw new InvalidOperationException("uuid must not be null");
                var condition = rdr["condition"]?.ToString() ?? throw new InvalidOperationException("condition must not be null");
                var language = rdr["language"]?.ToString() ?? throw new InvalidOperationException("language must not be null");
                var finish = rdr["finish"]?.ToString() ?? throw new InvalidOperationException("finish must not be null");
                int? locationId = rdr["locationId"] == DBNull.Value ? null : rdr["locationId"] is long locationLong ? (int)locationLong : Convert.ToInt32(rdr["locationId"]);
                string? comment = rdr["comment"] == DBNull.Value ? null : rdr["comment"]?.ToString();
                list.Add(new CollectionCardDbRow
                {
                    CardId = rdr["id"] is long idLong
                        ? (int)idLong
                        : Convert.ToInt32(rdr["id"]),

                    Identity = CollectionIdentityFactory.Create(uuid, condition, language, finish, locationId, comment),

                    CardsOwned = rdr["cardsOwned"] is long ownedLong
                        ? (int)ownedLong
                        : Convert.ToInt32(rdr["cardsOwned"]),

                    CardsForTrade = rdr["cardsForTrade"] is long tradeLong
                        ? (int)tradeLong
                        : Convert.ToInt32(rdr["cardsForTrade"])
                });
            }

            return list;
        }
        private static T? GetFieldValue<T>(DbDataReader reader, int ordinal)
        {
            var value = reader.GetValue(ordinal);

            if (value == DBNull.Value)
            {
                return default;
            }

            if (typeof(T) == typeof(int) &&
                value is long longValue)
            {
                return (T)(object)(int)longValue;
            }

            if (typeof(T) == typeof(int?) &&
                value is long nullableLongValue)
            {
                return (T)(object)(int?)nullableLongValue;
            }

            if (typeof(T) == typeof(double?) &&
                value is double doubleValue)
            {
                return (T)(object)(double?)doubleValue;
            }

            return (T)value;
        }
        private static bool GetBooleanValue(DbDataReader reader, int ordinal)
        {
            var value = reader.GetValue(ordinal);

            if (value == DBNull.Value)
            {
                return false;
            }

            if (value is bool boolValue)
            {
                return boolValue;
            }

            if (value is long longValue)
            {
                return longValue != 0;
            }

            return Convert.ToBoolean(value);
        }
    }
}
