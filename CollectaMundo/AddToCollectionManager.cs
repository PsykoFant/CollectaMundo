﻿using CollectaMundo.Models;
using ServiceStack;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using static CollectaMundo.Models.CardSet;

namespace CollectaMundo
{
    public class AddToCollectionManager
    {
        private static AddToCollectionManager? _instance;
        public static AddToCollectionManager Instance => _instance ??= new AddToCollectionManager();
        public ObservableCollection<CardItem> CardItemsToAdd { get; private set; }
        public ObservableCollection<CardItem> CardItemsToEdit { get; private set; }

        // Timer for delayed processing
        private readonly System.Timers.Timer _typingTimer;
        private const int TypingDelay = 500; // 500 milliseconds delay
        private TextBox? _lastTextBox;
        private ObservableCollection<CardItem>? _lastTargetCollection;

        public AddToCollectionManager()
        {
            CardItemsToAdd = [];
            CardItemsToEdit = [];

            // Initialize the timer
            _typingTimer = new System.Timers.Timer(TypingDelay);
            _typingTimer.Elapsed += TypingTimer_Elapsed;
            _typingTimer.AutoReset = false; // Ensure the timer runs only once per typing event
        }

        // Handling typing numbers directly into count and trade fields
        public void CardsOwnedTextHandler(object sender, ObservableCollection<CardItem> targetCollection)
        {
            _lastTextBox = sender as TextBox;
            _lastTargetCollection = targetCollection;

            if (_typingTimer.Enabled)
            {
                _typingTimer.Stop();
            }

            _typingTimer.Start(); // Restart the timer with each keystroke
        }
        private void TypingTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs? e)
        {
            if (sender == null || e == null)
            {
                return; // Safeguard against potential nulls, though they shouldn't be null
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                CardsOwnedTextChangedLogic(_lastTextBox, _lastTargetCollection);
            });
        }
        private static void CardsOwnedTextChangedLogic(TextBox? textBox, ObservableCollection<CardItem>? targetCollection)
        {
            if (textBox?.DataContext is CardItem cardItem)
            {
                // Try parsing the new value
                if (int.TryParse(textBox.Text, out int newCount) && newCount >= 0)
                {
                    // Update CardsOwned with the parsed value
                    cardItem.CardsOwned = newCount;

                    // Adjust CardsForTrade if necessary
                    if (cardItem.CardsOwned < cardItem.CardsForTrade)
                    {
                        cardItem.CardsForTrade = cardItem.CardsOwned;
                    }

                    // If CardsOwned drops to zero or below, remove the item
                    if (cardItem.CardsOwned <= 0 && targetCollection != null)
                    {
                        targetCollection.Remove(cardItem);
                    }
                }
                else
                {
                    // If not valid, reset to the previous valid value
                    textBox.Text = cardItem.CardsOwned.ToString();
                }
            }
        }
        public static void CardsForTradeTextHandler(object sender)
        {
            var textBox = sender as TextBox;
            if (textBox?.DataContext is CardItem cardItem)
            {
                // Use the TextBox's binding expression to check for validation errors
                var bindingExpression = textBox.GetBindingExpression(TextBox.TextProperty);
                if (bindingExpression.HasError)
                {
                    // Reset to the previous valid value if there's a validation error
                    textBox.Text = cardItem.CardsForTrade.ToString();
                }
                else
                {
                    // Try parsing the new value
                    if (int.TryParse(textBox.Text, out int newCount) && newCount >= 0)
                    {
                        // Update CardsOwned with the parsed value
                        cardItem.CardsForTrade = newCount;

                        // Adjust CardsForTrade if necessary
                        if (cardItem.CardsOwned < cardItem.CardsForTrade)
                        {
                            cardItem.CardsForTrade = cardItem.CardsOwned;
                        }

                    }
                    else
                    {
                        // If not valid, reset to the previous valid value
                        textBox.Text = cardItem.CardsOwned.ToString();
                    }
                }
            }
        }

        // Plus and minus buttons for card owned or cards for trade
        public void IncrementButtonHandler(object sender, RoutedEventArgs e)
        {
            // Retrieve the DataContext (bound item) of the button that was clicked
            var button = sender as Button;

            if (button?.DataContext is CardItem cardItem)
            {
                // Check the Tag property to determine which field to increment
                if (button.Tag?.ToString() == "CardsOwned")
                {
                    cardItem.CardsOwned++;
                }
                else if (button.Tag?.ToString() == "CardsForTrade")
                {
                    if (cardItem.CardsForTrade < cardItem.CardsOwned)
                    {
                        cardItem.CardsForTrade++;
                    }
                }
            }
        }
        public void DecrementButtonHandler(object sender, ObservableCollection<CardItem> targetCollection)
        {
            var button = sender as Button;
            if (button?.DataContext is CardItem cardItem)
            {
                // Decrease the count
                if (button.Tag?.ToString() == "CardsOwned")
                {
                    cardItem.CardsOwned--;
                    if (cardItem.CardsOwned < cardItem.CardsForTrade)
                    {
                        cardItem.CardsForTrade--;
                    }
                }
                else if (button.Tag?.ToString() == "CardsForTrade" && cardItem.CardsForTrade != 0)
                {
                    cardItem.CardsForTrade--;
                }

                // Check if the count has dropped to zero or below
                if (targetCollection == CardItemsToAdd)
                {
                    if (cardItem.CardsOwned <= 0)
                    {
                        // Remove the card item from the specified ObservableCollection
                        targetCollection.Remove(cardItem);
                    }
                }
            }
        }

        // Adds cards to the listview
        public static void AddCardsToListView(DataGrid dataGrid, Action showListViewAction, ObservableCollection<CardItem> cardItemsCollection)
        {
            // Show the corresponding list view (either for adding or editing)
            showListViewAction();

            // Handle selected cards based on the provided data grid
            foreach (CardSet selectedCard in dataGrid.SelectedItems)
            {
                AddOrEditCardHandler(selectedCard, cardItemsCollection);
            }

            // Unselect all items after handling
            dataGrid.UnselectAll();
        }
        public static async void AddOrEditCardHandler(CardSet selectedCard, ObservableCollection<CardItem> targetCollection)
        {
            if (selectedCard.Uuid == null)
            {
                MessageBox.Show("Card UUID is null, cannot fetch languages.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return; // Exit if UUID is null to prevent errors
            }

            try
            {
                // Check if the card with the same UUID already exists in the collection
                var existingCard = targetCollection.FirstOrDefault(card => card.Uuid == selectedCard.Uuid);
                if (existingCard != null)
                {
                    return; // Exit if the card already exists in the collection
                }

                await DBAccess.OpenConnectionAsync();
                var languages = await FetchLanguagesForCardAsync(selectedCard.Uuid);
                var finishes = await FetchFinishesForCardAsync(selectedCard.Uuid);
                DBAccess.CloseConnection();

                var newItem = new CardItem
                {
                    Name = selectedCard.Name,
                    SetName = selectedCard.SetName,
                    Uuid = selectedCard.Uuid,
                    CardsOwned = 1,
                    CardsForTrade = 0,
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
                    newItem.CardsForTrade = cardItem.CardsForTrade;
                    newItem.SelectedFinish = cardItem.SelectedFinish;
                    newItem.SelectedCondition = cardItem.SelectedCondition;
                }

                targetCollection.Add(newItem);
                AdjustColumnWidths();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to add card to collection: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"AddOrEditCardHandler error: {ex.Message}");
            }
        }
        public static void ShowCardsToAddListView()
        {
            MainWindow.CurrentInstance.AddStatusScrollViewer.Visibility = Visibility.Collapsed;
            MainWindow.CurrentInstance.CardsToAddListView.Visibility = Visibility.Visible;
            MainWindow.CurrentInstance.ButtonSubmitCardsToMyCollection.Visibility = Visibility.Visible;
            MainWindow.CurrentInstance.ButtonClearCardsToAdd.Visibility = Visibility.Visible;
        }
        public static void ShowCardsToEditListView()
        {
            MainWindow.CurrentInstance.EditStatusScrollViewer.Visibility = Visibility.Collapsed;
            MainWindow.CurrentInstance.CardsToEditListView.Visibility = Visibility.Visible;
            MainWindow.CurrentInstance.ButtonSubmitCardEditsInMyCollection.Visibility = Visibility.Visible;
            MainWindow.CurrentInstance.ButtonClearCardsToEdit.Visibility = Visibility.Visible;
        }
        public static void HideCardsToAddListView(bool showLogo)
        {
            MainWindow.CurrentInstance.LogoSmall.Visibility = showLogo ? Visibility.Visible : Visibility.Collapsed;
            MainWindow.CurrentInstance.CardsToAddListView.Visibility = Visibility.Collapsed;
            MainWindow.CurrentInstance.ButtonSubmitCardsToMyCollection.Visibility = Visibility.Collapsed;
            MainWindow.CurrentInstance.ButtonClearCardsToAdd.Visibility = Visibility.Collapsed;
        }
        public static void HideCardsToEditListView(bool showLogo)
        {
            MainWindow.CurrentInstance.LogoSmall.Visibility = showLogo ? Visibility.Visible : Visibility.Collapsed;
            MainWindow.CurrentInstance.CardsToEditListView.Visibility = Visibility.Collapsed;
            MainWindow.CurrentInstance.ButtonSubmitCardEditsInMyCollection.Visibility = Visibility.Collapsed;
            MainWindow.CurrentInstance.ButtonClearCardsToEdit.Visibility = Visibility.Collapsed;
        }
        private static async Task<List<string>> FetchLanguagesForCardAsync(string? uuid)
        {
            if (string.IsNullOrEmpty(uuid))
            {
                return ["English"]; // Return default list if UUID is null or empty
            }

            List<string> languages = [];
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
                using var reader = await command.ExecuteReaderAsync();
                while (reader.Read())
                {
                    string? language = reader["language"] as string; // Safely cast to string, which may be null
                    if (!string.IsNullOrEmpty(language))
                    {
                        languages.Add(language); // Only add non-null and non-empty strings
                    }
                }
            }
            return languages;
        }
        private static async Task<List<string>> FetchFinishesForCardAsync(string uuid)
        {
            var finishes = new List<string>();
            string query = @"SELECT finishes FROM cards WHERE uuid = @uuid UNION SELECT finishes FROM tokens WHERE uuid = @uuid";

            using (var command = new SQLiteCommand(query, DBAccess.connection))
            {
                command.Parameters.AddWithValue("@uuid", uuid);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var finish = reader["Finishes"]?.ToString();
                    if (!string.IsNullOrEmpty(finish))
                    {
                        finishes.AddRange(finish.Split(',').Select(f => f.Trim()));
                    }
                }
            }

            // Remove "signed" if it exists
            finishes = finishes.Distinct().Where(f => !f.Equals("signed", StringComparison.OrdinalIgnoreCase)).ToList();

            // Check if both "foil" and "etched" are present
            if (finishes.Contains("foil", StringComparer.OrdinalIgnoreCase) && finishes.Contains("etched", StringComparer.OrdinalIgnoreCase))
            {
                // If both "foil" and "etched" are present, return only "etched"
                finishes = finishes.Where(f => !f.Equals("foil", StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return finishes;
        }

        // Submit new or edited cards to collection
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
                foreach (var currentCardItem in CardItemsToAdd)
                {
                    var existingCardId = await CheckForExistingCardAsync(currentCardItem);
                    if (existingCardId.HasValue)
                    {
                        // Update the count in the database
                        string updateSql = @"UPDATE myCollection SET count = count + @newCount, trade = trade + @newTradeCount WHERE id = @id";
                        using var updateCommand = new SQLiteCommand(updateSql, DBAccess.connection);
                        updateCommand.Parameters.AddWithValue("@newCount", currentCardItem.CardsOwned);
                        updateCommand.Parameters.AddWithValue("@newTradeCount", currentCardItem.CardsForTrade);
                        updateCommand.Parameters.AddWithValue("@id", existingCardId.Value);

                        await updateCommand.ExecuteNonQueryAsync();
                        // Update the item in the list
                        var cardToUpdate = MainWindow.CurrentInstance.myCards.FirstOrDefault(c => c.Uuid == currentCardItem.Uuid);
                        if (cardToUpdate != null && cardToUpdate is CardItem card)
                        {
                            card.CardsOwned += currentCardItem.CardsOwned;
                        }
                    }
                    else
                    {
                        // No existing row, insert a new one
                        string insertSql = "INSERT INTO myCollection (uuid, count, trade, condition, language, finish) VALUES (@uuid, @count, @trade, @condition, @language, @finish)";
                        using var insertCommand = new SQLiteCommand(insertSql, DBAccess.connection);
                        insertCommand.Parameters.AddWithValue("@uuid", currentCardItem.Uuid);
                        insertCommand.Parameters.AddWithValue("@count", currentCardItem.CardsOwned);
                        insertCommand.Parameters.AddWithValue("@trade", currentCardItem.CardsForTrade);
                        insertCommand.Parameters.AddWithValue("@condition", currentCardItem.SelectedCondition);
                        insertCommand.Parameters.AddWithValue("@language", currentCardItem.Language);
                        insertCommand.Parameters.AddWithValue("@finish", currentCardItem.SelectedFinish);

                        await insertCommand.ExecuteNonQueryAsync();
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
                var cardDetails = CardItemsToAdd.Select(card =>
                    $"- {card.Name} (Condition: {card.SelectedCondition}, Language: {card.Language}, Finish: {card.SelectedFinish}, Cards owned: {card.CardsOwned}, Cards for trade: {card.CardsForTrade})")
                    .Aggregate((current, next) => current + "\n" + next);

                MainWindow.CurrentInstance.AddStatusScrollViewer.Visibility = Visibility.Visible;
                MainWindow.CurrentInstance.AddStatusTextBlock.Text = "Added the following cards to your collection:\n\n" + cardDetails;
                HideCardsToAddListView(false);

                CardItemsToAdd.Clear();

                // Reload my collection
                MainWindow.CurrentInstance.MyCollectionDataGrid.ItemsSource = null;
                await MainWindow.PopulateCardDataGridAsync(MainWindow.CurrentInstance.myCards, MainWindow.CurrentInstance.myCollectionQuery, MainWindow.CurrentInstance.MyCollectionDataGrid, true);
                await MainWindow.CurrentInstance.PopulateFilterUiElements();

                DBAccess.connection.Close();
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
                foreach (var currentCardItem in CardItemsToEdit)
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
                        using var deleteCommand = new SQLiteCommand(deleteSql, DBAccess.connection);
                        deleteCommand.Parameters.AddWithValue("@id", currentCardItem.CardId);
                        await deleteCommand.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        // If the count is set to 0, delete the card from myCollection
                        if (currentCardItem.CardsOwned == 0)
                        {
                            Debug.WriteLine($"CardsOwned set to 0, deleting card with id {currentCardItem.CardId}");

                            string deleteSql = "DELETE FROM myCollection WHERE id = @id";
                            using var deleteCommand = new SQLiteCommand(deleteSql, DBAccess.connection);
                            deleteCommand.Parameters.AddWithValue("@id", currentCardItem.CardId);
                            await deleteCommand.ExecuteNonQueryAsync();
                        }
                        else
                        {
                            Debug.WriteLine($"No card like this exists already - updating card with id {currentCardItem.CardId}");
                            // If there's no matching existing card ID, update the card in myCollection
                            string updateSql = @"UPDATE myCollection SET count = @count, trade = @trade, condition = @condition, language = @language, finish = @finish WHERE id = @cardId";
                            using var updateCommand = new SQLiteCommand(updateSql, DBAccess.connection);
                            updateCommand.Parameters.AddWithValue("@count", currentCardItem.CardsOwned);
                            updateCommand.Parameters.AddWithValue("@trade", currentCardItem.CardsForTrade);
                            updateCommand.Parameters.AddWithValue("@condition", currentCardItem.SelectedCondition);
                            updateCommand.Parameters.AddWithValue("@language", currentCardItem.Language);
                            updateCommand.Parameters.AddWithValue("@finish", currentCardItem.SelectedFinish);
                            updateCommand.Parameters.AddWithValue("@cardId", currentCardItem.CardId);

                            await updateCommand.ExecuteNonQueryAsync();
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
                var cardDetails = CardItemsToEdit.Select(card =>
                    card.CardsOwned == 0
                        ? $"{card.Name} - DELETED FROM COLLECTION"  // Display this message if card count is zero
                        : $"- {card.Name} (Condition: {card.SelectedCondition}, Language: {card.Language}, Finish: {card.SelectedFinish}, Cards owned: {card.CardsOwned}, Cards for trade: {card.CardsForTrade})")
                    .Aggregate((current, next) => current + "\n" + next);

                // Set the detailed string with linebreaks to the TextBlock
                MainWindow.CurrentInstance.EditStatusScrollViewer.Visibility = Visibility.Visible;
                MainWindow.CurrentInstance.EditStatusTextBlock.Text = "Edited the following cards in your collection:\n\n" + cardDetails;
                HideCardsToEditListView(false);

                CardItemsToEdit.Clear();

                // Reload my collection
                MainWindow.CurrentInstance.MyCollectionDataGrid.ItemsSource = null;
                await MainWindow.PopulateCardDataGridAsync(MainWindow.CurrentInstance.myCards, MainWindow.CurrentInstance.myCollectionQuery, MainWindow.CurrentInstance.MyCollectionDataGrid, true);
                await MainWindow.CurrentInstance.PopulateFilterUiElements();

                MainWindow.CurrentInstance.ApplyFilterSelection();

                DBAccess.connection.Close();
            }
        }
        private static async Task<int?> CheckForExistingCardAsync(CardItem card)
        {
            string selectSql = @"
                SELECT id FROM myCollection 
                WHERE uuid = @uuid 
                  AND condition = @condition 
                  AND language = @language 
                  AND finish = @finish";
            try
            {
                using var selectCommand = new SQLiteCommand(selectSql, DBAccess.connection);
                selectCommand.Parameters.AddWithValue("@uuid", card.Uuid);
                selectCommand.Parameters.AddWithValue("@condition", card.SelectedCondition);
                selectCommand.Parameters.AddWithValue("@language", card.Language);
                selectCommand.Parameters.AddWithValue("@finish", card.SelectedFinish);

                using var reader = await selectCommand.ExecuteReaderAsync();
                if (reader.Read())
                {
                    return reader.GetInt32(0); // 'id' is the first column in the SELECT query
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to check for existing card: {ex.Message}");
                MessageBox.Show($"Failed to check for existing card: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return null; // Return null if no existing entry is found or an exception occurs
        }

        // Right-click specific operations
        public static async void SubmitNewCardsToCollectionWithDefaultValues(List<CardSet> selectedCards)
        {
            if (DBAccess.connection == null)
            {
                MessageBox.Show("Database connection is not initialized.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await DBAccess.connection.OpenAsync();
            try
            {
                foreach (CardSet card in selectedCards)
                {
                    CardItem currentCardItem = card as CardItem ?? new CardItem
                    {
                        Uuid = card.Uuid,
                        Name = card.Name,
                        Language = "English", // Default language
                        SelectedCondition = "Near Mint", // Default condition
                        SelectedFinish = "nonfoil" // Default finish
                    };

                    // Fetch finishes if it's not a predefined nonfoil
                    if (currentCardItem.SelectedFinish == "nonfoil" && currentCardItem.Uuid != null)
                    {
                        var finishes = await FetchFinishesForCardAsync(currentCardItem.Uuid);
                        currentCardItem.SelectedFinish = finishes.Contains("nonfoil") ? "nonfoil" : finishes.FirstOrDefault() ?? "nonfoil";
                    }

                    // Fetch languages if it's not a predefined English
                    if (currentCardItem.Language == "English" && currentCardItem.Uuid != null)
                    {
                        var languages = await FetchLanguagesForCardAsync(currentCardItem.Uuid);
                        currentCardItem.Language = languages.Contains("English") ? "English" : languages.FirstOrDefault() ?? "English";
                    }

                    var existingCardId = await CheckForExistingCardAsync(currentCardItem);
                    if (existingCardId.HasValue)
                    {
                        string updateSql = @"UPDATE myCollection SET count = count + 1 WHERE id = @id";
                        using var updateCommand = new SQLiteCommand(updateSql, DBAccess.connection);
                        updateCommand.Parameters.AddWithValue("@id", existingCardId.Value);
                        await updateCommand.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        string insertSql = "INSERT INTO myCollection (uuid, count, trade, condition, language, finish) VALUES (@uuid, 1, 0, @condition, @language, @finish)";
                        using var insertCommand = new SQLiteCommand(insertSql, DBAccess.connection);
                        insertCommand.Parameters.AddWithValue("@uuid", currentCardItem.Uuid);
                        insertCommand.Parameters.AddWithValue("@condition", currentCardItem.SelectedCondition);
                        insertCommand.Parameters.AddWithValue("@language", currentCardItem.Language);
                        insertCommand.Parameters.AddWithValue("@finish", currentCardItem.SelectedFinish);
                        await insertCommand.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to add card with default values: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"Failed to add card with default values: {ex.Message}");
            }
            finally
            {
                // Provide update of the operation
                var cardDetails = selectedCards.Select(card =>
                    $"- {card.Name}").Aggregate((current, next) => current + "\n" + next);

                MainWindow.CurrentInstance.AddStatusScrollViewer.Visibility = Visibility.Visible;
                MainWindow.CurrentInstance.AddStatusTextBlock.Text = "Added the following cards with default values to your collection:\n\n" + cardDetails;

                // Reload the collection
                MainWindow.CurrentInstance.MyCollectionDataGrid.ItemsSource = null;
                await MainWindow.PopulateCardDataGridAsync(MainWindow.CurrentInstance.myCards, MainWindow.CurrentInstance.myCollectionQuery, MainWindow.CurrentInstance.MyCollectionDataGrid, true);
                await MainWindow.CurrentInstance.PopulateFilterUiElements();

                DBAccess.connection.Close();
            }
        }
        public async void DeleteCardsFromCollection(List<CardItem> selectedCards)
        {
            if (DBAccess.connection == null)
            {
                MessageBox.Show("Database connection is not initialized.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await DBAccess.connection.OpenAsync();
            try
            {
                foreach (CardItem card in selectedCards)
                {
                    // Delete card from database (myCollection)
                    string deleteSql = "DELETE FROM myCollection WHERE uuid = @uuid;";
                    using var deleteCommand = new SQLiteCommand(deleteSql, DBAccess.connection);
                    deleteCommand.Parameters.AddWithValue("@uuid", card.Uuid);
                    await deleteCommand.ExecuteNonQueryAsync();

                    // Check if CardItemsToEdit contains the card and remove it if found
                    var cardToEdit = CardItemsToEdit.FirstOrDefault(editCard => editCard.Uuid == card.Uuid);
                    if (cardToEdit != null)
                    {
                        CardItemsToEdit.Remove(cardToEdit);

                        if (CardItemsToEdit.Count == 0)
                        {
                            HideCardsToEditListView(true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete cards: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"Failed to delete cards: {ex.Message}");
            }
            finally
            {
                // Provide update of the operation
                var cardDetails = selectedCards.Select(card =>
                    $"- {card.Name}").Aggregate((current, next) => current + "\n" + next);

                MainWindow.CurrentInstance.EditStatusScrollViewer.Visibility = Visibility.Visible;
                MainWindow.CurrentInstance.EditStatusTextBlock.Text = "Deleted the following cards from your collection:\n\n" + cardDetails;

                // Reload the collection
                MainWindow.CurrentInstance.MyCollectionDataGrid.ItemsSource = null;
                await MainWindow.PopulateCardDataGridAsync(MainWindow.CurrentInstance.myCards, MainWindow.CurrentInstance.myCollectionQuery, MainWindow.CurrentInstance.MyCollectionDataGrid, true);
                await MainWindow.CurrentInstance.PopulateFilterUiElements();

                DBAccess.connection.Close();
            }
        }
        public async void SetCardsForTrade(List<CardItem> selectedCards, bool setForTrade)
        {
            if (DBAccess.connection == null)
            {
                MessageBox.Show("Database connection is not initialized.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await DBAccess.connection.OpenAsync();
            try
            {
                string sqlString = string.Empty;

                if (setForTrade)
                {
                    sqlString = "UPDATE myCollection SET trade = count WHERE uuid = @uuid;";
                }
                else
                {
                    sqlString = "UPDATE myCollection SET trade = 0 WHERE uuid = @uuid;";
                }


                foreach (CardItem card in selectedCards)
                {

                    using var setForTradeCommand = new SQLiteCommand(sqlString, DBAccess.connection);
                    setForTradeCommand.Parameters.AddWithValue("@uuid", card.Uuid);
                    await setForTradeCommand.ExecuteNonQueryAsync();

                    // Check if CardItemsToEdit contains the card and remove it if found
                    var cardToEdit = CardItemsToEdit.FirstOrDefault(editCard => editCard.Uuid == card.Uuid);
                    if (cardToEdit != null)
                    {
                        CardItemsToEdit.Remove(cardToEdit);

                        if (CardItemsToEdit.Count == 0)
                        {
                            HideCardsToEditListView(true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update trade count: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"Failed to update trade count: {ex.Message}");
            }
            finally
            {
                // Provide update of the operation
                var cardDetails = selectedCards.Select(card =>
                    $"- {card.Name}").Aggregate((current, next) => current + "\n" + next);

                MainWindow.CurrentInstance.EditStatusScrollViewer.Visibility = Visibility.Visible;
                MainWindow.CurrentInstance.EditStatusTextBlock.Text = "Updated trade status for the collowing cards:\n\n" + cardDetails;

                // Reload the collection
                MainWindow.CurrentInstance.MyCollectionDataGrid.ItemsSource = null;
                await MainWindow.PopulateCardDataGridAsync(MainWindow.CurrentInstance.myCards, MainWindow.CurrentInstance.myCollectionQuery, MainWindow.CurrentInstance.MyCollectionDataGrid, true);

                DBAccess.connection.Close();

                MainWindow.CurrentInstance.ApplyFilterSelection();
            }
        }


        // Adjust listviews column widths so text is not clipped
        public static void AdjustColumnWidths()
        {
            AdjustListViewColumnWidths(MainWindow.CurrentInstance.CardsToAddListView);
            AdjustListViewColumnWidths(MainWindow.CurrentInstance.CardsToEditListView);
        }
        private static void AdjustListViewColumnWidths(ListView listView)
        {
            if (listView.View is GridView gridView)
            {
                foreach (var column in gridView.Columns)
                {
                    // Measure the width of the column header
                    if (double.IsNaN(column.Width))
                    {
                        column.Width = column.ActualWidth;
                    }

                    // Reset the width to Auto (NaN) to resize according to content
                    column.Width = double.NaN;
                }
            }
        }

    }
}
