using Microsoft.Win32;
using ServiceStack;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace CollectaMundo
{
    public class BackupRestore
    {
        #region Classes used for csv-import
        public class TempCardItem
        {
            public Dictionary<string, string> Fields { get; set; } = new Dictionary<string, string>();
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
        public class ValueMapping : INotifyPropertyChanged
        {
            private string? _selectedCardSetValue;

            public string? CsvValue { get; set; }
            public List<string>? CardSetValue { get; set; }
            public string? SelectedCardSetValue
            {
                get => _selectedCardSetValue;
                set
                {
                    _selectedCardSetValue = value;
                    OnPropertyChanged(nameof(SelectedCardSetValue));
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            protected void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        public class ColumnMapping : INotifyPropertyChanged
        {
            private string? _selectedDatabaseField;
            private string? _selectedCsvHeader;
            public string? CardSetField { get; set; }

            private string? csvHeader;
            public string? CsvHeader
            {
                get => csvHeader;
                set
                {
                    if (csvHeader != value)
                    {
                        csvHeader = value;
                        OnPropertyChanged(nameof(CsvHeader));
                    }
                }
            }
            public string? SelectedCsvHeader
            {
                get => _selectedCsvHeader;
                set
                {
                    _selectedCsvHeader = value;
                    OnPropertyChanged(nameof(SelectedCsvHeader));
                }
            }
            public List<string>? CsvHeaders { get; set; } = new List<string>();
            public List<string>? DatabaseFields { get; set; }
            public string? SelectedDatabaseField
            {
                get => _selectedDatabaseField;
                set
                {
                    _selectedDatabaseField = value;
                    OnPropertyChanged(nameof(SelectedDatabaseField));
                }
            }


            public event PropertyChangedEventHandler? PropertyChanged;
            protected void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public static List<string> FieldsToMap { get; private set; } = new List<string>
            {
                "Condition",
                "Card Finish",
                "Cards Owned",
                "Cards For Trade/Selling",
                "Language"
            };


        #endregion

        // The temp object which holds the values read from csv-file
        public static ObservableCollection<TempCardItem> tempImport { get; private set; } = new ObservableCollection<TempCardItem>();

        // Create backup of my collection
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
                            if (i < reader.FieldCount - 1)
                            {
                                writer.Write(";");
                            }
                        }
                        writer.WriteLine();

                        // Write CSV rows
                        while (await reader.ReadAsync())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                var value = reader[i]?.ToString()?.Replace(";", ",") ?? string.Empty; // Replace semicolons to prevent CSV issues
                                writer.Write(value);
                                if (i < reader.FieldCount - 1)
                                {
                                    writer.Write(";");
                                }
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

        #region Open and parse csv-file
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
                if (header == null)
                {
                    return cardItems;
                }

                // Detect delimiter
                if (header.Contains(';'))
                {
                    delimiter = ';';
                }

                headers = ParseCsvLine(header, delimiter);

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null)
                    {
                        continue;
                    }

                    var values = ParseCsvLine(line, delimiter);
                    var cardItem = new TempCardItem();

                    for (int i = 0; i < headers.Count; i++)
                    {
                        cardItem.Fields[headers[i]] = values.Count > i ? values[i] : string.Empty;
                    }

                    cardItems.Add(cardItem);
                }
            }

            return cardItems;
        }

        // Helper method to parse a CSV line, respecting quoted fields
        private static List<string> ParseCsvLine(string line, char delimiter)
        {
            var values = new List<string>();
            var currentField = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        // Check if this is a double quote (escaped quote)
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            currentField.Append('"');
                            i++; // Skip the next quote
                        }
                        else
                        {
                            inQuotes = false; // End of quoted field
                        }
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == delimiter)
                    {
                        values.Add(currentField.ToString().Trim());
                        currentField.Clear();
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
            }

            // Add the last field
            values.Add(currentField.ToString().Trim());

            return values;
        }

        #endregion

        #region Import Wizard - mapping imported cards by card ID
        public static void PopulateIdColumnMappingListView(ListView listView)
        {
            try
            {
                var csvHeaders = tempImport.FirstOrDefault()?.Fields.Keys.ToList() ?? new List<string>();
                var databaseFields = GetCardIdentifierColumns(); // Fetch database columns

                var mappingItem = new ColumnMapping
                {
                    DatabaseFields = databaseFields,
                    CsvHeaders = csvHeaders,
                    SelectedDatabaseField = databaseFields.FirstOrDefault(),
                    SelectedCsvHeader = csvHeaders.FirstOrDefault()
                };

                listView.ItemsSource = new List<ColumnMapping> { mappingItem };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error populating ID column mapping list view: {ex.Message}");
                MessageBox.Show($"Error populating ID column mapping list view: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private static List<string> GetCardIdentifierColumns()
        {
            var columns = new List<string>();
            try
            {
                // Open the database connection
                DBAccess.OpenConnectionAsync().Wait();
                if (DBAccess.connection == null)
                {
                    throw new InvalidOperationException("Database connection is not initialized.");
                }

                // Get the column names from cardIdentifiers table
                string query = "PRAGMA table_info(cardIdentifiers);";
                using (var command = new SQLiteCommand(query, DBAccess.connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var columnName = reader["name"]?.ToString();
                            if (!string.IsNullOrEmpty(columnName))
                            {
                                columns.Add(columnName);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching card identifier columns: {ex.Message}");
                MessageBox.Show($"Error fetching card identifier columns: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                DBAccess.CloseConnection();
            }

            return columns;
        }
        public static async Task ProcessIdColumnMappingsAsync()
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                // Get the mapping from IdColumnMappingListView
                var idColumnMapping = MainWindow.CurrentInstance.IdColumnMappingListView.ItemsSource as List<ColumnMapping>;
                if (idColumnMapping == null || !idColumnMapping.Any())
                {
                    Debug.WriteLine("No ID column mappings found.");
                    return;
                }

                var mapping = idColumnMapping.First();

                if (string.IsNullOrEmpty(mapping.SelectedDatabaseField) || string.IsNullOrEmpty(mapping.SelectedCsvHeader))
                {
                    Debug.WriteLine("Both SelectedDatabaseField and SelectedCsvHeader must be set.");
                    return;
                }

                string databaseField = mapping.SelectedDatabaseField;
                string csvHeader = mapping.SelectedCsvHeader;

                await DBAccess.OpenConnectionAsync();

                // Build a dictionary to hold CSV values and their corresponding UUIDs
                var csvToUuidsMap = new Dictionary<string, List<string>>();

                // Use a StringBuilder to build a batch SQL query
                var batchQueryBuilder = new StringBuilder(@"
                    SELECT ci.uuid, ci.")
                    .Append(databaseField)
                    .Append(@" 
                        FROM cardIdentifiers ci
                        INNER JOIN cards c ON ci.uuid = c.uuid
                        WHERE (c.side IS NULL OR c.side = 'a') 
                        AND ci.")
                    .Append(databaseField)
                    .Append(" IN (");

                bool hasValues = false;
                foreach (var tempItem in tempImport)
                {
                    if (tempItem.Fields.TryGetValue(csvHeader, out var csvValue) && !string.IsNullOrEmpty(csvValue))
                    {
                        if (!csvToUuidsMap.ContainsKey(csvValue))
                        {
                            csvToUuidsMap[csvValue] = new List<string>();
                            batchQueryBuilder.Append("@csvValue_")
                                             .Append(csvToUuidsMap.Count - 1)
                                             .Append(",");
                            hasValues = true;
                        }
                    }
                }

                if (!hasValues)
                {
                    Debug.WriteLine("No valid CSV values found.");
                    return;
                }

                batchQueryBuilder.Length--; // Remove the trailing comma
                batchQueryBuilder.Append(");");

                Debug.WriteLine($"batchQueryBuilder: {batchQueryBuilder}");

                using (var command = new SQLiteCommand(batchQueryBuilder.ToString(), DBAccess.connection))
                {
                    int index = 0;
                    foreach (var csvValue in csvToUuidsMap.Keys)
                    {
                        command.Parameters.AddWithValue($"@csvValue_{index}", csvValue);
                        index++;
                    }

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var uuid = reader["uuid"]?.ToString();
                            var csvValue = reader[databaseField]?.ToString();

                            if (!string.IsNullOrEmpty(uuid) && !string.IsNullOrEmpty(csvValue))
                            {
                                if (csvToUuidsMap.ContainsKey(csvValue))
                                {
                                    csvToUuidsMap[csvValue].Add(uuid);
                                }
                                else
                                {
                                    Debug.WriteLine($"Unexpected csvValue '{csvValue}' found in query results.");
                                }
                            }
                        }
                    }
                }

                Debug.WriteLine("csvToUuidsMap contents:");
                foreach (var kvp in csvToUuidsMap)
                {
                    Debug.WriteLine($"CSV Value: {kvp.Key}, UUIDs: {string.Join(", ", kvp.Value)}");
                }

                // Process UUID results in parallel
                await Task.WhenAll(tempImport.Select(tempItem =>
                {
                    if (tempItem.Fields.TryGetValue(csvHeader, out var csvValue) && csvToUuidsMap.TryGetValue(csvValue, out var uuids))
                    {
                        return Task.Run(() => ProcessUuidResults(uuids, tempItem));
                    }
                    return Task.CompletedTask;
                }));

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing id column mapping: {ex.Message}");
                MessageBox.Show($"Error processing id column mapping: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                DBAccess.CloseConnection();
                stopwatch.Stop();
                Debug.WriteLine($"ProcessIdColumnMappingsAsync completed in {stopwatch.ElapsedMilliseconds} ms");
            }
        }





        #endregion

        #region Import Wizard - Mapping imported cards by searching on card name, set name and set code
        /* The logic is as follows:
         * If set code is mapped search by set code
         * If no match is found by searching by set code, search by set name
         * If set code is not mapped, search by set name
        */
        public static async Task SearchByCardNameOrSet(List<ColumnMapping> mappings)
        {
            try
            {
                await DBAccess.OpenConnectionAsync();
                if (DBAccess.connection == null)
                {
                    throw new InvalidOperationException("Database connection is not initialized.");
                }

                var nameCsvHeader = mappings.FirstOrDefault(m => m.CardSetField == "Card Name")?.CsvHeader;
                var setNameCsvHeader = mappings.FirstOrDefault(m => m.CardSetField == "Set Name")?.CsvHeader;
                var setCodeCsvHeader = mappings.FirstOrDefault(m => m.CardSetField == "Set Code")?.CsvHeader;

                if (nameCsvHeader == null)
                {
                    throw new InvalidOperationException("Name mapping not found.");
                }

                // Loop through all items in tempImport that do not already have a uuid value
                foreach (var item in tempImport.Where(i => !i.Fields.TryGetValue("uuid", out var uuid) || string.IsNullOrEmpty(uuid)))
                {
                    if (!item.Fields.TryGetValue(nameCsvHeader, out string? name))
                    {
                        Debug.WriteLine($"Fail: Could not find mapping for card name with header {nameCsvHeader}");
                        continue;
                    }

                    bool matchFound = false;

                    if (setCodeCsvHeader != null && item.Fields.TryGetValue(setCodeCsvHeader, out string? setCode))
                    {
                        matchFound = await SearchBySetCode(name, setCode, item);

                        if (!matchFound && setNameCsvHeader != null && item.Fields.TryGetValue(setNameCsvHeader, out string? setName))
                        {
                            matchFound = await SearchBySetName(name, setName, item);
                        }

                        if (!matchFound)
                        {
                            Debug.WriteLine($"Fail: Could not find a match by set code {setCode} for card name {name}");
                        }
                    }
                    else if (setNameCsvHeader != null && item.Fields.TryGetValue(setNameCsvHeader, out string? setName))
                    {
                        matchFound = await SearchBySetName(name, setName, item);

                        if (!matchFound)
                        {
                            Debug.WriteLine($"Fail: Could not find a match by set name {setName} for card name {name}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"Fail: Neither set code nor set name mappings found for card name {name}");
                    }
                }

                // Rename the CSV columns in tempImport
                RenameCsvHeaders(mappings);
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

        // Helper methods to SearchByCardNameOrSet
        private static void RenameCsvHeaders(List<ColumnMapping> mappings)
        {
            var nameMapping = mappings.FirstOrDefault(m => m.CardSetField == "Card Name");
            if (nameMapping != null && !string.IsNullOrEmpty(nameMapping.CsvHeader))
            {
                foreach (var item in tempImport)
                {
                    if (item.Fields.ContainsKey(nameMapping.CsvHeader))
                    {
                        item.Fields["CardName"] = item.Fields[nameMapping.CsvHeader];
                        item.Fields.Remove(nameMapping.CsvHeader);
                    }
                }

                Debug.WriteLine($"CSV Header '{nameMapping.CsvHeader}' renamed to 'CardName'");
            }
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
                    return ProcessUuidResults(uuids, item);
                }
                // If nothing is found is table cards, try the same in table tokens
                else if (tokenSetCode != null)
                {
                    uuids = await SearchTableForUuidAsync(name, "tokens", tokenSetCode);
                    if (uuids.Count > 0)
                    {
                        return ProcessUuidResults(uuids, item);
                    }
                }
            }

            Debug.WriteLine($"Fail: Could not find a match for {name} in set {setName}");
            return false;
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
                return ProcessUuidResults(uuids1, item);
            }

            // a token with a regular set code
            var uuids2 = await SearchTableForUuidAsync(name, "tokens", setCode);
            if (uuids2.Count > 0)
            {
                return ProcessUuidResults(uuids2, item);
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
                    return ProcessUuidResults(uuids3, item);
                }
            }

            // If nothing is found, bugger it, return false
            return false;
        }

        // Helper method to SearchBySetCode
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



        // Utility methods to help determine where to go after card name and set name/set code mapping
        public static bool AllItemsHaveUuid()
        {
            foreach (var tempItem in tempImport)
            {
                if (!tempItem.Fields.TryGetValue("uuid", out var uuid) || string.IsNullOrEmpty(uuid))
                {
                    return false;
                }
            }
            return true;
        }
        public static bool AnyItemWithMultipleUuidsField()
        {
            bool hasUuids = tempImport.Any(item =>
            {
                if (item.Fields.TryGetValue("uuids", out var uuids))
                {
                    if (!string.IsNullOrEmpty(uuids))
                    {
                        Debug.WriteLine($"Item with uuids found: {uuids}");
                        return true;
                    }
                }
                return false;
            });

            Debug.WriteLine($"Any item with 'uuids' field: {hasUuids}");
            return hasUuids;
        }
        public static bool AnyItemWithUuidField()
        {
            return tempImport.Any(item => item.Fields.TryGetValue("uuid", out var uuid) && !string.IsNullOrEmpty(uuid));
        }

        #endregion

        #region Import Wizard - Selecting between cards where multiple uuids were found
        // Populate the datagrid where a single version can be selected of a card with multiple identical set and card names
        public static void PopulateMultipleUuidsDataGrid()
        {
            try
            {
                var itemsWithMultipleUuids = tempImport
                    .Where(item => item.Fields.ContainsKey("uuids"))
                    .Select(item => new MultipleUuidsItem
                    {
                        Name = item.Fields.ContainsKey("CardName") ? item.Fields["CardName"] : "Unknown",
                        VersionedUuids = item.Fields["uuids"]
                            .Split(',')
                            .Select((uuid, index) => new UuidVersion { DisplayText = $"Version {index + 1}", Uuid = uuid })
                            .ToList(),
                        SelectedUuid = null // Set the initial selection to null
                    })
                    .ToList();

                Debug.WriteLine($"Populated MultipleUuidsDataGrid with {itemsWithMultipleUuids.Count} items.");

                MainWindow.CurrentInstance.MultipleUuidsDataGrid.ItemsSource = itemsWithMultipleUuids;
                MainWindow.CurrentInstance.MultipleUuidsDataGrid.Visibility = itemsWithMultipleUuids.Any() ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error populating multiple uuids datagrid: {ex.Message}");
                MessageBox.Show($"Error populating multiple uuids datagrid: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Update tempImport object with the cards where a uuid match was found
        public static void ProcessMultipleUuidSelections(List<MultipleUuidsItem> multipleUuidsItems)
        {
            foreach (var item in multipleUuidsItems)
            {
                if (!string.IsNullOrEmpty(item.SelectedUuid))
                {
                    var tempItem = tempImport.FirstOrDefault(t => t.Fields.ContainsKey("CardName") && t.Fields["CardName"] == item.Name);

                    // Remove the field uuids and add the field uuid with the selected version of the card
                    if (tempItem != null)
                    {
                        tempItem.Fields["uuid"] = item.SelectedUuid;
                        tempItem.Fields.Remove("uuids");
                    }
                }
            }
        }
        #endregion

        #region Import Wizard - Populating Importer UI elements for additional fields

        // Generalized method for populating a listview where values found in csv-file can be matched to appropriate db values
        public static async Task InitializeMappingListViewAsync(string csvHeader, bool fetchFromDatabase, string dbColumn, ListView listView)
        {
            try
            {
                List<string> mappingValues;

                // Mapping values for finish and language should be values found in cards table
                if (fetchFromDatabase)
                {
                    mappingValues = await GetUniqueValuesFromDbColumn(dbColumn);
                }
                // Mapping values for condition should be the ones specified in the Condition field in CardSet class. 
                // They are not found in the db because the are MCM gradings
                else
                {
                    mappingValues = GetConditionsFromCardSet();
                }

                PopulateColumnValuesMappingListView(listView, csvHeader, mappingValues);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing mapping list view: {ex.Message}");
                MessageBox.Show($"Error initializing mapping list view: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Helper method for InitializeMappingListViewAsync - unique values from cards table for the chosen CardSet field
        private static async Task<List<string>> GetUniqueValuesFromDbColumn(string dbColumn)
        {
            var uniqueValues = new HashSet<string>();

            string query = $"SELECT DISTINCT {dbColumn} FROM cards";

            await DBAccess.OpenConnectionAsync();
            using (var command = new SQLiteCommand(query, DBAccess.connection))
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var value = reader[dbColumn]?.ToString();
                    if (!string.IsNullOrEmpty(value))
                    {
                        var splitValues = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var splitValue in splitValues)
                        {
                            uniqueValues.Add(splitValue.Trim());
                        }
                    }
                }
            }
            DBAccess.CloseConnection();

            return uniqueValues.ToList();
        }

        // Helper method for InitializeMappingListViewAsync - get Condition values
        private static List<string> GetConditionsFromCardSet()
        {
            var cardItem = new CardSet.CardItem();
            return cardItem.Conditions;
        }

        // Helper method for InitializeMappingListViewAsync - populate the actual listview with both csv-values to map to and options in the dropdown to map with
        private static void PopulateColumnValuesMappingListView(ListView listView, string csvHeader, List<string> cardSetFields)
        {
            var csvValues = GetUniqueValuesFromCsv(csvHeader);

            var mappingItems = csvValues
                .Select(csvValue => new ValueMapping
                {
                    CsvValue = csvValue,
                    CardSetValue = cardSetFields,
                    SelectedCardSetValue = GuessMapping(csvValue, cardSetFields) // Leave as null if no match is found
                }).ToList();

            listView.ItemsSource = mappingItems;
        }

        // Helper method for PopulateColumnValuesMappingListView - get unique values for a specific csv-column to set as items to map to appropriate db values
        public static List<string> GetUniqueValuesFromCsv(string? csvHeader)
        {
            var uniqueValues = new HashSet<string>();

            if (csvHeader == null)
            {
                return uniqueValues.ToList();
            }

            foreach (var item in tempImport)
            {
                if (item.Fields.TryGetValue(csvHeader, out var value) && !string.IsNullOrEmpty(value))
                {
                    uniqueValues.Add(value);
                }
            }

            return uniqueValues.ToList();
        }

        // Try to guess which column name maps to cardItemsToAdd field by looking for matching column/field names
        public static string? GuessMapping(string searchValue, List<string> options)
        {
            var lowerSearchValue = searchValue.ToLower();
            return options.FirstOrDefault(option => option.ToLower().Contains(lowerSearchValue));
        }

        #endregion

        #region Import Wizard - Create a mapping directory with mappings for additional fields

        public static Dictionary<string, string> CreateMappingDictionary(ListView mappingListView, string cardSetField, string defaultValue)
        {
            try
            {
                // Initialize the dictionary that will hold the mappings for the specified field
                var fieldMappingDict = new Dictionary<string, string>();

                // Get the mappings from the specified ListView
                var mappings = mappingListView.ItemsSource as List<ValueMapping>;

                if (mappings == null)
                {
                    Debug.WriteLine("No mappings found.");
                    return fieldMappingDict;
                }

                // Populate the dictionary with mappings from the ListView
                foreach (var mapping in mappings)
                {
                    if (!string.IsNullOrEmpty(mapping.CsvValue))
                    {
                        var databaseValue = mapping.SelectedCardSetValue ?? defaultValue;
                        fieldMappingDict[mapping.CsvValue] = databaseValue;
                    }
                }

                return fieldMappingDict;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating mapping dictionary: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new Dictionary<string, string>();
            }
        }
        public static Dictionary<string, Dictionary<string, string>> FieldMappings { get; private set; } = new Dictionary<string, Dictionary<string, string>>();
        public static void StoreMapping(string cardSetField, Dictionary<string, string> mappingDict, bool isMapped)
        {
            if (!isMapped)
            {
                // Mark as unmapped with a special entry
                mappingDict["unmapped_field"] = "unmapped";
            }

            FieldMappings[cardSetField] = mappingDict;
        }
        public static void MarkFieldAsUnmapped(string cardSetField)
        {
            var mappingDict = new Dictionary<string, string>
            {
                ["unmapped_field"] = "unmapped"
            };

            StoreMapping(cardSetField, mappingDict, false);
        }

        //public static void UpdateCardItemsWithQuantity(string cardSetField)
        //{
        //    try
        //    {
        //         Find the corresponding CSV header for the given cardSetField in _mappings
        //        var csvHeader = MainWindow.CurrentInstance._mappings?.FirstOrDefault(field => field == cardSetField);

        //        if (string.IsNullOrEmpty(csvHeader))
        //        {
        //            Debug.WriteLine($"{cardSetField} mapping not found.");
        //            return;
        //        }

        //         Update items in cardItemsToAdd based on tempImport and the quantity values
        //        UpdateCardItems(cardSetField, csvHeader, 1, null);
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show($"Error updating items with quantity: {ex.Message}");
        //        MessageBox.Show($"Error updating items with quantity: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        //    }
        //}

        private static void UpdateCardItems(string cardSetField, string? csvHeader, object defaultValue, Dictionary<string, string>? mappingDict)
        {
            foreach (var tempItem in tempImport)
            {
                if (tempItem.Fields.TryGetValue("uuid", out var uuid) && !string.IsNullOrEmpty(uuid))
                {
                    if (!string.IsNullOrEmpty(csvHeader) && tempItem.Fields.TryGetValue(csvHeader, out var fieldValue))
                    {
                        Debug.WriteLine($"Found {cardSetField}: {fieldValue} for item with UUID: {uuid}");

                        if (mappingDict != null && mappingDict.TryGetValue(fieldValue, out var mappedValue))
                        {
                            tempItem.Fields[cardSetField] = mappedValue;
                            Debug.WriteLine($"Mapped {cardSetField}: {mappedValue} for item with UUID: {uuid}");
                        }
                        else
                        {
                            tempItem.Fields[cardSetField] = defaultValue.ToString();
                            Debug.WriteLine($"{cardSetField} {fieldValue} not found in mapping dictionary. Assigning default value '{defaultValue}'.");
                        }
                    }
                    else
                    {
                        tempItem.Fields[cardSetField] = defaultValue.ToString();
                        Debug.WriteLine($"{cardSetField} not found in tempItem for UUID: {uuid}. Assigning default value '{defaultValue}'.");
                    }
                }
                else
                {
                    Debug.WriteLine($"UUID not found or empty in tempItem");
                }
            }
        }
        public static void RenameTempImportField(string oldFieldName, string newFieldName)
        {
            foreach (var item in tempImport)
            {
                if (item.Fields.ContainsKey(oldFieldName))
                {
                    // Get the value associated with the old field name
                    var value = item.Fields[oldFieldName];

                    // Remove the old field name
                    item.Fields.Remove(oldFieldName);

                    // Add the new field name with the value
                    item.Fields[newFieldName] = value;
                }
            }
        }
        public static bool IsFieldMapped(List<ColumnMapping> mappingsList, string cardSetField)
        {
            // Find the mapping for the specified cardSetField
            var fieldMapping = mappingsList?.FirstOrDefault(mapping => mapping.CardSetField == cardSetField);

            // Return true if the mapping exists and the CsvHeader is not null or empty
            return fieldMapping != null && !string.IsNullOrEmpty(fieldMapping.CsvHeader);
        }




        // Generalized method to set default value for additional fields
        public static void UpdateTempImportWithDefaultField(string cardSetField, object defaultValue)
        {
            UpdateCardItems(cardSetField, null, defaultValue, null);
        }

        #endregion

        #region Import Wizard - Misc. helper and shared methods

        // Populate a listview where column headings found in the csv-file can be mapped to fields 
        public static void PopulateColumnMappingListView(ListView listView, List<string> cardSetFields)
        {
            try
            {
                var csvHeaders = tempImport.FirstOrDefault()?.Fields.Keys.ToList() ?? new List<string>();

                var mappingItems = cardSetFields.Select(field => new ColumnMapping
                {
                    CardSetField = field,
                    CsvHeaders = csvHeaders,
                    CsvHeader = GuessMapping(field, csvHeaders)
                }).ToList();

                listView.ItemsSource = mappingItems;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error populating mapping list view: {ex.Message}");
                MessageBox.Show($"Error populating mapping list view: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private static bool ProcessUuidResults(List<string> uuids, TempCardItem item)
        {
            if (uuids.Count == 1)
            {
                string singleUuid = uuids[0];
                item.Fields["uuid"] = singleUuid;
                Debug.WriteLine("Found a single uuid");
                return true;
            }
            else if (uuids.Count > 1)
            {
                // Optimized joining using StringBuilder
                var sb = new StringBuilder();
                for (int i = 0; i < uuids.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(','); // Append a comma before each UUID except the first one
                    }

                    sb.Append(uuids[i]);
                }
                item.Fields["uuids"] = sb.ToString();
                Debug.WriteLine("Found multiple uuids");
                return true;
            }
            else
            {
                Debug.WriteLine("Found no uuids");
                return false;
            }
        }
        public static void AssertNoInvalidUuidFields()
        {
            bool invalidUuidAndUuids = tempImport.Any(item =>
                item.Fields.TryGetValue("uuid", out var uuid) && !string.IsNullOrEmpty(uuid) &&
                item.Fields.TryGetValue("uuids", out var uuids) && !string.IsNullOrEmpty(uuids)
            );

            bool invalidUuidOrUuids = tempImport.Any(item =>
                (item.Fields.TryGetValue("uuid", out var uuid) && string.IsNullOrEmpty(uuid)) ||
                (item.Fields.TryGetValue("uuids", out var uuids) && string.IsNullOrEmpty(uuids))
            );

            if (invalidUuidAndUuids)
            {
                throw new InvalidOperationException("An item in tempImport has both 'uuid' and 'uuids' fields with values, which is not allowed.");
            }

            if (invalidUuidOrUuids)
            {
                throw new InvalidOperationException("An item in tempImport has 'uuid' or 'uuids' field with no value, which is not allowed.");
            }
        }
        #endregion

        public static void DebugFieldMappings()
        {
            try
            {
                Debug.WriteLine("==== Field Mappings ====");

                foreach (var fieldMapping in FieldMappings)
                {
                    Debug.WriteLine($"Field: {fieldMapping.Key}");
                    foreach (var mapping in fieldMapping.Value)
                    {
                        Debug.WriteLine($"  CSV Value: {mapping.Key} -> Mapped Value: {mapping.Value}");
                    }
                }

                Debug.WriteLine("========================");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in DebugFieldMappings: {ex.Message}");
            }
        }
        public static void DebugAllItems()
        {
            Debug.WriteLine("\n");
            Debug.WriteLine("Debugging tempImport items:");
            foreach (var tempItem in tempImport)
            {
                Debug.WriteLine("TempItem:");
                foreach (var field in tempItem.Fields)
                {
                    Debug.WriteLine($"{field.Key}: {field.Value}");
                }
                Debug.WriteLine("\n");
            }
            //Debug.WriteLine("\n");
            //Debug.WriteLine("Debugging cardItemsToAdd items:");
            //foreach (var cardItem in AddToCollectionManager.Instance.cardItemsToAdd)
            //{
            //    Debug.WriteLine($"CardItem - Uuid: {cardItem.Uuid}, CardsOwned: {cardItem.CardsOwned}, CardsForTrade: {cardItem.CardsForTrade}, Condition: {cardItem.SelectedCondition}, Finish: {cardItem.SelectedFinish}, Language: {cardItem.Language}");
            //}
        }
        public static void DebugImportProcess()
        {
            // Total number of items in tempImport
            int totalTempImportItems = tempImport.Count;

            // Number of tempImport items with a single uuid
            int singleUuidItems = tempImport.Count(item => item.Fields.ContainsKey("uuid") && !item.Fields.ContainsKey("uuids"));

            // Number of tempImport items with multiple uuids
            int multipleUuidItems = tempImport.Count(item => item.Fields.ContainsKey("uuids"));

            // Total number of items in cardItemsToAdd
            int totalCardItemsToAdd = AddToCollectionManager.Instance.cardItemsToAdd.Count;

            // Debug write lines
            Debug.WriteLine($"Total number of items in tempImport: {totalTempImportItems}");
            Debug.WriteLine($"Number of tempImport items with single uuid: {singleUuidItems}");
            Debug.WriteLine($"Number of tempImport items with multiple uuids: {multipleUuidItems}");
            Debug.WriteLine($"Total number of items in cardItemsToAdd: {totalCardItemsToAdd}");
        }
    }


}



