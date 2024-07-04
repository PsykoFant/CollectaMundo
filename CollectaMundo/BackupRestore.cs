using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

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
        public static ObservableCollection<CardSet.CardItem> tempImport { get; private set; } = new ObservableCollection<CardSet.CardItem>();
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
                        Debug.WriteLine($"Name: {item.Name}, Set: {item.SetCode}, Count: {item.Count}, Condition: {item.SelectedCondition}, Language: {item.Language}, Finish: {item.SelectedFinish}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error importing CSV: {ex.Message}");
                MessageBox.Show($"Error importing CSV: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static async Task<ObservableCollection<CardSet.CardItem>> ParseCsvFileAsync(string filePath)
        {
            var cardItems = new ObservableCollection<CardSet.CardItem>();
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
                    var cardItem = new CardSet.CardItem { CsvHeaders = headers }; // Set headers here

                    // Assuming the CSV columns are ordered as Name, Set, Count, Condition, Language, Finish
                    for (int i = 0; i < headers.Count; i++)
                    {
                        switch (headers[i].ToLower())
                        {
                            case "name":
                                cardItem.Name = values[i];
                                break;
                            case "set":
                                cardItem.SetCode = values[i];
                                break;
                            case "count":
                                cardItem.Count = int.Parse(values[i]);
                                break;
                            case "condition":
                                cardItem.SelectedCondition = values[i];
                                break;
                            case "language":
                                cardItem.Language = values[i];
                                break;
                            case "finish":
                                cardItem.SelectedFinish = values[i];
                                break;
                        }
                    }

                    cardItems.Add(cardItem);
                }
            }

            return cardItems;
        }

        public static async Task SearchAndAddUuidAsync()
        {
            try
            {
                await DBAccess.OpenConnectionAsync();
                if (DBAccess.connection == null)
                {
                    throw new InvalidOperationException("Database connection is not initialized.");
                }

                foreach (var item in tempImport)
                {
                    string name = item.Name;
                    string set = item.SetCode;

                    string query = "SELECT uuid FROM cards WHERE name = @name AND setCode = @set";
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
                                Debug.WriteLine($"Success: Found a unique uuid for card {name} in set {set}");
                                item.Uuid = uuids[0];
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
