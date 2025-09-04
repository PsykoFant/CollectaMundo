using CollectaMundo.DomainLogic.CardLists.Models;
using System.Data.Common;
using System.Data.SQLite;

namespace CollectaMundo.Data.CardLists
{
    public class CardListRepository() : ICardListRepository
    {
        public async Task<IReadOnlyList<CardCore>> ReadAllCardsCoresAsync(SQLiteConnection conn)
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
            var list = new List<CardCore>(capacity: 120000);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(CoreFromAllCardsRow(reader));
            }
            return list;
        }
        public async Task<List<MyCollectionRow>> ReadMyCollectionAsync(SQLiteConnection conn)
        {
            using var cmd = new SQLiteCommand("SELECT id, uuid, cardsOwned, cardsForTrade, condition, language, finish FROM myCollection", conn);

            var list = new List<MyCollectionRow>();
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                list.Add(new MyCollectionRow
                {
                    Id = rdr["id"] is long li ? (int)li : (int)(rdr["id"] ?? 0),
                    Uuid = rdr["uuid"]?.ToString() ?? "",
                    CardsOwned = rdr["cardsOwned"] is long lo ? (int)lo : (int)(rdr["cardsOwned"] ?? 0),
                    CardsForTrade = rdr["cardsForTrade"] is long lt ? (int)lt : (int)(rdr["cardsForTrade"] ?? 0),
                    Condition = rdr["condition"]?.ToString(),
                    Language = rdr["language"]?.ToString(),
                    Finish = rdr["finish"]?.ToString()
                });
            }
            return list;
        }
        private static CardCore CoreFromAllCardsRow(DbDataReader r)
        {
            return new CardCore
            {
                Name = GetFieldValue<string>(r, "Name") ?? "",
                ManaCostRaw = GetFieldValue<string>(r, "ManaCost"),
                ManaCost = ProcessManaCost(GetFieldValue<string>(r, "ManaCost") ?? ""),
                Colors = GetUniqueCommaSeparatedField(r, "Colors"),
                Type = GetUniqueCommaSeparatedField(r, "Type"),
                Types = GetUniqueCommaSeparatedField(r, "Types"),
                SuperTypes = GetUniqueCommaSeparatedField(r, "SuperTypes"),
                SubTypes = GetUniqueCommaSeparatedField(r, "SubTypes"),
                Keywords = GetUniqueCommaSeparatedField(r, "Keywords"),
                Text = GetFieldValue<string>(r, "RulesText"),
                Side = GetFieldValue<string>(r, "Side"),
                Language = GetFieldValue<string>(r, "Language"),
                Uuid = GetFieldValue<string>(r, "Uuid") ?? "",
                OtherFaceIds = ParseOtherFaceIds(GetFieldValue<string>(r, "OtherIDs")),
                SetCode = GetFieldValue<string>(r, "SetCode"),
                Rarity = GetFieldValue<string>(r, "Rarity"),
                Finishes = GetFieldValue<string>(r, "Finishes"),
                ManaValue = GetFieldValue<double?>(r, "ManaValue") ?? 0,
            };
        }

        // Utility to process ManaCost string
        private static string ProcessManaCost(string manaCostRaw)
        {
            char[] separator = ['{', '}'];
            return string.Join(",", manaCostRaw.Split(separator, StringSplitOptions.RemoveEmptyEntries)).Trim(',');
        }
        private static string GetUniqueCommaSeparatedField(DbDataReader reader, string columnName)
        {
            // Get the raw string (using our existing generic method)
            string? rawValue = GetFieldValue<string>(reader, columnName);
            if (string.IsNullOrEmpty(rawValue))
            {
                return string.Empty;
            }

            // Split on commas, trim, deduplicate (case-insensitive), and rejoin.
            var uniqueItems = rawValue
                .Split([','], StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase);

            return string.Join(",", uniqueItems);
        }

        // Utility to safely retrieve field values
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
        private static List<string> ParseOtherFaceIds(string? raw)
        {
            return string.IsNullOrWhiteSpace(raw)
                ? []
                : raw.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
        }


    }
}
