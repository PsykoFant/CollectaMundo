using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace CardboardHoarder
{
    public partial class MainWindow : Window
    {

        public static MainWindow? CurrentInstance { get; private set; } // Used by ShowOrHideStatusWindow to reference MainWindow
        public MainWindow()
        {
            InitializeComponent();
            CurrentInstance = this; // Used by ShowOrHideStatusWindow to reference MainWindow
            DownloadAndPrepDB.StatusMessageUpdated += UpdateStatusTextBox; // Update the statusbox with messages from methods in DownloadAndPrepareDB
            UpdateDB.StatusMessageUpdated += UpdateStatusTextBox;

            GridSearchAndFilter.Visibility = Visibility.Hidden;
            GridMyCollection.Visibility = Visibility.Hidden;
            GridStatus.Visibility = Visibility.Hidden;
            Loaded += async (sender, args) => { await PrepareSystem(); };
        }

        private async Task PrepareSystem()
        {
            await DownloadAndPrepDB.CheckDatabaseExistenceAsync();
            GridSearchAndFilter.Visibility = Visibility.Visible;
            await DBAccess.OpenConnectionAsync();
            await LoadDataAsync();
            DBAccess.CloseConnection(true);
        }
        public static void ShowOrHideStatusWindow(bool visible)
        {
            if (CurrentInstance != null)
            {
                var gridStatus = CurrentInstance.GridStatus;
                gridStatus.Visibility = visible ? Visibility.Visible : Visibility.Hidden;
            }
        }
        private void UpdateStatusTextBox(string message)
        {
            Dispatcher.Invoke(() =>
            {
                statusTextBox.Text = message;
            });
        }
        private void ResetGrids()
        {
            GridSearchAndFilter.Visibility = Visibility.Hidden;
            GridMyCollection.Visibility = Visibility.Hidden;
        }
        private void MenuSearchAndFilter_Click(object sender, RoutedEventArgs e)
        {
            ResetGrids();
            GridSearchAndFilter.Visibility = Visibility.Visible;
        }
        private void MenuMyCollection_Click(object sender, RoutedEventArgs e)
        {
            ResetGrids();
            GridMyCollection.Visibility = Visibility.Visible;
        }
        private async Task LoadDataAsync()
        {
            Debug.WriteLine("Loading data asynchronously...");

            try
            {
                string query = "SELECT name, SetCode FROM cards";
                using var command = new SQLiteCommand(query, DBAccess.connection);

                using var reader = await command.ExecuteReaderAsync();
                var items = new List<CardSet>();
                while (await reader.ReadAsync())
                {
                    items.Add(new CardSet
                    {
                        Name = reader["Name"].ToString(),
                        SetCode = reader["SetCode"].ToString()
                    });
                }

                // Ensure UI updates are performed on the UI thread
                Dispatcher.Invoke(() =>
                {
                    mainCardWindowDatagrid.ItemsSource = items;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while loading data: {ex.Message}");
            }
        }


        // Test kode
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DisplayImageFromDatabase(imageInput.Text, testImage, 1);
        }
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            DisplayImageFromDatabase(manaCostTextBox.Text, manaCostImageTester, 2);
        }
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            DisplayImageFromDatabase(manaSymbolTextBox.Text, manaSymbolImageTester, 3);
        }
        private void DisplayImageFromDatabase(string inputFieldText, System.Windows.Controls.Image targetImageControl, int querySelector)
        {
            try
            {
                // Get the uniqueManaSymbol from the textBox
                string symbol = inputFieldText;

                // Query to retrieve manaSymbolImage from uniqueManaSymbols
                string query = "";
                string field = "";
                if (querySelector == 1)
                {
                    query = "SELECT keyruneImage FROM keyruneImages WHERE setCode = @symbol";
                    field = "keyruneImage";
                }
                else if (querySelector == 2)
                {
                    query = "SELECT manacostImage FROM uniqueManaCostImages WHERE uniqueManaCost = @symbol";
                    field = "manaCostImage";
                }
                else if (querySelector == 3)
                {
                    query = "SELECT manaSymbolImage FROM uniqueManaSymbols WHERE uniqueManaSymbol = @symbol";
                    field = "manaSymbolImage";
                }
                using (SQLiteConnection connection = DBAccess.GetConnection())
                {
                    connection.Open();

                    using (SQLiteCommand command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@symbol", symbol);

                        using (SQLiteDataReader reader = command.ExecuteReader(CommandBehavior.SequentialAccess))
                        {
                            if (reader.Read())
                            {
                                // Get the BLOB data
                                byte[] imageData = (byte[])reader[field];

                                // Display the image in the testImage control
                                DisplayImage(imageData, targetImageControl);
                            }
                            else
                            {
                                MessageBox.Show("No image found for the specified uniqueManaSymbol.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }
        private void DisplayImage(byte[] imageData, System.Windows.Controls.Image targetImageControl)
        {
            try
            {
                // Convert byte array to BitmapImage
                BitmapImage bitmapImage = ConvertByteArrayToBitmapImage(imageData);

                // Display the image in the testImage control
                targetImageControl.Source = bitmapImage;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error displaying image: {ex.Message}");
            }
        }
        public static BitmapImage? ConvertByteArrayToBitmapImage(byte[] imageData)
        {
            try
            {
                if (imageData != null && imageData.Length > 0)
                {
                    using (MemoryStream stream = new MemoryStream(imageData))
                    {
                        BitmapImage bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.StreamSource = stream;
                        bitmapImage.EndInit();
                        return bitmapImage;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error converting byte array to BitmapImage: {ex.Message}");
            }

            return null;
        }

        private async void checkForUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            await UpdateDB.CheckForUpdatesAsync(); // Assuming the method is named CheckForUpdatesAsync and is async
        }

        private async void updateDbButton_Click(object sender, RoutedEventArgs e)
        {
            ResetGrids();
            await UpdateDB.UpdateCardDatabaseAsync();
        }
    }
}