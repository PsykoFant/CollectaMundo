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
                                    c.isOnlineOnly      AS IsOnlineOnly,
                                    c.finishes          AS Finishes,
                                    c.side              AS Side,
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
                                    0                   AS IsOnlineOnly,
                                    t.finishes          AS Finishes,
                                    t.side              AS Side,
                                    NULL                AS Rarity
                                FROM tokens t
                                LEFT JOIN tokenIdentifiers ti
                                    ON ti.uuid = t.uuid
                                """;

            using var cmd = new SQLiteCommand(query, conn);
            var list = new List<PrintingCardDbRow>(capacity: 120000);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(CardPrintingDbRowFromReader(reader));
            }

            return list;
        }
        private static PrintingCardDbRow CardPrintingDbRowFromReader(DbDataReader r)
        {
            return new PrintingCardDbRow
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
                IsOnlineOnly = GetFieldValue<int>(r, "IsOnlineOnly"),
                Uuid = GetFieldValue<string>(r, "Uuid"),
                Language = GetFieldValue<string>(r, "Language"),
                SetCode = GetFieldValue<string>(r, "SetCode"),
                Rarity = GetFieldValue<string>(r, "Rarity"),
                Finishes = GetFieldValue<string>(r, "Finishes")
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
                int? locationId = rdr["locationId"] == DBNull.Value
                    ? null
                    : rdr["locationId"] is long locationLong
                        ? (int)locationLong
                        : Convert.ToInt32(rdr["locationId"]);
                string? comment = rdr["comment"] == DBNull.Value
                    ? null
                    : rdr["comment"]?.ToString();

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
        private static T? GetFieldValue<T>(DbDataReader reader, string columnName)
        {
            var value = reader[columnName];

            if (value == DBNull.Value)
            {
                return default;
            }

            if (typeof(T) == typeof(int) && value is long longValue)
            {
                return (T)(object)(int)longValue;
            }

            if (typeof(T) == typeof(int?) && value is long nullableLongValue)
            {
                return (T)(object)(int?)nullableLongValue;
            }

            if (typeof(T) == typeof(double?) && value is double doubleValue)
            {
                return (T)(object)(double?)doubleValue;
            }

            return (T)value;
        }
    }
}
