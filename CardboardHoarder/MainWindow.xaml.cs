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
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Set up varibales
        // Used for displaying images
        private string _imageSourceUrl = string.Empty;
        private string _imageSourceUrl2nd = string.Empty;
        public string ImageSourceUrl
        {
            get => _imageSourceUrl;
            set
            {
                if (_imageSourceUrl != value)
                {
                    _imageSourceUrl = value;
                    OnPropertyChanged(nameof(ImageSourceUrl));
                }
            }
        }
        public string ImageSourceUrl2nd
        {
            get => _imageSourceUrl2nd;
            set
            {
                if (_imageSourceUrl2nd != value)
                {
                    _imageSourceUrl2nd = value;
                    OnPropertyChanged(nameof(ImageSourceUrl2nd));
                }
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private static MainWindow? _currentInstance;
        private ICollectionView? dataView;
        private List<CardSet> cards = new List<CardSet>();
        private FilterContext filterContext = new FilterContext();
        private FilterManager filterManager;

        #endregion
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

            // Hide app sections not selected
            GridSearchAndFilter.Visibility = Visibility.Hidden;
            GridMyCollection.Visibility = Visibility.Hidden;
            GridStatus.Visibility = Visibility.Hidden;

            // Update the statusbox with messages from methods in DownloadAndPrepareDB
            DownloadAndPrepDB.StatusMessageUpdated += UpdateStatusTextBox;
            // Update the statusbox with messages from methods in UpdateDB
            UpdateDB.StatusMessageUpdated += UpdateStatusTextBox;

            // Set up system
            Loaded += async (sender, args) => { await PrepareSystem(); };

            filterManager = new FilterManager(filterContext);

            // Pick up filtering comboboxes changes
            filterCardNameComboBox.SelectionChanged += ComboBox_SelectionChanged;
            filterSetNameComboBox.SelectionChanged += ComboBox_SelectionChanged;
            allOrNoneComboBox.SelectionChanged += ComboBox_SelectionChanged;
            ManaValueComboBox.SelectionChanged += ComboBox_SelectionChanged;
            ManaValueOperatorComboBox.SelectionChanged += ComboBox_SelectionChanged;
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
        private void ComboBox_DropDownOpened(object sender, EventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                try
                {
                    var (defaultText, filterTextBoxName, listBoxName) = GetComboBoxConfig(comboBox.Name);

                    var filterTextBox = comboBox.Template.FindName(filterTextBoxName, comboBox) as TextBox;
                    if (filterTextBox != null && (string.IsNullOrWhiteSpace(filterTextBox.Text) || filterTextBox.Text == defaultText))
                    {
                        PopulateListBoxWithValues(comboBox, listBoxName);
                        filterTextBox.Foreground = new SolidColorBrush(Colors.Gray);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in ComboBox_DropDownOpened: {ex.Message}");
                }
            }
        }
        private void PopulateListBoxWithValues(ComboBox comboBox, string listBoxName) // Make sure the embedded listbox has the right values
        {
            try
            {
                var listBox = comboBox.Template.FindName(listBoxName, comboBox) as ListBox;
                if (listBox != null)
                {
                    // Get both items source and the corresponding selected items set.
                    var (itemsSource, selectedItems) = GetDataSetAndSelection(listBoxName);
                    listBox.ItemsSource = itemsSource;

                    listBox.Dispatcher.Invoke(() =>
                    {
                        foreach (var item in itemsSource)
                        {
                            var listBoxItem = listBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                            if (listBoxItem != null)
                            {
                                var checkBox = FindVisualChild<CheckBox>(listBoxItem);
                                if (checkBox != null)
                                {
                                    checkBox.IsChecked = selectedItems.Contains(item);
                                }
                            }
                        }
                    }, System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in PopulateListBoxWithValues: {ex.Message}");
            }
        }
        private (IEnumerable<string> items, HashSet<string> selectedItems) GetDataSetAndSelection(string listBoxName) // Get the data to populate the listbox with, including already selected items
        {
            IEnumerable<string> itemsSource;
            HashSet<string> selectedItemsSet;

            switch (listBoxName)
            {
                case "FilterSuperTypesListBox":
                    itemsSource = filterContext.AllSuperTypes;
                    selectedItemsSet = filterContext.SelectedSuperTypes;
                    break;
                case "FilterTypesListBox":
                    itemsSource = filterContext.AllTypes;
                    selectedItemsSet = filterContext.SelectedTypes;
                    break;
                case "FilterSubTypesListBox":
                    itemsSource = filterContext.AllSubTypes;
                    selectedItemsSet = filterContext.SelectedSubTypes;
                    break;
                case "FilterKeywordsListBox":
                    itemsSource = filterContext.AllKeywords;
                    selectedItemsSet = filterContext.SelectedKeywords;
                    break;
                default:
                    throw new InvalidOperationException($"ListBox name not recognized: {listBoxName}");
            }

            return (itemsSource.Distinct().OrderBy(type => type).ToList(), selectedItemsSet);
        }
        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e) // Filter checkbox elements in the embedded listbox based text typed in the embedded testbox
        {
            if (sender is TextBox textBox)
            {
                try
                {
                    // Finding the parent ComboBox by traversing up the visual tree
                    DependencyObject parent = VisualTreeHelper.GetParent(textBox);
                    while (parent != null && !(parent is ComboBox))
                    {
                        parent = VisualTreeHelper.GetParent(parent);
                    }

                    // Explicitly check for null before casting
                    if (parent is ComboBox comboBox)
                    {
                        // Get configuration for this specific ComboBox
                        var (defaultText, _, listBoxName) = GetComboBoxConfig(comboBox.Name);

                        // Check if the typed text is the default text
                        if (textBox.Text == defaultText)
                        {
                            return; // Ignore the default placeholder text
                        }

                        // Finding the associated ListBox using the dynamically determined name
                        var listBox = comboBox.Template.FindName(listBoxName, comboBox) as ListBox;
                        if (listBox != null)
                        {
                            UpdateListBoxItems(listBox, textBox.Text);

                            // Ensure the ComboBox's dropdown is open to show filtered results
                            if (!comboBox.IsDropDownOpen)
                            {
                                comboBox.IsDropDownOpen = true;
                            }
                        }
                    }
                    else
                    {
                        // Log or handle the scenario where the parent ComboBox is not found
                        Debug.WriteLine("Parent ComboBox not found.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in FilterTextBox_TextChanged: {ex.Message}");
                }
            }
        }
        private void UpdateListBoxItems(ListBox listBox, string filterText) // This method updates the listbox items based on text typed in FilterTextBox
        {
            try
            {
                var (dataSet, selectedItems) = GetDataSetAndSelection(listBox.Name);

                List<string> filteredItems = !string.IsNullOrWhiteSpace(filterText)
                    ? dataSet.Where(type => type.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0).ToList()
                    : dataSet.Distinct().OrderBy(type => type).ToList();

                listBox.ItemsSource = filteredItems;

                listBox.Dispatcher.Invoke(() =>
                {
                    foreach (var item in filteredItems)
                    {
                        var listBoxItem = listBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                        if (listBoxItem != null) // Check if listBoxItem is not null
                        {
                            var checkBox = FindVisualChild<CheckBox>(listBoxItem);
                            if (checkBox != null) // Check if checkBox is not null
                            {
                                checkBox.IsChecked = selectedItems.Contains(item);
                            }
                        }
                    }
                }, System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in UpdateListBoxItems: {ex.Message}");
            }
        }
        private (string defaultText, string textBoxName, string listBoxName) GetComboBoxConfig(string comboBoxName) // Generic method for getting embedded textbox and listbox elements based on the combobox
        {
            return comboBoxName switch
            {
                "SuperTypesComboBox" => (filterContext.SuperTypesDefaultText, "FilterSuperTypesTextBox", "FilterSuperTypesListBox"),
                "TypesComboBox" => (filterContext.TypesDefaultText, "FilterTypesTextBox", "FilterTypesListBox"),
                "SubTypesComboBox" => (filterContext.SubTypesDefaultText, "FilterSubTypesTextBox", "FilterSubTypesListBox"),
                "KeywordsComboBox" => (filterContext.KeywordsDefaultText, "FilterKeywordsTextBox", "FilterKeywordsListBox"),
                _ => throw new InvalidOperationException($"Configuration not found for ComboBox: {comboBoxName}")
            };
        }
        private void AndOrCheckBox_Toggled(object sender, RoutedEventArgs e) // Trigger filtering and update label when an and/or checkbox is toggled
        {
            ApplyFilterSelection(filterManager.ApplyFilter(cards));
        }
        // When combobox textboxes get focus/defocus        
        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is TextBox textBox)
                {
                    string defaultText = textBox.Name switch
                    {
                        "FilterSuperTypesTextBox" => filterContext.SuperTypesDefaultText,
                        "FilterTypesTextBox" => filterContext.TypesDefaultText,
                        "FilterSubTypesTextBox" => filterContext.SubTypesDefaultText,
                        "FilterKeywordsTextBox" => filterContext.KeywordsDefaultText,
                        "FilterRulesTextTextBox" => filterContext.RulesTextDefaultText,
                        _ => ""
                    };

                    if (textBox.Text == defaultText)
                    {
                        textBox.Text = string.Empty;
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
                    string defaultText = textBox.Name switch
                    {
                        "FilterSuperTypesTextBox" => filterContext.SuperTypesDefaultText,
                        "FilterTypesTextBox" => filterContext.TypesDefaultText,
                        "FilterSubTypesTextBox" => filterContext.SubTypesDefaultText,
                        "FilterKeywordsTextBox" => filterContext.KeywordsDefaultText,
                        "FilterRulesTextTextBox" => filterContext.RulesTextDefaultText,
                        _ => ""
                    };

                    if (string.IsNullOrWhiteSpace(textBox.Text))
                    {
                        textBox.Text = defaultText;
                        textBox.Foreground = new SolidColorBrush(Colors.Gray);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in TextBox_LostFocus: {ex.Message}");
            }
        }
        // When a combobox checkbox item is checked or unchecked
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
                            "Type" => filterContext.SelectedTypes,
                            "SuperType" => filterContext.SelectedSuperTypes,
                            "SubType" => filterContext.SelectedSubTypes,
                            "Keywords" => filterContext.SelectedKeywords,
                            "Colors" => filterContext.SelectedColors,
                            _ => null
                        };

                        if (targetCollection != null)
                        {
                            targetCollection.Add(label);
                            ApplyFilterSelection(filterManager.ApplyFilter(cards));
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
                            "Type" => filterContext.SelectedTypes,
                            "SuperType" => filterContext.SelectedSuperTypes,
                            "SubType" => filterContext.SelectedSubTypes,
                            "Keywords" => filterContext.SelectedKeywords,
                            "Colors" => filterContext.SelectedColors,
                            _ => null
                        };

                        if (targetCollection != null)
                        {
                            targetCollection.Remove(label);
                            ApplyFilterSelection(filterManager.ApplyFilter(cards));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An error occurred while unchecking the checkbox: {ex.Message}");
            }
        }
        private void CheckBox_Loaded(object sender, RoutedEventArgs e) // Make sure combobox checkbox items are loaded
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is string dataContext)
            {
                switch (checkBox.Tag as string)
                {
                    case "Type":
                        checkBox.IsChecked = filterContext.SelectedTypes.Contains(dataContext);
                        break;
                    case "SuperType":
                        checkBox.IsChecked = filterContext.SelectedSuperTypes.Contains(dataContext);
                        break;
                    case "SubType":
                        checkBox.IsChecked = filterContext.SelectedSubTypes.Contains(dataContext);
                        break;
                    case "Keywords":
                        checkBox.IsChecked = filterContext.SelectedKeywords.Contains(dataContext);
                        break;
                    case "Colors":
                        checkBox.IsChecked = filterContext.SelectedColors.Contains(dataContext);
                        break;
                }
            }
        }
        private static T? FindVisualChild<T>(DependencyObject obj) where T : DependencyObject // Because we use custom combobox, we need this method to find embedded elements
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
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) // Trigger filtering when a non-customized checkbox is loaded
        {
            ApplyFilterSelection(filterManager.ApplyFilter(cards));
        }
        private void filterRulesTextButton_Click(object sender, RoutedEventArgs e) // Apply filter for rulestext freetext search
        {
            ApplyFilterSelection(filterManager.ApplyFilter(cards));
        }
        private void ApplyFilterSelection(IEnumerable<CardSet> filteredCards)
        {
            mainCardWindowDatagrid.ItemsSource = filteredCards;
            cardCountLabel.Content = $"Cards shown: {filteredCards.Count()}";
        }
        // Reset filter elements
        private void ClearFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            // Reset filter TextBoxes for each ComboBox
            ResetFilterTextBox(SubTypesComboBox, "FilterSuperTypesTextBox", filterContext.SuperTypesDefaultText);
            ResetFilterTextBox(TypesComboBox, "FilterTypesTextBox", filterContext.TypesDefaultText);
            ResetFilterTextBox(SubTypesComboBox, "FilterSubTypesTextBox", filterContext.SubTypesDefaultText);
            ResetFilterTextBox(KeywordsComboBox, "FilterKeywordsTextBox", filterContext.KeywordsDefaultText);

            // Clear non-custom comboboxes
            filterCardNameComboBox.SelectedIndex = -1;
            filterSetNameComboBox.SelectedIndex = -1;
            allOrNoneComboBox.SelectedIndex = 0;
            ManaValueComboBox.SelectedIndex = -1;
            ManaValueOperatorComboBox.SelectedIndex = -1;

            // Clear selections in the colors listbox
            ClearListBoxSelections(filterColorsListBox);

            // Clear the internal HashSets
            filterContext.SelectedTypes.Clear();
            filterContext.SelectedSuperTypes.Clear();
            filterContext.SelectedSubTypes.Clear();
            filterContext.SelectedKeywords.Clear();
            filterContext.SelectedColors.Clear();

            // Clear rulestext textbox
            FilterRulesTextTextBox.Text = filterContext.RulesTextDefaultText;
            FilterRulesTextTextBox.Foreground = new SolidColorBrush(Colors.Gray);

            // Clear search item labels
            CardRulesTextLabel.Content = string.Empty;
            CardTypeLabel.Content = string.Empty;
            CardSuperTypesLabel.Content = string.Empty;
            CardSubTypeLabel.Content = string.Empty;
            CardKeywordsLabel.Content = string.Empty;

            // Uncheck CheckBoxes if necessary
            typesAndOr.IsChecked = false;
            superTypesAndOr.IsChecked = false;
            subTypesAndOr.IsChecked = false;
            keywordsAndOr.IsChecked = false;

            // Update filter label and apply filters to refresh the DataGrid            
            ApplyFilterSelection(filterManager.ApplyFilter(cards));
        }
        private void ResetFilterTextBox(ComboBox comboBox, string textBoxName, string defaultText)
        {
            if (comboBox.Template.FindName(textBoxName, comboBox) is TextBox filterTextBox)
            {
                filterTextBox.Text = defaultText;
                filterTextBox.Foreground = new SolidColorBrush(Colors.Gray);
            }
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

        #region Show selected card image
        // Show the card image for the highlighted datagrid row
        private async void MainCardWindowDatagrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (mainCardWindowDatagrid.SelectedItem is CardSet selectedCard && !string.IsNullOrEmpty(selectedCard.Uuid))
                {
                    await DBAccess.OpenConnectionAsync();
                    string? scryfallId = await GetScryfallIdByUuidAsync(selectedCard.Uuid);
                    DBAccess.CloseConnection();

                    if (!string.IsNullOrEmpty(scryfallId) && scryfallId.Length >= 2)
                    {
                        char dir1 = scryfallId[0];
                        char dir2 = scryfallId[1];

                        string cardImageUrl = $"https://cards.scryfall.io/normal/front/{dir1}/{dir2}/{scryfallId}.jpg";
                        string secondCardImageUrl = $"https://cards.scryfall.io/normal/back/{dir1}/{dir2}/{scryfallId}.jpg";


                        if (selectedCard.Side == "a" || selectedCard.Side == "b")
                        {
                            ImageSourceUrl = cardImageUrl;
                            ImageSourceUrl2nd = secondCardImageUrl;
                        }
                        else
                        {
                            ImageSourceUrl = cardImageUrl;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in selection changed: {ex.Message}");
            }
        }
        // Get the scryfallId for url to show the selected card image
        public async Task<string?> GetScryfallIdByUuidAsync(string uuid)
        {
            string query = "SELECT scryfallId FROM cardIdentifiers WHERE uuid = @uuid";

            using (var command = new SQLiteCommand(query, DBAccess.connection))
            {
                command.Parameters.AddWithValue("@uuid", uuid);

                try
                {
                    var result = await command.ExecuteScalarAsync();

                    if (result != null && result != DBNull.Value)
                    {
                        return result.ToString();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in GetScryfallIdByUuidAsync: {ex.Message}");
                }
            }
            return null;
        }
        #endregion

        #region Load data and populate UI elements
        private async Task LoadDataAsync()
        {
            Debug.WriteLine("Loading data asynchronously...");
            try
            {
                string query =
                    "SELECT COALESCE(c.faceName, c.name) AS Name, " +
                    "s.name AS SetName, " +
                    "k.keyruneImage AS KeyRuneImage, " +
                    "c.manaCost AS ManaCost, " +
                    "u.manaCostImage AS ManaCostImage, " +
                    "c.types AS Types, " +
                    "c.supertypes AS SuperTypes, " +
                    "c.subtypes AS SubTypes, " +
                    "c.type AS Type, " +
                    "c.keywords AS Keywords, " +
                    "c.text AS RulesText, " +
                    "c.manaValue AS ManaValue, " +
                    "c.uuid AS Uuid, " +
                    "c.finishes AS Finishes, " +
                    "c.side AS Side " +
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

                    var manaCostRaw = reader["ManaCost"]?.ToString() ?? string.Empty;
                    var manaCostProcessed = string.Join(",", manaCostRaw.Split(new[] { '{', '}' }, StringSplitOptions.RemoveEmptyEntries)).Trim(',');

                    cards.Add(new CardSet
                    {
                        Name = reader["Name"]?.ToString() ?? string.Empty,
                        SetName = reader["SetName"]?.ToString() ?? string.Empty,
                        SetIcon = setIconImageSource,
                        ManaCost = manaCostProcessed,
                        ManaCostImage = manaCostImageSource,
                        Types = reader["Types"]?.ToString() ?? string.Empty,
                        SuperTypes = reader["SuperTypes"]?.ToString() ?? string.Empty,
                        SubTypes = reader["SubTypes"]?.ToString() ?? string.Empty,
                        Type = reader["Type"]?.ToString() ?? string.Empty,
                        Keywords = reader["Keywords"]?.ToString() ?? string.Empty,
                        Text = reader["RulesText"]?.ToString() ?? string.Empty,
                        ManaValue = double.TryParse(reader["ManaValue"]?.ToString(), out double manaValue) ? manaValue : 0,
                        Uuid = reader["Uuid"]?.ToString() ?? string.Empty,
                        Finishes = reader["Finishes"]?.ToString() ?? string.Empty,
                        Side = reader["Side"]?.ToString() ?? string.Empty,
                    });
                }

                Dispatcher.Invoke(() =>
                {
                    cardCountLabel.Content = $"Cards shown: {cards.Count}";
                    mainCardWindowDatagrid.ItemsSource = cards;
                    dataView = CollectionViewSource.GetDefaultView(cards);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while loading data: {ex.Message}");
            }
        }
        // Convert byte array (for set icon) into an image to display in the datagrid
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
        private Task FillComboBoxesAsync()
        {
            try
            {
                // Get the values to populate the comboboxes
                var cardNames = cards.Select(card => card.Name).Distinct().ToList();
                var setNames = cards.Select(card => card.SetName).Distinct().ToList();
                var types = cards.Select(card => card.Types).Distinct().ToList();
                var superTypes = cards.Select(card => card.SuperTypes).Distinct().ToList();
                var subTypes = cards.Select(card => card.SubTypes).Distinct().ToList();
                var keywords = cards.Select(card => card.Keywords).Distinct().ToList();

                filterContext.AllColors.AddRange(new[] { "W", "U", "B", "R", "G", "C", "X" });

                var allOrNoneColorsOption = new List<string> { "Cards with any of these colors", "Cards with all of these colors", "Cards with none of these colors" };
                var manaValueOptions = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 1000000 };
                var manaValueCompareOptions = new List<string> { "less than", "less than/eq", "greater than", "greater than/eq", "equal to" };

                // Set up elements in card type listbox
                filterContext.AllTypes = types
                    .Where(type => type != null)
                    .SelectMany(type => type!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    .Select(p => p.Trim())
                    .Distinct()
                    .OrderBy(type => type)
                    .ToList();

                // Set up elements in supertype listbox
                filterContext.AllSuperTypes = superTypes
                    .Where(type => type != null)
                    .SelectMany(type => type!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    .Select(p => p.Trim())
                    .Distinct()
                    .OrderBy(type => type)
                    .ToList();

                // Set up elements in subtype listbox
                filterContext.AllSubTypes = subTypes
                    .Where(type => type != null)
                    .SelectMany(type => type!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    .Select(p => p.Trim())
                    .Distinct()
                    .OrderBy(type => type)
                    .ToList();

                // Set up elements in keywords listbox
                filterContext.AllKeywords = keywords
                    .Where(keyword => keyword != null)
                    .SelectMany(keyword => keyword!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    .Select(p => p.Trim())
                    .Distinct()
                    .OrderBy(keyword => keyword)
                    .ToList();

                Dispatcher.Invoke(() =>
                {
                    FilterRulesTextTextBox.Text = filterContext.RulesTextDefaultText;
                    filterCardNameComboBox.ItemsSource = cardNames.OrderBy(name => name).ToList();
                    filterSetNameComboBox.ItemsSource = setNames.OrderBy(name => name).ToList();
                    filterColorsListBox.ItemsSource = filterContext.AllColors;
                    allOrNoneComboBox.ItemsSource = allOrNoneColorsOption;
                    allOrNoneComboBox.SelectedIndex = 0;
                    ManaValueComboBox.ItemsSource = manaValueOptions;
                    ManaValueComboBox.SelectedIndex = -1;
                    ManaValueOperatorComboBox.ItemsSource = manaValueCompareOptions;
                    ManaValueOperatorComboBox.SelectedIndex = -1;
                    SetDefaultTextInComboBox(SuperTypesComboBox, "FilterSuperTypesTextBox", filterContext.SuperTypesDefaultText);
                    SetDefaultTextInComboBox(TypesComboBox, "FilterTypesTextBox", filterContext.TypesDefaultText);
                    SetDefaultTextInComboBox(SubTypesComboBox, "FilterSubTypesTextBox", filterContext.SubTypesDefaultText);
                    SetDefaultTextInComboBox(KeywordsComboBox, "FilterKeywordsTextBox", filterContext.KeywordsDefaultText);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while filling comboboxes: {ex.Message}");
            }
            return Task.CompletedTask;
        }
        void SetDefaultTextInComboBox(ComboBox comboBox, string textBoxName, string defaultText)
        {
            var filterTextBox = comboBox.Template.FindName(textBoxName, comboBox) as TextBox;
            if (filterTextBox != null)
            {
                filterTextBox.Text = defaultText;
                filterTextBox.Foreground = new SolidColorBrush(Colors.Gray);
            }
        }

        #endregion

        #region UI elements for updating card database
        private async void checkForUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            await UpdateDB.CheckForUpdatesAsync();
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
    }
}