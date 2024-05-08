using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace CardboardHoarder
{
    public class AddToCollectionManager
    {
        private ObservableCollection<CardSet.CardItem> cardItems;
        public AddToCollectionManager(ObservableCollection<CardSet.CardItem> cardItems)
        {
            this.cardItems = cardItems;
        }
        public void IncrementCount_Click(object sender, RoutedEventArgs e)
        {
            // Retrieve the DataContext (bound item) of the button that was clicked
            var button = sender as Button;
            if (button?.DataContext is CardSet.CardItem cardItem)
            {
                // Increment the count
                cardItem.Count++;
            }
        }
        public void DecrementCount_Click(object sender, RoutedEventArgs e)
        {
            // Retrieve the DataContext (bound item) of the button that was clicked
            var button = sender as Button;
            if (button?.DataContext is CardSet.CardItem cardItem)
            {
                // Decrease the count
                cardItem.Count--;

                // Check if the count has dropped to zero or below
                if (cardItem.Count <= 0)
                {
                    // Find and remove the card item from the ObservableCollection
                    cardItems.Remove(cardItem);
                }
            }
        }
        public async void AddToCollection_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is CardSet selectedCard)
            {
                if (selectedCard.Uuid == null)
                {
                    MessageBox.Show("Card UUID is null, cannot fetch languages.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return; // Exit if UUID is null to prevent errors
                }

                try
                {
                    var finishes = selectedCard.Finishes?.Split(',')
                                     .Select(f => f.Trim())
                                     .ToList() ?? new List<string>();

                    // Open the connection asynchronously and fetch languages
                    await DBAccess.OpenConnectionAsync();
                    var languages = await FetchLanguagesForCardAsync(selectedCard.Uuid);
                    DBAccess.CloseConnection();

                    languages.Insert(0, "English"); // Ensure "English" is always an option and default

                    var newItem = new CardSet.CardItem
                    {
                        Name = selectedCard.Name,
                        SetName = selectedCard.SetName,
                        Uuid = selectedCard.Uuid,
                        Count = 1,
                        AvailableFinishes = finishes,
                        SelectedFinish = finishes.FirstOrDefault(),
                        Languages = languages,
                        SelectedLanguage = "English"
                    };

                    cardItems.Add(newItem);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to add card to collection: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Debug.WriteLine($"AddToCollection_Click error: {ex.Message}");
                }
            }
        }
        private async Task<List<string>> FetchLanguagesForCardAsync(string? uuid)
        {
            if (string.IsNullOrEmpty(uuid))
            {
                return new List<string> { "English" }; // Return default list if UUID is null or empty
            }

            List<string> languages = new List<string>();
            string query = "SELECT language FROM cardForeignData WHERE uuid = @uuid";
            using (var command = new SQLiteCommand(query, DBAccess.connection))
            {
                command.Parameters.AddWithValue("@uuid", uuid);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (reader.Read())
                    {
                        string? language = reader["language"] as string; // Safely cast to string, which may be null
                        if (!string.IsNullOrEmpty(language))
                        {
                            languages.Add(language); // Only add non-null and non-empty strings
                        }
                    }
                }
            }
            return languages;
        }

    }
}
