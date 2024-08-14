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
        #region Fields and classes used for csv-import
        private static bool isConditionMapped;
        private static bool isFinishMapped;
        private static bool isCardsOwnedMapped;
        private static bool isCardsForTradedMapped;
        private static bool isLanguageMapped;
        private static List<string>? _mappings;
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
        public static ObservableCollection<TempCardItem> tempImport { get; private set; } = new ObservableCollection<TempCardItem>();
        public static List<string> AdditionalFieldsList { get; private set; } = new List<string>
            {
                "Condition",
                "Card Finish",
                "Cards Owned",
                "Cards For Trade/Selling",
                "Language"
            };
        public static Dictionary<string, Dictionary<string, string>> FieldMappings { get; private set; } = new Dictionary<string, Dictionary<string, string>>();

        #endregion

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

        #region Import Wizard - Step 1 - Open and parse csv-file
        public static async Task ImportCollectionButton()
        {
            // Select the csv-file and create a tempImport object with the content
            await ImportCsvAsync();

            PopulateIdColumnMappingListView(MainWindow.CurrentInstance.IdColumnMappingListView);
            MainWindow.CurrentInstance.GridImportWizard.Visibility = Visibility.Visible;
            MainWindow.CurrentInstance.GridImportIdColumnMapping.Visibility = Visibility.Visible;
        }
        private static async Task ImportCsvAsync()
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

        // Prepare the next step
        private static void PopulateIdColumnMappingListView(ListView listView)
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
                Debug.WriteLine($"Error populating ID column field list view: {ex.Message}");
                MessageBox.Show($"Error populating ID column field list view: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        #endregion

        #region Import Wizard - Step 2a - Find UUIDs - Mapping by card ID
        public static async Task ButtonIdColumnMappingNext()
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                await ProcessIdColumnMappingsAsync();

                AssertNoInvalidUuidFields();

                if (AllItemsHaveUuid())
                {
                    Debug.WriteLine("All items have uuid");
                    GoToAdditionalFieldsMapping();
                }
                else
                {
                    Debug.WriteLine("Not all items have uuid");
                    // Prepare the listview to map card name, set name and set code and go to the first import wizard screen
                    var cardSetFields = new List<string> { "Card Name", "Set Name", "Set Code" };
                    PopulateColumnMappingListView(MainWindow.CurrentInstance.NameAndSetMappingListView, cardSetFields);
                    MainWindow.CurrentInstance.GridImportNameAndSetMapping.Visibility = Visibility.Visible;
                }
                MainWindow.CurrentInstance.GridImportIdColumnMapping.Visibility = Visibility.Collapsed;

                //DebugAllItems();
                //DebugImportProcess();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error field by ID column: {ex.Message}");
                MessageBox.Show($"Error field by ID column: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                stopwatch.Stop();
                Debug.WriteLine($"ButtonIdColumnMappingNext completed in {stopwatch.ElapsedMilliseconds} ms");
            }
        }
        private static async Task ProcessIdColumnMappingsAsync()
        {


            try
            {
                // Get the field from IdColumnMappingListView
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
                    .Append(@" IN (");

                // Prepare a similar batch query for tokens
                var tokenQueryBuilder = new StringBuilder(@"
                        UNION ALL
                        SELECT ti.uuid, ti.")
                    .Append(databaseField)
                    .Append(@"
                        FROM tokenIdentifiers ti
                        INNER JOIN tokens t ON ti.uuid = t.uuid
                        WHERE (t.side IS NULL OR t.side = 'a') 
                        AND ti.")
                    .Append(databaseField)
                    .Append(@" IN (");

                bool hasValues = false;
                int index = 0;
                foreach (var tempItem in tempImport)
                {
                    if (tempItem.Fields.TryGetValue(csvHeader, out var csvValue) && !string.IsNullOrEmpty(csvValue))
                    {
                        if (!csvToUuidsMap.ContainsKey(csvValue))
                        {
                            csvToUuidsMap[csvValue] = new List<string>();
                            batchQueryBuilder.Append($"@csvValue_{index},");
                            tokenQueryBuilder.Append($"@csvValue_{index},");
                            index++;
                            hasValues = true;
                        }
                    }
                }

                if (!hasValues)
                {
                    Debug.WriteLine("No valid CSV values found.");
                    return;
                }

                // Remove the trailing comma and close the "IN" clause
                batchQueryBuilder.Length--;
                batchQueryBuilder.Append(")");

                tokenQueryBuilder.Length--;
                tokenQueryBuilder.Append(")");

                // Combine both parts into one query
                batchQueryBuilder.Append(tokenQueryBuilder);

                using (var command = new SQLiteCommand(batchQueryBuilder.ToString(), DBAccess.connection))
                {
                    index = 0;
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

                //Debug.WriteLine("csvToUuidsMap contents:");
                //foreach (var kvp in csvToUuidsMap)
                //{
                //    Debug.WriteLine($"CSV Value: {kvp.Key}, UUIDs: {string.Join(", ", kvp.Value)}");
                //}

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
                Debug.WriteLine($"Error processing id column field: {ex.Message}");
                MessageBox.Show($"Error processing id column field: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                DBAccess.CloseConnection();
            }
        }

        #endregion

        #region Import Wizard - Step 2b - Find UUIDs - Mapping by card name, set name and set code        
        public static async Task ButtonNameAndSetMappingNext()
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            /*
            --Assumptions--
            An item in tempImport object can have a single uuid field with value
            An item in tempImport object can have a multiple uuids field with value

            Valid scenarios
             - A single item can have single uuid and no multiple uuids
             - A single item can have no uuid and multiple uuids
             - A single item can have no uuid and no multiple uuids

            Invalid scenario:
             - A single item has single uuid field or multiple uuids fields with no value
             - A single item has both single uuid and multiple uuids fields

            --Actions-- 
            Depending on different combinations, three possible actions should happen:

            1. Go to Multiple uuids field screen (if at least one item has multiple uuids)
            2. Go to Additional fields field screen (if at least one item has single uuid OR all items have single uuid AND no items have multiple uuids)
            0. Error screen (if no items have single uuid AND no item has multiple uuids)

            --Possible scenarios--
            No items have have single uuid, no items have multiple uuids --> 0. Error screen
            No items have single uuid, at least one item has multiple uuids --> 1. Go to Multiple uuids field screen
            All items have single uuid --> 2. Go to Additional fields field screen
            At least one item has single uuid, no items have multiple uuids --> 2. Go to Additional fields field screen
            At least one item has single uuids, at least one item has multiple uuids --> 1. Go to Multiple uuids field screen

            --Control Flow Pseudocode--
            Assert for invalid scenarios
            All items have single uuid?
                True: 2. Go to Additional fields field screen
                False:
                    At least one item has multiple uuids?
                        True: 1. Go to Multiple uuids screen select
                        False: 
                            At least one item has single uuid?
                                True: 2. Go to Additional Fields screen
                                False: 0. Error

            */

            // Make a list of the mapped items
            var mappings = MainWindow.CurrentInstance.NameAndSetMappingListView.Items.Cast<ColumnMapping>().ToList();

            var nameMapping = mappings.FirstOrDefault(m => m.CardSetField == "Card Name")?.CsvHeader;
            var setNameMapping = mappings.FirstOrDefault(m => m.CardSetField == "Set Name")?.CsvHeader;
            var setCodeMapping = mappings.FirstOrDefault(m => m.CardSetField == "Set Code")?.CsvHeader;

            // Check if "Name" and either "Set Name" or "Set Code" are mapped
            if (string.IsNullOrEmpty(nameMapping) || (string.IsNullOrEmpty(setNameMapping) && string.IsNullOrEmpty(setCodeMapping)))
            {
                MessageBox.Show("Both name and either set name or set code must be set", "Mapping Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                // Search for unique uuids based on selected csv-headings for card name, set, and set code
                await SearchByCardNameOrSet(mappings);

                // Assert for invalid scenarios
                AssertNoInvalidUuidFields();

                // Do all items in tempImport have single uuid
                if (AllItemsHaveUuid())
                {
                    GoToAdditionalFieldsMapping();
                }
                else
                {
                    // Were multiple uuids found for any items in tempImport?
                    if (AnyItemWithMultipleUuidsField())
                    {
                        PopulateMultipleUuidsDataGrid();
                        MainWindow.CurrentInstance.GridImportMultipleUuidsSelection.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        // Ok then ... were single uuid found for ANY items in tempImport?
                        if (AnyItemWithUuidField())
                        {
                            GoToAdditionalFieldsMapping();
                        }
                        // If not, the import has failed
                        else
                        {
                            tempImport.Clear();
                            MainWindow.CurrentInstance.GridImportWizard.Visibility = Visibility.Collapsed;
                            MessageBox.Show("Was not able to map any cards in the import file the main card database", "Import failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
                MainWindow.CurrentInstance.GridImportNameAndSetMapping.Visibility = Visibility.Collapsed;

                //DebugAllItems();
                //DebugImportProcess();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing field using card and set name and set code: {ex.Message}");
                MessageBox.Show($"Error processing field using card and set name and set code: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                stopwatch.Stop();
                Debug.WriteLine($"ButtonNameAndSetMappingNext completed in {stopwatch.ElapsedMilliseconds} ms");
            }

        }
        private static async Task SearchByCardNameOrSet(List<ColumnMapping> mappings)
        {
            /* The logic is as follows:
            * If set code is mapped search by set code
            * If no match is found by searching by set code, search by set name
            * If set code is not mapped, search by set name
           */
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
                    throw new InvalidOperationException("Name field not found.");
                }

                // Loop through all items in tempImport that do not already have a uuid value
                foreach (var item in tempImport.Where(i => !i.Fields.TryGetValue("uuid", out var uuid) || string.IsNullOrEmpty(uuid)))
                {
                    if (!item.Fields.TryGetValue(nameCsvHeader, out string? name))
                    {
                        //Debug.WriteLine($"Fail: Could not find field for card name with header {nameCsvHeader}");
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

                        //if (!matchFound)
                        //{
                        //    Debug.WriteLine($"Fail: Could not find a match by set code {setCode} for card name {name}");
                        //}
                    }
                    else if (setNameCsvHeader != null && item.Fields.TryGetValue(setNameCsvHeader, out string? setName))
                    {
                        matchFound = await SearchBySetName(name, setName, item);

                        //if (!matchFound)
                        //{
                        //    Debug.WriteLine($"Fail: Could not find a match by set name {setName} for card name {name}");
                        //}
                    }
                    //else
                    //{
                    //    Debug.WriteLine($"Fail: Neither set code nor set name mappings found for card name {name}");
                    //}
                }

                // Rename the CSV columns in tempImport based on the mappings
                RenameFieldsInTempImport(mappings);
                //DebugItemsWithoutUuid();
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
        private static async Task<bool> SearchBySetName(string name, string setName, TempCardItem item)
        {
            // Remove "Extras: " from the beginning of setName if it exists
            if (setName.StartsWith("Extras: ", StringComparison.OrdinalIgnoreCase))
            {
                setName = setName.Substring(8);
            }

            //Debug.WriteLine($"Trying to search by set name: {setName}");

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

            // Query to find the token set code from the sets table based on the set name
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
                //Debug.WriteLine($"Fail: SetCode was null for {setName}");
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
            }

            // If nothing is found in table cards, try the same in table tokens
            if (tokenSetCode != null)
            {
                // Modify the name if it contains " // "
                if (name.Contains(" // "))
                {
                    name = name.Split(new[] { " // " }, StringSplitOptions.None)[0];
                }

                // Remove "Art Card: " from the beginning of setName if it exists
                if (name.StartsWith("Art Card: ", StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(10);
                }

                uuids = await SearchTableForUuidAsync(name, "tokens", tokenSetCode);
                if (uuids.Count > 0)
                {
                    return ProcessUuidResults(uuids, item);
                }
            }

            //Debug.WriteLine($"Fail: Could not find a match for {name} in set {setName}");
            return false;
        }
        private static async Task<bool> SearchBySetCode(string name, string setCode, TempCardItem item)
        {
            //Debug.WriteLine($"Trying to search by set code: {setCode}");

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
            string query = $@"
                SELECT uuid FROM {table} 
                WHERE name = @cardName AND setCode = @setCode AND (side = 'a' OR side IS NULL)
                UNION ALL
                SELECT uuid FROM {table} 
                WHERE faceName = @cardName AND setCode = @setCode AND (side = 'a' OR side IS NULL)";

            //Debug.WriteLine($"Trying to search {table} for card name {cardName} and setCode {setCode}");

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

        // Utility methods to help determine where to go after card name and set name/set code field
        private static bool AllItemsHaveUuid()
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
        private static bool AnyItemWithMultipleUuidsField()
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
        private static bool AnyItemWithUuidField()
        {
            return tempImport.Any(item => item.Fields.TryGetValue("uuid", out var uuid) && !string.IsNullOrEmpty(uuid));
        }

        #endregion

        #region Import Wizard - Step 2c - Find UUIDs - Choose between multiple uuids found

        // Populate the datagrid where a single version can be selected of a card with multiple identical set and card names       
        public static void ButtonMultipleUuidsNext()
        {
            // Directly retrieve items from DataGrid
            var multipleUuidsItems = new List<MultipleUuidsItem>();
            bool allSelected = true;

            // Check that all dropdowns has a value selected
            foreach (var item in MainWindow.CurrentInstance.MultipleUuidsDataGrid.Items)
            {
                if (item is MultipleUuidsItem multipleUuidsItem)
                {
                    // Find the corresponding DataGridRow and ComboBox
                    DataGridRow row = (DataGridRow)MainWindow.CurrentInstance.MultipleUuidsDataGrid.ItemContainerGenerator.ContainerFromItem(multipleUuidsItem);
                    if (row != null)
                    {
                        ComboBox? comboBox = MainWindow.FindVisualChild<ComboBox>(row);
                        if (comboBox != null && comboBox.SelectedItem is UuidVersion selectedVersion)
                        {
                            multipleUuidsItem.SelectedUuid = selectedVersion.Uuid;
                        }
                        else
                        {
                            allSelected = false;
                        }
                    }
                    multipleUuidsItems.Add(multipleUuidsItem);
                }
            }

            // Check if all dropdowns have a selected value
            if (!allSelected)
            {
                MessageBox.Show("Please select a version for all cards before proceeding.", "Selection Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MainWindow.CurrentInstance.GridImportMultipleUuidsSelection.Visibility = Visibility.Collapsed;

            // Convert to List explicitly to ensure we have a concrete collection to work with
            var multipleUuidsList = multipleUuidsItems.ToList();

            // Update tempImport and cardItemsToAdd with the uuids for the selected versions of the cards
            ProcessMultipleUuidSelections(multipleUuidsList);

            // Prepare the listview to map additional fields and make the screen visible
            PopulateColumnMappingListView(MainWindow.CurrentInstance.AddionalFieldsMappingListView, AdditionalFieldsList);
            MainWindow.CurrentInstance.GridImportAdditionalFieldsMapping.Visibility = Visibility.Visible;
        }

        // Update tempImport object with the cards where a uuid match was found
        private static void ProcessMultipleUuidSelections(List<MultipleUuidsItem> multipleUuidsItems)
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
        private static void AssertNoInvalidUuidFields()
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

        // Prepare the next step
        private static void GoToAdditionalFieldsMapping()
        {
            PopulateColumnMappingListView(MainWindow.CurrentInstance.AddionalFieldsMappingListView, AdditionalFieldsList);
            MainWindow.CurrentInstance.GridImportAdditionalFieldsMapping.Visibility = Visibility.Visible;
        }
        #endregion

        #region Import Wizard - Step 2 - Find UUIDs - helper methods
        // Prepare for multiple UUIDs selection if necessary      
        private static void PopulateMultipleUuidsDataGrid()
        {
            try
            {
                var itemsWithMultipleUuids = tempImport
                    .Where(item => item.Fields.ContainsKey("uuids"))
                    .Select(item => new MultipleUuidsItem
                    {
                        Name = item.Fields.ContainsKey("Card Name") ? item.Fields["Card Name"] : "Unknown",
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

        // Handle the UUIDs found anywhere in step 2
        private static bool ProcessUuidResults(List<string> uuids, TempCardItem item)
        {
            if (uuids.Count == 1)
            {
                string singleUuid = uuids[0];
                item.Fields["uuid"] = singleUuid;
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
                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion

        #region Import Wizard - Step 3 - Map additional fields
        public static async Task ButtonAdditionalFieldsNext()
        {
            // Create list of the mapped items
            var mappingsList = MainWindow.CurrentInstance.AddionalFieldsMappingListView.ItemsSource as List<ColumnMapping>;

            if (mappingsList == null)
            {
                MessageBox.Show("No mappings found. Please ensure you have selected the appropriate mappings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Rename the fields in tempImport based on the mappings
            RenameFieldsInTempImport(mappingsList);

            // Store the CardSetField values in _mappings (List<string>)
            _mappings = mappingsList.Select(m => m.CardSetField ?? string.Empty).ToList();

            // Check if the additional fields have a value selected
            foreach (var field in AdditionalFieldsList)
            {
                switch (field)
                {
                    case "Condition":
                        isConditionMapped = IsFieldMapped(mappingsList, field);
                        break;
                    case "Card Finish":
                        isFinishMapped = IsFieldMapped(mappingsList, field);
                        break;
                    case "Cards Owned":
                        isCardsOwnedMapped = IsFieldMapped(mappingsList, field);
                        break;
                    case "Cards For Trade/Selling":
                        isCardsForTradedMapped = IsFieldMapped(mappingsList, field);
                        break;
                    case "Language":
                        isLanguageMapped = IsFieldMapped(mappingsList, field);
                        break;
                    default:
                        break;
                }
            }

            MainWindow.CurrentInstance.GridImportAdditionalFieldsMapping.Visibility = Visibility.Collapsed;


            if (!isCardsOwnedMapped)
            {
                MarkFieldAsUnmapped("Cards Owned");
            }

            if (!isCardsForTradedMapped)
            {
                MarkFieldAsUnmapped("Cards For Trade/Selling");
            }

            if (isConditionMapped)
            {
                await GoToAdditionalFieldMappingGeneric("Condition", MainWindow.CurrentInstance.ConditionsMappingListView, "", MainWindow.CurrentInstance.GridImportCardConditionsMapping);
            }
            else
            {
                MarkFieldAsUnmapped("Condition");

                if (isFinishMapped)
                {
                    await GoToAdditionalFieldMappingGeneric("Card Finish", MainWindow.CurrentInstance.FinishesMappingListView, "finishes", MainWindow.CurrentInstance.GridImportFinishesMapping);
                }
                else
                {
                    MarkFieldAsUnmapped("Card Finish");
                    if (isLanguageMapped)
                    {
                        await GoToAdditionalFieldMappingGeneric("Language", MainWindow.CurrentInstance.LanguageMappingListView, "language", MainWindow.CurrentInstance.GridImportLanguageMapping);
                    }
                    else
                    {
                        MarkFieldAsUnmapped("Language");
                        MainWindow.CurrentInstance.GridImportConfirm.Visibility = Visibility.Visible;
                    }
                }
            }
            DebugFieldMappings();
        }
        public static async Task ButtonConditionMappingNext()
        {
            // Generate the field dictionary for "Condition"
            var conditionMappings = CreateMappingDictionary(
                MainWindow.CurrentInstance.ConditionsMappingListView,
                "Condition",
                "Near Mint");

            // Store the finishesMappings dictionary
            StoreMapping("Condition", conditionMappings, true);

            MainWindow.CurrentInstance.GridImportCardConditionsMapping.Visibility = Visibility.Collapsed;

            if (isFinishMapped) { await GoToAdditionalFieldMappingGeneric("Card Finish", MainWindow.CurrentInstance.FinishesMappingListView, "finishes", MainWindow.CurrentInstance.GridImportFinishesMapping); }
            else
            {
                MarkFieldAsUnmapped("Card Finish");
                if (isLanguageMapped) { await GoToAdditionalFieldMappingGeneric("Language", MainWindow.CurrentInstance.LanguageMappingListView, "language", MainWindow.CurrentInstance.GridImportLanguageMapping); }
                else
                {
                    MarkFieldAsUnmapped("Language");
                    MainWindow.CurrentInstance.GridImportConfirm.Visibility = Visibility.Visible;
                }
            }
            DebugFieldMappings();
        }
        public static async Task ButtonFinishesMappingNext()
        {
            // Generate the field dictionary for "Card Finish"
            var finishesMappings = CreateMappingDictionary(
                MainWindow.CurrentInstance.FinishesMappingListView,
                "Card Finish",
                "nonfoil");

            // Store the finishesMappings dictionary
            StoreMapping("Card Finish", finishesMappings, true);

            MainWindow.CurrentInstance.GridImportFinishesMapping.Visibility = Visibility.Collapsed;

            if (isLanguageMapped) { await GoToAdditionalFieldMappingGeneric("Language", MainWindow.CurrentInstance.LanguageMappingListView, "language", MainWindow.CurrentInstance.GridImportLanguageMapping); }
            else
            {
                MarkFieldAsUnmapped("Language");
                MainWindow.CurrentInstance.GridImportConfirm.Visibility = Visibility.Visible;
            }
            DebugFieldMappings();
        }
        public static void ButtonLanguageMappingNext()
        {
            // Generate the field dictionary for "Language"
            var languageMappings = CreateMappingDictionary(
                MainWindow.CurrentInstance.LanguageMappingListView,
                "Language",
                "English");

            // Store the language dictionary
            StoreMapping("Language", languageMappings, true);

            MainWindow.CurrentInstance.GridImportLanguageMapping.Visibility = Visibility.Collapsed;
            DisplayItemsWithoutUuidInTextBlock();
            MainWindow.CurrentInstance.GridImportConfirm.Visibility = Visibility.Visible;

            //DebugFieldMappings();
            DebugItemsWithoutUuid();
        }

        #endregion

        #region Import Wizard - Step 3 - Map additional fields - helper methods
        // Determine which additional field field screen to go to
        private static bool IsFieldMapped(List<ColumnMapping> mappingsList, string cardSetField)
        {
            // Find the field for the specified cardSetField
            var fieldMapping = mappingsList?.FirstOrDefault(mapping => mapping.CardSetField == cardSetField);

            // Return true if the field exists and the CsvHeader is not null or empty
            return fieldMapping != null && !string.IsNullOrEmpty(fieldMapping.CsvHeader);
        }

        // Go to the next additional field field
        private static async Task GoToAdditionalFieldMappingGeneric(string cardSetField, ListView listView, string tableField, Grid grid)
        {
            // Find the corresponding CSV header for the given cardSetField in _mappings
            var csvHeader = _mappings?.FirstOrDefault(header => header == cardSetField);

            if (!string.IsNullOrEmpty(csvHeader))
            {
                if (await PopulateAdditionalFieldsMappingListViewAsync(csvHeader, !string.IsNullOrEmpty(tableField), tableField, listView))
                {
                    grid.Visibility = Visibility.Visible;
                }

            }
            else
            {
                Debug.WriteLine($"Mapping for {cardSetField} not found.");
            }
        }

        // Populating additional fields field UI elements
        private static async Task<bool> PopulateAdditionalFieldsMappingListViewAsync(string csvHeader, bool fetchFromDatabase, string dbColumn, ListView listView)
        {
            try
            {
                List<string> mappingValues;

                // Mapping values for finish and language should be values found in the cards table
                if (fetchFromDatabase)
                {
                    mappingValues = await GetUniqueValuesFromDbColumn(dbColumn);
                }
                // Mapping values for condition should be the ones specified in the Condition field in the CardSet class.
                else
                {
                    var cardItem = new CardSet.CardItem();
                    mappingValues = cardItem.Conditions;

                }

                // Get unique values from the CSV header
                var csvValues = GetUniqueValuesFromCsv(csvHeader);

                // Check if csvValues is empty and write a debug message if it is. Set it to umapped if it is
                if (csvValues == null || !csvValues.Any())
                {
                    Debug.WriteLine($"No unique values found for CSV header: {csvHeader}, setting it to unmapped");

                    if (listView == MainWindow.CurrentInstance.ConditionsMappingListView)
                    {
                        MarkFieldAsUnmapped("Condition");
                        if (isFinishMapped) { await GoToAdditionalFieldMappingGeneric("Card Finish", MainWindow.CurrentInstance.FinishesMappingListView, "finishes", MainWindow.CurrentInstance.GridImportFinishesMapping); }
                        else if (isLanguageMapped) { await GoToAdditionalFieldMappingGeneric("Language", MainWindow.CurrentInstance.LanguageMappingListView, "language", MainWindow.CurrentInstance.GridImportLanguageMapping); }
                        else { MainWindow.CurrentInstance.GridImportConfirm.Visibility = Visibility.Visible; }
                    }
                    else if (listView == MainWindow.CurrentInstance.FinishesMappingListView)
                    {
                        MarkFieldAsUnmapped("Card Finish");
                        if (isLanguageMapped) { await GoToAdditionalFieldMappingGeneric("Language", MainWindow.CurrentInstance.LanguageMappingListView, "language", MainWindow.CurrentInstance.GridImportLanguageMapping); }
                        else { MainWindow.CurrentInstance.GridImportConfirm.Visibility = Visibility.Visible; }
                    }
                    else
                    {
                        MarkFieldAsUnmapped("Language");
                        MainWindow.CurrentInstance.GridImportConfirm.Visibility = Visibility.Visible;
                    }
                    return false;
                }
                // Create the field items for the list view
                var mappingItems = csvValues
                    .Select(csvValue => new ValueMapping
                    {
                        CsvValue = csvValue,
                        CardSetValue = mappingValues,
                        SelectedCardSetValue = GuessMapping(csvValue, mappingValues) // Leave as null if no match is found
                    })
                    .ToList();

                // Populate the list view with the field items
                listView.ItemsSource = mappingItems;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing field list view: {ex.Message}");
                MessageBox.Show($"Error initializing field list view: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
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
        private static List<string> GetUniqueValuesFromCsv(string? csvHeader)
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

        // Create and manage the dictionary to manage additional fields field
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
                MessageBox.Show($"Error creating field dictionary: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new Dictionary<string, string>();
            }
        }
        private static void StoreMapping(string cardSetField, Dictionary<string, string> mappingDict, bool isMapped)
        {
            if (!isMapped)
            {
                // Mark as unmapped with a special entry
                mappingDict["unmapped_field"] = "unmapped";
            }

            FieldMappings[cardSetField] = mappingDict;
        }
        private static void MarkFieldAsUnmapped(string cardSetField)
        {
            var mappingDict = new Dictionary<string, string>
            {
                ["unmapped_field"] = "unmapped"
            };

            StoreMapping(cardSetField, mappingDict, false);
        }

        #endregion

        #region Import Wizard - Misc. shared methods

        // Try to guess which column name maps to cardItemsToAdd field by looking for matching column/field names
        private static string? GuessMapping(string searchValue, List<string> options)
        {
            var lowerSearchValue = searchValue.ToLower();

            // Dictionary of educated guesses for each search value
            var educatedGuesses = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
    {
        { "Card Name", new List<string> { "Name", "Card", "CardName" } },
        { "Set Name", new List<string> { "Set", "Edition", "Edition Name" } },
        { "Set Code", new List<string> { "Code", "Edition Code" } },
        { "Card Finish", new List<string> { "Finish", "Foil", "Printing" } },
        { "Condition", new List<string> { "Card Condition" } },
        { "Cards Owned", new List<string> { "#", "Quantity", "Count", "Card Count" } },
        { "Cards For Trade/Selling", new List<string> { "Trade", "Sell", "Tradelist", "Tradelist Count" } },
        { "Language", new List<string> { "Card Language" } }
    };

            // Check if there are specific guesses for the search value
            if (educatedGuesses.TryGetValue(searchValue, out var guesses))
            {
                // Try to match any of the educated guesses with the options
                foreach (var guess in guesses)
                {
                    var matchedOption = options.FirstOrDefault(option => option.Equals(guess, StringComparison.OrdinalIgnoreCase));
                    if (matchedOption != null)
                    {
                        return matchedOption;
                    }
                }
            }

            // Fallback to general matching by checking if any option contains the search value
            return options.FirstOrDefault(option => option.ToLower().Contains(lowerSearchValue));
        }

        // Rename fields on tempImport object
        private static void RenameFieldsInTempImport(List<ColumnMapping> mappings)
        {
            foreach (var mapping in mappings)
            {
                if (!string.IsNullOrEmpty(mapping.CsvHeader) && !string.IsNullOrEmpty(mapping.CardSetField))
                {
                    // Rename fields in tempImport based on the field
                    foreach (var item in tempImport)
                    {
                        if (item.Fields.ContainsKey(mapping.CsvHeader))
                        {
                            // Get the value associated with the old field name
                            var value = item.Fields[mapping.CsvHeader];

                            // Remove the old field name
                            item.Fields.Remove(mapping.CsvHeader);

                            // Add the new field name with the value
                            item.Fields[mapping.CardSetField] = value;
                        }
                    }
                    Debug.WriteLine($"CSV Header '{mapping.CsvHeader}' renamed to '{mapping.CardSetField}'");
                }
            }
        }

        // Generic method to populate a field listview from anywhere in the wizard
        private static void PopulateColumnMappingListView(ListView listView, List<string> cardSetFields)
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
                Debug.WriteLine($"Error populating field list view: {ex.Message}");
                MessageBox.Show($"Error populating field list view: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void DisplayItemsWithoutUuidInTextBlock()
        {
            // Clear the TextBlock content first
            MainWindow.CurrentInstance.ItemsUnableToImportTextBlock.Text = "Unable to find matching cards in the database for the following items:\n\n";

            foreach (var item in tempImport)
            {
                // Check if the item does not have a "uuid" field or if the "uuid" field is empty
                if (!item.Fields.TryGetValue("uuid", out var uuid) || string.IsNullOrEmpty(uuid))
                {
                    // Try to get the "Card Name", "Set Name", and "Set Code" values
                    item.Fields.TryGetValue("Card Name", out var cardName);
                    item.Fields.TryGetValue("Set Name", out var setName);
                    item.Fields.TryGetValue("Set Code", out var setCode);

                    // Append the details to the TextBlock, separating them by commas
                    MainWindow.CurrentInstance.ItemsUnableToImportTextBlock.Text += $"{cardName}, {setName}, {setCode}\n";
                }
            }
        }
        private static void DebugItemsWithoutUuid()
        {
            foreach (var item in tempImport)
            {
                // Check if the item does not have a "uuid" field or if the "uuid" field is empty
                if (!item.Fields.TryGetValue("uuid", out var uuid) || string.IsNullOrEmpty(uuid))
                {
                    // Output the details of the item to the debug console
                    Debug.WriteLine("Item without UUID:");

                    foreach (var field in item.Fields)
                    {
                        Debug.WriteLine($"{field.Key}: {field.Value}");
                    }

                    Debug.WriteLine("------------------------------");
                }
            }
        }


        // Debug methods
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
        }
        private static void DebugImportProcess()
        {
            // Total number of items in tempImport
            int totalTempImportItems = tempImport.Count;

            // Number of tempImport items with a single uuid
            int singleUuidItems = tempImport.Count(item => item.Fields.ContainsKey("uuid") && !item.Fields.ContainsKey("uuids"));

            // Number of tempImport items with multiple uuids
            int multipleUuidItems = tempImport.Count(item => item.Fields.ContainsKey("uuids"));

            // Debug write lines
            Debug.WriteLine($"Total number of items in tempImport: {totalTempImportItems}");
            Debug.WriteLine($"Number of tempImport items with single uuid: {singleUuidItems}");
            Debug.WriteLine($"Number of tempImport items with multiple uuids: {multipleUuidItems}");
        }
        #endregion
    }
}