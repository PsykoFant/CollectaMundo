using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CardboardHoarder
{
    public partial class MainWindow : Window
    {

        // Used by ShowOrHideStatusWindow to reference MainWindow
        private static MainWindow? _currentInstance;
        public static MainWindow CurrentInstance
        {
            get
            {
                if (_currentInstance == null)
                {
                    throw new InvalidOperationException("CurrentInstance is not initialized.");
                }

                return _currentInstance;
            }
            private set => _currentInstance = value;
        }

        public MainWindow()
        {
            InitializeComponent();
            _currentInstance = this;
            DownloadAndPrepDB.StatusMessageUpdated += UpdateStatusTextBox; // Update the statusbox with messages from methods in DownloadAndPrepareDB            
            UpdateDB.StatusMessageUpdated += UpdateStatusTextBox; // Update the statusbox with messages from methods in UpdateDB

            GridSearchAndFilter.Visibility = Visibility.Hidden;
            GridMyCollection.Visibility = Visibility.Hidden;
            GridStatus.Visibility = Visibility.Hidden;
            Loaded += async (sender, args) => { await PrepareSystem(); };

            DisplaySvgImage("https://svgs.scryfall.io/sets/mid.svg");
        }

        private async Task PrepareSystem()
        {
            await DownloadAndPrepDB.CheckDatabaseExistenceAsync();
            GridSearchAndFilter.Visibility = Visibility.Visible;
            await DBAccess.OpenConnectionAsync();
            await LoadDataAsync();
            DBAccess.CloseConnection();
        }
        public static async Task ShowStatusWindowAsync(bool visible)
        {
            if (CurrentInstance != null)
            {
                await CurrentInstance.Dispatcher.InvokeAsync(() =>
                {
                    if (visible)
                    {
                        CurrentInstance.MenuSearchAndFilterButton.IsEnabled = false;
                        CurrentInstance.MenuMyCollectionButton.IsEnabled = false;
                        CurrentInstance.MenuDecksButton.IsEnabled = false;
                        CurrentInstance.updateDbButton.IsEnabled = false;
                        CurrentInstance.checkForUpdatesButton.IsEnabled = false;

                        CurrentInstance.GridStatus.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        CurrentInstance.MenuSearchAndFilterButton.IsEnabled = true;
                        CurrentInstance.MenuMyCollectionButton.IsEnabled = true;
                        CurrentInstance.MenuDecksButton.IsEnabled = true;
                        CurrentInstance.updateDbButton.IsEnabled = true;
                        CurrentInstance.updateDbButton.Visibility = Visibility.Hidden;
                        CurrentInstance.checkForUpdatesButton.IsEnabled = true;


                        CurrentInstance.GridStatus.Visibility = Visibility.Hidden;
                    }
                });
            }
        }

        private void UpdateStatusTextBox(string message)
        {
            Dispatcher.Invoke(() =>
            {
                statusLabel.Content = message;
            });
        }
        public void ResetGrids()
        {
            infoLabel.Content = "";
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
        private async void checkForUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            await UpdateDB.CheckForUpdatesAsync(); // Assuming the method is named CheckForUpdatesAsync and is async
        }
        private async void updateDbButton_Click(object sender, RoutedEventArgs e)
        {
            ResetGrids();
            await UpdateDB.UpdateCardDatabaseAsync();
        }


        private async void DisplaySvgImage(string svgUrl)
        {
            //var byteArray = await DownloadAndPrepDB.ConvertSvgToPngAsync(svgUrl);
            var byteArray = await DownloadAndPrepDB.ConvertSvgToByteArraySharpVectorsAsync(svgUrl);

            if (byteArray != null)
            {
                using var stream = new MemoryStream(byteArray);
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = stream;
                bitmapImage.EndInit();

                // Ensure the image is set on the UI thread
                Dispatcher.Invoke(() => iconTester.Source = bitmapImage);
            }
        }



        private static async Task<byte[]> ConvertSvgToByteArrayAsync(string svgUrl)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var svgData = await httpClient.GetStringAsync(svgUrl);
                    var svgStream = new MemoryStream(Encoding.UTF8.GetBytes(svgData));
                    var settings = new WpfDrawingSettings();
                    var reader = new FileSvgReader(settings);
                    var drawing = reader.Read(svgStream);

                    DrawingImage drawingImage = new DrawingImage(drawing);
                    var drawingVisual = new DrawingVisual();
                    using (var drawingContext = drawingVisual.RenderOpen())
                    {
                        drawingContext.DrawImage(drawingImage, new Rect(0, 0, drawingImage.Width, drawingImage.Height));
                    }
                    RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap((int)drawingImage.Width, (int)drawingImage.Height, 96, 96, PixelFormats.Pbgra32);
                    renderTargetBitmap.Render(drawingVisual);

                    BitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));

                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        encoder.Save(memoryStream);
                        return memoryStream.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error converting SVG to byte array: {ex.Message}");
                return null;
            }
        }



        private async Task LoadDataAsync()
        {
            Debug.WriteLine("Loading data asynchronously...");
            try
            {
                string query = "SELECT c.name as Name, c.setCode as SetCode, k.keyruneImage FROM cards c LEFT JOIN keyruneImages k ON c.SetCode = k.setCode";
                using var command = new SQLiteCommand(query, DBAccess.connection);

                using var reader = await command.ExecuteReaderAsync();
                var items = new List<CardSet>();
                while (await reader.ReadAsync())
                {
                    var keyruneImage = reader["keyruneImage"] as byte[];
                    var imageSource = ConvertByteArrayToBitmapImage(keyruneImage); // Implement this method to convert byte[] to ImageSource or similar

                    items.Add(new CardSet
                    {
                        Name = reader["Name"].ToString(),
                        SetCode = reader["SetCode"].ToString(),
                        SetIcon = imageSource
                    });
                }

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


    }
}