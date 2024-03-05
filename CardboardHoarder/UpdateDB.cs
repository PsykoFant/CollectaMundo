using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;

namespace CardboardHoarder
{
    public class UpdateDB
    {
        public static event Action<string>? StatusMessageUpdated;
        public static async Task CheckForUpdatesAsync()
        {
            try
            {
                await DBAccess.OpenConnectionAsync();
                await GetDateFromMetaAsync();

                /*
                string lastUpdatedInDb = await ReadLastUpdatedDateAsync();
                string lastUpdatedOnServer = await FetchDataDateAsync();


                if (CompareDates(lastUpdatedInDb, lastUpdatedOnServer) < 0)
                {
                    Debug.WriteLine("There is a newer database");
                    Debug.WriteLine("There is a newer database");
                    MainWindow.CurrentInstance.updateCheckLabel.Visibility = Visibility.Visible;
                    MainWindow.CurrentInstance.updateDbButton.Visibility = Visibility.Visible;
                    MainWindow.CurrentInstance.updateCheckLabel.Content = "There is a newer database";
                }
                else
                {
                    Debug.WriteLine("Database is up to date");
                    Debug.WriteLine("You are already up to date");
                    MainWindow.CurrentInstance.updateCheckLabel.Visibility = Visibility.Visible;
                    MainWindow.CurrentInstance.updateCheckLabel.Content = "You are already up to date";
                }
                */
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
            MainWindow.CurrentInstance.updateCheckLabel.Content = "Updating card database...";

            // Download new card database to currentuser/downloads
            await DownloadAndPrepDB.DownloadDatabaseIfNotExistsAsync(DBAccess.newDatabasePath);

            await DBAccess.OpenConnectionAsync();
            // Copy tables from new card database
            await CopyTablesAsync();

            /*

            // Generate new custom data if needed
            await DownloadAndPrepDB.GenerateManaSymbolsFromSvgAsync();
            // Now run the last two functions in parallel
            var generateManaCostImagesTask = DownloadAndPrepDB.GenerateManaCostImagesAsync();
            var generateSetKeyruneFromSvgTask = DownloadAndPrepDB.GenerateSetKeyruneFromSvgAsync();
            await Task.WhenAll(generateManaCostImagesTask, generateSetKeyruneFromSvgTask);

            */

            DBAccess.CloseConnection();

            StatusMessageUpdated?.Invoke($"Card database has been updated!");
            await Task.Delay(3000); // for UI to update
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
                // Read last updated from the newly updated database
                string lastUpdated = await GetDateFromMetaAsync();
                // Update Last updated in appsettings.json
                await UpdateLastUpdatedDateAsync(lastUpdated);
            }
            catch (SQLiteException ex)
            {
                Debug.WriteLine($"Error copying table: {ex.Message}");
            }
        }
        public static async Task UpdateLastUpdatedDateAsync(string updatedDate)
        {
            try
            {
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");

                // Load the current JSON from the file or create a new JObject if the file doesn't exist
                JObject json = File.Exists(filePath) ? JObject.Parse(await File.ReadAllTextAsync(filePath)) : new JObject();

                // Update or create the LastUpdated section
                if (json["LastUpdated"] == null)
                    json["LastUpdated"] = new JObject();

                json["LastUpdated"]["LastUpdatedDate"] = updatedDate;

                // Write the updated JSON back to the file
                await File.WriteAllTextAsync(filePath, json.ToString());
                Debug.WriteLine($"Updated date: {updatedDate}");
            }
            catch (Exception ex)
            {
                // Properly handle exceptions
                Debug.WriteLine($"Error updating appsettings.json: {ex.Message}");
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
                            dateValue = reader["date"].ToString();
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

        private static async Task<string> ReadLastUpdatedDateAsync()
        {
            // Assuming "appsettings.json" is in the output directory next to the executable
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");

            try
            {
                var jsonText = await File.ReadAllTextAsync(filePath);
                var json = JObject.Parse(jsonText);

                // Retrieve the LastUpdatedDate
                string lastUpdatedDate = json["LastUpdated"]?["LastUpdatedDate"]?.ToString();

                return lastUpdatedDate ?? "Date not found.";
            }
            catch (FileNotFoundException)
            {
                return "appsettings.json not found.";
            }
            catch (JsonException)
            {
                return "Error parsing appsettings.json.";
            }
        }
        private static int CompareDates(string dbDate, string serverDate)
        {
            DateTime date1 = DateTime.Parse(dbDate);
            DateTime date2 = DateTime.Parse(serverDate);

            return DateTime.Compare(date1, date2);
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
    }
}
