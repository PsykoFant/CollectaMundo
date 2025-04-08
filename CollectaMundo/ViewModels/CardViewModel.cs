using CollectaMundo.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.Windows;
using static CollectaMundo.MainWindow;

namespace CollectaMundo.ViewModels
{
    public class CardViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // Cards lists for AllCardsDataGrid
        public List<CardSet> Cards { get; set; } = [];

        private List<CardSet> _filteredCards = [];
        public List<CardSet> FilteredCards
        {
            get => _filteredCards;
            set
            {
                if (_filteredCards != value)
                {
                    _filteredCards = value;
                    OnPropertyChanged(nameof(FilteredCards));
                }
            }
        }


        // MyCollecdtion lists for MyCollectionDataGrid
        //public List<CardSet> MyCollection { get; set; } = [];

        //private List<CardSet> _filteredMyCollection = [];
        //public List<CardSet> FilteredMyCollection
        //{
        //    get => _filteredMyCollection;
        //    set
        //    {
        //        if (_filteredMyCollection != value)
        //        {
        //            _filteredMyCollection = value;
        //            OnPropertyChanged(nameof(FilteredMyCollection));
        //        }
        //    }
        //}


        //// AllCardsForDecks lists for AllCardsForDecksDataGrid

        //public List<CardSet> AllCardsForDecks { get; set; } = [];

        //private List<CardSet> _filteredAllCardsForDecks = [];
        //public List<CardSet> FilteredAllCardsForDecks
        //{
        //    get => _filteredAllCardsForDecks;
        //    set
        //    {
        //        if (_filteredAllCardsForDecks != value)
        //        {
        //            _filteredAllCardsForDecks = value;
        //            OnPropertyChanged(nameof(FilteredAllCardsForDecks));
        //        }
        //    }
        //}

        // For displaying list of color icons for color filtering
        public ObservableCollection<CardSet> ColorIcons { get; } = [];


        // Async method to populate data
        public static async Task PopulateCardDataGridAsync(List<CardSet> cardList, string query, DataGridContext context)
        {
            try
            {
                cardList.Clear();
                Debug.WriteLine($"Populating {context} ...");

                List<CardSet> tempCardList = [];
                using SQLiteCommand command = new SQLiteCommand(query, DBAccess.connection);
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

                // Fields specific for Cards
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
    }
}
