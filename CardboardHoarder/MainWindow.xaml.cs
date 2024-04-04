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
        private ICollectionView? dataView;
        private List<CardSet> items = new List<CardSet>();

        // Lists for populating listboxes
        private List<string> allTypes = new List<string>();
        private List<string> allSuperTypes = new List<string>();
        private List<string> allSubTypes = new List<string>();
        private List<string> allKeywords = new List<string>();

        // Hashsets to store selected checkbox items in listboxes
        private HashSet<string> selectedTypes = new HashSet<string>();
        private HashSet<string> selectedSuperTypes = new HashSet<string>();
        private HashSet<string> selectedSubTypes = new HashSet<string>();
        private HashSet<string> selectedKeywords = new HashSet<string>();

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

            // Handle card name and set filtering
            filterCardNameComboBox.SelectionChanged += ComboBox_SelectionChanged;
            filterSetNameComboBox.SelectionChanged += ComboBox_SelectionChanged;
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

        #region Filter elements handling        
        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (sender is TextBox textBox)
                {
                    List<string> allItems = new List<string>();
                    ListBox targetListBox = null;
                    HashSet<string> selectedItems = new HashSet<string>();
                    string placeholderText = string.Empty;

                    // Determine the context based on which TextBox is sending the event
                    switch (textBox.Name)
                    {
                        case "filterTypesTextBox":
                            allItems = allTypes;
                            targetListBox = filterTypesListBox;
                            selectedItems = selectedTypes;
                            placeholderText = "Filter card types...";
                            break;
                        case "filterSuperTypesTextBox":
                            allItems = allSuperTypes;
                            targetListBox = filterSuperTypesListBox;
                            selectedItems = selectedSuperTypes;
                            placeholderText = "Filter supertypes...";
                            break;
                        case "filterSubTypesTextBox":
                            allItems = allSubTypes;
                            targetListBox = filterSubTypesListBox;
                            selectedItems = selectedSubTypes;
                            placeholderText = "Filter subtypes...";
                            break;
                        case "filterKeywordsTextBox":
                            allItems = allKeywords;
                            targetListBox = filterKeywordsListBox;
                            selectedItems = selectedKeywords;
                            placeholderText = "Filter keywords...";
                            break;
                    }

                    if (targetListBox != null && textBox.Text != placeholderText)
                    {
                        var filteredItems = string.IsNullOrWhiteSpace(textBox.Text)
                            ? allItems
                            : allItems.Where(type => type.IndexOf(textBox.Text, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

                        targetListBox.ItemsSource = filteredItems;

                        // Reapply the selected state to the checkboxes
                        targetListBox.Dispatcher.Invoke(() =>
                        {
                            foreach (var item in filteredItems)
                            {
                                var listBoxItem = targetListBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                                if (listBoxItem != null)
                                {
                                    var checkBox = FindVisualChild<CheckBox>(listBoxItem);
                                    if (checkBox != null && selectedItems.Contains(item))
                                    {
                                        checkBox.IsChecked = true;
                                    }
                                }
                            }
                        }, System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in FilterTextBox_TextChanged: {ex.Message}");
            }
        }
        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is TextBox textBox)
                {
                    string placeholderText = textBox.Name switch
                    {
                        "filterTypesTextBox" => "Filter card types...",
                        "filterSuperTypesTextBox" => "Filter supertypes...",
                        "filterSubTypesTextBox" => "Filter subtypes...",
                        "filterKeywordsTextBox" => "Filter keywords...",
                        _ => ""
                    };

                    if (textBox.Text == placeholderText)
                    {
                        textBox.Text = "";
                        textBox.Foreground = new SolidColorBrush(Colors.Black);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in TextBox_GotFocus: {ex.Message}");
            }
        }
        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is TextBox textBox)
                {
                    string placeholderText = textBox.Name switch
                    {
                        "filterTypesTextBox" => "Filter card types...",
                        "filterSuperTypesTextBox" => "Filter supertypes...",
                        "filterSubTypesTextBox" => "Filter subtypes...",
                        "filterKeywordsTextBox" => "Filter subtypes...",
                        _ => ""
                    };

                    if (string.IsNullOrWhiteSpace(textBox.Text))
                    {
                        textBox.Text = placeholderText;
                        textBox.Foreground = new SolidColorBrush(Colors.Gray);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in TextBox_LostFocus: {ex.Message}");
            }
        }
        private void CheckBox_Checked(object sender, RoutedEventArgs e)
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
                    var label = contentPresenter.Content as string;
                    if (!string.IsNullOrEmpty(label))
                    {
                        HashSet<string>? targetCollection = checkBox.Tag switch
                        {
                            "Type" => selectedTypes,
                            "SuperType" => selectedSuperTypes,
                            "SubType" => selectedSubTypes,
                            "Keywords" => selectedKeywords,
                            _ => null
                        };

                        if (targetCollection != null)
                        {
                            targetCollection.Add(label);
                            UpdateFilterLabel();
                            ApplyFilter(); // Trigger filtering
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An error occurred while checking the checkbox: {ex.Message}");
            }
        }
        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
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
                    var label = contentPresenter.Content as string;
                    if (!string.IsNullOrEmpty(label))
                    {
                        HashSet<string>? targetCollection = checkBox.Tag switch
                        {
                            "Type" => selectedTypes,
                            "SuperType" => selectedSuperTypes,
                            "SubType" => selectedSubTypes,
                            "Keywords" => selectedKeywords,
                            _ => null
                        };

                        if (targetCollection != null)
                        {
                            targetCollection.Remove(label);
                            UpdateFilterLabel();
                            ApplyFilter(); // Trigger filtering
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An error occurred while unchecking the checkbox: {ex.Message}");
            }
        }
        private void CheckBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is string dataContext)
            {
                switch (checkBox.Tag as string)
                {
                    case "Type":
                        checkBox.IsChecked = selectedTypes.Contains(dataContext);
                        break;
                    case "SuperType":
                        checkBox.IsChecked = selectedSuperTypes.Contains(dataContext);
                        break;
                    case "SubType":
                        checkBox.IsChecked = selectedSubTypes.Contains(dataContext);
                        break;
                    case "Keywords":
                        checkBox.IsChecked = selectedKeywords.Contains(dataContext);
                        break;
                }
            }
        }
        private void AndOrCheckBox_Toggled(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
            UpdateFilterLabel();
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
            ClearListBoxSelections(filterKeywordsListBox);

            // Clear the internal HashSets
            selectedTypes.Clear();
            selectedSuperTypes.Clear();
            selectedSubTypes.Clear();
            selectedKeywords.Clear();

            // Clear listbox searchboxes
            filterTypesTextBox.Text = string.Empty;
            filterTypesTextBox.Foreground = new SolidColorBrush(Colors.Gray);
            filterTypesTextBox.Text = "Filter card types...";

            filterSuperTypesTextBox.Text = string.Empty;
            filterSuperTypesTextBox.Foreground = new SolidColorBrush(Colors.Gray);
            filterSuperTypesTextBox.Text = "Filter supertypes...";

            filterSubTypesTextBox.Text = string.Empty;
            filterSubTypesTextBox.Foreground = new SolidColorBrush(Colors.Gray);
            filterSubTypesTextBox.Text = "Filter subtypes...";

            filterKeywordsTextBox.Text = string.Empty;
            filterKeywordsTextBox.Foreground = new SolidColorBrush(Colors.Gray);
            filterKeywordsTextBox.Text = "Filter keywords...";

            // Clear search items labels
            cardTypeLabel.Content = "";
            cardSuperTypesLabel.Content = "";
            cardSubTypeLabel.Content = "";
            cardKeywordsLabel.Content = "";

            // Uncheck CheckBoxes if necessary
            typesAndOr.IsChecked = false;
            superTypesAndOr.IsChecked = false;
            subTypesAndOr.IsChecked = false;
            keywordsAndOr.IsChecked = false;

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
        #endregion

        #region Apply filtering
        private void UpdateFilterLabel()
        {
            UpdateLabelContent(selectedTypes, cardTypeLabel, CurrentInstance.typesAndOr.IsChecked ?? false, "Card types");
            UpdateLabelContent(selectedSuperTypes, cardSuperTypesLabel, CurrentInstance.superTypesAndOr.IsChecked ?? false, "Card supertypes");
            UpdateLabelContent(selectedSubTypes, cardSubTypeLabel, CurrentInstance.subTypesAndOr.IsChecked ?? false, "Card subtypes");
            UpdateLabelContent(selectedKeywords, cardKeywordsLabel, CurrentInstance.keywordsAndOr.IsChecked ?? false, "Keywords");
        }
        private void UpdateLabelContent(HashSet<string> selectedItems, Label targetLabel, bool useAnd, string prefix)
        {
            if (selectedItems.Count > 0)
            {
                string conjunction = useAnd ? " AND " : " OR ";
                string content = $"{prefix}: {string.Join(conjunction, selectedItems)}";
                targetLabel.Content = content;
            }
            else
            {
                targetLabel.Content = "";
            }
        }
        private void ApplyFilter()
        {
            try
            {
                var filteredItems = items.AsEnumerable();

                // Card name and set combobox filtering
                string cardFilter = filterCardNameComboBox.SelectedItem?.ToString() ?? "";
                string setFilter = filterSetNameComboBox.SelectedItem?.ToString() ?? "";

                if (!string.IsNullOrEmpty(cardFilter))
                {
                    filteredItems = filteredItems.Where(item => item.Name.Contains(cardFilter));
                }
                if (!string.IsNullOrEmpty(setFilter))
                {
                    filteredItems = filteredItems.Where(item => item.SetName.Contains(setFilter));
                }

                // Listbox filter selections
                filteredItems = FilterByCriteria(filteredItems, selectedTypes, CurrentInstance.typesAndOr.IsChecked ?? false, item => item.Types);
                filteredItems = FilterByCriteria(filteredItems, selectedSuperTypes, CurrentInstance.superTypesAndOr.IsChecked ?? false, item => item.SuperTypes);
                filteredItems = FilterByCriteria(filteredItems, selectedSubTypes, CurrentInstance.subTypesAndOr.IsChecked ?? false, item => item.SubTypes);
                filteredItems = FilterByCriteria(filteredItems, selectedKeywords, CurrentInstance.keywordsAndOr.IsChecked ?? false, item => item.Keywords);

                var finalFilteredItems = filteredItems.ToList();
                cardCountLabel.Content = $"Cards shown: {finalFilteredItems.Count}";
                Dispatcher.Invoke(() => { mainCardWindowDatagrid.ItemsSource = finalFilteredItems; });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while filtering datagrid: {ex.Message}");
            }
        }
        private IEnumerable<CardSet> FilterByCriteria(IEnumerable<CardSet> items, HashSet<string> selectedCriteria, bool useAnd, Func<CardSet, string> propertySelector)
        {
            if (items == null)
            {
                return Enumerable.Empty<CardSet>();
            }

            if (selectedCriteria == null || selectedCriteria.Count == 0)
            {
                return items;
            }

            try
            {
                return items.Where(item =>
                {
                    var criteria = propertySelector(item)?.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
                    return useAnd ? selectedCriteria.All(st => criteria.Contains(st)) : selectedCriteria.Any(st => criteria.Contains(st));
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while filtering items: {ex.Message}");
                return Enumerable.Empty<CardSet>();
            }
        }


        #endregion

        #region Load data and populate UI elements
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
                    "c.subtypes AS SubTypes, " +
                    "c.type AS Type, " +
                    "c.keywords AS Keywords " +
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
                        SubTypes = reader["SubTypes"]?.ToString() ?? string.Empty,
                        Type = reader["Type"]?.ToString() ?? string.Empty,
                        Keywords = reader["Keywords"]?.ToString() ?? string.Empty,
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
                var keywords = await DownloadAndPrepDB.GetUniqueValuesAsync("cards", "keywords");

                // Set up elements in card type listbox
                allTypes.Clear();
                foreach (var type in types)
                {
                    allTypes.AddRange(type.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()));
                }
                allTypes = allTypes.Distinct().OrderBy(type => type).ToList();

                // Set up elements in supertype listbox
                allSuperTypes.Clear();
                foreach (var type in superTypes)
                {
                    allSuperTypes.AddRange(type.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()));
                }
                allSuperTypes = allSuperTypes.Distinct().OrderBy(type => type).ToList();

                // Set up elements in subtype listbox
                allSubTypes.Clear();
                foreach (var type in subTypes)
                {
                    allSubTypes.AddRange(type.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()));
                }
                allSubTypes = allSubTypes.Distinct().OrderBy(type => type).ToList();

                // Set up elements in keywords listbox
                allKeywords.Clear();
                foreach (var keyword in keywords)
                {
                    allKeywords.AddRange(keyword.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()));
                }
                allKeywords = allKeywords.Distinct().OrderBy(keyword => keyword).ToList();

                Dispatcher.Invoke(() =>
                {
                    filterCardNameComboBox.ItemsSource = cardNames.OrderBy(name => name).ToList();
                    filterSetNameComboBox.ItemsSource = setNames.OrderBy(name => name).ToList();
                    filterTypesListBox.ItemsSource = allTypes;
                    filterSuperTypesListBox.ItemsSource = allSuperTypes;
                    filterSubTypesListBox.ItemsSource = allSubTypes;
                    filterKeywordsListBox.ItemsSource = allKeywords;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while filling comboboxes: {ex.Message}");
            }
        }
        #endregion        

        #region UI elements for updating card database
        private async void checkForUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            await UpdateDB.CheckForUpdatesAsync(); // Assuming the method is named CheckForUpdatesAsync and is async
        }
        private async void updateDbButton_Click(object sender, RoutedEventArgs e)
        {
            ResetGrids();
            await UpdateDB.UpdateCardDatabaseAsync();
        }
        private void UpdateStatusTextBox(string message)
        {
            Dispatcher.Invoke(() =>
            {
                statusLabel.Content = message;
            });
        }
        #endregion

        #region Top menu navigation
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
        public void ResetGrids()
        {
            infoLabel.Content = "";
            GridSearchAndFilter.Visibility = Visibility.Hidden;
            GridMyCollection.Visibility = Visibility.Hidden;
        }
        #endregion

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