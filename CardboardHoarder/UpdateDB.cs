using System.Data.SQLite;
using System.Diagnostics;
using System.Windows;

namespace CardboardHoarder
{
    public class UpdateDB
    {
        public static event Action<string>? StatusMessageUpdatedFuck;
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
                DBAccess.CloseConnection();
            }
        }
        public static async Task UpdateCardDatabaseAsync()
        {
            // Download new card database to currentuser/downloads
            //await DownloadAndPrepDB.DownloadDatabaseIfNotExistsAsync(DBAccess.newDatabasePath);

            // Copy tables from new card database
            await DBAccess.OpenConnectionAsync();

            await CopyTableAsync();

            DBAccess.CloseConnection();
        }
        public static async Task CopyTableAsync()
        {
            // debug
            MainWindow.ShowOrHideStatusWindow(true);

            try
            {
                StatusMessageUpdatedFuck?.Invoke("Copying updated tables to card database");

                // Check and drop the table in regularDb if it exists
                Dictionary<string, string> tables = new()
                    {
                    {"cardForeignData", $"DROP TABLE IF EXISTS cardForeignData;" },
                    {"cardIdentifiers", $"DROP TABLE IF EXISTS cardIdentifiers;" },
                    {"cardLegalities", $"DROP TABLE IF EXISTS cardLegalities;" },
                    {"cardPurchaseUrls", $"DROP TABLE IF EXISTS cardPurchaseUrls;" },
                    {"cardRulings", $"DROP TABLE IF EXISTS cardRulings;" },
                    {"cards", $"DROP TABLE IF EXISTS cards;" },
                    {"meta", $"DROP TABLE IF EXISTS meta;" },
                    {"setBoosterContentWeights", $"DROP TABLE IF EXISTS setBoosterContentWeights;" },
                    {"setBoosterContents", $"DROP TABLE IF EXISTS setBoosterContents;" },
                    {"setBoosterSheetCards", $"DROP TABLE IF EXISTS setBoosterSheetCards;" },
                    {"setBoosterSheets", $"DROP TABLE IF EXISTS setBoosterSheets;" },
                    {"setTranslations", $"DROP TABLE IF EXISTS setTranslations;" },
                    {"sets", $"DROP TABLE IF EXISTS sets;" },
                    {"tokenIdentifiers", $"DROP TABLE IF EXISTS tokenIdentifiers;" },
                    {"tokens", $"DROP TABLE IF EXISTS tokens;" },
                    };
                foreach (var item in tables)
                {
                    using (var dropCommand = new SQLiteCommand(item.Value, DBAccess.connection))
                    {
                        await dropCommand.ExecuteNonQueryAsync();
                        StatusMessageUpdatedFuck?.Invoke($"Updated table {item.Key} has been copied from download...");
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
                        await updateFuckingStatusWindow();
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

        public static async Task updateFuckingStatusWindow()
        {
            StatusMessageUpdatedFuck?.Invoke($"fuck fuck fuck");
        }



    }


}
