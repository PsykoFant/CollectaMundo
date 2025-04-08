using CollectaMundo.Models;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.Windows;
using static CollectaMundo.MainWindow;

namespace CollectaMundo.Managers
{
    internal class CardListManager
    {
        private static readonly string colorIconsQuery = "SELECT * FROM uniqueManaSymbols WHERE uniqueManaSymbol IN ('W', 'U', 'B', 'R', 'G', 'C', 'X') ORDER BY CASE uniqueManaSymbol WHEN 'W' THEN 1 WHEN 'U' THEN 2 WHEN 'B' THEN 3 WHEN 'R' THEN 4 WHEN 'G' THEN 5 WHEN 'C' THEN 6 WHEN 'X' THEN 7 END;";
        public static async Task CreateCardListObjectAsync(List<CardSet> cardList, CardListObject context)
        {
            try
            {
                string query = context switch
                {
                    CardListObject.AllCards => "SELECT * FROM view_allCards",
                    CardListObject.MyCollection => "SELECT * FROM view_myCollection;",
                    CardListObject.AllCardsForDecks => "SELECT * FROM view_allCardsForDecks;",
                    CardListObject.CardsInDecks => "SELECT * FROM view_cardsInDecks;",
                    CardListObject.ColorIcons => colorIconsQuery,
                    _ => throw new ArgumentOutOfRangeException(nameof(context), $"Invalid context: {context}")
                };

                cardList.Clear();
                Debug.WriteLine($"Populating {context} ...");

                List<CardSet> tempCardList = [];
                using SQLiteCommand command = new(query, DBAccess.connection);
                using DbDataReader reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    try
                    {
                        CardSet card = CreateCardFromReader(reader, context);
                        tempCardList.Add(card);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error while creating card: {ex.Message}");
                        throw;
                    }
                }

                cardList.AddRange(tempCardList);
                Debug.WriteLine($"Loaded {cardList.Count} cards into {context}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while loading cards: {ex.Message}");
                MessageBox.Show($"Error while loading cards: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private static CardSet CreateCardFromReader(DbDataReader reader, CardListObject context)
        {
            try
            {
                if (context == CardListObject.ColorIcons)
                {
                    // Use the specialized logic for color icons.
                    return new CardSet
                    {
                        ManaCostImageBytes = reader["ManaSymbolImage"] as byte[],
                        ManaCostRaw = reader["uniqueManaSymbol"]?.ToString() ?? string.Empty
                    };
                }

                CardSet card = new()

                {
                    // Fields common to all CardSet lists
                    Name = GetFieldValue<string>(reader, "Name") ?? string.Empty,
                    ManaCost = ProcessManaCost(GetFieldValue<string>(reader, "ManaCost") ?? string.Empty),
                    Colors = GetUniqueCommaSeparatedField(reader, "Colors"),
                    Type = GetUniqueCommaSeparatedField(reader, "Type"),
                    ManaValue = GetFieldValue<double?>(reader, "ManaValue") ?? 0,
                    ManaCostImageBytes = GetFieldValue<byte[]>(reader, "ManaCostImage"),
                    ManaCostRaw = GetFieldValue<string>(reader, "ManaCost") ?? string.Empty
                };

                // Fields applicable to all except CardsInDecks
                if (context != CardListObject.CardsInDecks)
                {
                    card.Types = GetUniqueCommaSeparatedField(reader, "Types");
                    card.SuperTypes = GetUniqueCommaSeparatedField(reader, "SuperTypes");
                    card.SubTypes = GetUniqueCommaSeparatedField(reader, "SubTypes");
                    card.Keywords = GetUniqueCommaSeparatedField(reader, "Keywords");
                    card.Text = GetFieldValue<string>(reader, "RulesText") ?? string.Empty;
                    card.Side = GetFieldValue<string>(reader, "Side") ?? string.Empty;
                }

                // Fields applicable to all except AllCardsForDecks or CardsInDecks
                if (context != CardListObject.AllCardsForDecks && context != CardListObject.CardsInDecks)
                {
                    card.Language = GetFieldValue<string>(reader, "Language") ?? string.Empty;
                    card.Uuid = GetFieldValue<string>(reader, "Uuid") ?? string.Empty;
                    card.SetName = GetFieldValue<string>(reader, "SetName") ?? string.Empty;
                    card.Rarity = GetFieldValue<string>(reader, "Rarity") ?? string.Empty;
                    card.Finishes = GetFieldValue<string>(reader, "Finishes");
                    card.ReleaseDate = ParseDate(GetFieldValue<string>(reader, "ReleaseDate"));

                    // Populate raw data fields for parallel processing
                    card.KeyRuneImageBytes = GetFieldValue<byte[]>(reader, "KeyRuneImage");
                }

                // Fields only for MyCollection & CardsInDecks lists
                if (context == CardListObject.MyCollection || context == CardListObject.CardsInDecks)
                {
                    card.CardId = GetFieldValue<int?>(reader, "CardId");
                }

                // Fields specific for AllCards
                if (context == CardListObject.AllCards)
                {
                    card.NormalPrice = GetFieldValue<decimal?>(reader, "NormalPrice");
                    card.FoilPrice = GetFieldValue<decimal?>(reader, "FoilPrice");
                    card.EtchedPrice = GetFieldValue<decimal?>(reader, "EtchedPrice");
                }

                // Fields specific for MyCollection
                if (context == CardListObject.MyCollection)
                {
                    card.CardsOwned = GetFieldValue<int?>(reader, "CardsOwned") ?? 0;
                    card.CardsForTrade = GetFieldValue<int?>(reader, "CardsForTrade") ?? 0;
                    card.SelectedCondition = GetFieldValue<string>(reader, "Condition");
                    card.SelectedFinish = GetFieldValue<string>(reader, "Finish");
                    card.CardInCollectionPrice = card.SelectedFinish switch
                    {
                        "foil" => ParsePrice("FoilPrice", reader),
                        "etched" => ParsePrice("EtchedPrice", reader),
                        _ => ParsePrice("NormalPrice", reader)
                    };
                }

                // Fields specific for CardsInDecks
                if (context == CardListObject.CardsInDecks)
                {
                    card.Count = GetFieldValue<int?>(reader, "Count") ?? 0;
                }

                return card;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in CreateCardFromReader: {ex.Message}");
                throw;
            }
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
