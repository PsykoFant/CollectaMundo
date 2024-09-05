using Newtonsoft.Json.Linq;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;

namespace CollectaMundo
{
    public class UpdateDB
    {
        /// <summary>
        /// Perform an update check by comparing the date from meta table in card db with data fetched from mtgjson server (meta)
        /// If the date fetched from mtgjson is newer enable the update database button.
        /// Pushing the button will download the latest version of AllPrintings.sqlite to the users download folder.
        /// Drop all non-custom data tables and copy the tables from the downloaded AllPrintings to the existing AllPrintings
        /// Then create any new set icons, mana symbols or mana cost images that might need to be created from any new cards added to AllPrintings
        /// </summary>

        private static string downloadsPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        private static string newDatabasePath = Path.Combine(downloadsPath, "Downloads", "AllPrintings.sqlite");

        // For updating statuswindow
        public static event Action<string>? StatusMessageUpdated;
        public static async Task CheckForUpdatesAsync()
        {
            try
            {
                MainWindow.CurrentInstance.UtilsInfoLabel.Content = "Checking for updates...";

                // Read updated date from card db
                await DBAccess.OpenConnectionAsync();
                int numberOfSetsInDb = (await DownloadAndPrepDB.GetUniqueValuesAsync("sets", "code")).Count;
                Debug.WriteLine(numberOfSetsInDb.ToString());
                DBAccess.CloseConnection();

                // Fetch last updated from server
                int numberOfSetsOnServer = await FetchSetsCountAsync();

                // Compare the two
                if (numberOfSetsOnServer > numberOfSetsInDb)
                {
                    Debug.WriteLine("There is a newer database");
                    MainWindow.CurrentInstance.UtilsInfoLabel.Content = "There is a newer database";
                    MainWindow.CurrentInstance.UpdateDbButton.Visibility = Visibility.Visible;
                }
                else
                {
                    Debug.WriteLine("You are already up to date");
                    MainWindow.CurrentInstance.UtilsInfoLabel.Content = "You are already up to date";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An error occurred checking for updates: {ex.Message}");
                MessageBox.Show($"An error occurred checking for updates: {ex.Message}", "Update Check Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
        public static async Task UpdateCardDatabaseAsync()
        {
            try
            {
                // Disbale buttons while updating
                await MainWindow.ShowStatusWindowAsync(true);

                // Download new card database to currentuser/downloads
                await DownloadAndPrepDB.DownloadDatabaseIfNotExistsAsync(newDatabasePath, "Downloading fresh card database and updating...");

                await DBAccess.OpenConnectionAsync();
                // Copy tables from new card database
                await CopyTablesAsync();

                // Generate new custom data if needed
                await DownloadAndPrepDB.GenerateManaSymbolsFromSvgAsync();
                // Now run the last two functions in parallel
                var generateManaCostImagesTask = DownloadAndPrepDB.GenerateManaCostImagesAsync();
                var generateSetKeyruneFromSvgTask = DownloadAndPrepDB.GenerateSetKeyruneFromSvgAsync();
                await Task.WhenAll(generateManaCostImagesTask, generateSetKeyruneFromSvgTask);

                DBAccess.CloseConnection();

                StatusMessageUpdated?.Invoke($"Card database has been updated!");
                await Task.Delay(1000); // Leave the message for a few seconds

                StatusMessageUpdated?.Invoke($"Reloading card database...");
                await Task.Delay(1000); // Leave the message for a few seconds
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating card database: {ex.Message}");
                MessageBox.Show($"An error occurred updating the card database: {ex.Message}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                await MainWindow.ShowStatusWindowAsync(false);

                // Reenable buttons and go to search and filter                
                MainWindow.CurrentInstance.ResetGrids();
                MainWindow.CurrentInstance.UpdateDbButton.Visibility = Visibility.Collapsed;
                MainWindow.CurrentInstance.GridUtilitiesSection.Visibility = Visibility.Visible;

                await MainWindow.CurrentInstance.LoadDataIntoUiElements();
            }
        }
        private static async Task CopyTablesAsync()
        {
            try
            {
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
                    }
                    await Task.Delay(50); // for UI to update
                    StatusMessageUpdated?.Invoke($"Table {item.Key} dropped...");
                }

                // Attach the newly downloaded database to update from
                string attachTempDb = $"ATTACH DATABASE '{newDatabasePath}' AS tempDb;";
                await new SQLiteCommand(attachTempDb, DBAccess.connection).ExecuteNonQueryAsync();

                foreach (var item in tables)
                {
                    using (var command = new SQLiteCommand(
                       $"CREATE TABLE {item.Key} AS SELECT * FROM tempDb.{item.Key};",
                        DBAccess.connection))
                    {
                        await command.ExecuteNonQueryAsync();
                    }
                    await Task.Delay(50); // for UI to update
                    StatusMessageUpdated?.Invoke($"Updated table {item.Key} has been copied from download...");
                    Debug.WriteLine($"Updated table {item.Key} has been copied from download...");
                }

                // Copy table from tempDb to regularDb
                string detachDb = "DETACH DATABASE tempDb;";
                using (var detachCommand = new SQLiteCommand(detachDb, DBAccess.connection))
                {
                    await detachCommand.ExecuteNonQueryAsync();
                    Debug.WriteLine($"Detached tempDb...");
                }

                // Recreate indices and views after copying tables
                await DownloadAndPrepDB.CreateIndices();
                await DownloadAndPrepDB.CreateViews();
            }
            catch (SQLiteException ex)
            {
                Debug.WriteLine($"Error copying tables: {ex.Message}");
                MessageBox.Show($"Error copying tables: {ex.Message}", "Filter Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private static async Task<int> FetchSetsCountAsync()
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.GetStringAsync("https://mtgjson.com/api/v5/SetList.json");
                    var json = JObject.Parse(response);
                    var sets = json["data"] as JArray;
                    int count = sets?.Count ?? 0;
                    Debug.WriteLine($"Number of sets fetched: {count}");
                    return count;
                }
            }
            catch (HttpRequestException httpEx)
            {
                Debug.WriteLine($"An error occurred during HTTP request: {httpEx.Message}");
                MessageBox.Show($"An error occurred during HTTP request: {httpEx.Message}", "HttpRequestException", MessageBoxButton.OK, MessageBoxImage.Error);

            }
            return 0;
        }
    }
}
