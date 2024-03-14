using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CardboardHoarder
{
    public partial class MainWindow : Window
    {

        // Used by ShowOrHideStatusWindow to reference MainWindow
        private static MainWindow? _currentInstance;
        private ICollectionView dataView;
        private List<CardSet> items = new List<CardSet>();
        private HashSet<string> selectedSuperTypes = new HashSet<string>();


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

            filterCardNameComboBox.SelectionChanged += FilterDataGrid;
            filterSetNameComboBox.SelectionChanged += FilterDataGrid;
            filterTypesNameComboBox.SelectionChanged += FilterDataGrid;


            //DisplaySvgImage("https://svgs.scryfall.io/sets/mid.svg");
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var checkBox = FindVisualChild<CheckBox>(sender as DependencyObject);
            if (checkBox != null && checkBox.Content is ContentPresenter contentPresenter)
            {
                var label = contentPresenter.Content as string; // Assuming the content is directly a string.
                if (!string.IsNullOrEmpty(label))
                {
                    selectedSuperTypes.Add(label);
                    UpdateFilterLabel();
                    FilterDataGrid(null, null); // Trigger filtering
                }
            }
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            var checkBox = FindVisualChild<CheckBox>(sender as DependencyObject);
            if (checkBox != null && checkBox.Content is ContentPresenter contentPresenter)
            {
                var label = contentPresenter.Content as string; // Assuming the content is directly a string.
                if (!string.IsNullOrEmpty(label))
                {
                    selectedSuperTypes.Remove(label);
                    UpdateFilterLabel();
                    FilterDataGrid(null, null); // Trigger filtering
                }
            }
        }



        private static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T)
                    return (T)child;
                else
                {
                    T childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }


        private void UpdateFilterLabel()
        {
            if (selectedSuperTypes.Count > 0)
            {
                filterLabel.Content = "SuperTypes: " + string.Join(", ", selectedSuperTypes);
            }
            else
            {
                filterLabel.Content = "";
            }
        }





        private async Task PrepareSystem()
        {
            await DownloadAndPrepDB.CheckDatabaseExistenceAsync();
            GridSearchAndFilter.Visibility = Visibility.Visible;
            await DBAccess.OpenConnectionAsync();

            var LoadDataAsyncTask = LoadDataAsync();
            var FillComboBoxesAsyncTask = FillComboBoxesAsync();
            await Task.WhenAll(LoadDataAsyncTask, FillComboBoxesAsyncTask);
            DBAccess.CloseConnection();
        }
        private async Task FillComboBoxesAsync()
        {
            // Get the values to populate the comboboxes
            var cardNames = await DownloadAndPrepDB.GetUniqueValuesAsync("cards", "name");
            var setNames = await DownloadAndPrepDB.GetUniqueValuesAsync("sets", "name");
            var types = await DownloadAndPrepDB.GetUniqueValuesAsync("cards", "types");
            var superTypes = await DownloadAndPrepDB.GetUniqueValuesAsync("cards", "supertypes");

            // types with commas should not be listed. We don't want "Summon, Wolf" in the dropdown. We want "Summon" and "Wolf"
            var typesList = new List<string>();
            foreach (var type in types)
            {
                typesList.AddRange(type.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()));
            }
            var superTypesList = new List<string>();
            foreach (var type in superTypes)
            {
                superTypesList.AddRange(type.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()));
            }

            Dispatcher.Invoke(() =>
            {
                filterCardNameComboBox.ItemsSource = cardNames.OrderBy(name => name).ToList();
                filterSetNameComboBox.ItemsSource = setNames.OrderBy(name => name).ToList();
                filterTypesNameComboBox.ItemsSource = typesList.OrderBy(types => types).Distinct().ToList();
                filterSuperTypesListBox.ItemsSource = superTypesList.OrderBy(types => types).Distinct().ToList();

            });
        }
        private void FilterDataGrid(object sender, SelectionChangedEventArgs e)
        {
            string cardFilter = filterCardNameComboBox.SelectedItem?.ToString() ?? "";
            string setFilter = filterSetNameComboBox.SelectedItem?.ToString() ?? "";
            string typesFilter = filterTypesNameComboBox.SelectedItem?.ToString() ?? "";

            // Use the HashSet selectedSuperTypes directly for filtering
            var filteredItems = items.Where(item =>
                (string.IsNullOrEmpty(cardFilter) || item.Name.Contains(cardFilter)) &&
                (string.IsNullOrEmpty(setFilter) || item.SetName.Contains(setFilter)) &&
                (string.IsNullOrEmpty(typesFilter) || item.Types.Contains(typesFilter)) &&
                (selectedSuperTypes.Count == 0 || selectedSuperTypes.Any(superType => item.SuperTypes.Contains(superType)))
            ).ToList();

            Dispatcher.Invoke(() => { mainCardWindowDatagrid.ItemsSource = filteredItems; });
        }


        private async Task LoadDataAsync()
        {
            Debug.WriteLine("Loading data asynchronously...");
            try
            {
                string query =
                    "SELECT c.name AS Name, " +
                    "s.name AS SetName, " +
                    "k.keyruneImage AS KeyRuneImage, " +
                    "c.manaCost AS ManaCost, " +
                    "u.manaCostImage AS ManaCostImage, " +
                    "c.types AS Types, " +
                    "c.supertypes AS SuperTypes " +
                    "FROM cards c " +
                    "JOIN sets s ON c.setCode = s.code " +
                    "LEFT JOIN keyruneImages k ON c.setCode = k.setCode " +
                    "LEFT JOIN uniqueManaCostImages u ON c.manaCost = u.uniqueManaCost";

                using var command = new SQLiteCommand(query, DBAccess.connection);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var keyruneImage = reader["KeyRuneImage"] as byte[];
                    var setIconImageSource = ConvertByteArrayToBitmapImage(keyruneImage);
                    var manaCostImage = reader["ManaCostImage"] as byte[];
                    var manaCostImageSource = ConvertByteArrayToBitmapImage(manaCostImage);

                    items.Add(new CardSet
                    {
                        Name = reader["Name"].ToString(),
                        SetName = reader["SetName"].ToString(),
                        SetIcon = setIconImageSource,
                        ManaCost = reader["ManaCost"].ToString(),
                        ManaCostImage = manaCostImageSource,
                        Types = reader["Types"].ToString(),
                        SuperTypes = reader["SuperTypes"].ToString(),
                    });
                }

                Dispatcher.Invoke(() =>
                {
                    mainCardWindowDatagrid.ItemsSource = items;
                    dataView = CollectionViewSource.GetDefaultView(items);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while loading data: {ex.Message}");
            }
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


        private static BitmapImage? ConvertByteArrayToBitmapImage(byte[] imageData)
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

    }
}