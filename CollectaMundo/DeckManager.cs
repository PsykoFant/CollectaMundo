using CollectaMundo.Models;
using System.Data.SQLite;
using System.Diagnostics;
using System.Windows;

namespace CollectaMundo
{
    public class DeckManager
    {
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

                    // Go to deck Editor to edit new deck
                    MainWindow.CurrentInstance.GridDecksOverview.Visibility = Visibility.Collapsed;
                    MainWindow.CurrentInstance.HeadlineDecks.Content = "Deck Editor";
                    MainWindow.CurrentInstance.DeckNameTextBox.Text = deckName;
                    MainWindow.CurrentInstance.DeckDescriptionTextBox.Text = deckDescription;
                    MainWindow.CurrentInstance.DeckFormatTextBox.Text = $"Target format: {targetFormat}";
                    MainWindow.CurrentInstance.ExistingDeckFormatComboBox.SelectedItem = targetFormat;
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
    }
}
