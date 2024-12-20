using CollectaMundo.Models;
using System.Data.SQLite;
using System.Diagnostics;
using System.Windows;

namespace CollectaMundo
{
    public class DeckManager
    {
        private static readonly string loadDeckCardsQuery = $"SELECT * FROM view_cardsInDecks WHERE DeckId = {MainWindow.CurrentInstance.CurrentDeck.DeckId};";
        public static async Task LoadDeck(int deckId)
        {
            try
            {
                string loadDeckQuery = @"SELECT deckName, deckDescription, targetFormat FROM myDecks WHERE id = @id;";

                await DBAccess.OpenConnectionAsync();

                using var command = new SQLiteCommand(loadDeckQuery, DBAccess.connection);
                command.Parameters.AddWithValue("@id", deckId);
                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync()) // Ensure there's a row to read
                {
                    string deckName = reader["deckName"]?.ToString() ?? string.Empty;
                    string deckDescription = reader["deckDescription"]?.ToString() ?? string.Empty;
                    string targetFormat = reader["targetFormat"]?.ToString() ?? string.Empty;

                    // Update the currentDeck object
                    MainWindow.CurrentInstance.CurrentDeck = new Deck
                    {
                        DeckId = deckId,
                        DeckName = deckName,
                        Description = deckDescription,
                        TargetFormat = targetFormat
                    };

                    //Fill deck datagrid with cards                    
                    Debug.WriteLine(loadDeckCardsQuery);
                    await MainWindow.PopulateCardDataGridAsync(MainWindow.CurrentInstance.cardsInDecks, loadDeckCardsQuery, MainWindow.CurrentInstance.DeckDataGrid);

                    // Go to deck Editor to edit new deck
                    MainWindow.CurrentInstance.DeckNameTextBox.Text = deckName;
                    MainWindow.CurrentInstance.DeckDescriptionTextBox.Text = deckDescription;
                    MainWindow.CurrentInstance.DeckFormatTextBox.Text = $"Target format: {targetFormat}";
                    MainWindow.CurrentInstance.ExistingDeckFormatComboBox.SelectedItem = targetFormat;

                    MainWindow.CurrentInstance.GridDecksOverview.Visibility = Visibility.Collapsed;
                    MainWindow.CurrentInstance.HeadlineDecks.Content = "Deck Editor";
                    MainWindow.CurrentInstance.GridTopMenu.IsEnabled = false;
                    MainWindow.CurrentInstance.GridFiltering.Visibility = Visibility.Visible;
                    MainWindow.CurrentInstance.GridCardImages.Visibility = Visibility.Visible;
                    MainWindow.CurrentInstance.GridDeckEditor.Visibility = Visibility.Visible;
                }
                else
                {
                    Debug.WriteLine($"No deck found with ID: {deckId}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading deck: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"Error loading deck: {ex}");
            }
            finally
            {
                DBAccess.CloseConnection();
            }

        }
        public static async Task SubmitNewDeck()
        {
            try
            {
                // Get values from UI elements
                int deckId = 0;
                string deckName = MainWindow.CurrentInstance.AddDeckNameTextBox.Text?.Trim() ?? string.Empty;
                string deckDescription = MainWindow.CurrentInstance.AddDeckDescriptionTextBox.Text?.Trim() ?? string.Empty;
                string targetFormat = MainWindow.CurrentInstance.NewDeckFormatComboBox.SelectedItem?.ToString() ?? string.Empty;

                // Validate input
                if (string.IsNullOrWhiteSpace(deckName))
                {
                    MessageBox.Show("Your deck must have a name. ", "Oopsie", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // SQL to insert into myDecks table
                string insertSql = @"INSERT INTO myDecks (deckName, deckDescription, targetFormat) VALUES (@deckName, @deckDescription, @targetFormat);";

                // SQL to fetch the last inserted DeckId
                string getIdSql = @"SELECT last_insert_rowid();";

                await DBAccess.OpenConnectionAsync();

                if (DBAccess.connection == null)
                {
                    throw new InvalidOperationException("Database connection is not initialized.");
                }

                using SQLiteTransaction transaction = DBAccess.connection.BeginTransaction();

                try
                {
                    using SQLiteCommand insertCommand = new(insertSql, DBAccess.connection, transaction);

                    // Bind parameters
                    insertCommand.Parameters.AddWithValue("@deckName", deckName);
                    insertCommand.Parameters.AddWithValue("@deckDescription", deckDescription);
                    insertCommand.Parameters.AddWithValue("@targetFormat", targetFormat);

                    // Execute the command
                    await insertCommand.ExecuteNonQueryAsync();

                    // Get the last inserted DeckId
                    using SQLiteCommand getIdCommand = new(getIdSql, DBAccess.connection, transaction);
                    deckId = Convert.ToInt32(await getIdCommand.ExecuteScalarAsync());

                    // Update the currentDeck object
                    MainWindow.CurrentInstance.CurrentDeck = new Deck
                    {
                        DeckId = deckId,
                        DeckName = deckName,
                        Description = deckDescription,
                        TargetFormat = targetFormat
                    };

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw new InvalidOperationException("Failed to insert new deck.", ex);
                }

                // Clear input fields and reset UI after successful insertion
                MainWindow.CurrentInstance.AddDeckNameTextBox.Text = string.Empty;
                MainWindow.CurrentInstance.AddDeckDescriptionTextBox.Text = string.Empty;
                MainWindow.CurrentInstance.NewDeckFormatComboBox.SelectedIndex = -1;
                MainWindow.CurrentInstance.AddDeckButton.Visibility = Visibility.Visible;
                MainWindow.CurrentInstance.GridAddNewDeckForm.Visibility = Visibility.Collapsed;

                // Go to deck Editor to edit new deck
                await LoadDeck(deckId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while adding the deck: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"Error in SubmitNewDeckButton_Click: {ex}");
            }
            finally
            {
                DBAccess.CloseConnection();
            }
        }
        public static async Task DeleteDeck(int deckId)
        {
            try
            {
                string deleteDeckQuery = @"DELETE from myDecks WHERE id = @id;";

                await DBAccess.OpenConnectionAsync();

                using var command = new SQLiteCommand(deleteDeckQuery, DBAccess.connection);
                command.Parameters.AddWithValue("@id", deckId);
                using var reader = await command.ExecuteReaderAsync();

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting deck: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"Error deleting deck: {ex}");
            }
            finally
            {
                DBAccess.CloseConnection();
            }

        }
        public static async Task<bool> UpdateDeckInfo(string columnToUpdate, string valueToUpdate)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(valueToUpdate) && columnToUpdate == "deckName")
                {
                    MessageBox.Show("Your deck must have a name. ", "Oopsie", MessageBoxButton.OK, MessageBoxImage.Warning);
                    MainWindow.CurrentInstance.DeckNameTextBox.Text = MainWindow.CurrentInstance.CurrentDeck.DeckName;
                    return false;
                }

                string updateQuery = $"UPDATE myDecks SET {columnToUpdate} = @value WHERE id = @deckId;";

                await DBAccess.OpenConnectionAsync();

                if (DBAccess.connection == null)
                {
                    throw new InvalidOperationException("Database connection is not initialized.");
                }

                using SQLiteTransaction transaction = DBAccess.connection.BeginTransaction();

                try
                {
                    using SQLiteCommand insertCommand = new(updateQuery, DBAccess.connection, transaction);

                    // Bind parameters
                    insertCommand.Parameters.AddWithValue("@value", valueToUpdate);
                    insertCommand.Parameters.AddWithValue("@deckId", MainWindow.CurrentInstance.CurrentDeck.DeckId);

                    // Execute the command
                    await insertCommand.ExecuteNonQueryAsync();

                    transaction.Commit();
                    return true;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw new InvalidOperationException("Failed to update deck.", ex);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while adding the deck: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"Error in SubmitNewDeckButton_Click: {ex}");
                return false;
            }
            finally
            {
                DBAccess.CloseConnection();
            }
        }
        public static async Task SubmitCardToDeck(string cardName, int deckId)
        {
            try
            {
                await DBAccess.OpenConnectionAsync();

                // Check if the card already exists in the deck
                string checkQuery = @"SELECT count FROM cardsInDecks WHERE deckId = @deckId AND name = @name;";
                string updateQuery = @"UPDATE cardsInDecks SET count = count + 1 WHERE deckId = @deckId AND name = @name;";
                string insertQuery = @"INSERT INTO cardsInDecks (deckId, name, count) VALUES (@deckId, @name, 1);";

                using var checkCommand = new SQLiteCommand(checkQuery, DBAccess.connection);
                checkCommand.Parameters.AddWithValue("@deckId", deckId);
                checkCommand.Parameters.AddWithValue("@name", cardName);

                object result = await checkCommand.ExecuteScalarAsync();

                if (result != null) // Card exists, update count
                {
                    using var updateCommand = new SQLiteCommand(updateQuery, DBAccess.connection);
                    updateCommand.Parameters.AddWithValue("@deckId", deckId);
                    updateCommand.Parameters.AddWithValue("@name", cardName);
                    await updateCommand.ExecuteNonQueryAsync();
                }
                else // Card does not exist, insert new row
                {
                    using var insertCommand = new SQLiteCommand(insertQuery, DBAccess.connection);
                    insertCommand.Parameters.AddWithValue("@deckId", deckId);
                    insertCommand.Parameters.AddWithValue("@name", cardName);
                    await insertCommand.ExecuteNonQueryAsync();
                }

                // Refresh deck datagrid
                Debug.WriteLine(loadDeckCardsQuery);
                await MainWindow.PopulateCardDataGridAsync(MainWindow.CurrentInstance.cardsInDecks, loadDeckCardsQuery, MainWindow.CurrentInstance.DeckDataGrid);


            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error adding card to deck: {ex.Message}");
                MessageBox.Show($"Error adding card to deck: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                DBAccess.CloseConnection();
            }
        }
    }
}
