using CollectaMundo.Models;
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
using System.Windows.Media;

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
            public Dictionary<string, string> Fields { get; set; } = [];
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
            public string? CMImportKey { get; set; } // Unique key to identify each instance distinctly
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
            public List<string>? CsvHeaders { get; set; } = [];
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
        public static ObservableCollection<TempCardItem> TempImport { get; private set; } = [];
        public static List<string> AdditionalFieldsList { get; private set; } =
            [
                "Condition",
                "Card Finish",
                "Cards Owned",
                "Cards For Trade/Selling",
                "Language"
            ];
        public static Dictionary<string, Dictionary<string, string>> FieldMappings { get; private set; } = [];

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

                using (SQLiteCommand command = new("SELECT * FROM myCollection", DBAccess.connection))
                using (System.Data.Common.DbDataReader reader = await command.ExecuteReaderAsync())
                {
                    // Check if the reader has any rows
                    if (!reader.HasRows)
                    {
                        MessageBox.Show("Your collection is empty - nothing to back up", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // If there are rows, proceed with creating the backup
                    string backupFilePath = Path.Combine(backupFolderPath, $"CollectaMundoMyCollection_backup_{DateTime.Now:yyyyMMdd}.csv");

                    using (StreamWriter writer = new(backupFilePath, false, Encoding.UTF8))
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
                                string value = reader[i]?.ToString()?.Replace(";", ",") ?? string.Empty; // Replace semicolons to prevent CSV issues
                                writer.Write(value);
                                if (i < reader.FieldCount - 1)
                                {
                                    writer.Write(";");
                                }
                            }
                            writer.WriteLine();
                        }
                    }

                    MessageBox.Show($"Backup created successfully at {backupFilePath}", "A backup of your collection has been created!", MessageBoxButton.OK, MessageBoxImage.Information);
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
        public static async Task BeginImportButton()
        {
            MainWindow.CurrentInstance.GridTopMenu.IsEnabled = false;
            MainWindow.CurrentInstance.GridSideMenu.IsEnabled = false;

            // Select the csv-file and create a TempImport object with the content
            await ImportCsvAsync();
            PopulateIdColumnMappingListView(MainWindow.CurrentInstance.IdColumnMappingListView);
            MainWindow.CurrentInstance.ButtonCancelImport.Visibility = Visibility.Visible;
            MainWindow.CurrentInstance.GridImportStartScreen.Visibility = Visibility.Collapsed;
            MainWindow.CurrentInstance.GridImportIdColumnMapping.Visibility = Visibility.Visible;
        }
        private static async Task ImportCsvAsync()
        {
            try
            {
                OpenFileDialog openFileDialog = new()
                {
                    Filter = "CSV files (*.csv)|*.csv",
                    Title = "Select a CSV file"
                };

                bool? result = openFileDialog.ShowDialog();
                if (result == true)
                {
                    string filePath = openFileDialog.FileName;
                    TempImport = await ParseCsvFileAsync(filePath);
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
            ObservableCollection<TempCardItem> cardItems = [];
            List<string> headers = [];
            char delimiter = ',';

            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                // Read the header
                string? header = await reader.ReadLineAsync();
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
                    string? line = await reader.ReadLineAsync();
                    if (line == null)
                    {
                        continue;
                    }

                    List<string> values = ParseCsvLine(line, delimiter);
                    TempCardItem cardItem = new();

                    for (int i = 0; i < headers.Count; i++)
                    {
                        // Clean the values before adding them to the cardItem.Fields
                        string cleanedValue = RemoveUnwantedPrefixes(values.Count > i ? values[i] : string.Empty);
                        cardItem.Fields[headers[i]] = cleanedValue;
                    }

                    // Add a unique key to each item
                    string uniqueKey = Guid.NewGuid().ToString(); // Use GUID for uniqueness
                    cardItem.Fields["CMImportKey"] = uniqueKey;

                    cardItems.Add(cardItem);
                }
            }

            return cardItems;
        }

        private static List<string> ParseCsvLine(string line, char delimiter)
        {
            List<string> values = [];
            StringBuilder currentField = new();
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
        private static string RemoveUnwantedPrefixes(string input)
        {
            if (input.StartsWith("Extras: "))
            {
                input = input.Substring("Extras: ".Length).Trim();
            }
            else if (input.StartsWith("Art Card: "))
            {
                input = input.Substring("Art Card: ".Length).Trim();
            }

            return input;
        }

        // Prepare the next step
        private static void PopulateIdColumnMappingListView(ListView listView)
        {
            try
            {
                List<string> csvHeaders = TempImport.FirstOrDefault()?.Fields.Keys.ToList() ?? [];
                List<string> databaseFields = GetCardIdentifierColumns(); // Fetch database columns

                ColumnMapping mappingItem = new()
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
            List<string> columns = [];
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
                using (SQLiteCommand command = new(query, DBAccess.connection))
                {
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string? columnName = reader["name"]?.ToString();
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
            MainWindow.CurrentInstance.ButtonIdColumnMappingNext.IsEnabled = false;
            MainWindow.CurrentInstance.CrunchingDataLabel.Visibility = Visibility.Visible;
            MainWindow.CurrentInstance.CrunchingDataLabel.Content = "Crunching data - please wait...";
            MainWindow.CurrentInstance.ButtonSkipIdColumnMapping.Visibility = Visibility.Collapsed;

            try
            {
                // Run the long-running process on a background thread
                await Task.Run(async () =>
                {
                    await ProcessIdColumnMappingsAsync();
                    AssertNoInvalidUuidFields();

                    if (AllItemsHaveUuid())
                    {
                        // Switch back to the UI thread to update UI elements
                        Application.Current.Dispatcher.Invoke(GoToAdditionalFieldsMapping);
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(GoToNameAndSetMapping);
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error field by ID column: {ex.Message}");
                MessageBox.Show($"Error field by ID column: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Ensure UI updates are made on the UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MainWindow.CurrentInstance.CrunchingDataLabel.Visibility = Visibility.Collapsed;
                    MainWindow.CurrentInstance.ButtonSkipIdColumnMapping.Visibility = Visibility.Visible;
                    MainWindow.CurrentInstance.ButtonIdColumnMappingNext.IsEnabled = true;
                });
            }
        }
        public static void ButtonSkipIdColumnMapping()
        {
            GoToNameAndSetMapping();
        }
        private static void GoToNameAndSetMapping()
        {
            List<string> cardSetFields = ["Card Name", "Set Name", "Set Code"];
            PopulateColumnMappingListView(MainWindow.CurrentInstance.NameAndSetMappingListView, cardSetFields);

            MainWindow.CurrentInstance.GridImportIdColumnMapping.Visibility = Visibility.Collapsed;
            MainWindow.CurrentInstance.GridImportNameAndSetMapping.Visibility = Visibility.Visible;
        }
        private static async Task ProcessIdColumnMappingsAsync()
        {
            try
            {
                // Get the field from IdColumnMappingListView
                if (MainWindow.CurrentInstance.IdColumnMappingListView.ItemsSource is not List<ColumnMapping> idColumnMapping || idColumnMapping.Count == 0)
                {
                    Debug.WriteLine("No ID column mappings found.");
                    return;
                }

                ColumnMapping mapping = idColumnMapping.First();

                if (string.IsNullOrEmpty(mapping.SelectedDatabaseField) || string.IsNullOrEmpty(mapping.SelectedCsvHeader))
                {
                    Debug.WriteLine("Both SelectedDatabaseField and SelectedCsvHeader must be set.");
                    return;
                }

                string databaseField = mapping.SelectedDatabaseField;
                string csvHeader = mapping.SelectedCsvHeader;

                await DBAccess.OpenConnectionAsync();

                // Build a dictionary to hold CSV values and their corresponding UUIDs
                Dictionary<string, List<string>> csvToUuidsMap = [];

                // Use a StringBuilder to build a batch SQL query
                StringBuilder batchQueryBuilder = new StringBuilder(@"
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
                StringBuilder tokenQueryBuilder = new StringBuilder(@"
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
                foreach (TempCardItem tempItem in TempImport)
                {
                    if (tempItem.Fields.TryGetValue(csvHeader, out string? csvValue) && !string.IsNullOrEmpty(csvValue))
                    {
                        if (!csvToUuidsMap.ContainsKey(csvValue))
                        {
                            csvToUuidsMap[csvValue] = [];
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

                using (SQLiteCommand command = new(batchQueryBuilder.ToString(), DBAccess.connection))
                {
                    index = 0;
                    foreach (string csvValue in csvToUuidsMap.Keys)
                    {
                        command.Parameters.AddWithValue($"@csvValue_{index}", csvValue);
                        index++;
                    }

                    using (System.Data.Common.DbDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string? uuid = reader["uuid"]?.ToString();
                            string? csvValue = reader[databaseField]?.ToString();

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
                await Task.WhenAll(TempImport.Select(tempItem =>
                {
                    if (tempItem.Fields.TryGetValue(csvHeader, out string? csvValue) && csvToUuidsMap.TryGetValue(csvValue, out List<string>? uuids))
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
            MainWindow.CurrentInstance.CrunchingDataLabel.Visibility = Visibility.Visible;
            MainWindow.CurrentInstance.ButtonNameAndSetMappingNext.IsEnabled = false;

            // Make a list of the mapped items
            List<ColumnMapping> mappings = MainWindow.CurrentInstance.NameAndSetMappingListView.Items.Cast<ColumnMapping>().ToList();

            string? nameMapping = mappings.FirstOrDefault(m => m.CardSetField == "Card Name")?.CsvHeader;
            string? setNameMapping = mappings.FirstOrDefault(m => m.CardSetField == "Set Name")?.CsvHeader;
            string? setCodeMapping = mappings.FirstOrDefault(m => m.CardSetField == "Set Code")?.CsvHeader;

            // Check if "Name" and either "Set Name" or "Set Code" are mapped
            if (string.IsNullOrEmpty(nameMapping) || (string.IsNullOrEmpty(setNameMapping) && string.IsNullOrEmpty(setCodeMapping)))
            {
                MessageBox.Show("Both name and either set name or set code must be set", "Mapping Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Run the long-running process on a background thread
                await Task.Run(async () =>
                {
                    // Perform the search and validation operations
                    await SearchByCardNameOrSet(mappings);

                    // Assert for invalid scenarios
                    AssertNoInvalidUuidFields();

                    // Determine next step based on the UUID presence
                    if (AllItemsHaveUuid())
                    {
                        // Switch back to the UI thread to update UI elements
                        Application.Current.Dispatcher.Invoke(GoToAdditionalFieldsMapping);
                    }
                    else
                    {
                        if (AnyItemWithMultipleUuidsField())
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                PopulateMultipleUuidsDataGrid();
                                MainWindow.CurrentInstance.GridImportMultipleUuidsSelection.Visibility = Visibility.Visible;
                            });
                        }
                        else if (AnyItemWithUuidField())
                        {
                            Application.Current.Dispatcher.Invoke(GoToAdditionalFieldsMapping);
                        }
                        else
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                EndImport();
                                MainWindow.CurrentInstance.GridImportWizard.Visibility = Visibility.Collapsed;
                                MessageBox.Show("Was not able to map any cards in the import file to the main card database", "Import failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                            });
                        }
                    }

                    // Hide the current mapping grid
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MainWindow.CurrentInstance.GridImportNameAndSetMapping.Visibility = Visibility.Collapsed;
                    });
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing field using card and set name and set code: {ex.Message}");
                MessageBox.Show($"Error processing field using card and set name and set code: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                MainWindow.CurrentInstance.CrunchingDataLabel.Visibility = Visibility.Collapsed;
                MainWindow.CurrentInstance.ButtonNameAndSetMappingNext.IsEnabled = true;
            }
        }
        private static async Task SearchByCardNameOrSet(List<ColumnMapping> mappings)
        {
            try
            {
                await DBAccess.OpenConnectionAsync();
                if (DBAccess.connection == null)
                {
                    throw new InvalidOperationException("Database connection is not initialized.");
                }

                string? nameCsvHeader = mappings.FirstOrDefault(m => m.CardSetField == "Card Name")?.CsvHeader;
                string? setNameCsvHeader = mappings.FirstOrDefault(m => m.CardSetField == "Set Name")?.CsvHeader;
                string? setCodeCsvHeader = mappings.FirstOrDefault(m => m.CardSetField == "Set Code")?.CsvHeader;

                if (nameCsvHeader == null)
                {
                    throw new InvalidOperationException("Name field not found.");
                }

                // Rename the CSV columns in TempImport based on the mappings
                RenameFieldsInTempImport(mappings);

                if (!string.IsNullOrEmpty(setCodeCsvHeader))
                {
                    await SearchBySetCode();
                }

                if (!string.IsNullOrEmpty(setNameCsvHeader))
                {
                    await SearchBySetName();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error searching by card name or set: {ex.Message}");
                MessageBox.Show($"Error searching by card name or set: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                DBAccess.CloseConnection();

            }
        }
        private static async Task SearchBySetCode()
        {
            const int batchSize = 800;

            try
            {
                Dictionary<string, List<string>> csvToUuidsMap = [];

                for (int batchStart = 0; batchStart < TempImport.Count; batchStart += batchSize)
                {
                    int batchEnd = Math.Min(batchStart + batchSize, TempImport.Count);
                    List<TempCardItem> currentBatch = TempImport.Skip(batchStart).Take(batchEnd - batchStart)
                        .Where(item => !item.Fields.ContainsKey("uuid") && !item.Fields.ContainsKey("uuids"))
                        .ToList();

                    if (currentBatch.Count == 0)
                    {
                        continue;
                    }

                    StringBuilder batchQueryBuilder = new();
                    batchQueryBuilder.Append(@"
                        SELECT uuid, name, setCode
                        FROM view_cardToken
                        WHERE name ");

                    StringBuilder scenario2QueryBuilder = new(@"
                        UNION ALL
                        SELECT uuid, name, tokenSetCode AS setCode
                        FROM view_cardToken
                        WHERE tokenSetCode <> setCode
                        AND name ");

                    StringBuilder scenario3QueryBuilder = new(@"
                        UNION ALL
                        SELECT uuid, faceName AS name, tokenSetCode AS setCode
                        FROM view_cardToken
                        WHERE faceName ");

                    StringBuilder cardNameInClause = BuildInClause("cardName", currentBatch, out List<SQLiteParameter> nameParameters, "Card Name");
                    batchQueryBuilder.Append(cardNameInClause);
                    scenario2QueryBuilder.Append(cardNameInClause);
                    scenario3QueryBuilder.Append(cardNameInClause);

                    StringBuilder setCodeInClause = BuildInClause("setCode", currentBatch, out List<SQLiteParameter> setCodeParameters, "Set Code");
                    batchQueryBuilder.Append(" AND setCode ").Append(setCodeInClause);
                    scenario2QueryBuilder.Append(" AND tokenSetCode ").Append(setCodeInClause);
                    scenario3QueryBuilder.Append(" AND tokenSetCode ").Append(setCodeInClause);

                    batchQueryBuilder.Append(scenario2QueryBuilder);
                    batchQueryBuilder.Append(scenario3QueryBuilder);

                    await ExecuteBatchQuery(batchQueryBuilder.ToString(), [.. nameParameters, .. setCodeParameters], csvToUuidsMap, "name", "setCode", "cardName");
                }

                await ProcessUuidResultsForField("Set Code", csvToUuidsMap);

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error searching by set code: {ex.Message}");
                MessageBox.Show($"Error searching by set code: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Debug.WriteLine("Status of import after search by name and set code:");
            }
        }
        private static async Task SearchBySetName()
        {
            const int batchSize = 800;

            try
            {
                Dictionary<string, List<string>> csvToUuidsMap = [];

                for (int batchStart = 0; batchStart < TempImport.Count; batchStart += batchSize)
                {
                    int batchEnd = Math.Min(batchStart + batchSize, TempImport.Count);
                    List<TempCardItem> currentBatch = TempImport.Skip(batchStart).Take(batchEnd - batchStart)
                        .Where(item => !item.Fields.ContainsKey("uuid") && !item.Fields.ContainsKey("uuids"))
                        .ToList();

                    if (currentBatch.Count == 0)
                    {
                        continue;
                    }

                    StringBuilder batchQueryBuilder = new();
                    batchQueryBuilder.Append(@"
                        SELECT uuid, name, setName
                        FROM view_cardToken
                        WHERE name ");

                    StringBuilder scenario2QueryBuilder = new(@"
                        UNION ALL
                        SELECT uuid, faceName AS name, setName
                        FROM view_cardToken
                        WHERE faceName ");

                    StringBuilder cardNameInClause = BuildInClause("cardName", currentBatch, out List<SQLiteParameter> nameParameters, "Card Name");
                    batchQueryBuilder.Append(cardNameInClause);
                    scenario2QueryBuilder.Append(cardNameInClause);

                    StringBuilder setNameInClause = BuildInClause("setName", currentBatch, out List<SQLiteParameter> setNameParameters, "Set Name");
                    batchQueryBuilder.Append(" AND setName ").Append(setNameInClause);
                    scenario2QueryBuilder.Append(" AND setName ").Append(setNameInClause);

                    batchQueryBuilder.Append(scenario2QueryBuilder);

                    await ExecuteBatchQuery(batchQueryBuilder.ToString(), [.. nameParameters, .. setNameParameters], csvToUuidsMap, "name", "setName", "cardName");
                }

                await ProcessUuidResultsForField("Set Name", csvToUuidsMap);

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error searching by set name: {ex.Message}");
                MessageBox.Show($"Error searching by set name: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Helper methods for SearchBySetCode and SearchBySetName
        private static StringBuilder BuildInClause(string parameterPrefix, List<TempCardItem> currentBatch, out List<SQLiteParameter> parameters, string searchField)
        {
            StringBuilder queryBuilder = new();
            parameters = [];
            queryBuilder.Append("IN (");

            int index = 0;
            foreach (TempCardItem tempItem in currentBatch)
            {
                if (tempItem.Fields.TryGetValue(searchField, out string? value) && !string.IsNullOrEmpty(value))
                {
                    queryBuilder.Append($"@{parameterPrefix}_{index},");
                    parameters.Add(new SQLiteParameter($"@{parameterPrefix}_{index}", value));
                    index++;
                }
            }

            if (index > 0)
            {
                queryBuilder.Length--; // Remove the trailing comma
            }
            queryBuilder.Append(")");

            return queryBuilder;
        }
        private static async Task ExecuteBatchQuery(string query, List<SQLiteParameter> parameters, Dictionary<string, List<string>> csvToUuidsMap, string searchField, string fieldName, string keyField)
        {
            using (SQLiteCommand command = new(query, DBAccess.connection))
            {
                command.Parameters.AddRange([.. parameters]);

                using (System.Data.Common.DbDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string? uuid = reader["uuid"]?.ToString();
                        string? cardName = reader[searchField]?.ToString();
                        string? setField = reader[fieldName]?.ToString();

                        string key = $"{cardName}_{setField}";

                        if (!string.IsNullOrEmpty(uuid) && !string.IsNullOrEmpty(key))
                        {
                            if (!csvToUuidsMap.ContainsKey(key))
                            {
                                csvToUuidsMap[key] = [];
                            }
                            csvToUuidsMap[key].Add(uuid);
                        }
                    }
                }
            }
        }
        private static async Task ProcessUuidResultsForField(string fieldName, Dictionary<string, List<string>> csvToUuidsMap)
        {
            await Task.WhenAll(TempImport.Select(tempItem =>
            {
                if (tempItem.Fields.TryGetValue("Card Name", out string? cardName) &&
                    tempItem.Fields.TryGetValue(fieldName, out string? setValue))
                {
                    string key = $"{cardName}_{setValue}";
                    if (csvToUuidsMap.TryGetValue(key, out List<string>? uuids))
                    {
                        return Task.Run(() => ProcessUuidResults(uuids, tempItem));
                    }
                }
                return Task.CompletedTask;
            }));
        }

        #endregion

        #region Import Wizard - Step 2c - Find UUIDs - Choose between multiple uuids found

        // Populate the datagrid where a single version can be selected of a card with multiple identical set and card names       
        private static void PopulateMultipleUuidsDataGrid()
        {
            try
            {
                List<MultipleUuidsItem> itemsWithMultipleUuids = TempImport
                    .Where(item => item.Fields.ContainsKey("uuids"))
                    .Select(item => new MultipleUuidsItem
                    {
                        Name = item.Fields.ContainsKey("Card Name") ? item.Fields["Card Name"] : "Unknown",
                        VersionedUuids = item.Fields["uuids"].Split(',').Select((uuid, index) => new UuidVersion { DisplayText = $"Version {index + 1}", Uuid = uuid }).ToList(),
                        CMImportKey = item.Fields["CMImportKey"],
                        SelectedUuid = null // Set the initial selection to null
                    })
                    .ToList();

                Debug.WriteLine($"Populated MultipleUuidsDataGrid with {itemsWithMultipleUuids.Count} items.");

                MainWindow.CurrentInstance.MultipleUuidsDataGrid.ItemsSource = itemsWithMultipleUuids;
                MainWindow.CurrentInstance.MultipleUuidsDataGrid.Visibility = itemsWithMultipleUuids.Count != 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error populating multiple uuids datagrid: {ex.Message}");
                MessageBox.Show($"Error populating multiple uuids datagrid: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public static void ButtonMultipleUuidsNext()
        {
            // Directly retrieve items from DataGrid
            List<MultipleUuidsItem> multipleUuidsItems = [];
            bool allSelected = true;

            // Check that all dropdowns has a value selected
            foreach (object? item in MainWindow.CurrentInstance.MultipleUuidsDataGrid.Items)
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
            List<MultipleUuidsItem> multipleUuidsList = [.. multipleUuidsItems];

            // Update TempImport and CardItemsToAdd with the uuids for the selected versions of the cards
            ProcessMultipleUuidSelections(multipleUuidsList);

            // Prepare the listview to map additional fields and make the screen visible
            PopulateColumnMappingListView(MainWindow.CurrentInstance.AddionalFieldsMappingListView, AdditionalFieldsList);

            MainWindow.CurrentInstance.ImagePromoLabel.Content = string.Empty;
            MainWindow.CurrentInstance.ImageSetLabel.Content = string.Empty;
            MainWindow.CurrentInstance.ImageSourceUrl = null;
            MainWindow.CurrentInstance.ImageSourceUrl2nd = null;
            MainWindow.CurrentInstance.GridImportAdditionalFieldsMapping.Visibility = Visibility.Visible;
        }

        // Update TempImport object with the cards where a uuid match was found
        private static void ProcessMultipleUuidSelections(List<MultipleUuidsItem> multipleUuidsItems)
        {
            foreach (MultipleUuidsItem item in multipleUuidsItems)
            {
                if (!string.IsNullOrEmpty(item.SelectedUuid))
                {
                    TempCardItem? tempItem = TempImport.FirstOrDefault(t => t.Fields.ContainsKey("CMImportKey") && t.Fields["CMImportKey"] == item.CMImportKey);

                    // Remove the field uuids and add the field uuid with the selected version of the card
                    if (tempItem != null)
                    {
                        tempItem.Fields["uuid"] = item.SelectedUuid;
                        tempItem.Fields.Remove("uuids");
                    }
                }
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
                StringBuilder sb = new();
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

        // Helper method for getting debug info for AssertNoInvalidUuidFields()
        private static string GetItemDetails(TempCardItem item)
        {
            item.Fields.TryGetValue("Card Name", out string? cardName);
            item.Fields.TryGetValue("Set Name", out string? setName);
            item.Fields.TryGetValue("Set Code", out string? setCode);

            return $"Card Name: {cardName}, Set Name: {setName}, Set Code: {setCode}, UUID: {item.Fields.GetValueOrDefault("uuid")}, UUIDs: {item.Fields.GetValueOrDefault("uuids")}";
        }

        // Utility methods to help determine whether to proceed to additionalfields mapping
        private static void AssertNoInvalidUuidFields()
        {
            List<TempCardItem> invalidUuidAndUuidsItems = TempImport.Where(item =>
                item.Fields.TryGetValue("uuid", out string? uuid) && !string.IsNullOrEmpty(uuid) &&
                item.Fields.TryGetValue("uuids", out string? uuids) && !string.IsNullOrEmpty(uuids)
            ).ToList();

            List<TempCardItem> invalidUuidOrUuidsItems = TempImport.Where(item =>
                (item.Fields.TryGetValue("uuid", out string? uuid) && string.IsNullOrEmpty(uuid)) ||
                (item.Fields.TryGetValue("uuids", out string? uuids) && string.IsNullOrEmpty(uuids))
            ).ToList();

            if (invalidUuidAndUuidsItems.Count != 0)
            {
                foreach (TempCardItem? item in invalidUuidAndUuidsItems)
                {
                    Debug.WriteLine($"Invalid item with both 'uuid' and 'uuids' fields with values: {GetItemDetails(item)}");
                }
                throw new InvalidOperationException("One or more items in TempImport have both 'uuid' and 'uuids' fields with values, which is not allowed.");
            }

            if (invalidUuidOrUuidsItems.Count != 0)
            {
                foreach (TempCardItem? item in invalidUuidOrUuidsItems)
                {
                    Debug.WriteLine($"Invalid item with 'uuid' or 'uuids' field with no value: {GetItemDetails(item)}");
                }
                throw new InvalidOperationException("One or more items in TempImport have 'uuid' or 'uuids' field with no value, which is not allowed.");
            }
        }
        private static bool AllItemsHaveUuid()
        {
            foreach (TempCardItem tempItem in TempImport)
            {
                if (!tempItem.Fields.TryGetValue("uuid", out string? uuid) || string.IsNullOrEmpty(uuid))
                {
                    return false;
                }
            }
            return true;
        }
        private static bool AnyItemWithMultipleUuidsField()
        {
            bool hasUuids = TempImport.Any(item =>
            {
                if (item.Fields.TryGetValue("uuids", out string? uuids))
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
            return TempImport.Any(item => item.Fields.TryGetValue("uuid", out string? uuid) && !string.IsNullOrEmpty(uuid));
        }

        #endregion

        #region Import Wizard - Step 3 - Map additional fields
        public static async Task ButtonAdditionalFieldsNext()
        {
            // Create list of the mapped items

            if (MainWindow.CurrentInstance.AddionalFieldsMappingListView.ItemsSource is not List<ColumnMapping> mappingsList)
            {
                MessageBox.Show("No mappings found. Please ensure you have selected the appropriate mappings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Rename the fields in TempImport based on the mappings
            RenameFieldsInTempImport(mappingsList);

            // Store the CardSetField values in _mappings (List<string>)
            _mappings = mappingsList.Select(m => m.CardSetField ?? string.Empty).ToList();

            // Check if the additional fields have a value selected
            foreach (string field in AdditionalFieldsList)
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
                        GoToFinalStep();
                    }
                }
            }
        }
        public static async Task ButtonConditionMappingNext()
        {
            // Generate the field dictionary for "Condition"
            Dictionary<string, string> conditionMappings = CreateMappingDictionary(MainWindow.CurrentInstance.ConditionsMappingListView, "Condition", "Near Mint");

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
                    GoToFinalStep();
                }
            }
        }
        public static async Task ButtonFinishesMappingNext()
        {
            // Generate the field dictionary for "Card Finish"
            Dictionary<string, string> finishesMappings = CreateMappingDictionary(MainWindow.CurrentInstance.FinishesMappingListView, "Card Finish", "nonfoil");

            StoreMapping("Card Finish", finishesMappings, true);

            MainWindow.CurrentInstance.GridImportFinishesMapping.Visibility = Visibility.Collapsed;

            if (isLanguageMapped) { await GoToAdditionalFieldMappingGeneric("Language", MainWindow.CurrentInstance.LanguageMappingListView, "language", MainWindow.CurrentInstance.GridImportLanguageMapping); }
            else
            {
                MarkFieldAsUnmapped("Language");
                GoToFinalStep();
            }
        }
        public static void ButtonLanguageMappingNext()
        {
            // Generate the field dictionary for "Language"
            Dictionary<string, string> languageMappings = CreateMappingDictionary(MainWindow.CurrentInstance.LanguageMappingListView, "Language", "English");
            StoreMapping("Language", languageMappings, true);

            Thickness currentMargin = MainWindow.CurrentInstance.ButtonCancelImport.Margin;
            Thickness newMargin = new(currentMargin.Left, 180, currentMargin.Right, currentMargin.Bottom);
            MainWindow.CurrentInstance.ButtonCancelImport.Margin = newMargin;

            MainWindow.CurrentInstance.GridImportLanguageMapping.Visibility = Visibility.Collapsed;
            GoToFinalStep();
        }

        #endregion

        #region Import Wizard - Step 3 - Map additional fields - helper methods
        // Determine which additional field field screen to go to
        private static bool IsFieldMapped(List<ColumnMapping> mappingsList, string cardSetField)
        {
            // Find the field for the specified cardSetField
            ColumnMapping? fieldMapping = mappingsList?.FirstOrDefault(mapping => mapping.CardSetField == cardSetField);

            // Return true if the field exists and the CsvHeader is not null or empty
            return fieldMapping != null && !string.IsNullOrEmpty(fieldMapping.CsvHeader);
        }

        // Go to the next additional field field
        private static async Task GoToAdditionalFieldMappingGeneric(string cardSetField, ListView listView, string tableField, Grid grid)
        {
            // Find the corresponding CSV header for the given cardSetField in _mappings
            string? csvHeader = _mappings?.FirstOrDefault(header => header == cardSetField);

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
                    CardSet.CardItem cardItem = new();
                    mappingValues = cardItem.Conditions;

                }

                // Get unique values from the CSV header
                List<string> csvValues = GetUniqueValuesFromCsv(csvHeader);

                // Check if csvValues is empty and write a debug message if it is. Set it to umapped if it is
                if (csvValues == null || csvValues.Count == 0)
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
                List<ValueMapping> mappingItems = csvValues
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
            HashSet<string> uniqueValues = [];

            string query = $"SELECT DISTINCT {dbColumn} FROM cards";

            await DBAccess.OpenConnectionAsync();
            using (SQLiteCommand command = new(query, DBAccess.connection))
            using (System.Data.Common.DbDataReader reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    string? value = reader[dbColumn]?.ToString();
                    if (!string.IsNullOrEmpty(value))
                    {
                        string[] splitValues = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string splitValue in splitValues)
                        {
                            uniqueValues.Add(splitValue.Trim());
                        }
                    }
                }
            }
            DBAccess.CloseConnection();

            return [.. uniqueValues];
        }
        private static List<string> GetUniqueValuesFromCsv(string? csvHeader)
        {
            HashSet<string> uniqueValues = [];

            if (csvHeader == null)
            {
                return [.. uniqueValues];
            }

            foreach (TempCardItem item in TempImport)
            {
                if (item.Fields.TryGetValue(csvHeader, out string? value) && !string.IsNullOrEmpty(value))
                {
                    uniqueValues.Add(value);
                }
            }

            return [.. uniqueValues];
        }

        // Create and manage the dictionary to manage additional fields field
        private static Dictionary<string, string> CreateMappingDictionary(ListView mappingListView, string cardSetField, string defaultValue)
        {
            try
            {
                // Initialize the dictionary that will hold the mappings for the specified field
                Dictionary<string, string> fieldMappingDict = [];

                // Get the mappings from the specified ListView

                if (mappingListView.ItemsSource is not List<ValueMapping> mappings)
                {
                    Debug.WriteLine("No mappings found.");
                    return fieldMappingDict;
                }

                // Populate the dictionary with mappings from the ListView
                foreach (ValueMapping mapping in mappings)
                {
                    if (!string.IsNullOrEmpty(mapping.CsvValue))
                    {
                        string databaseValue = mapping.SelectedCardSetValue ?? defaultValue;
                        fieldMappingDict[mapping.CsvValue] = databaseValue;
                    }
                }

                return fieldMappingDict;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating field dictionary: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return [];
            }
        }
        private static void StoreMapping(string cardSetField, Dictionary<string, string> mappingDict, bool isMapped)
        {
            if (!isMapped)
            {
                mappingDict["unmapped_field"] = "unmapped";
            }
            FieldMappings[cardSetField] = mappingDict;
        }
        private static void MarkFieldAsUnmapped(string cardSetField)
        {
            Dictionary<string, string> mappingDict = new() { ["unmapped_field"] = "unmapped" };
            StoreMapping(cardSetField, mappingDict, false);
        }

        // Prepare the final step
        private static void GoToFinalStep()
        {
            GenerateSummaryInTextBlock();
            MainWindow.CurrentInstance.GridImportConfirm.Visibility = Visibility.Visible;
        }

        #endregion

        #region Import Wizard - Confirm, import and end wizard
        public static void SaveUnimportedItemsToFile()
        {
            // Create a SaveFileDialog to prompt the user to choose a save location
            SaveFileDialog saveFileDialog = new()
            {
                FileName = "UnimportedItems", // Default file name
                DefaultExt = ".txt", // Default file extension
                Filter = "Text documents (.txt)|*.txt" // Filter files by extension
            };

            // Show the dialog and get the chosen file path
            bool? result = saveFileDialog.ShowDialog();

            if (result == true)
            {
                // Get the selected file path
                string filePath = saveFileDialog.FileName;

                // Generate the content for the file
                List<string> lines = ["Unable to find matching cards in the database for the following items:\n"];

                foreach (TempCardItem item in TempImport)
                {
                    // Check if the item does not have a "uuid" field or if the "uuid" field is empty
                    if (!item.Fields.TryGetValue("uuid", out string? uuid) || string.IsNullOrEmpty(uuid))
                    {
                        // Try to get the "Card Name", "Set Name", and "Set Code" values
                        item.Fields.TryGetValue("Card Name", out string? cardName);
                        item.Fields.TryGetValue("Set Name", out string? setName);
                        item.Fields.TryGetValue("Set Code", out string? setCode);

                        // Add the line with card details
                        lines.Add($"{cardName}, {setName}, {setCode}");
                    }
                }

                // Write all lines to the file
                File.WriteAllLines(filePath, lines);
            }
        }

        // Generate the summary on the final screen
        private static void GenerateSummaryInTextBlock()
        {
            // Clear the existing content of the container
            MainWindow.CurrentInstance.ImportSummaryContainer.Children.Clear();

            // Add the summary text in a two-column table format
            Grid summaryGrid = new()
            {
                Margin = new Thickness(0, 10, 0, 10)
            };

            // Define two columns
            summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            int countReadyToImport = 0;
            int countUnableToImport = 0;
            int totalCardsToAdd = 0;

            foreach (TempCardItem item in TempImport)
            {
                if (item.Fields.TryGetValue("uuid", out string? uuid) && !string.IsNullOrEmpty(uuid))
                {
                    countReadyToImport++;
                    if (item.Fields.TryGetValue("Cards Owned", out string? cardsOwnedValue) && int.TryParse(cardsOwnedValue, out int cardsOwned))
                    {
                        totalCardsToAdd += cardsOwned;
                    }
                }
                else
                {
                    countUnableToImport++;
                }
            }

            MainWindow.CurrentInstance.LabelIndividualCards.Content = countReadyToImport.ToString();
            MainWindow.CurrentInstance.LabelTotalSumOfCards.Content = totalCardsToAdd.ToString();
            MainWindow.CurrentInstance.LabelSumOfUnAbleToImport.Content = countUnableToImport.ToString();

            // Add rows to the summaryGrid to ensure the items are displayed on separate lines
            summaryGrid.RowDefinitions.Add(new RowDefinition());
            summaryGrid.RowDefinitions.Add(new RowDefinition());
            summaryGrid.RowDefinitions.Add(new RowDefinition());

            // Add the summary grid to the container
            MainWindow.CurrentInstance.ImportSummaryContainer.Children.Add(summaryGrid);

            // If there are items without uuids, create a Grid to display them in a table format
            if (countUnableToImport > 0)
            {
                MainWindow.CurrentInstance.LabelUnableToImportCardsHeading.Visibility = Visibility.Visible;
                MainWindow.CurrentInstance.SaveListOfUnimportedItems.Visibility = Visibility.Visible;

                Grid tableGrid = new()
                {
                    Margin = new Thickness(0, 10, 0, 0)
                };

                // Define three columns
                tableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                tableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                tableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Add header row
                tableGrid.RowDefinitions.Add(new RowDefinition());
                AddTextToGrid(tableGrid, "Card Name", 0, 0, FontWeights.Bold);
                AddTextToGrid(tableGrid, "Set Name", 0, 1, FontWeights.Bold);
                AddTextToGrid(tableGrid, "Set Code", 0, 2, FontWeights.Bold);

                int row = 1;

                foreach (TempCardItem item in TempImport)
                {
                    if (!item.Fields.TryGetValue("uuid", out string? uuid) || string.IsNullOrEmpty(uuid))
                    {
                        // Use null-coalescing operator to provide a default value if any of these are null
                        string cardName = item.Fields.TryGetValue("Card Name", out string? cn) ? cn : "Unknown";
                        string setName = item.Fields.TryGetValue("Set Name", out string? sn) ? sn : "Unknown";
                        string setCode = item.Fields.TryGetValue("Set Code", out string? sc) ? sc : "Unknown";

                        // Add data rows
                        tableGrid.RowDefinitions.Add(new RowDefinition());
                        AddTextToGrid(tableGrid, cardName, row, 0);
                        AddTextToGrid(tableGrid, setName, row, 1);
                        AddTextToGrid(tableGrid, setCode, row, 2);

                        row++;
                    }
                }

                // Add the Grid to the container
                MainWindow.CurrentInstance.ImportSummaryContainer.Children.Add(tableGrid);
            }
        }
        private static void AddTextToGrid(Grid grid, string text, int row, int column, FontWeight? fontWeight = null)
        {
            TextBlock textBlock = new() { Text = text, Margin = new Thickness(5) };
            if (fontWeight.HasValue)
            {
                textBlock.FontWeight = fontWeight.Value;
            }
            Grid.SetRow(textBlock, row);
            Grid.SetColumn(textBlock, column);
            grid.Children.Add(textBlock);
        }

        // Add imported cards to database
        public static async Task AddItemsToDatabaseAsync()
        {
            // Disable UI elements during import
            MainWindow.CurrentInstance.ButtonCancelImport.Visibility = Visibility.Collapsed;
            MainWindow.CurrentInstance.ButtonImportConfirm.Visibility = Visibility.Collapsed;
            MainWindow.CurrentInstance.LabelUnableToImportCardsHeading.Visibility = Visibility.Collapsed;
            MainWindow.CurrentInstance.SaveListOfUnimportedItems.Visibility = Visibility.Collapsed;
            MainWindow.CurrentInstance.ImportSummaryContainer.Visibility = Visibility.Collapsed;
            MainWindow.CurrentInstance.CrunchingDataLabel.Visibility = Visibility.Visible;
            MainWindow.CurrentInstance.CrunchingDataLabel.Content = "Importing cards";

            const int batchSize = 4000; // Optimal batch size for performance

            try
            {
                // Open the database connection
                await DBAccess.OpenConnectionAsync();

                // Process TempImport in batches
                for (int batchStart = 0; batchStart < TempImport.Count; batchStart += batchSize)
                {
                    int batchEnd = Math.Min(batchStart + batchSize, TempImport.Count);
                    List<TempCardItem> currentBatch = TempImport.Skip(batchStart).Take(batchEnd - batchStart).ToList();

                    // Run batch processing in a background thread
                    await Task.Run(async () =>
                    {
                        // Create a list to store all database commands for this batch
                        List<SQLiteCommand> commands = [];

                        foreach (TempCardItem? tempItem in currentBatch)
                        {
                            // Extract relevant fields
                            string uuid = tempItem.Fields.TryGetValue("uuid", out string? uuidValue) ? uuidValue : string.Empty;
                            string condition = tempItem.Fields.TryGetValue("Condition", out string? conditionValue) ? conditionValue : "Near Mint";
                            string finish = tempItem.Fields.TryGetValue("Card Finish", out string? finishValue) ? finishValue : "nonfoil";
                            string language = tempItem.Fields.TryGetValue("Language", out string? languageValue) ? languageValue : "English";
                            string cardsOwnedStr = tempItem.Fields.TryGetValue("Cards Owned", out string? cardsOwnedValue) ? cardsOwnedValue : "1";
                            string cardsForTradeStr = tempItem.Fields.TryGetValue("Cards For Trade/Selling", out string? cardsForTradeValue) ? cardsForTradeValue : "0";

                            // Convert 'Cards Owned' and 'Cards For Trade/Selling' to integers
                            int cardsOwned = int.TryParse(cardsOwnedStr, out int owned) ? owned : 1;
                            int cardsForTrade = int.TryParse(cardsForTradeStr, out int trade) ? trade : 0;

                            // Map values using FieldMappings
                            condition = MapFieldValue("Condition", condition, "Near Mint");
                            finish = MapFieldValue("Card Finish", finish, "nonfoil");
                            language = MapFieldValue("Language", language, "English");

                            // Check available languages for this UUID
                            string checkLanguagesQuery = @"
                                SELECT language FROM cardForeignData WHERE uuid = @uuid
                                UNION
                                SELECT language FROM cards WHERE uuid = @uuid
                                UNION
                                SELECT language FROM tokens WHERE uuid = @uuid";

                            HashSet<string> availableLanguages = [];

                            using (SQLiteCommand checkLanguagesCommand = new(checkLanguagesQuery, DBAccess.connection))
                            {
                                checkLanguagesCommand.Parameters.AddWithValue("@uuid", uuid);
                                using (System.Data.Common.DbDataReader reader = await checkLanguagesCommand.ExecuteReaderAsync())
                                {
                                    while (await reader.ReadAsync())
                                    {
                                        string? languageFromDb = reader["language"]?.ToString();
                                        if (!string.IsNullOrEmpty(languageFromDb))
                                        {
                                            availableLanguages.Add(languageFromDb);
                                        }
                                    }
                                }
                            }

                            // Adjust language if not available
                            if (!availableLanguages.Contains(language))
                            {
                                language = "English"; // Default to English if the desired language is not available
                            }

                            // Check if an entry exists in 'myCollection' with the same uuid, language, and finish
                            string query = "SELECT count, trade FROM myCollection WHERE uuid = @uuid AND language = @language AND finish = @finish";
                            using (SQLiteCommand command = new(query, DBAccess.connection))
                            {
                                command.Parameters.AddWithValue("@uuid", uuid);
                                command.Parameters.AddWithValue("@language", language);
                                command.Parameters.AddWithValue("@finish", finish);

                                using (System.Data.Common.DbDataReader reader = await command.ExecuteReaderAsync())
                                {
                                    if (await reader.ReadAsync())
                                    {
                                        // Row exists, update 'count' and 'trade' columns
                                        int currentCount = reader.GetInt32(0);
                                        int currentTrade = reader.GetInt32(1);

                                        // Update the values
                                        currentCount += cardsOwned;
                                        currentTrade += cardsForTrade;

                                        // Update query
                                        string updateQuery = "UPDATE myCollection SET count = @count, trade = @trade WHERE uuid = @uuid AND language = @language AND finish = @finish";
                                        SQLiteCommand updateCommand = new(updateQuery, DBAccess.connection);
                                        updateCommand.Parameters.AddWithValue("@count", currentCount);
                                        updateCommand.Parameters.AddWithValue("@trade", currentTrade);
                                        updateCommand.Parameters.AddWithValue("@uuid", uuid);
                                        updateCommand.Parameters.AddWithValue("@language", language);
                                        updateCommand.Parameters.AddWithValue("@finish", finish);
                                        commands.Add(updateCommand);
                                    }
                                    else
                                    {
                                        // Row does not exist, insert a new entry
                                        string insertQuery = @"INSERT INTO myCollection (uuid, count, trade, condition, finish, language) VALUES (@uuid, @count, @trade, @condition, @finish, @language)";
                                        SQLiteCommand insertCommand = new(insertQuery, DBAccess.connection);
                                        insertCommand.Parameters.AddWithValue("@uuid", uuid);
                                        insertCommand.Parameters.AddWithValue("@count", cardsOwned);
                                        insertCommand.Parameters.AddWithValue("@trade", cardsForTrade);
                                        insertCommand.Parameters.AddWithValue("@condition", condition);
                                        insertCommand.Parameters.AddWithValue("@finish", finish);
                                        insertCommand.Parameters.AddWithValue("@language", language);
                                        commands.Add(insertCommand);
                                    }
                                }
                            }
                        }

                        // Execute all commands for the batch
                        if (DBAccess.connection != null)
                        {
                            using (SQLiteTransaction transaction = DBAccess.connection.BeginTransaction())
                            {
                                foreach (SQLiteCommand cmd in commands)
                                {
                                    await cmd.ExecuteNonQueryAsync();
                                }
                                transaction.Commit();
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error adding items to the database: {ex.Message}");
                MessageBox.Show($"Error adding items to the database: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Ensure the database connection is closed
                await DBAccess.OptimizeDb();
                DBAccess.CloseConnection();
                MainWindow.CurrentInstance.GridImportConfirm.Visibility = Visibility.Collapsed;
                MainWindow.CurrentInstance.CrunchingDataLabel.Visibility = Visibility.Collapsed;
                MainWindow.CurrentInstance.GridImportSuccess.Visibility = Visibility.Visible;
                Debug.WriteLine("Import complete");
            }
        }

        // Helper method to map field values using FieldMappings
        private static string MapFieldValue(string field, string csvValue, string defaultValue)
        {
            // Find the mapping dictionary for the specified field
            Dictionary<string, string> mapping = FieldMappings.FirstOrDefault(m => m.Key == field).Value;

            if (mapping != null)
            {
                // Check for an exact match in the CSV values
                if (mapping.TryGetValue(csvValue, out string? mappedValue) && !string.IsNullOrEmpty(mappedValue) && mappedValue != "unmapped")
                {
                    return mappedValue;
                }
            }

            // If no mapping or 'unmapped', return the default value
            return defaultValue;
        }
        public static async Task EndImportWizard()
        {
            await DBAccess.OpenConnectionAsync();
            await MainWindow.PopulateCardDataGridAsync(MainWindow.CurrentInstance.myCards, MainWindow.CurrentInstance.myCollectionQuery, MainWindow.CurrentInstance.MyCollectionDataGrid);
            DBAccess.CloseConnection();

            EndImport();

            // Go to My Collections
            MainWindow.CurrentInstance.ResetGrids();
            MainWindow.CurrentInstance.MenuMyCollectionButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5cb9ca"));
            MainWindow.CurrentInstance.GridFiltering.Visibility = Visibility.Visible;
            MainWindow.CurrentInstance.LogoSmall.Visibility = Visibility.Visible;
            MainWindow.CurrentInstance.GridMyCollection.Visibility = Visibility.Visible;
        }

        #endregion

        #region Import Wizard - Misc. helper and shared methods
        public static void EndImport()
        {
            TempImport.Clear();

            // Make sure all the import wizard screens are collapsed (we don't know when the import is cancelled)
            MainWindow.CurrentInstance.GridImportWizard.Visibility = Visibility.Collapsed;
            MainWindow.CurrentInstance.GridImportStartScreen.Visibility = Visibility.Collapsed;
            MainWindow.CurrentInstance.GridImportIdColumnMapping.Visibility = Visibility.Collapsed;
            MainWindow.CurrentInstance.GridImportNameAndSetMapping.Visibility = Visibility.Collapsed;
            MainWindow.CurrentInstance.GridImportMultipleUuidsSelection.Visibility = Visibility.Collapsed;
            MainWindow.CurrentInstance.GridImportAdditionalFieldsMapping.Visibility = Visibility.Collapsed;
            MainWindow.CurrentInstance.GridImportCardConditionsMapping.Visibility = Visibility.Collapsed;
            MainWindow.CurrentInstance.GridImportFinishesMapping.Visibility = Visibility.Collapsed;
            MainWindow.CurrentInstance.GridImportLanguageMapping.Visibility = Visibility.Collapsed;
            MainWindow.CurrentInstance.GridImportConfirm.Visibility = Visibility.Collapsed;
            MainWindow.CurrentInstance.GridImportSuccess.Visibility = Visibility.Collapsed;

            // Reenable menu buttons
            MainWindow.CurrentInstance.GridTopMenu.IsEnabled = true;
            MainWindow.CurrentInstance.GridSideMenu.IsEnabled = true;

            // Reset other Import UI elements            
            MainWindow.CurrentInstance.CrunchingDataLabel.Content = string.Empty;
            MainWindow.CurrentInstance.ButtonCancelImport.Visibility = Visibility.Collapsed;
            MainWindow.CurrentInstance.ButtonImportConfirm.Visibility = Visibility.Visible;
            MainWindow.CurrentInstance.SaveListOfUnimportedItems.Visibility = Visibility.Collapsed;
            MainWindow.CurrentInstance.LabelUnableToImportCardsHeading.Visibility = Visibility.Collapsed;
            MainWindow.CurrentInstance.CrunchingDataLabel.Visibility = Visibility.Collapsed;
            MainWindow.CurrentInstance.Inspiredtinkering.Visibility = Visibility.Visible;

            Thickness currentMargin = MainWindow.CurrentInstance.ButtonCancelImport.Margin;
            Thickness newMargin = new(currentMargin.Left, 350, currentMargin.Right, currentMargin.Bottom);
            MainWindow.CurrentInstance.ButtonCancelImport.Margin = newMargin;

            // Reset card images
            MainWindow.CurrentInstance.ImagePromoLabel.Content = string.Empty;
            MainWindow.CurrentInstance.ImageSetLabel.Content = string.Empty;
            MainWindow.CurrentInstance.ImageSourceUrl = null;
            MainWindow.CurrentInstance.ImageSourceUrl2nd = null;
        }

        // Try to guess which column name maps to CardItemsToAdd field by looking for matching column/field names
        private static string? GuessMapping(string searchValue, List<string> options)
        {
            string lowerSearchValue = searchValue.ToLower();

            // Dictionary of educated guesses for each search value
            Dictionary<string, List<string>> educatedGuesses = new(StringComparer.OrdinalIgnoreCase)
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
            if (educatedGuesses.TryGetValue(searchValue, out List<string>? guesses))
            {
                // Try to match any of the educated guesses with the options
                foreach (string guess in guesses)
                {
                    string? matchedOption = options.FirstOrDefault(option => option.Equals(guess, StringComparison.OrdinalIgnoreCase));
                    if (matchedOption != null)
                    {
                        return matchedOption;
                    }
                }
            }

            // Fallback to general matching by checking if any option contains the search value
            return options.FirstOrDefault(option => option.ToLower().Contains(lowerSearchValue));
        }

        // Rename fields on TempImport object
        private static void RenameFieldsInTempImport(List<ColumnMapping> mappings)
        {
            foreach (ColumnMapping mapping in mappings)
            {
                if (!string.IsNullOrEmpty(mapping.CsvHeader) && !string.IsNullOrEmpty(mapping.CardSetField))
                {
                    // Rename fields in TempImport based on the field
                    foreach (TempCardItem item in TempImport)
                    {
                        if (item.Fields.ContainsKey(mapping.CsvHeader))
                        {
                            // Get the value associated with the old field name
                            string value = item.Fields[mapping.CsvHeader];

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
                List<string> csvHeaders = TempImport.FirstOrDefault()?.Fields.Keys.ToList() ?? [];

                List<ColumnMapping> mappingItems = cardSetFields.Select(field => new ColumnMapping
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

        #endregion

        #region Debug methods
        public static void DebugFieldMappings()
        {
            try
            {
                Debug.WriteLine("==== Field Mappings ====");

                foreach (KeyValuePair<string, Dictionary<string, string>> fieldMapping in FieldMappings)
                {
                    Debug.WriteLine($"Field: {fieldMapping.Key}");
                    foreach (KeyValuePair<string, string> mapping in fieldMapping.Value)
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
            Debug.WriteLine("Debugging TempImport items:");
            foreach (TempCardItem tempItem in TempImport)
            {
                Debug.WriteLine("TempItem:");
                foreach (KeyValuePair<string, string> field in tempItem.Fields)
                {
                    Debug.WriteLine($"{field.Key}: {field.Value}");
                }
                Debug.WriteLine("\n");
            }
        }
        private static void DebugImportProcess()
        {
            // Total number of items in TempImport
            int totalTempImportItems = TempImport.Count;

            // Number of TempImport items with a single uuid
            int singleUuidItems = TempImport.Count(item => item.Fields.ContainsKey("uuid") && !item.Fields.ContainsKey("uuids"));

            // Number of TempImport items with multiple uuids
            int multipleUuidItems = TempImport.Count(item => item.Fields.ContainsKey("uuids"));

            int noUuidItems = TempImport.Count(item => !item.Fields.ContainsKey("uuid") && !item.Fields.ContainsKey("uuids"));

            // Debug write lines
            Debug.WriteLine($"Total number of items in TempImport: {totalTempImportItems}");
            Debug.WriteLine($"Number of TempImport items with single uuid: {singleUuidItems}");
            Debug.WriteLine($"Number of TempImport items with multiple uuids: {multipleUuidItems}");
            Debug.WriteLine($"Number of TempImport items with no uuid or uuids: {noUuidItems}");
        }
        private static void DebugItemsWithoutUuid()
        {
            foreach (TempCardItem item in TempImport)
            {
                // Check if the item does not have a "uuid" field or if the "uuid" field is empty
                if (!item.Fields.TryGetValue("uuid", out string? uuid) || string.IsNullOrEmpty(uuid))
                {
                    // Output the details of the item to the debug console
                    Debug.WriteLine("Item without UUID:");

                    foreach (KeyValuePair<string, string> field in item.Fields)
                    {
                        Debug.WriteLine($"{field.Key}: {field.Value}");
                    }

                    Debug.WriteLine("------------------------------");
                }
            }
        }
        #endregion
    }
}