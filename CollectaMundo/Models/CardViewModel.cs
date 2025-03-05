using CollectaMundo.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.Windows;
using System.Windows.Data;
using static CollectaMundo.MainWindow;

namespace CollectaMundo.Models
{
    public class CardViewModel : INotifyPropertyChanged
    {
        public static Dictionary<string, (string Property, FilterType Type, OperatorType[]? Operators, bool ShouldNotSplit)> CriteriaMappings => FilterCriteriaMappings.CriteriaMappings;

        public ObservableCollection<CardSet> ColorIcons { get; } = [];

        public List<CardSet> allCards = [];
        public List<CardSet> myCollection = [];
        public List<CardSet> allCardsForDecks = [];
        private List<CardSet> cardsInDecks = [];

        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public event PropertyChangedEventHandler? PropertyChanged;

        // `ListCollectionView` for UI binding
        public ListCollectionView AllCardsView { get; }
        public ListCollectionView MyCollectionView { get; }
        public ListCollectionView AllCardsForDecksView { get; }
        public ListCollectionView CardsInDecksView { get; }

        public CardViewModel()
        {
            // Bind ListCollectionView to Lists
            AllCardsView = new ListCollectionView(allCards);
            MyCollectionView = new ListCollectionView(myCollection);
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
                System.Windows.MessageBox.Show($"Error while loading cards: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private static CardSet CreateCardFromReader(DbDataReader reader, DataGridContext context)
        {
            try
            {
                CardSet card = new()

                {
                    // ✅ Fields common to all CardSet lists
                    Name = GetFieldValue<string>(reader, "Name") ?? string.Empty,
                    ManaCost = ProcessManaCost(GetFieldValue<string>(reader, "ManaCost") ?? string.Empty),
                    Colors = GetFieldValue<string>(reader, "Colors") ?? string.Empty,
                    Type = GetFieldValue<string>(reader, "Type") ?? string.Empty,
                    ManaValue = GetFieldValue<double?>(reader, "ManaValue") ?? 0,
                    ManaCostImageBytes = GetFieldValue<byte[]>(reader, "ManaCostImage"),
                    ManaCostRaw = GetFieldValue<string>(reader, "ManaCost") ?? string.Empty
                };

                // ✅ Fields applicable to all except CardsInDecks
                if (context != DataGridContext.CardsInDecks)
                {
                    card.Types = GetFieldValue<string>(reader, "Types") ?? string.Empty;
                    card.SuperTypes = GetFieldValue<string>(reader, "SuperTypes") ?? string.Empty;
                    card.SubTypes = GetFieldValue<string>(reader, "SubTypes") ?? string.Empty;
                    card.Keywords = GetFieldValue<string>(reader, "Keywords") ?? string.Empty;
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
                    card.SetIconBytes = GetFieldValue<byte[]>(reader, "KeyRuneImage");
                }

                // ✅ Fields only for MyCollection & CardsInDecks lists
                if (context == DataGridContext.MyCollection || context == DataGridContext.CardsInDecks)
                {
                    card.CardId = GetFieldValue<int?>(reader, "CardId");
                }

                // ✅ Fields specific for AllCards
                if (context == DataGridContext.AllCards)
                {
                    card.NormalPrice = GetFieldValue<decimal?>(reader, "NormalPrice");
                    card.FoilPrice = GetFieldValue<decimal?>(reader, "FoilPrice");
                    card.EtchedPrice = GetFieldValue<decimal?>(reader, "EtchedPrice");
                }

                // ✅ Fields specific for CardInCollection
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

                // ✅ Fields specific for CardsInDecks
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
    }


}
