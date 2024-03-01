using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace CardboardHoarder
{
    public class UpdateDB
    {
        public static async Task CheckForUpdatesAsync()
        {
            await DBAccess.OpenConnectionAsync(DBAccess.connection, DBAccess.connectionString);
            string query = "SELECT date FROM meta WHERE rowid = 1;";
            try
            {
                using (var command = new SQLiteCommand(query, DBAccess.connection))
                {
                    var result = await command.ExecuteScalarAsync();
                    if (DateTime.TryParse(result?.ToString(), out var tableDate))
                    {
                        if (DateTime.Today > tableDate)
                        {
                            Debug.WriteLine("There is a newer database");
                            MainWindow.CurrentInstance.updateCheckLabel.Visibility = Visibility.Visible;
                            MainWindow.CurrentInstance.updateButton.Visibility = Visibility.Visible;
                            MainWindow.CurrentInstance.updateCheckLabel.Content = "There is a newer database";
                        }
                        else
                        {
                            Debug.WriteLine("You are already up to date");
                            MainWindow.CurrentInstance.updateCheckLabel.Visibility = Visibility.Visible;
                            MainWindow.CurrentInstance.updateCheckLabel.Content = "You are already up to date";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An error occurred: {ex.Message}");
            }
            finally
            {
                DBAccess.CloseConnection(DBAccess.connection);
            }
        }
        public static async Task UpdateCardDatabaseAsync()
        {
            // Download new card database to currentuser/downloads
            string downloadsPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string newDatabasePath = Path.Combine(downloadsPath, "Downloads", "AllPrintings.sqlite");

            await DownloadAndPrepDB.DownloadDatabaseIfNotExistsAsync(newDatabasePath);

        }

    }
}
