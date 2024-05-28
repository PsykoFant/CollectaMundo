using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using static CardboardHoarder.CardSet;

namespace CardboardHoarder
{
    public class AddToCollectionManager
    {
        public ObservableCollection<CardSet.CardItem> cardItemsToAdd { get; private set; }

        public AddToCollectionManager(ObservableCollection<CardSet.CardItem> cardItems = null)
        {
            cardItemsToAdd = cardItems ?? new ObservableCollection<CardSet.CardItem>();
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
                    cardItemsToAdd.Remove(cardItem);
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

                    cardItemsToAdd.Add(newItem);
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

        public async void SubmitToCollection(object sender, RoutedEventArgs e)
        {
            if (DBAccess.connection == null)
            {
                MessageBox.Show("Database connection is not initialized.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return; // Exit the method to prevent further execution.
            }

            await DBAccess.connection.OpenAsync();
            try
            {
                foreach (var currentCardItem in cardItemsToAdd)
                {
                    var existingCardId = await CheckForExistingCardAsync(currentCardItem);
                    if (existingCardId.HasValue)
                    {
                        // Update the count in the database
                        string updateSql = @"UPDATE myCollection SET count = count + @newCount WHERE id = @id";
                        using (var updateCommand = new SQLiteCommand(updateSql, DBAccess.connection))
                        {
                            updateCommand.Parameters.AddWithValue("@newCount", currentCardItem.Count);
                            updateCommand.Parameters.AddWithValue("@id", existingCardId.Value);

                            await updateCommand.ExecuteNonQueryAsync();
                            // Update the item in the list
                            var cardToUpdate = MainWindow.CurrentInstance.myCards.FirstOrDefault(c => c.Uuid == currentCardItem.Uuid);
                            if (cardToUpdate != null && cardToUpdate is CardItem card)
                            {
                                card.Count += currentCardItem.Count;
                            }
                        }
                    }
                    else
                    {
                        // No existing row, insert a new one
                        string insertSql = "INSERT INTO myCollection (uuid, count, condition, language, finish) VALUES (@uuid, @count, @condition, @language, @finish)";
                        using (var insertCommand = new SQLiteCommand(insertSql, DBAccess.connection))
                        {
                            insertCommand.Parameters.AddWithValue("@uuid", currentCardItem.Uuid);
                            insertCommand.Parameters.AddWithValue("@count", currentCardItem.Count);
                            insertCommand.Parameters.AddWithValue("@condition", currentCardItem.SelectedCondition);
                            insertCommand.Parameters.AddWithValue("@language", currentCardItem.SelectedLanguage ?? "English");
                            insertCommand.Parameters.AddWithValue("@finish", currentCardItem.SelectedFinish ?? "Standard");

                            await insertCommand.ExecuteNonQueryAsync();
                        }
                    }
                }
                MessageBox.Show("Database updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to update the database: {ex.Message}");
                MessageBox.Show($"Failed to update the database: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Reload my collection
                MainWindow.CurrentInstance.MyCollectionDatagrid.ItemsSource = null;
                await MainWindow.CurrentInstance.LoadDataAsync(MainWindow.CurrentInstance.myCards, MainWindow.CurrentInstance.myCollectionQuery, MainWindow.CurrentInstance.MyCollectionDatagrid, true);
                DBAccess.connection.Close();

                cardItemsToAdd.Clear();
                MainWindow.CurrentInstance.CardsToAddListView.Visibility = Visibility.Collapsed;
                MainWindow.CurrentInstance.ButtonAddCardsToMyCollection.Visibility = Visibility.Collapsed;

            }
        }
        private async Task<int?> CheckForExistingCardAsync(CardItem cardItem)
        {
            string selectSql = @"SELECT id, count FROM myCollection WHERE uuid = @uuid AND condition = @condition AND language = @language AND finish = @finish";
            try
            {
                using (var selectCommand = new SQLiteCommand(selectSql, DBAccess.connection))
                {
                    selectCommand.Parameters.AddWithValue("@uuid", cardItem.Uuid);
                    selectCommand.Parameters.AddWithValue("@condition", cardItem.SelectedCondition);
                    selectCommand.Parameters.AddWithValue("@language", cardItem.SelectedLanguage);
                    selectCommand.Parameters.AddWithValue("@finish", cardItem.SelectedFinish);

                    using (var reader = await selectCommand.ExecuteReaderAsync())
                    {
                        if (reader.Read())
                        {
                            return reader.GetInt32(0);  // 'id' is the first column in the SELECT query
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to check for existing card: {ex.Message}");
                MessageBox.Show($"Failed to check for existing card: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);


            }
            return null; // Return null if no existing entry is found or an exception occurs
        }


    }
}
