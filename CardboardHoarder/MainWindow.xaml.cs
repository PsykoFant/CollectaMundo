using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;


namespace CardboardHoarder
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            DatabaseHelper.CheckDatabaseExistence();
            InitializeComponent();
            GridSearchAndFilter.Visibility = Visibility.Visible;
            GridMyCollection.Visibility = Visibility.Hidden;
            LoadData();
        }


        private void DisplayImageFromDatabase()
        {
            try
            {
                // Get the uniqueManaSymbol from the textBox
                string uniqueManaSymbol = imageInput.Text;

                // Query to retrieve manaSymbolImage from uniqueManaSymbols
                string query = "SELECT manaCostImage FROM uniqueManaCostImages WHERE uniqueManaCost = @symbol";

                using (SQLiteConnection connection = DatabaseHelper.GetConnection())
                {
                    connection.Open();

                    using (SQLiteCommand command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@symbol", uniqueManaSymbol);

                        using (SQLiteDataReader reader = command.ExecuteReader(CommandBehavior.SequentialAccess))
                        {
                            if (reader.Read())
                            {
                                // Get the BLOB data
                                byte[] imageData = (byte[])reader["manaCostImage"];

                                // Display the image in the testImage control
                                DisplayImage(imageData);
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

        private void DisplayImage(byte[] imageData)
        {
            try
            {
                // Convert byte array to BitmapImage
                BitmapImage bitmapImage = ConvertByteArrayToBitmapImage(imageData);

                // Display the image in the testImage control
                testImage.Source = bitmapImage;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error displaying image: {ex.Message}");
            }
        }
        public static BitmapImage ConvertByteArrayToBitmapImage(byte[] imageData)
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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DisplayImageFromDatabase();
        }
    }
}