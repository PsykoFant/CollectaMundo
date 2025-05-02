using CollectaMundo.DomainLogic.Models;
using System.Data.Common;
using System.Data.SQLite;

namespace CollectaMundo.Data
{
    public class CardListRepository : ICardListRepository
    {
        public Task<IReadOnlyList<CardSet>> GetAllCardsAsync()
            => MapCardSetsAsync(
                new SQLiteCommand("SELECT * FROM view_allCards", DBAccess.connection),
                CreateAllCardFromReader
              );

        public Task<IReadOnlyList<CardSet>> GetMyCollectionAsync()
            => MapCardSetsAsync(
                new SQLiteCommand("SELECT * FROM view_myCollection;", DBAccess.connection),
                CreateMyCollectionFromReader
              );


        // --- private methods ---
        private async Task<IReadOnlyList<CardSet>> MapCardSetsAsync(
            SQLiteCommand cmd,
            Func<DbDataReader, CardSet> mapRow
        )
        {
            var list = new List<CardSet>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(mapRow(reader));  // any exceptions bubble up
            }
            return list;
        }

        // AllCards‐specific
        private static CardSet CreateAllCardFromReader(DbDataReader reader)
        {
            var card = MapCommonProperties(reader);

            // these columns *only* exist in view_allCards
            card.NormalPrice = GetFieldValue<decimal?>(reader, "NormalPrice");
            card.FoilPrice = GetFieldValue<decimal?>(reader, "FoilPrice");
            card.EtchedPrice = GetFieldValue<decimal?>(reader, "EtchedPrice");

            return card;
        }

        // MyCollection‐specific
        private static CardSet CreateMyCollectionFromReader(DbDataReader reader)
        {
            var card = MapCommonProperties(reader);

            // these columns live in view_myCollection
            card.CardId = GetFieldValue<int?>(reader, "CardId");
            card.CardsOwned = GetFieldValue<int?>(reader, "CardsOwned") ?? 0;
            card.CardsForTrade = GetFieldValue<int?>(reader, "CardsForTrade") ?? 0;
            card.SelectedCondition = GetFieldValue<string>(reader, "Condition");
            card.SelectedFinish = GetFieldValue<string>(reader, "Finish");
            // …etc if needed…

            return card;
        }

        // shared between both
        private static CardSet MapCommonProperties(DbDataReader reader)
            => new CardSet
            {
                Name = GetFieldValue<string>(reader, "Name") ?? string.Empty,
                ManaCost = ProcessManaCost(GetFieldValue<string>(reader, "ManaCost") ?? string.Empty),
                Colors = GetUniqueCommaSeparatedField(reader, "Colors"),
                Type = GetUniqueCommaSeparatedField(reader, "Type"),
                ManaValue = GetFieldValue<double?>(reader, "ManaValue") ?? 0,
                ManaCostImageBytes = GetFieldValue<byte[]>(reader, "ManaCostImage"),
                ManaCostRaw = GetFieldValue<string>(reader, "ManaCost") ?? string.Empty,

                // and *unconditional* AllCards columns—since they exist in both views you can safely assign:
                Types = GetUniqueCommaSeparatedField(reader, "Types"),
                SuperTypes = GetUniqueCommaSeparatedField(reader, "SuperTypes"),
                SubTypes = GetUniqueCommaSeparatedField(reader, "SubTypes"),
                Keywords = GetUniqueCommaSeparatedField(reader, "Keywords"),
                Text = GetFieldValue<string>(reader, "RulesText") ?? string.Empty,
                Side = GetFieldValue<string>(reader, "Side") ?? string.Empty,
                Language = GetFieldValue<string>(reader, "Language") ?? string.Empty,
                Uuid = GetFieldValue<string>(reader, "Uuid") ?? string.Empty,
                SetName = GetFieldValue<string>(reader, "SetName") ?? string.Empty,
                Rarity = GetFieldValue<string>(reader, "Rarity") ?? string.Empty,
                Finishes = GetFieldValue<string>(reader, "Finishes"),
                ReleaseDate = ParseDate(GetFieldValue<string>(reader, "ReleaseDate")),
                KeyRuneImageBytes = GetFieldValue<byte[]>(reader, "KeyRuneImage"),
            };




        // --- helper methods ---
        private static string ProcessManaCost(string raw)
        {
            // split "{X}{Y}" → ["X","Y"]
            var parts = raw.Split(new[] { '{', '}' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(",", parts).Trim(',');
        }

        private static string GetUniqueCommaSeparatedField(DbDataReader reader, string columnName)
        {
            var raw = GetFieldValue<string>(reader, columnName);
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            var uniques = raw
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase);
            return string.Join(",", uniques);
        }

        private static T? GetFieldValue<T>(DbDataReader reader, string columnName)
        {
            if (reader[columnName] == DBNull.Value) return default;
            object v = reader[columnName]!;
            // SQLite returns longs for INTEGER, so convert if necessary
            if (typeof(T) == typeof(int?) && v is long l)
                return (T)(object)(int?)l;
            return (T)v;
        }

        private static DateTime? ParseDate(string? raw) => DateTime.TryParse(raw, out var dt) ? dt : null;
    }
}
