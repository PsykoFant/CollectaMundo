using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared;
using System.Data.Common;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardLists
{
    public class CardListRepo() : ICardListRepo
    {
        public async Task<IReadOnlyList<CardCoreDto>> ReadAllCardsCoreDtosAsync(SQLiteConnection conn)
        {
            const string query = $@"
                        SELECT 
                            c.name        AS Name,
		                    c.setCode     AS SetCode,
                            c.manaCost    AS ManaCost,
                            c.types       AS Types,
                            c.colors      AS Colors,
                            c.supertypes  AS SuperTypes,
                            c.subtypes    AS SubTypes,
                            c.type        AS Type,
                            c.keywords    AS Keywords,
                            c.text        AS RulesText,
                            c.manaValue   AS ManaValue,
                            c.language    AS Language,
                            c.uuid        AS Uuid,
							c.otherFaceIds AS OtherIDs,
                            c.finishes    AS Finishes,
                            c.side        AS Side,
                            c.rarity      AS Rarity
                        FROM cards c
                        UNION ALL
                        SELECT 
                            t.name        AS Name,
		                    t.setCode     AS SetCode,
                            t.manaCost    AS ManaCost,
                            t.types       AS Types,
                            t.colors      AS Colors,
                            t.supertypes  AS SuperTypes,
                            t.subtypes    AS SubTypes,
                            t.type        AS Type,
                            t.keywords    AS Keywords,
                            t.text        AS RulesText,
                            NULL          AS ManaValue,
                            t.language    AS Language,
                            t.uuid        AS Uuid,
							t.otherFaceIds AS OtherIDs,
                            t.finishes    AS Finishes,
                            t.side        AS Side,
                            NULL          AS Rarity
                        FROM tokens t";

            using var cmd = new SQLiteCommand(query, conn);
            var list = new List<CardCoreDto>(capacity: 120000);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(DtoFromReader(reader));
            }

            return list;
        }
        private static CardCoreDto DtoFromReader(DbDataReader r)
        {
            return new CardCoreDto
            {
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
                Language = GetFieldValue<string>(r, "Language"),
                Uuid = GetFieldValue<string>(r, "Uuid"),
                OtherFaceIds = GetFieldValue<string>(r, "OtherIDs"),
                SetCode = GetFieldValue<string>(r, "SetCode"),
                Rarity = GetFieldValue<string>(r, "Rarity"),
                Finishes = GetFieldValue<string>(r, "Finishes"),
                ManaValue = GetFieldValue<double?>(r, "ManaValue"),
            };
        }
        public async Task<List<MyCollectionRow>> ReadMyCollectionAsync(SQLiteConnection conn)
        {
            using var cmd = new SQLiteCommand("SELECT id, uuid, cardsOwned, cardsForTrade, condition, language, finish FROM myCollection", conn);

            var list = new List<MyCollectionRow>();
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                var uuid = rdr["uuid"]?.ToString() ?? throw new InvalidOperationException("uuid must not be null");
                var condition = rdr["condition"]?.ToString() ?? throw new InvalidOperationException("condition must not be null");
                var language = rdr["language"]?.ToString() ?? throw new InvalidOperationException("language must not be null");
                var finish = rdr["finish"]?.ToString() ?? throw new InvalidOperationException("finish must not be null");

                list.Add(new MyCollectionRow
                {
                    CardId = rdr["id"] is long li ? (int)li : Convert.ToInt32(rdr["id"]),
                    Identity = new CollectionIdentity(uuid, condition, language, finish),
                    CardsOwned = rdr["cardsOwned"] is long lo ? (int)lo : Convert.ToInt32(rdr["cardsOwned"]),
                    CardsForTrade = rdr["cardsForTrade"] is long lt ? (int)lt : Convert.ToInt32(rdr["cardsForTrade"])
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

            object value = reader[columnName];

            // Explicit conversion for specific cases
            if (typeof(T) == typeof(int?) && value is long longValue)
            {
                return (T)(object)(int?)longValue;
            }

            return (T)value;
        }
    }
}
