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
        private HashSet<string> selectedTypes = new HashSet<string>();
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

            //DisplaySvgImage("https://svgs.scryfall.io/sets/mid.svg");
        }


        private void SuperTypesCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox_Checked(sender, e, selectedSuperTypes);
        }

        private void TypeCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox_Checked(sender, e, selectedTypes);
        }


        private void CheckBox_Checked(object sender, RoutedEventArgs e, HashSet<string> targetCollection)
        {
            try
            {
                var dependencyObject = sender as DependencyObject;
                if (dependencyObject == null)
                {
                    return; // Exit if casting failed
                }

                var checkBox = FindVisualChild<CheckBox>(dependencyObject);
                if (checkBox != null && checkBox.Content is ContentPresenter contentPresenter)
                {
                    var label = contentPresenter.Content as string; // Assuming the content is directly a string.
                    if (!string.IsNullOrEmpty(label))
                    {
                        targetCollection.Add(label);
                        UpdateFilterLabel();
                        FilterDataGrid(null, null); // Trigger filtering
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An error occurred checking the checkbox: {ex}");
            }
        }


        private void TypeCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckBox_Unchecked(sender, e, selectedTypes);
        }

        private void SuperTypesCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckBox_Unchecked(sender, e, selectedSuperTypes);
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e, HashSet<string> targetCollection)
        {
            try
            {
                var dependencyObject = sender as DependencyObject;
                if (dependencyObject == null)
                {
                    return; // Exit if casting failed
                }

                var checkBox = FindVisualChild<CheckBox>(dependencyObject);
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
            catch (Exception ex)
            {
                Debug.WriteLine($"An error occurred unchecking the checkbox: {ex}");
            }
        }
        private static T? FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            try
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                    if (child is T correctChild)
                    {
                        return correctChild;
                    }

                    T? childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                    {
                        return childOfChild;
                    }
                }
            }
            catch (Exception ex)
            {
                // Optionally log the exception if needed
                Debug.WriteLine($"An error occurred while searching for visual child: {ex}");
            }

            return null;
        }

        private void UpdateFilterLabel()
        {
            var contentParts = new List<string>();

            if (selectedTypes.Count > 0)
            {
                contentParts.Add("Types: " + string.Join(", ", selectedTypes));
            }

            if (selectedSuperTypes.Count > 0)
            {
                contentParts.Add("SuperTypes: " + string.Join(", ", selectedSuperTypes));
            }

            filterLabel.Content = contentParts.Count > 0 ? string.Join(" - ", contentParts) : "";
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
                filterTypesListBox.ItemsSource = typesList.OrderBy(types => types).Distinct().ToList();
                filterSuperTypesListBox.ItemsSource = superTypesList.OrderBy(types => types).Distinct().ToList();

            });
        }
        private void FilterDataGrid(object? sender, SelectionChangedEventArgs? e)
        {
            string cardFilter = filterCardNameComboBox.SelectedItem?.ToString() ?? "";
            string setFilter = filterSetNameComboBox.SelectedItem?.ToString() ?? "";

            // Use the HashSet selectedSuperTypes directly for filtering
            var filteredItems = items.Where(item =>
                (string.IsNullOrEmpty(cardFilter) || item.Name.Contains(cardFilter)) &&
                (string.IsNullOrEmpty(setFilter) || item.SetName.Contains(setFilter)) &&
                (selectedTypes.Count == 0 || selectedTypes.Any(Type => item.Types.Contains(Type))) &&
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
                    BitmapImage? setIconImageSource = keyruneImage != null ? ConvertByteArrayToBitmapImage(keyruneImage) : null;

                    var manaCostImage = reader["ManaCostImage"] as byte[];
                    BitmapImage? manaCostImageSource = manaCostImage != null ? ConvertByteArrayToBitmapImage(manaCostImage) : null;


                    items.Add(new CardSet
                    {
                        Name = reader["Name"]?.ToString() ?? string.Empty,
                        SetName = reader["SetName"]?.ToString() ?? string.Empty,
                        SetIcon = setIconImageSource,
                        ManaCost = reader["ManaCost"]?.ToString() ?? string.Empty,
                        ManaCostImage = manaCostImageSource,
                        Types = reader["Types"]?.ToString() ?? string.Empty,
                        SuperTypes = reader["SuperTypes"]?.ToString() ?? string.Empty,
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
    }
}