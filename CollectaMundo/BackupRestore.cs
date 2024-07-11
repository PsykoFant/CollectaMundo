using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

/*
 * (0. Søg på uuid, code eller whatever)
 * 1. Søg på navn og set, set code
 *  - Hvis unikt match
 *      - Tilføj uuid til tempImport
 *      - Tilføj uuid til cardItemsToAdd
 *  - Hvis multiple match
 *      - Tilføj multiple = true på tempImport
 *      - Tilføj array med fundne uuids på tempImport
 * 2. Match multiples
 *  - Listview med alle multiple = true på tempImport
 *  - Anden kolonne Navn på fundne multiples, vælg i dropdown
 *      - Tilføj uuid til tempImport
 *      - Tilføj uuid til cardItemsToAdd
 * 3. Map quantity
 *  - Tilføj quantity til CardItemsToAdd fra tempImport hvor uuid matcher uuid fra tempImport 
 * 4. Map condition
 *  - Map conditions fra liste over tilgængelige conditions fra tempImport
 *  - Tilføj conditions til CardItemsToAdd fra fra tempImport hvor uuid matcher uuid fra tempImport
 * 6. Map foil
 * 5. Map language
*/

namespace CollectaMundo
{
    public class BackupRestore
    {
        public static async Task CreateCsvBackupAsync()
        {
            try
            {
                string backupFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "CollectaMundoBackup");
                Directory.CreateDirectory(backupFolderPath);

                await DBAccess.OpenConnectionAsync();
                if (DBAccess.connection == null)
                {
                    throw new InvalidOperationException("Database connection is not initialized.");
                }

                using (var command = new SQLiteCommand("SELECT * FROM myCollection", DBAccess.connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    // Check if the reader has any rows
                    if (!reader.HasRows)
                    {
                        MessageBox.Show("Your collection is empty - nothing to back up", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // If there are rows, proceed with creating the backup
                    string backupFilePath = Path.Combine(backupFolderPath, $"CollectaMundoMyCollection_backup_{DateTime.Now:yyyyMMdd}.csv");

                    using (var writer = new StreamWriter(backupFilePath, false, Encoding.UTF8))
                    {
                        // Write CSV header
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            writer.Write(reader.GetName(i));
                            if (i < reader.FieldCount - 1) writer.Write(";");
                        }
                        writer.WriteLine();

                        // Write CSV rows
                        while (await reader.ReadAsync())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                var value = reader[i]?.ToString()?.Replace(";", ",") ?? string.Empty; // Replace semicolons to prevent CSV issues
                                writer.Write(value);
                                if (i < reader.FieldCount - 1) writer.Write(";");
                            }
                            writer.WriteLine();
                        }
                    }

                    MessageBox.Show($"Backup created successfully at {backupFilePath}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating CSV backup: {ex.Message}");
                MessageBox.Show($"Error creating CSV backup: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                DBAccess.CloseConnection();
            }
        }
        // Class to store data read from csv-file and hold uuids found by search
        public class TempCardItem
        {
            public Dictionary<string, string> Fields { get; set; } = new Dictionary<string, string>();
        }

        public static ObservableCollection<TempCardItem> tempImport { get; private set; } = new ObservableCollection<TempCardItem>();
        public static async Task ImportCsvAsync()
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv",
                    Title = "Select a CSV file"
                };

                bool? result = openFileDialog.ShowDialog();
                if (result == true)
                {
                    string filePath = openFileDialog.FileName;
                    tempImport = await ParseCsvFileAsync(filePath);

                    // Log the object's content
                    foreach (var item in tempImport)
                    {
                        foreach (var field in item.Fields)
                        {
                            Debug.WriteLine($"{field.Key}: {field.Value}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error importing CSV: {ex.Message}");
                MessageBox.Show($"Error importing CSV: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private static async Task<ObservableCollection<TempCardItem>> ParseCsvFileAsync(string filePath)
        {
            var cardItems = new ObservableCollection<TempCardItem>();
            List<string> headers = new List<string>();
            char delimiter = ',';

            using (var reader = new StreamReader(filePath, Encoding.UTF8))
            {
                // Read the header
                var header = await reader.ReadLineAsync();
                if (header == null) return cardItems;

                // Detect delimiter
                if (header.Contains(';'))
                {
                    delimiter = ';';
                }

                headers = header.Split(delimiter).ToList();

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null) continue;

                    var values = line.Split(delimiter);
                    var cardItem = new TempCardItem();

                    for (int i = 0; i < headers.Count; i++)
                    {
                        cardItem.Fields[headers[i]] = values.Length > i ? values[i] : string.Empty;
                    }

                    cardItems.Add(cardItem);
                }
            }

            return cardItems;
        }
        public static async Task SearchByCardNameOrSet(List<ColumnMapping> mappings)
        {
            try
            {
                await DBAccess.OpenConnectionAsync();
                if (DBAccess.connection == null)
                {
                    throw new InvalidOperationException("Database connection is not initialized.");
                }

                var nameMapping = mappings.FirstOrDefault(m => m.CardSetField == "Name")?.CsvHeader;
                var setNameMapping = mappings.FirstOrDefault(m => m.CardSetField == "Set Name")?.CsvHeader;
                var setCodeMapping = mappings.FirstOrDefault(m => m.CardSetField == "Set Code")?.CsvHeader;

                if (nameMapping == null)
                {
                    throw new InvalidOperationException("Name mapping not found.");
                }

                foreach (var item in tempImport)
                {
                    if (!item.Fields.TryGetValue(nameMapping, out string name))
                    {
                        Debug.WriteLine($"Fail: Could not find mapping for card setName with header {nameMapping}");
                        continue;
                    }

                    bool matchFound = false;

                    if (setCodeMapping != null && item.Fields.TryGetValue(setCodeMapping, out string setCode))
                    {
                        matchFound = await SearchBySetCode(name, setCode, item);

                        if (!matchFound && setNameMapping != null && item.Fields.TryGetValue(setNameMapping, out string setName))
                        {
                            matchFound = await SearchBySetName(name, setName, item);
                        }

                        if (!matchFound)
                        {
                            Debug.WriteLine($"Fail: Could not find a match by set code {setCode}");
                        }
                    }
                    else if (setNameMapping != null && item.Fields.TryGetValue(setNameMapping, out string setName))
                    {
                        matchFound = await SearchBySetName(name, setName, item);

                        if (!matchFound)
                        {
                            Debug.WriteLine($"Fail: matchFound was false - Could not find a match by set setName {setName}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error searching for UUIDs: {ex.Message}");
                MessageBox.Show($"Error searching for UUIDs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                DBAccess.CloseConnection();
            }
        }
        private static async Task<bool> SearchBySetCode(string name, string setCode, TempCardItem item)
        {
            Debug.WriteLine($"Trying to search by set code: {setCode}");

            // We are trying three combinations:
            // a regular card with a regular set code
            // a token with a regular set code
            // a token with a token set code

            // a regular card with a regular set code
            var uuids1 = await SearchTableForUuidAsync(name, "cards", setCode);

            if (uuids1.Count > 0)
            {
                return ProcessUuidResults(uuids1, name, setCode, item);
            }

            // a token with a regular set code
            var uuids2 = await SearchTableForUuidAsync(name, "tokens", setCode);
            if (uuids2.Count > 0)
            {
                return ProcessUuidResults(uuids2, name, setCode, item);
            }

            // a token with a token set code
            string tokenSetCodeQuery = "SELECT tokenSetCode FROM sets WHERE code = @setCode";
            string? tokenSetCode = null;

            using (var command = new SQLiteCommand(tokenSetCodeQuery, DBAccess.connection))
            {
                command.Parameters.AddWithValue("@setCode", setCode);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        tokenSetCode = reader["tokenSetCode"]?.ToString();
                    }
                }
            }
            if (tokenSetCode != null)
            {
                var uuids3 = await SearchTableForUuidAsync(name, "tokens", tokenSetCode);
                if (uuids3.Count > 0)
                {
                    return ProcessUuidResults(uuids3, name, tokenSetCode, item);
                }
            }

            // If nothing is found, bugger it, return false
            return false;
        }
        private static async Task<bool> SearchBySetName(string name, string setName, TempCardItem item)
        {
            Debug.WriteLine($"Trying to search by set setName: {setName}");

            // Query to find the set code from the sets table based on the set name
            string cardsSetCodeQuery = "SELECT code FROM sets WHERE name = @setName";
            string? cardsSetCode = null;

            using (var cardsSetCodeCommand = new SQLiteCommand(cardsSetCodeQuery, DBAccess.connection))
            {
                cardsSetCodeCommand.Parameters.AddWithValue("@setName", setName);

                using (var setCodeReader = await cardsSetCodeCommand.ExecuteReaderAsync())
                {
                    if (await setCodeReader.ReadAsync())
                    {
                        cardsSetCode = setCodeReader["code"]?.ToString();
                    }
                }
            }

            // Query to find the set tokensSetCode from the sets table based on the set name
            string tokenSetCodeQuery = "SELECT tokenSetCode FROM sets WHERE name = @setName";
            string? tokenSetCode = null;

            using (var tokensSetCodeCommand = new SQLiteCommand(tokenSetCodeQuery, DBAccess.connection))
            {
                tokensSetCodeCommand.Parameters.AddWithValue("@setName", setName);

                using (var setCodeReader = await tokensSetCodeCommand.ExecuteReaderAsync())
                {
                    if (await setCodeReader.ReadAsync())
                    {
                        tokenSetCode = setCodeReader["tokenSetCode"]?.ToString();
                    }
                }
            }

            if (cardsSetCode == null && tokenSetCode == null)
            {
                Debug.WriteLine($"Fail: SetCode was null for {setName}");
                return false;
            }

            // Use the found set code to search in the cards and tokens table
            var uuids = new List<string>();

            if (cardsSetCode != null)
            {
                // Try to find uuid by matching set name and cardsSetCode in table cards
                uuids = await SearchTableForUuidAsync(name, "cards", cardsSetCode);
                if (uuids.Count > 0)
                {
                    return ProcessUuidResults(uuids, name, cardsSetCode, item);
                }
                // If nothing is found is table cards, try the same in table tokens
                else if (tokenSetCode != null)
                {
                    uuids = await SearchTableForUuidAsync(name, "tokens", tokenSetCode);
                    if (uuids.Count > 0)
                    {
                        return ProcessUuidResults(uuids, name, tokenSetCode, item);
                    }
                }
            }

            Debug.WriteLine($"Fail: Could not find a match for {name} in set {setName}");
            return false;
        }
        private static async Task<List<string>> SearchTableForUuidAsync(string cardName, string table, string setCode)
        {
            // Construct the query string with the table name
            string query = $"SELECT uuid FROM {table} WHERE name = @cardName AND setCode = @setCode AND (side = 'a' OR side IS NULL)";

            Debug.WriteLine($"Trying to search {table} for card name {cardName} and setCode {setCode}");

            List<string> uuids = new List<string>();

            using (var command = new SQLiteCommand(query, DBAccess.connection))
            {
                command.Parameters.AddWithValue("@cardName", cardName);
                command.Parameters.AddWithValue("@setCode", setCode);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var uuid = reader["uuid"]?.ToString();
                        if (!string.IsNullOrEmpty(uuid))
                        {
                            uuids.Add(uuid);
                        }
                    }
                }
            }

            return uuids;
        }
        private static bool ProcessUuidResults(List<string> uuids, string name, string set, TempCardItem item)
        {
            if (uuids.Count == 0)
            {
                Debug.WriteLine($"Fail: Could not find any cards with setName {name} and set {set}. Uuids: {uuids.Count}");
                return false;
            }
            else if (uuids.Count == 1)
            {
                Debug.WriteLine($"Success: Found a unique uuid for card {name} in set {set}: {uuids[0]}");
                item.Fields["uuid"] = uuids[0];

                // Add the card to cardItemsToAdd in AddToCollectionManager
                AddToCollectionManager.Instance.cardItemsToAdd.Add(new CardSet.CardItem
                {
                    Uuid = uuids[0],
                });

                return true;
            }
            else
            {
                Debug.WriteLine($"Whoops: Found more than one card with setName {name} and set {set}. Uuids: {uuids.Count}");
                item.Fields["Name"] = name;
                item.Fields["Set"] = set;
                item.Fields["uuids"] = string.Join(",", uuids); // Storing the uuids as a comma-separated string

                return true;
            }
        }
        public static void PopulateColumnMappingListView()
        {
            var csvHeaders = tempImport.FirstOrDefault()?.Fields.Keys.ToList() ?? new List<string>();

            var mappingItems = new List<ColumnMapping>
            {
                new ColumnMapping { CardSetField = "Name", CsvHeaders = csvHeaders },
                new ColumnMapping { CardSetField = "Set Name", CsvHeaders = csvHeaders },
                new ColumnMapping { CardSetField = "Set Code", CsvHeaders = csvHeaders },
            };

            MainWindow.CurrentInstance.NameAndSetMappingListView.ItemsSource = mappingItems;
        }
        public class UuidVersion
        {
            public string? DisplayText { get; set; }
            public string? Uuid { get; set; }
        }

        public class MultipleUuidsItem
        {
            public string? Name { get; set; }
            public List<UuidVersion>? VersionedUuids { get; set; }
            public string? SelectedUuid { get; set; }
        }
        public static void PopulateMultipleUuidsDataGrid()
        {
            var itemsWithMultipleUuids = tempImport
                .Where(item => item.Fields.ContainsKey("uuids"))
                .Select(item => new MultipleUuidsItem
                {
                    Name = item.Fields.ContainsKey("Name") ? item.Fields["Name"] : "Unknown",
                    VersionedUuids = item.Fields["uuids"]
                        .Split(',')
                        .Select((uuid, index) => new UuidVersion { DisplayText = $"Version {index + 1}", Uuid = uuid })
                        .ToList(),
                    SelectedUuid = null // Set the initial selection to null
                })
                .ToList();

            MainWindow.CurrentInstance.MultipleUuidsDataGrid.ItemsSource = itemsWithMultipleUuids;
            MainWindow.CurrentInstance.MultipleUuidsDataGrid.Visibility = itemsWithMultipleUuids.Any() ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}


