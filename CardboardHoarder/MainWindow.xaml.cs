using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq.Dynamic.Core;
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

        // Used for subtypes listbox and filtering
        private List<string> allSubTypes = new List<string>();
        private HashSet<string> selectedSubTypes = new HashSet<string>();

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

            // Pick up filtering input
            typesAndOr.Checked += CheckBox_Toggled;
            typesAndOr.Unchecked += CheckBox_Toggled;
            superTypesAndOr.Checked += CheckBox_Toggled;
            superTypesAndOr.Unchecked += CheckBox_Toggled;
            filterCardNameComboBox.SelectionChanged += ComboBox_SelectionChanged;
            filterSetNameComboBox.SelectionChanged += ComboBox_SelectionChanged;

            // Handle subtype filtering
            filterSubTypesTextBox.Text = "Filter subtypes...";
            filterSubTypesTextBox.Foreground = new SolidColorBrush(Colors.Gray);
            subTypesAndOr.Checked += CheckBox_Toggled;
            subTypesAndOr.Unchecked += CheckBox_Toggled;
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

        // Card types filtering logic
        private void TypeCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox_Checked(sender, selectedTypes);
        }
        private void TypeCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckBox_Unchecked(sender, selectedTypes);
        }


        // Subtypes filtering logic
        private void FilterSubTypesTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (filterSubTypesTextBox.Text != "Filter subtypes...")
            {
                var filteredSubTypes = string.IsNullOrWhiteSpace(filterSubTypesTextBox.Text)
                ? allSubTypes
                : allSubTypes.Where(type => type.IndexOf(filterSubTypesTextBox.Text, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                filterSubTypesListBox.ItemsSource = filteredSubTypes;
            }
        }
        private void FilterSubTypesTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (filterSubTypesTextBox.Text == "Filter subtypes...")
            {
                filterSubTypesTextBox.Text = "";
                filterSubTypesTextBox.Foreground = new SolidColorBrush(Colors.Black); // Or any other color for input text
            }
        }
        private void FilterSubTypesTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(filterSubTypesTextBox.Text))
            {
                filterSubTypesTextBox.Text = "Filter subtypes...";
                filterSubTypesTextBox.Foreground = new SolidColorBrush(Colors.Gray);
            }
        }
        private void SubTypesCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox_Checked(sender, selectedSubTypes);
        }
        private void SubTypesCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckBox_Unchecked(sender, selectedSubTypes);
        }

        // Supertypes filtering logic        
        private void SuperTypesCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox_Checked(sender, selectedSuperTypes);
        }
        private void SuperTypesCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckBox_Unchecked(sender, selectedSuperTypes);
        }

        // Common methods for listbox filtering elements
        private void CheckBox_Checked(object sender, HashSet<string> targetCollection)
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
                        ApplyFilter(); // Trigger filtering
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An error occurred checking the checkbox: {ex}");
            }
        }
        private void CheckBox_Unchecked(object sender, HashSet<string> targetCollection)
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
                        targetCollection.Remove(label);
                        UpdateFilterLabel();
                        ApplyFilter(); // Trigger filtering
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An error occurred unchecking the checkbox: {ex}");
            }
        }
        private void CheckBox_Toggled(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
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
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilter();
        }

        // Reset filter elements
        private void ClearFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            // Reset filter-related controls
            filterCardNameComboBox.SelectedIndex = -1;
            filterSetNameComboBox.SelectedIndex = -1;

            // Clear selections in the ListBoxes
            ClearListBoxSelections(filterTypesListBox);
            ClearListBoxSelections(filterSuperTypesListBox);
            ClearListBoxSelections(filterSubTypesListBox);

            // Clear the internal HashSets
            selectedTypes.Clear();
            selectedSuperTypes.Clear();
            selectedSubTypes.Clear();

            // Clear listbox searchboxes
            filterSubTypesTextBox.Text = string.Empty;

            // Uncheck CheckBoxes if necessary
            typesAndOr.IsChecked = false;
            superTypesAndOr.IsChecked = false;
            subTypesAndOr.IsChecked = false;

            // Update filter label and apply filters to refresh the DataGrid
            UpdateFilterLabel();
            ApplyFilter();
        }
        private void ClearListBoxSelections(ListBox listBox)
        {
            foreach (var item in listBox.Items)
            {
                var container = listBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                if (container != null)
                {
                    var checkBox = FindVisualChild<CheckBox>(container);
                    if (checkBox != null)
                    {
                        checkBox.IsChecked = false;
                    }
                }
            }
        }

        // Apply filtering
        private void UpdateFilterLabel()
        {
            var contentParts = new List<string>();

            if (selectedTypes.Count > 0)
            {
                contentParts.Add("Card types: " + string.Join(", ", selectedTypes));
            }

            if (selectedSuperTypes.Count > 0)
            {
                contentParts.Add("Supertypes: " + string.Join(", ", selectedSuperTypes));
            }

            if (selectedSubTypes.Count > 0)
            {
                contentParts.Add("Subtypes: " + string.Join(", ", selectedSubTypes));
            }

            filterLabel.Content = contentParts.Count > 0 ? string.Join(" - ", contentParts) : "";
        }
        private void ApplyFilter()
        {
            try
            {
                string cardFilter = filterCardNameComboBox.SelectedItem?.ToString() ?? "";
                string setFilter = filterSetNameComboBox.SelectedItem?.ToString() ?? "";

                var filteredItems = items.AsEnumerable();

                if (!string.IsNullOrEmpty(cardFilter))
                {
                    filteredItems = filteredItems.Where(item => item.Name.Contains(cardFilter));
                }
                if (!string.IsNullOrEmpty(setFilter))
                {
                    filteredItems = filteredItems.Where(item => item.SetName.Contains(setFilter));
                }

                if (selectedTypes.Count > 0)
                {
                    bool useAndForTypes = CurrentInstance.typesAndOr.IsChecked == true;
                    filteredItems = filteredItems.Where(item =>
                    {
                        var itemTypes = item.Types.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        return useAndForTypes
                            ? selectedTypes.All(selectedType => itemTypes.Contains(selectedType))
                            : selectedTypes.Any(selectedType => itemTypes.Contains(selectedType));
                    });
                }

                if (selectedSuperTypes.Count > 0)
                {
                    bool useAndForSuperTypes = CurrentInstance.superTypesAndOr.IsChecked == true;
                    filteredItems = filteredItems.Where(item =>
                        useAndForSuperTypes
                        ? selectedSuperTypes.All(st => item.SuperTypes.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Contains(st))
                        : selectedSuperTypes.Any(st => item.SuperTypes.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Contains(st))
                    );
                }

                if (selectedSubTypes.Count > 0)
                {
                    bool useAndForSubTypes = CurrentInstance.subTypesAndOr.IsChecked == true;
                    filteredItems = filteredItems.Where(item =>
                        useAndForSubTypes
                        ? selectedSubTypes.All(st => item.SubTypes.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Contains(st))
                        : selectedSubTypes.Any(st => item.SubTypes.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Contains(st))
                    );
                }

                var finalFilteredItems = filteredItems.ToList();
                cardCountLabel.Content = $"Cards shown: {finalFilteredItems.Count}";
                Dispatcher.Invoke(() => { mainCardWindowDatagrid.ItemsSource = finalFilteredItems; });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while filtering datagrid: {ex.Message}");
            }
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
                    "c.supertypes AS SuperTypes, " +
                    "c.subtypes AS SubTypes " +
                    "FROM cards c " +
                    "JOIN sets s ON c.setCode = s.code " +
                    "LEFT JOIN keyruneImages k ON c.setCode = k.setCode " +
                    "LEFT JOIN uniqueManaCostImages u ON c.manaCost = u.uniqueManaCost " +
                    "WHERE c.availability LIKE '%paper%'";

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
                        SubTypes = reader["SubTypes"]?.ToString() ?? string.Empty,
                    });
                }

                Dispatcher.Invoke(() =>
                {
                    cardCountLabel.Content = $"Cards shown: {items.Count}";
                    mainCardWindowDatagrid.ItemsSource = items;
                    dataView = CollectionViewSource.GetDefaultView(items);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while loading data: {ex.Message}");
            }
        }
        private async Task FillComboBoxesAsync()
        {
            try
            {
                // Get the values to populate the comboboxes
                var cardNames = await DownloadAndPrepDB.GetUniqueValuesAsync("cards", "name");
                var setNames = await DownloadAndPrepDB.GetUniqueValuesAsync("sets", "name");
                var types = await DownloadAndPrepDB.GetUniqueValuesAsync("cards", "types");
                var superTypes = await DownloadAndPrepDB.GetUniqueValuesAsync("cards", "supertypes");
                var subTypes = await DownloadAndPrepDB.GetUniqueValuesAsync("cards", "subtypes");

                var typesList = new List<string>();
                foreach (var type in types)
                {
                    typesList.AddRange(type.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()));
                }

                // Remove silly Types entries from un-sets, old cards etc. 
                var entriesToRemove = new HashSet<string> { "Eaturecray", "Ever", "Goblin", "Horror", "Jaguar", "See", "Knights", "Wolf", "Scariest", "You'll" };
                typesList = typesList.Where(type => !entriesToRemove.Contains(type)).ToList();

                var superTypesList = new List<string>();
                foreach (var type in superTypes)
                {
                    superTypesList.AddRange(type.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()));
                }

                allSubTypes.Clear();
                foreach (var type in subTypes)
                {
                    allSubTypes.AddRange(type.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()));
                }
                allSubTypes = allSubTypes.Distinct().OrderBy(type => type).ToList();

                Dispatcher.Invoke(() =>
                {
                    filterCardNameComboBox.ItemsSource = cardNames.OrderBy(name => name).ToList();
                    filterSetNameComboBox.ItemsSource = setNames.OrderBy(name => name).ToList();
                    filterTypesListBox.ItemsSource = typesList.OrderBy(types => types).Distinct().ToList();
                    filterSuperTypesListBox.ItemsSource = superTypesList.OrderBy(types => types).Distinct().ToList();
                    filterSubTypesListBox.ItemsSource = allSubTypes;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while filling comboboxes: {ex.Message}");
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