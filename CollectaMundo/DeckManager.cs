using CollectaMundo.Models;
using System.Data.SQLite;
using System.Diagnostics;
using System.Windows;

namespace CollectaMundo
{
    public class DeckManager
    {
        public static async Task SubmitNewDeck()
        {
            try
            {
                // Get values from UI elements
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
                    int newDeckId = Convert.ToInt32(await getIdCommand.ExecuteScalarAsync());

                    // Update the currentDeck object
                    MainWindow.CurrentInstance.CurrentDeck = new Deck
                    {
                        DeckId = newDeckId,
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
                MainWindow.CurrentInstance.GridDecksOverview.Visibility = Visibility.Collapsed;
                MainWindow.CurrentInstance.HeadlineDecks.Content = "Deck Editor";
                MainWindow.CurrentInstance.DeckNameTextBox.Text = deckName;
                MainWindow.CurrentInstance.DeckDescriptionTextBox.Text = deckDescription;
                MainWindow.CurrentInstance.GridDeckEditor.Visibility = Visibility.Visible;
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
        public static async Task UpdateDeckName(string columnToUpdate, string valueToUpdate)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(valueToUpdate) && columnToUpdate == "deckName")
                {
                    MessageBox.Show("Your deck must have a name. ", "Oopsie", MessageBoxButton.OK, MessageBoxImage.Warning);
                    MainWindow.CurrentInstance.DeckNameTextBox.Text = MainWindow.CurrentInstance.CurrentDeck.DeckName;
                    return;
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
            }
            finally
            {
                DBAccess.CloseConnection();
            }
        }
    }
}
