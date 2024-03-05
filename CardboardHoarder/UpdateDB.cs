using System.Data.SQLite;
using System.Diagnostics;
using System.Windows;

namespace CardboardHoarder
{
    public class UpdateDB
    {
        public static event Action<string>? StatusMessageUpdated;
        public static async Task CheckForUpdatesAsync()
        {
            await DBAccess.OpenConnectionAsync();
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
                DBAccess.CloseConnection(true);
            }
        }
        public static async Task UpdateCardDatabaseAsync()
        {

            MainWindow.ShowOrHideStatusWindow(true);

            // Download new card database to currentuser/downloads
            //await DownloadAndPrepDB.DownloadDatabaseIfNotExistsAsync(DBAccess.newDatabasePath);

            //await DBAccess.OpenTempConnectionAsync(false);
            await DBAccess.OpenConnectionAsync();

            await CopyTableAsync("meta");

            DBAccess.CloseConnection(true);
            //DBAccess.CloseConnection(false);

        }
        public static async Task CopyTableAsync(string tableName)
        {
            try
            {
                // Check and drop the table in regularDb if it exists
                Dictionary<string, string> tables = new()
                    {
                        {"meta", $"DROP TABLE IF EXISTS meta;" },
                    };
                foreach (var item in tables)
                {
                    using (var dropCommand = new SQLiteCommand(item.Value, DBAccess.connection))
                    {
                        await dropCommand.ExecuteNonQueryAsync();
                        Debug.WriteLine($"Table {item.Key} has been dropped");
                    }
                }

                // Attach the newly downloaded database to update from
                string attachTempDb = $"ATTACH DATABASE 'c:/Users/Energinet/Downloads/AllPrintings.sqlite' AS tempDb;";
                await new SQLiteCommand(attachTempDb, DBAccess.connection).ExecuteNonQueryAsync();


                foreach (var item in tables)
                {
                    string copyTable = $"CREATE TABLE {item.Key} AS SELECT * FROM tempDb.{item.Key};";
                    using (var copyCmd = new SQLiteCommand(copyTable, DBAccess.connection))
                    {
                        await copyCmd.ExecuteNonQueryAsync();
                        Debug.WriteLine($"Updated table {item.Key} has been copied from download...");
                    }
                }

                // Copy table from tempDb to regularDb
                string detachDb = "DETACH DATABASE tempDb;";
                using (var detachCommand = new SQLiteCommand(detachDb, DBAccess.connection))
                {
                    await detachCommand.ExecuteNonQueryAsync();
                    Debug.WriteLine($"Detached tempDb...");
                }
            }
            catch (SQLiteException ex)
            {
                Debug.WriteLine($"Error copying table: {ex.Message}");
            }
        }





    }


}
