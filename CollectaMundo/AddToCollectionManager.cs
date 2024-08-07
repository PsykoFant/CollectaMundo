﻿using ServiceStack;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using static CollectaMundo.CardSet;

namespace CollectaMundo
{
    public class AddToCollectionManager
    {
        private static AddToCollectionManager? _instance;
        public static AddToCollectionManager Instance => _instance ?? (_instance = new AddToCollectionManager());
        public ObservableCollection<CardSet.CardItem> cardItemsToAdd { get; private set; }
        public ObservableCollection<CardSet.CardItem> cardItemsToEdit { get; private set; }
        public AddToCollectionManager()
        {
            cardItemsToAdd = new ObservableCollection<CardSet.CardItem>();
            cardItemsToEdit = new ObservableCollection<CardSet.CardItem>();
        }
        public void IncrementCount_Click(object sender, RoutedEventArgs e)
        {
            // Retrieve the DataContext (bound item) of the button that was clicked
            var button = sender as Button;
            if (button?.DataContext is CardSet.CardItem cardItem)
            {
                // Increment the count
                cardItem.CardsOwned++;
            }
        }
        public void DecrementCount_Click(object sender, RoutedEventArgs e, ObservableCollection<CardSet.CardItem> targetCollection)
        {
            var button = sender as Button;
            if (button?.DataContext is CardSet.CardItem cardItem)
            {
                // Decrease the count
                cardItem.CardsOwned--;

                // Check if the count has dropped to zero or below

                if (targetCollection == cardItemsToAdd)
                {
                    if (cardItem.CardsOwned <= 0)
                    {
                        // Remove the card item from the specified ObservableCollection
                        targetCollection.Remove(cardItem);
                    }
                }
            }
        }
        public async void EditOrAddCard_Click(object sender, RoutedEventArgs e, ObservableCollection<CardSet.CardItem> targetCollection)
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

                    // Open the connection asynchronously and fetch languages
                    await DBAccess.OpenConnectionAsync();
                    var languages = await FetchLanguagesForCardAsync(selectedCard.Uuid);
                    var finishes = await FetchFinishesForCardAsync(selectedCard.Uuid);
                    DBAccess.CloseConnection();

                    var newItem = new CardSet.CardItem
                    {
                        Name = selectedCard.Name,
                        SetName = selectedCard.SetName,
                        Uuid = selectedCard.Uuid,
                        CardsOwned = 1,
                        AvailableFinishes = finishes,
                        SelectedFinish = finishes.FirstOrDefault(),
                        Language = selectedCard.Language,
                        OtherLanguages = languages,
                        SelectedCondition = "Near Mint",
                    };

                    // Adjust properties if the selected card is to edit an existing card item.
                    if (selectedCard is CardItem cardItem)
                    {
                        newItem.CardId = cardItem.CardId;
                        newItem.CardsOwned = cardItem.CardsOwned;
                        newItem.SelectedFinish = cardItem.SelectedFinish;
                        newItem.SelectedCondition = cardItem.SelectedCondition;
                    }

                    targetCollection.Add(newItem);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to add card to collection: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Debug.WriteLine($"EditOrAddCard_Click error: {ex.Message}");
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
            // Updated query to select language from both 'cardForeignData' and 'cards' tables
            string query = @"
                SELECT language FROM cardForeignData WHERE uuid = @uuid
                UNION
                SELECT language FROM cards WHERE uuid = @uuid
                UNION
                SELECT language FROM tokens WHERE uuid = @uuid";
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
        private async Task<List<string>> FetchFinishesForCardAsync(string uuid)
        {
            var finishes = new List<string>();
            string query = @"SELECT finishes FROM cards WHERE uuid = @uuid UNION SELECT finishes FROM tokens WHERE uuid = @uuid";
            using (var command = new SQLiteCommand(query, DBAccess.connection))
            {
                command.Parameters.AddWithValue("@uuid", uuid);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (reader.Read())
                    {
                        var finish = reader["Finishes"].ToString();
                        if (!string.IsNullOrEmpty(finish))
                        {
                            finishes.AddRange(finish.Split(',').Select(f => f.Trim()));
                        }
                    }
                }
            }
            return finishes.Distinct().ToList();
        }
        public async void SubmitNewCardsToCollection(object sender, RoutedEventArgs e)
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
                            updateCommand.Parameters.AddWithValue("@newCount", currentCardItem.CardsOwned);
                            updateCommand.Parameters.AddWithValue("@id", existingCardId.Value);

                            await updateCommand.ExecuteNonQueryAsync();
                            // Update the item in the list
                            var cardToUpdate = MainWindow.CurrentInstance.myCards.FirstOrDefault(c => c.Uuid == currentCardItem.Uuid);
                            if (cardToUpdate != null && cardToUpdate is CardItem card)
                            {
                                card.CardsOwned += currentCardItem.CardsOwned;
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
                            insertCommand.Parameters.AddWithValue("@count", currentCardItem.CardsOwned);
                            insertCommand.Parameters.AddWithValue("@condition", currentCardItem.SelectedCondition);
                            insertCommand.Parameters.AddWithValue("@language", currentCardItem.Language);
                            insertCommand.Parameters.AddWithValue("@finish", currentCardItem.SelectedFinish ?? "nonfoil");

                            await insertCommand.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to update the database: {ex.Message}");
                MessageBox.Show($"Failed to update the database: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Provide update of the operation
                var cardDetails = cardItemsToAdd.Select(card =>
                    $"- {card.Name} (CardsOwned: {card.CardsOwned}, Condition: {card.SelectedCondition}, Language: {card.Language}, Finish: {card.SelectedFinish})")
                    .Aggregate((current, next) => current + "\n" + next);

                // Set the detailed string with linebreaks to the TextBlock
                MainWindow.CurrentInstance.AddStatusTextBlock.Visibility = Visibility.Visible;
                MainWindow.CurrentInstance.AddStatusTextBlock.Text = "Added the following cards to your collection:\n\n" + cardDetails;

                // Reload my collection
                MainWindow.CurrentInstance.MyCollectionDatagrid.ItemsSource = null;
                await MainWindow.CurrentInstance.LoadDataAsync(MainWindow.CurrentInstance.myCards, MainWindow.CurrentInstance.myCollectionQuery, MainWindow.CurrentInstance.MyCollectionDatagrid, true);
                DBAccess.connection.Close();

                cardItemsToAdd.Clear();
                MainWindow.CurrentInstance.CardsToAddListView.Visibility = Visibility.Collapsed;
                MainWindow.CurrentInstance.ButtonAddCardsToMyCollection.Visibility = Visibility.Collapsed;

            }
        }
        public async void SubmitEditedCardsToCollection(object sender, RoutedEventArgs e)
        {
            if (DBAccess.connection == null)
            {
                MessageBox.Show("Database connection is not initialized.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await DBAccess.connection.OpenAsync();
            try
            {
                foreach (var currentCardItem in cardItemsToEdit)
                {
                    var existingCardId = await CheckForExistingCardAsync(currentCardItem);
                    if (existingCardId.HasValue && existingCardId != currentCardItem.CardId)
                    {
                        // Update the existing row in the myCollection table if it is not the same card
                        string updateSql = @"UPDATE myCollection SET count = count + @newCount WHERE id = @id";
                        using (var updateCommand = new SQLiteCommand(updateSql, DBAccess.connection))
                        {
                            updateCommand.Parameters.AddWithValue("@newCount", currentCardItem.CardsOwned);
                            updateCommand.Parameters.AddWithValue("@id", existingCardId.Value);

                            await updateCommand.ExecuteNonQueryAsync();
                        }

                        // Additionally, remove the currentCardItem from the table as it's being edited and updated
                        string deleteSql = "DELETE FROM myCollection WHERE id = @id";
                        using (var deleteCommand = new SQLiteCommand(deleteSql, DBAccess.connection))
                        {
                            deleteCommand.Parameters.AddWithValue("@id", currentCardItem.CardId);
                            await deleteCommand.ExecuteNonQueryAsync();
                        }
                    }
                    else
                    {
                        // If the count is set to 0, delete the card from myCollection
                        if (currentCardItem.CardsOwned == 0)
                        {
                            Debug.WriteLine($"CardsOwned set to 0, deleting card with id {currentCardItem.CardId}");

                            string deleteSql = "DELETE FROM myCollection WHERE id = @id";
                            using (var deleteCommand = new SQLiteCommand(deleteSql, DBAccess.connection))
                            {
                                deleteCommand.Parameters.AddWithValue("@id", currentCardItem.CardId);
                                await deleteCommand.ExecuteNonQueryAsync();
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"No card like this exists already - updating card with id {currentCardItem.CardId}");
                            // If there's no matching existing card ID, update the card in myCollection
                            string updateSql = @"UPDATE myCollection SET count = @count, condition = @condition, language = @language, finish = @finish WHERE id = @cardId";
                            using (var updateCommand = new SQLiteCommand(updateSql, DBAccess.connection))
                            {
                                updateCommand.Parameters.AddWithValue("@count", currentCardItem.CardsOwned);
                                updateCommand.Parameters.AddWithValue("@condition", currentCardItem.SelectedCondition);
                                updateCommand.Parameters.AddWithValue("@language", currentCardItem.Language);
                                updateCommand.Parameters.AddWithValue("@finish", currentCardItem.SelectedFinish);
                                updateCommand.Parameters.AddWithValue("@cardId", currentCardItem.CardId);

                                await updateCommand.ExecuteNonQueryAsync();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to update the database: {ex.Message}");
                MessageBox.Show($"Failed to update the database: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Provide update of the operation
                var cardDetails = cardItemsToEdit.Select(card =>
                    card.CardsOwned == 0
                        ? $"{card.Name} - DELETED FROM COLLECTION"  // Display this message if card count is zero
                        : $"- {card.Name} (CardsOwned: {card.CardsOwned}, Condition: {card.SelectedCondition}, Language: {card.Language}, Finish: {card.SelectedFinish})")
                    .Aggregate((current, next) => current + "\n" + next);

                // Set the detailed string with linebreaks to the TextBlock
                MainWindow.CurrentInstance.EditStatusTextBlock.Visibility = Visibility.Visible;
                MainWindow.CurrentInstance.EditStatusTextBlock.Text = "Edited the following cards in your collection:\n\n" + cardDetails;

                // Reload my collection
                MainWindow.CurrentInstance.MyCollectionDatagrid.ItemsSource = null;
                await MainWindow.CurrentInstance.LoadDataAsync(MainWindow.CurrentInstance.myCards, MainWindow.CurrentInstance.myCollectionQuery, MainWindow.CurrentInstance.MyCollectionDatagrid, true);
                DBAccess.connection.Close();

                MainWindow.CurrentInstance.ApplyFilterSelection();

                cardItemsToEdit.Clear();
                MainWindow.CurrentInstance.CardsToEditListView.Visibility = Visibility.Collapsed;
                MainWindow.CurrentInstance.ButtonEditCardsInMyCollection.Visibility = Visibility.Collapsed;
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
                    selectCommand.Parameters.AddWithValue("@language", cardItem.Language);
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
