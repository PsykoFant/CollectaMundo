using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.Infrastructure.CardLists.Models;
using System.Data.Common;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardLists
{
    public class CardListRepo : ICardListRepo
    {
        public async Task<IReadOnlyList<CardPrintingDbRow>> ReadAllCardPrintingDbRowsAsync(SQLiteConnection conn)
        {
            const string query = """
                                SELECT 
                                    c.scryfallOracleId AS ScryfallOracleId,
                                    c.name             AS Name,
                                    c.setCode          AS SetCode,
                                    c.manaCost         AS ManaCost,
                                    c.types            AS Types,
                                    c.colors           AS Colors,
                                    c.supertypes       AS SuperTypes,
                                    c.subtypes         AS SubTypes,
                                    c.type             AS Type,
                                    c.keywords         AS Keywords,
                                    c.text             AS RulesText,
                                    c.manaValue        AS ManaValue,
                                    c.language         AS Language,
                                    c.uuid             AS Uuid,
                                    c.otherFaceIds     AS OtherFaceIds,
                                    c.finishes         AS Finishes,
                                    c.side             AS Side,
                                    c.rarity           AS Rarity
                                FROM cards c

                                UNION ALL

                                SELECT 
                                    t.scryfallOracleId AS ScryfallOracleId,
                                    t.name             AS Name,
                                    t.setCode          AS SetCode,
                                    t.manaCost         AS ManaCost,
                                    t.types            AS Types,
                                    t.colors           AS Colors,
                                    t.supertypes       AS SuperTypes,
                                    t.subtypes         AS SubTypes,
                                    t.type             AS Type,
                                    t.keywords         AS Keywords,
                                    t.text             AS RulesText,
                                    NULL               AS ManaValue,
                                    t.language         AS Language,
                                    t.uuid             AS Uuid,
                                    t.otherFaceIds     AS OtherFaceIds,
                                    t.finishes         AS Finishes,
                                    t.side             AS Side,
                                    NULL               AS Rarity
                                FROM tokens t
                                """;

            using var cmd = new SQLiteCommand(query, conn);
            var list = new List<CardPrintingDbRow>(capacity: 120000);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(CardPrintingDbRowFromReader(reader));
            }

            return list;
        }
        private static CardPrintingDbRow CardPrintingDbRowFromReader(DbDataReader r)
        {
            return new CardPrintingDbRow
            {
                ScryfallOracleId = GetFieldValue<string>(r, "ScryfallOracleId"),

                Name = GetFieldValue<string>(r, "Name"),
                ManaCostRaw = GetFieldValue<string>(r, "ManaCost"),
                Colors = GetFieldValue<string>(r, "Colors"),
                Type = GetFieldValue<string>(r, "Type"),
                Types = GetFieldValue<string>(r, "Types"),
                SuperTypes = GetFieldValue<string>(r, "SuperTypes"),
                SubTypes = GetFieldValue<string>(r, "SubTypes"),
                Keywords = GetFieldValue<string>(r, "Keywords"),
                RulesText = GetFieldValue<string>(r, "RulesText"),
                Side = GetFieldValue<string>(r, "Side"),
                OtherFaceIds = GetFieldValue<string>(r, "OtherFaceIds"),
                ManaValue = GetFieldValue<double?>(r, "ManaValue"),

                Uuid = GetFieldValue<string>(r, "Uuid"),
                Language = GetFieldValue<string>(r, "Language"),
                SetCode = GetFieldValue<string>(r, "SetCode"),
                Rarity = GetFieldValue<string>(r, "Rarity"),
                Finishes = GetFieldValue<string>(r, "Finishes")
            };
        }
        public async Task<List<MyCollectionRow>> ReadMyCollectionAsync(SQLiteConnection conn)
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

            var list = new List<MyCollectionRow>();
            using var rdr = await cmd.ExecuteReaderAsync();

            while (await rdr.ReadAsync())
            {
                var uuid = rdr["uuid"]?.ToString() ?? throw new InvalidOperationException("uuid must not be null");
                var condition = rdr["condition"]?.ToString() ?? throw new InvalidOperationException("condition must not be null");
                var language = rdr["language"]?.ToString() ?? throw new InvalidOperationException("language must not be null");
                var finish = rdr["finish"]?.ToString() ?? throw new InvalidOperationException("finish must not be null");
                int? locationId = rdr["locationId"] == DBNull.Value
                    ? null
                    : rdr["locationId"] is long locationLong
                        ? (int)locationLong
                        : Convert.ToInt32(rdr["locationId"]);
                string? comment = rdr["comment"] == DBNull.Value
                    ? null
                    : rdr["comment"]?.ToString();

                list.Add(new MyCollectionRow
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
        private static T? GetFieldValue<T>(DbDataReader reader, string columnName)
        {
            if (reader[columnName] == DBNull.Value)
            {
                return default;
            }

            var value = reader[columnName];

            if (typeof(T) == typeof(int?) && value is long longValue)
            {
                return (T)(object)(int?)longValue;
            }

            if (typeof(T) == typeof(double?) && value is double doubleValue)
            {
                return (T)(object)(double?)doubleValue;
            }

            return (T)value;
        }
    }
}
