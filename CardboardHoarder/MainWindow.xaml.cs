using System.Data;
using System.Data.SQLite;
using System.Windows;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using Microsoft.Extensions.Configuration;


namespace CardboardHoarder
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static IConfiguration Configuration { get; set; }        
        public MainWindow()
        {
            var builder = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            Configuration = builder.Build();

            CheckDatabaseExistence();
            InitializeComponent();
            GridSearchAndFilter.Visibility = Visibility.Visible;
            GridMyCollection.Visibility = Visibility.Hidden;

            LoadData();
        }

        private static string GetSQLitePath()
        {
            // Retrieve the SQLite database path from appsettings.json
            return Configuration["DatabaseSettings:SQLitePath"] ?? string.Empty;
        }
        public static void CheckDatabaseExistence()
        {
            Debug.WriteLine("Inside CheckDatabaseExistence()");
            try
            {
                // Retrieve the SQLite database path from appsettings.json
                string sqlitePath = GetSQLitePath();
                string databasePath = Path.Combine(sqlitePath, "AllPrintings.sqlite");

                // Check if the database file exists
                if (!File.Exists(databasePath))
                {
                    // Output a message to the console
                    Debug.WriteLine($"The database file '{databasePath}' does not exist.");
                    DownloadDatabaseIfNotExists();

                    // https://mtgjson.com/api/v5/AllPrintings.sqlite
                }

            }
            catch (Exception ex)
            {
                // Handle exceptions (e.g., log, show error message, etc.)
                Debug.WriteLine($"Error while checking database existence: {ex.Message}");
            }
        }
        public static void DownloadDatabaseIfNotExists()
        {
            try
            {
                // Retrieve the SQLite database path from appsettings.json
                string sqlitePath = Configuration["DatabaseSettings:SQLitePath"] ?? "defaultPath";
                string databasePath = Path.Combine(sqlitePath, "AllPrintings.sqlite");

                // Check if the database file exists
                if (!File.Exists(databasePath))
                {
                    // Output a message to the console
                    Debug.WriteLine($"The database file '{databasePath}' does not exist. Downloading...");

                    // Ensure the directory exists
                    Directory.CreateDirectory(sqlitePath);

                    // Create and show the DownloadProgressWindow
                    DownloadProgressWindow downloadProgressWindow = new DownloadProgressWindow();
                    downloadProgressWindow.Show();

                    // Download the database file from the specified URL using HttpClient
                    string downloadUrl = "https://mtgjson.com/api/v5/AllPrintings.sqlite";
                    using (HttpClient httpClient = new HttpClient())
                    {
                        byte[] fileContent = httpClient.GetByteArrayAsync(downloadUrl).Result;
                        File.WriteAllBytes(databasePath, fileContent);
                    }

                    Debug.WriteLine($"Download completed. The database file '{databasePath}' is now available.");

                    // Close the DownloadProgressWindow after download completion
                    downloadProgressWindow.Close();
                }
                else
                {
                    Debug.WriteLine($"The database file '{databasePath}' already exists.");
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions (e.g., log, show error message, etc.)
                Debug.WriteLine($"Error while downloading database file: {ex.Message}");
            }
        }
        private void reset_grids()
        {
            GridSearchAndFilter.Visibility = Visibility.Hidden;
            GridMyCollection.Visibility = Visibility.Hidden;
        }
        private void MenuSearchAndFilter_Click(object sender, RoutedEventArgs e)
        {
            reset_grids();
            GridSearchAndFilter.Visibility = Visibility.Visible;
        }
        private void MenuMyCollection_Click(object sender, RoutedEventArgs e)
        {
            reset_grids();
            GridMyCollection.Visibility = Visibility.Visible;
        }
        private void LoadData()
        {
            using (SQLiteConnection connection = DatabaseHelper.GetConnection())
            {
                try
                {
                    connection.Open();
                    string query = "SELECT name, SetCode FROM cards"; // Adjust the query accordingly
                    SQLiteCommand command = new SQLiteCommand(query, connection);

                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        mainCardWindowDatagrid.ItemsSource = reader.Cast<IDataRecord>()
                            .Select(r => new CardSet
                            {
                                Name = r["Name"]?.ToString() ?? string.Empty,
                                SetCode = r["SetCode"]?.ToString() ?? string.Empty
                            })
                            .ToList();
                    }
                }
                catch (Exception ex)
                {
                    // Handle exceptions (e.g., log, show error message, etc.)
                    Console.WriteLine($"Error while loading data: {ex.Message}");
                }
                finally
                {
                    // Ensure the connection is closed in the finally block
                    if (connection.State == ConnectionState.Open)
                    {
                        connection.Close();
                    }
                }
            }
        }        
    }
}