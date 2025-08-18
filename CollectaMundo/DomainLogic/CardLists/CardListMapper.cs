using CollectaMundo.DomainLogic.CardLists.Models;
using System.Data.Common;

namespace CollectaMundo.DomainLogic.CardLists
{
    // DomainLogic layer: pure, no ADO-NET dependencies
    public static class CardListMapper
    {
        //public static CardSet FromAllCardsRow(DbDataReader r)
        //{
        //    var c = MapCommon(r);

        //    // Fields applicable to all except CardsInDecks
        //    c.Types = GetUniqueCommaSeparatedField(r, "Types");
        //    c.SuperTypes = GetUniqueCommaSeparatedField(r, "SuperTypes");
        //    c.SubTypes = GetUniqueCommaSeparatedField(r, "SubTypes");
        //    c.Keywords = GetUniqueCommaSeparatedField(r, "Keywords");
        //    c.Text = GetFieldValue<string>(r, "RulesText") ?? string.Empty;
        //    c.Side = GetFieldValue<string>(r, "Side") ?? string.Empty;

        //    // Fields applicable to all except AllCardsForDecks or CardsInDecks
        //    c.Language = GetFieldValue<string>(r, "Language") ?? string.Empty;
        //    c.Uuid = GetFieldValue<string>(r, "Uuid") ?? string.Empty;
        //    c.SetName = GetFieldValue<string>(r, "SetName") ?? string.Empty;
        //    c.Rarity = GetFieldValue<string>(r, "Rarity") ?? string.Empty;
        //    c.Finishes = GetFieldValue<string>(r, "Finishes");
        //    c.ReleaseDate = ParseDate(GetFieldValue<string>(r, "ReleaseDate"));
        //    c.KeyRuneImageBytes = GetFieldValue<byte[]>(r, "KeyRuneImage");

        //    // Fields specific for AllCards
        //    c.NormalPrice = GetFieldValue<decimal?>(r, "NormalPrice");
        //    c.FoilPrice = GetFieldValue<decimal?>(r, "FoilPrice");
        //    c.EtchedPrice = GetFieldValue<decimal?>(r, "EtchedPrice");
        //    return c;
        //}
        //public static CardSet FromMyCollectionRow(DbDataReader r)
        //{
        //    var c = MapCommon(r);

        //    // Fields only for MyCollection & CardsInDecks lists
        //    c.CardId = GetFieldValue<int?>(r, "CardId");

        //    // Fields applicable to all except CardsInDecks
        //    c.Types = GetUniqueCommaSeparatedField(r, "Types");
        //    c.SuperTypes = GetUniqueCommaSeparatedField(r, "SuperTypes");
        //    c.SubTypes = GetUniqueCommaSeparatedField(r, "SubTypes");
        //    c.Keywords = GetUniqueCommaSeparatedField(r, "Keywords");
        //    c.Text = GetFieldValue<string>(r, "RulesText") ?? string.Empty;
        //    c.Side = GetFieldValue<string>(r, "Side") ?? string.Empty;

        //    // Fields applicable to all except AllCardsForDecks or CardsInDecks
        //    c.Language = GetFieldValue<string>(r, "Language") ?? string.Empty;
        //    c.Uuid = GetFieldValue<string>(r, "Uuid") ?? string.Empty;
        //    c.SetName = GetFieldValue<string>(r, "SetName") ?? string.Empty;
        //    c.Rarity = GetFieldValue<string>(r, "Rarity") ?? string.Empty;
        //    c.Finishes = GetFieldValue<string>(r, "Finishes");
        //    c.ReleaseDate = ParseDate(GetFieldValue<string>(r, "ReleaseDate"));
        //    c.KeyRuneImageBytes = GetFieldValue<byte[]>(r, "KeyRuneImage");

        //    // Fields specific for MyCollection
        //    c.CardsOwned = GetFieldValue<int?>(r, "CardsOwned") ?? 0;
        //    c.CardsForTrade = GetFieldValue<int?>(r, "CardsForTrade") ?? 0;
        //    c.SelectedCondition = GetFieldValue<string>(r, "Condition");
        //    c.SelectedFinish = GetFieldValue<string>(r, "Finish");
        //    c.CardInCollectionPrice = c.SelectedFinish switch
        //    {
        //        "foil" => ParsePrice("FoilPrice", r),
        //        "etched" => ParsePrice("EtchedPrice", r),
        //        _ => ParsePrice("NormalPrice", r)
        //    };
        //    return c;
        //}
        public static CardSet FromAllCardsForDecksRow(DbDataReader r)
        {
            var c = MapCommon(r);

            // Fields applicable to all except CardsInDecks
            c.Types = GetUniqueCommaSeparatedField(r, "Types");
            c.SuperTypes = GetUniqueCommaSeparatedField(r, "SuperTypes");
            c.SubTypes = GetUniqueCommaSeparatedField(r, "SubTypes");
            c.Keywords = GetUniqueCommaSeparatedField(r, "Keywords");
            c.Text = GetFieldValue<string>(r, "RulesText") ?? string.Empty;
            c.Side = GetFieldValue<string>(r, "Side") ?? string.Empty;

            return c;
        }
        public static CardSet FromAllCardsInDecksRow(DbDataReader r)
        {
            var c = MapCommon(r);

            // Fields only for MyCollection & CardsInDecks lists
            c.CardId = GetFieldValue<int?>(r, "CardId");

            // Fields specific for CardsInDecks
            c.Count = GetFieldValue<int?>(r, "Count") ?? 0;

            return c;
        }
        public static CardSet FromColorIconsRow(DbDataReader r)
        {
            return new CardSet
            {
                //ManaCostImageBytes = GetFieldValue<byte[]>(r, "ManaSymbolImage"),
                ManaCostRaw = GetFieldValue<string>(r, "uniqueManaSymbol") ?? string.Empty
            };
        }
        private static CardSet MapCommon(DbDataReader r)
          => new()
          {
              // Fields common to all CardSet lists
              Name = GetFieldValue<string>(r, "Name") ?? string.Empty,
              ManaCost = ProcessManaCost(GetFieldValue<string>(r, "ManaCost") ?? string.Empty),
              Colors = GetUniqueCommaSeparatedField(r, "Colors"),
              Type = GetUniqueCommaSeparatedField(r, "Type"),
              ManaValue = GetFieldValue<double?>(r, "ManaValue") ?? 0,
              //ManaCostImageBytes = GetFieldValue<byte[]>(r, "ManaCostImage"),
              ManaCostRaw = GetFieldValue<string>(r, "ManaCost") ?? string.Empty
          };



        public static CardCore CoreFromAllCardsRow(DbDataReader r)
        {
            // Reuse your existing helpers like GetFieldValue<T>, ProcessManaCost, GetUniqueCommaSeparatedField, ParseDate
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
                SetName = GetFieldValue<string>(r, "SetName"),
                Rarity = GetFieldValue<string>(r, "Rarity"),
                Finishes = GetFieldValue<string>(r, "Finishes"),
                ReleaseDate = ParseDate(GetFieldValue<string>(r, "ReleaseDate")),
                ManaValue = GetFieldValue<double?>(r, "ManaValue") ?? 0,

                KeyRuneImageBytes = GetFieldValue<byte[]>(r, "KeyRuneImage"),
                ManaCostImageBytes = GetFieldValue<byte[]>(r, "ManaCostImage"),

                NormalPrice = GetFieldValue<decimal?>(r, "NormalPrice"),
                FoilPrice = GetFieldValue<decimal?>(r, "FoilPrice"),
                EtchedPrice = GetFieldValue<decimal?>(r, "EtchedPrice"),
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

        // Utility to parse nullable decimal price fields
        private static decimal? ParsePrice(string priceColumn, DbDataReader reader)
        {
            return decimal.TryParse(reader[priceColumn]?.ToString(), out decimal price) ? price : null;
        }

        // Utility to parse nullable DateTime fields
        private static DateTime? ParseDate(string? dateRaw)
        {
            return DateTime.TryParse(dateRaw, out DateTime parsedDate) ? parsedDate : null;
        }

    }
}
