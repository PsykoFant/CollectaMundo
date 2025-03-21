using CollectaMundo.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Data;
using static CollectaMundo.MainWindow;

namespace CollectaMundo.ViewModels
{
    public class CardViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<CardSet> ColorIcons { get; } = [];

        private readonly List<CardSet> _allCards = [];
        private readonly List<CardSet> _myCollection = [];
        public List<CardSet> allCardsForDecks = [];
        private List<CardSet> cardsInDecks = [];

        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public event PropertyChangedEventHandler? PropertyChanged;

        // Properties for binding to card count label
        public List<CardSet> AllCards => _allCards;
        public List<CardSet> MyCollection => _myCollection;

        // `ListCollectionView` for Datagrid UI binding
        public ListCollectionView AllCardsView { get; }
        public ListCollectionView MyCollectionView { get; }
        public ListCollectionView AllCardsForDecksView { get; }
        public ListCollectionView CardsInDecksView { get; }

        public CardViewModel()
        {
            // Bind ListCollectionView to Lists
            AllCardsView = new ListCollectionView(_allCards);
            MyCollectionView = new ListCollectionView(_myCollection);
            AllCardsForDecksView = new ListCollectionView(allCardsForDecks);
            CardsInDecksView = new ListCollectionView(cardsInDecks);
        }

        // Async method to populate data
        public static async Task PopulateCardDataGridAsync(List<CardSet> cardList, ListCollectionView view, string query, DataGridContext context)
        {
            try
            {
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

                // Refresh the ListCollectionView to reflect new data
                view.Refresh();

                Debug.WriteLine($"Loaded {cardList.Count} cards into {context}");

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while loading cards: {ex.Message}");
                MessageBox.Show($"Error while loading cards: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private static CardSet CreateCardFromReader(DbDataReader reader, DataGridContext context)
        {
            try
            {
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

                // ✅ Fields applicable to all except CardsInDecks
                if (context != DataGridContext.CardsInDecks)
                {
                    card.Types = GetUniqueCommaSeparatedField(reader, "Types");
                    card.SuperTypes = GetUniqueCommaSeparatedField(reader, "SuperTypes");
                    card.SubTypes = GetUniqueCommaSeparatedField(reader, "SubTypes");
                    card.Keywords = GetUniqueCommaSeparatedField(reader, "Keywords");
                    card.Text = GetFieldValue<string>(reader, "RulesText") ?? string.Empty;
                    card.Side = GetFieldValue<string>(reader, "Side") ?? string.Empty;
                }

                // ✅ Fields applicable to all except AllCardsForDecks or CardsInDecks
                if (context != DataGridContext.AllCardsForDecks && context != DataGridContext.CardsInDecks)
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
                if (context == DataGridContext.MyCollection || context == DataGridContext.CardsInDecks)
                {
                    card.CardId = GetFieldValue<int?>(reader, "CardId");
                }

                // Fields specific for AllCards
                if (context == DataGridContext.AllCards)
                {
                    card.NormalPrice = GetFieldValue<decimal?>(reader, "NormalPrice");
                    card.FoilPrice = GetFieldValue<decimal?>(reader, "FoilPrice");
                    card.EtchedPrice = GetFieldValue<decimal?>(reader, "EtchedPrice");
                }

                // Fields specific for CardInCollection
                if (context == DataGridContext.MyCollection)
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
                if (context == DataGridContext.CardsInDecks)
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
            // Utility to process ManaCost string
            static string ProcessManaCost(string manaCostRaw)
            {
                char[] separator = ['{', '}'];
                return string.Join(",", manaCostRaw.Split(separator, StringSplitOptions.RemoveEmptyEntries)).Trim(',');
            }


            static string GetUniqueCommaSeparatedField(DbDataReader reader, string columnName)
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
            static T? GetFieldValue<T>(DbDataReader reader, string columnName)
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
            static decimal? ParsePrice(string priceColumn, DbDataReader reader)
            {
                return decimal.TryParse(reader[priceColumn]?.ToString(), out decimal price) ? price : null;
            }

            // Utility to parse nullable DateTime fields
            static DateTime? ParseDate(string? dateRaw)
            {
                return DateTime.TryParse(dateRaw, out DateTime parsedDate) ? parsedDate : null;
            }
        }
        public async Task LoadColorIconsAsync()
        {
            string query = "SELECT * FROM uniqueManaSymbols WHERE uniqueManaSymbol IN ('W', 'U', 'B', 'R', 'G', 'C', 'X') " +
                           "ORDER BY CASE uniqueManaSymbol WHEN 'W' THEN 1 WHEN 'U' THEN 2 WHEN 'B' THEN 3 WHEN 'R' THEN 4 " +
                           "WHEN 'G' THEN 5 WHEN 'C' THEN 6 WHEN 'X' THEN 7 END;";

            try
            {
                List<CardSet> tempCardList = [];
                using SQLiteCommand command = new(query, DBAccess.connection);
                using DbDataReader reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    tempCardList.Add(CreateColorIcon(reader));
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ColorIcons.Clear();
                    foreach (var item in tempCardList)
                    {
                        ColorIcons.Add(item);
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while loading color icons: {ex.Message}");
            }
        }
        private static CardSet CreateColorIcon(DbDataReader reader)
        {
            return new CardSet
            {
                ManaCostImageBytes = reader["ManaSymbolImage"] as byte[],
                ManaCostRaw = reader["uniqueManaSymbol"]?.ToString() ?? string.Empty
            };
        }

        // Debug
        public void DebugRandomCards(int numberOfCards = 1)
        {
            if (_allCards == null || _allCards.Count == 0)
            {
                Debug.WriteLine("No cards loaded.");
                return;
            }

            // Create a new Random instance.
            Random random = new Random();

            // Get as many cards as we have (up to numberOfCards)
            int count = Math.Min(numberOfCards, _allCards.Count);

            // Select count random cards
            var randomCards = _allCards.OrderBy(card => random.Next()).Take(count);

            // Define the list of property names you want to output.
            string[] propertiesToOutput =
            [
        "Name", "SetName", "ReleaseDate", "KeyRuneImage", "ManaCost", "ManaCostImage",
        "Types", "Colors", "SuperTypes", "SubTypes", "Type", "Keywords", "Text", // assuming "RulesText" is stored in "Text"
        "ManaValue", "Language", "Uuid", "Finishes", "Side", "Rarity",
        "CardsOwned", "CardsForTrade", "SelectedCondition"
            ];

            Debug.WriteLine($"Displaying {count} random cards out of {_allCards.Count}:");

            // Iterate over the random cards.
            foreach (var card in randomCards)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("----- Card -----");
                foreach (var propName in propertiesToOutput)
                {
                    // Try to get the property by name.
                    PropertyInfo? prop = typeof(CardSet).GetProperty(propName);

                    if (prop == null)
                    {
                        sb.AppendLine($"{propName}: <Not found>");
                        continue;
                    }

                    try
                    {
                        object? value = prop.GetValue(card);
                        if (value is System.Collections.IEnumerable enumerable && !(value is string))
                        {
                            List<string> items = new List<string>();
                            foreach (var item in enumerable)
                            {
                                items.Add(item?.ToString() ?? "null");
                            }
                            sb.AppendLine($"{propName}: [{string.Join(", ", items)}]");
                        }
                        else
                        {
                            sb.AppendLine($"{propName}: {value?.ToString() ?? "null"}");
                        }
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"{propName}: Error retrieving value ({ex.Message})");
                    }
                }
                Debug.WriteLine(sb.ToString());
            }
        }
        public void DebugCardByName(string cardName)
        {
            if (string.IsNullOrWhiteSpace(cardName))
            {
                Debug.WriteLine("No card name supplied.");
                return;
            }

            // Search for a card by name (case-insensitive)
            var card = _allCards.FirstOrDefault(c =>
                !string.IsNullOrWhiteSpace(c.Name) &&
                c.Name.Equals(cardName, StringComparison.OrdinalIgnoreCase));

            if (card == null)
            {
                Debug.WriteLine($"No card found with name: {cardName}");
                return;
            }

            // Define the list of properties to output.
            string[] propertiesToOutput =
            [
                "Name",
                "SetName",
                "ReleaseDate",
                "KeyRuneImage", // if applicable (e.g. the property holding the key rune image)
                "ManaCost",
                "ManaCostImage", // if applicable
                "Types",
                "Colors",
                "SuperTypes",
                "SubTypes",
                "Type",
                "Keywords",
                "Text", // assuming this holds the RulesText
                "ManaValue",
                "Language",
                "Uuid",
                "Finishes",
                "Side",
                "Rarity",
                "CardsOwned",
                "CardsForTrade",
                "SelectedCondition",
                "SelectedFinish"
            ];

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("----- Debug Card -----");
            foreach (var propName in propertiesToOutput)
            {
                // Use reflection to get the property.
                PropertyInfo? prop = typeof(CardSet).GetProperty(propName);
                if (prop == null)
                {
                    sb.AppendLine($"{propName}: <Not found>");
                    continue;
                }

                try
                {
                    object? value = prop.GetValue(card);
                    if (value is System.Collections.IEnumerable enumerable && !(value is string))
                    {
                        List<string> items = new List<string>();
                        foreach (var item in enumerable)
                        {
                            items.Add(item?.ToString() ?? "null");
                        }
                        sb.AppendLine($"{propName}: [{string.Join(", ", items)}]");
                    }
                    else
                    {
                        sb.AppendLine($"{propName}: {value?.ToString() ?? "null"}");
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"{propName}: Error retrieving value ({ex.Message})");
                }
            }
            Debug.WriteLine(sb.ToString());
        }

    }


}
