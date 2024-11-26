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

                await DBAccess.OpenConnectionAsync();

                using SQLiteCommand command = new(insertSql, DBAccess.connection);

                // Bind parameters
                command.Parameters.AddWithValue("@deckName", deckName);
                command.Parameters.AddWithValue("@deckDescription", deckDescription);
                command.Parameters.AddWithValue("@targetFormat", targetFormat);

                // Execute the command
                await command.ExecuteNonQueryAsync();

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
    }
}
