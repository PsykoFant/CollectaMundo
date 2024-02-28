using System.Data;
using System.Data.SQLite;
using System.Drawing;
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
                } else if (querySelector == 2)
                {
                    query = "SELECT manacostImage FROM uniqueManaCostImages WHERE uniqueManaCost = @symbol";
                    field = "manaCostImage";
                } else if (querySelector == 3)
                {
                    query = "SELECT manaSymbolImage FROM uniqueManaSymbols WHERE uniqueManaSymbol = @symbol";
                    field = "manaSymbolImage";
                }



                using (SQLiteConnection connection = DatabaseHelper.GetConnection())
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


    }
}