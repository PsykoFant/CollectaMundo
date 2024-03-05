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
            await DBAccess.OpenConnectionAsync(true);
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

            await DBAccess.OpenTempConnectionAsync(false);
            await DBAccess.OpenConnectionAsync(true);

            await CopyTableAsync("meta", DBAccess.temDbConnection, DBAccess.connection);

        }

        public static async Task CopyTableAsync(string tableName, SQLiteConnection tempDbConnection, SQLiteConnection regularDbConnection)
        {
            try
            {
                {
                    // Attach databases with aliases
                    string attachMainDb = $"ATTACH DATABASE 'c:/code/AllPrintings/AllPrintings.sqlite' AS mainDb;";
                    string attachTempDb = $"ATTACH DATABASE 'c:/Users/Energinet/Downloads/AllPrintings.sqlite' AS tempDb;";
                    await new SQLiteCommand(attachMainDb, regularDbConnection).ExecuteNonQueryAsync();
                    await new SQLiteCommand(attachTempDb, regularDbConnection).ExecuteNonQueryAsync();

                    // Check and drop the table in regularDb if it exists
                    try
                    {
                        string checkTable = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{tableName}';";
                        using (var cmd = new SQLiteCommand(checkTable, regularDbConnection))
                        {
                            var result = cmd.ExecuteScalar();
                            if (result != null && result.ToString() == tableName)
                            {
                                string dropTable = $"DROP TABLE {tableName};";
                                using (var dropCmd = new SQLiteCommand(dropTable, regularDbConnection))
                                {
                                    Debug.WriteLine("Trying to drop table");
                                    await dropCmd.ExecuteNonQueryAsync();
                                    Debug.WriteLine("Table has been dropped");
                                }
                            }
                            else
                            {
                                Debug.WriteLine("Table does not exist - proceeding with creation.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.ToString());
                    }

                    // Copy table from tempDb to regularDb
                    string copyTable = $"CREATE TABLE mainDb.{tableName} AS SELECT * FROM tempDb.{tableName};";
                    using (var copyCmd = new SQLiteCommand(copyTable, regularDbConnection))
                    {
                        Debug.WriteLine("Trying to create the new table...");
                        await copyCmd.ExecuteNonQueryAsync();
                        Debug.WriteLine("Table has been created...");
                    }

                }
            }
            catch (SQLiteException ex)
            {
                Debug.WriteLine($"Error copying table: {ex.Message}");
            }
        }





    }


}
