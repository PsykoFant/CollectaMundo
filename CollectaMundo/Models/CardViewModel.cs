using CollectaMundo.Utilities;
using System.ComponentModel;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.Windows;
using System.Windows.Data;
using static CollectaMundo.MainWindow;
using static CollectaMundo.Models.CardSet;

namespace CollectaMundo.Models
{
    public class CardViewModel : INotifyPropertyChanged
    {
        // Core List<T> for performance
        public List<CardSet> allCards = new();
        public List<CardSet> myCards = new();
        private List<CardSet> allCardsForDecks = new();
        private List<CardSet> cardsInDecks = new();
        public Dictionary<string, string> CriteriaKeyToPropertyMap => FilterCriteriaMappings.CriteriaKeyToPropertyMap;

        // `ListCollectionView` for UI binding
        public ListCollectionView AllCardsView { get; }
        public ListCollectionView MyCardsView { get; }
        public ListCollectionView AllCardsForDecksView { get; }
        public ListCollectionView CardsInDecksView { get; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public CardViewModel()
        {
            // Bind ListCollectionView to Lists
            AllCardsView = new ListCollectionView(allCards);
            MyCardsView = new ListCollectionView(myCards);
            AllCardsForDecksView = new ListCollectionView(allCardsForDecks);
            CardsInDecksView = new ListCollectionView(cardsInDecks);
        }

        // Async method to populate data
        public async Task PopulateCardDataGridAsync(List<CardSet> cardList, ListCollectionView view, string query, DataGridContext context)
        {
            try
            {
                cardList.Clear();

                Debug.WriteLine($"Populating {context} ...");

                List<CardSet> tempCardList = new();
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
                // Instantiate appropriate subclass based on the context
                CardSet card = context switch
                {
                    DataGridContext.AllCards => new PricedCardSet(),
                    DataGridContext.MyCollection => new CardInCollection(),
                    DataGridContext.CardsInDecks => new CardInDeck(),
                    _ => new CardSet()
                };

                // ✅ Fields common to all CardSet lists
                card.Name = GetFieldValue<string>(reader, "Name") ?? string.Empty;
                card.ManaCost = ProcessManaCost(GetFieldValue<string>(reader, "ManaCost") ?? string.Empty);
                card.Colors = GetFieldValue<string>(reader, "Colors") ?? string.Empty;
                card.Type = GetFieldValue<string>(reader, "Type") ?? string.Empty;
                card.ManaValue = GetFieldValue<double?>(reader, "ManaValue") ?? 0;
                card.ManaCostImageBytes = GetFieldValue<byte[]>(reader, "ManaCostImage");
                card.ManaCostRaw = GetFieldValue<string>(reader, "ManaCost") ?? string.Empty;

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

                // ✅ Handle subclass-specific fields
                switch (card)
                {
                    case PricedCardSet pricedCard:
                        pricedCard.NormalPrice = GetFieldValue<decimal?>(reader, "NormalPrice");
                        pricedCard.FoilPrice = GetFieldValue<decimal?>(reader, "FoilPrice");
                        pricedCard.EtchedPrice = GetFieldValue<decimal?>(reader, "EtchedPrice");
                        break;

                    case CardInCollection cardInCollection:
                        cardInCollection.CardsOwned = GetFieldValue<int?>(reader, "CardsOwned") ?? 0;
                        cardInCollection.CardsForTrade = GetFieldValue<int?>(reader, "CardsForTrade") ?? 0;
                        cardInCollection.SelectedCondition = GetFieldValue<string>(reader, "Condition");
                        cardInCollection.SelectedFinish = GetFieldValue<string>(reader, "Finish");
                        cardInCollection.CardInCollectionPrice = cardInCollection.SelectedFinish switch
                        {
                            "foil" => ParsePrice("FoilPrice", reader),
                            "etched" => ParsePrice("EtchedPrice", reader),
                            _ => ParsePrice("NormalPrice", reader)
                        };
                        break;

                    case CardInDeck cardInDeck:
                        cardInDeck.Count = GetFieldValue<int?>(reader, "Count") ?? 0;
                        break;
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


        private static T? GetFieldValue<T>(DbDataReader reader, string columnName)
        {
            if (reader[columnName] == DBNull.Value)
            {
                return default;
            }

            return (T)reader[columnName];
        }

        private static string ProcessManaCost(string manaCostRaw)
        {
            char[] separator = ['{', '}'];
            return string.Join(",", manaCostRaw.Split(separator, StringSplitOptions.RemoveEmptyEntries)).Trim(',');
        }
    }
}
