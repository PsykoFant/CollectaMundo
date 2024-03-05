using Newtonsoft.Json.Linq;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.Net.Http;
using System.Windows;

namespace CardboardHoarder
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

        // For updating statuswindow
        public static event Action<string>? StatusMessageUpdated;
        public static async Task CheckForUpdatesAsync()
        {
            try
            {
                // Read updated date from card db
                await DBAccess.OpenConnectionAsync();
                string lastUpdatedInDb = await GetDateFromMetaAsync();
                DBAccess.CloseConnection();

                // Fetch last updated from server
                string lastUpdatedOnServer = await FetchDataDateAsync();

                // Compare the two
                if (CompareDates(lastUpdatedInDb, lastUpdatedOnServer) < 0)
                {
                    Debug.WriteLine("There is a newer database");
                    MainWindow.CurrentInstance.infoLabel.Content = "There is a newer database";
                    MainWindow.CurrentInstance.updateDbButton.Visibility = Visibility.Visible;
                }
                else
                {
                    Debug.WriteLine("You are already up to date");
                    MainWindow.CurrentInstance.infoLabel.Content = "You are already up to date";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An error occurred: {ex.Message}");
            }

        }
        public static async Task UpdateCardDatabaseAsync()
        {
            // Disbale buttons while updating
            await MainWindow.ShowStatusWindowAsync(true);
            MainWindow.CurrentInstance.infoLabel.Content = "Updating card database...";

            // Download new card database to currentuser/downloads
            await DownloadAndPrepDB.DownloadDatabaseIfNotExistsAsync(DBAccess.newDatabasePath);

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
            await Task.Delay(3000); // Leave the message for a few seconds
            await MainWindow.ShowStatusWindowAsync(false);

            // Reenable buttons and go to search and filter
            MainWindow.CurrentInstance.ResetGrids();
            MainWindow.CurrentInstance.GridSearchAndFilter.Visibility = Visibility.Visible;
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
                string attachTempDb = $"ATTACH DATABASE 'c:/Users/Energinet/Downloads/AllPrintings.sqlite' AS tempDb;";
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
            }
            catch (SQLiteException ex)
            {
                Debug.WriteLine($"Error copying table: {ex.Message}");
            }
        }
        public static async Task<string> GetDateFromMetaAsync()
        {
            string query = "SELECT date FROM meta WHERE rowid = 1;";
            string dateValue = "";

            try
            {
                using (SQLiteCommand command = new SQLiteCommand(query, DBAccess.connection))
                {
                    using (DbDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (reader.Read())
                        {
                            dateValue = reader["date"]?.ToString() ?? string.Empty;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle exception
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
            Debug.WriteLine($"This is the date that was read from db: {dateValue}");
            return dateValue;
        }
        private static async Task<string> FetchDataDateAsync()
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.GetStringAsync("https://mtgjson.com/api/v5/Meta.json");
                    var json = JObject.Parse(response);
                    Debug.WriteLine($"Date fetched from server: {json["data"]?["date"]?.ToString()}");
                    return json["data"]?["date"]?.ToString() ?? string.Empty;
                }
            }
            catch (HttpRequestException httpEx)
            {
                // Handle HTTP request exceptions
                Debug.WriteLine($"An error occurred during HTTP request: {httpEx.Message}");
            }
            catch (Exception ex)
            {
                // Handle other exceptions
                Debug.WriteLine($"An error occurred: {ex.Message}");
            }

            return string.Empty;
        }
        private static int CompareDates(string dbDate, string serverDate)
        {
            DateTime date1 = DateTime.Parse(dbDate);
            DateTime date2 = DateTime.Parse(serverDate);

            return DateTime.Compare(date1, date2);
        }

    }
}
