using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace CollectaMundo
{

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
                        Debug.WriteLine($"Fail: Could not find mapping for card name with header {nameMapping}");
                        continue;
                    }

                    string set = string.Empty;
                    string query = string.Empty;

                    if (setCodeMapping != null && item.Fields.TryGetValue(setCodeMapping, out set))
                    {
                        query = "SELECT uuid FROM cards WHERE name = @name AND setCode = @set";
                    }
                    else if (setNameMapping != null && item.Fields.TryGetValue(setNameMapping, out string setName))
                    {
                        // Lookup set code from set name
                        string setCodeQuery = "SELECT code FROM sets WHERE name = @setName";
                        string setCode;

                        using (var setCodeCommand = new SQLiteCommand(setCodeQuery, DBAccess.connection))
                        {
                            setCodeCommand.Parameters.AddWithValue("@setName", setName);

                            using (var setCodeReader = await setCodeCommand.ExecuteReaderAsync())
                            {
                                if (await setCodeReader.ReadAsync())
                                {
                                    setCode = setCodeReader["code"].ToString();
                                }
                                else
                                {
                                    Debug.WriteLine($"Fail: Could not find set code for set name {setName}");
                                    continue;
                                }
                            }
                        }

                        set = setCode;
                        query = "SELECT uuid FROM cards WHERE name = @name AND setCode = @set";
                    }
                    else
                    {
                        Debug.WriteLine($"Fail: Could not find mapping for card set with headers {setCodeMapping ?? "N/A"} or {setNameMapping ?? "N/A"}");
                        continue;
                    }

                    using (var command = new SQLiteCommand(query, DBAccess.connection))
                    {
                        command.Parameters.AddWithValue("@name", name);
                        command.Parameters.AddWithValue("@set", set);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            List<string> uuids = new List<string>();
                            while (await reader.ReadAsync())
                            {
                                uuids.Add(reader["uuid"].ToString());
                            }

                            if (uuids.Count == 0)
                            {
                                Debug.WriteLine($"Fail: Could not find any cards with name {name} and set {set}");
                            }
                            else if (uuids.Count == 1)
                            {
                                Debug.WriteLine($"Success: Found a unique uuid for card {name} in set {set}: {uuids[0]}");
                                item.Fields["uuid"] = uuids[0];
                            }
                            else
                            {
                                Debug.WriteLine($"Fail: Found more than one card with name {name} and set {set}");
                            }
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

    }
}
