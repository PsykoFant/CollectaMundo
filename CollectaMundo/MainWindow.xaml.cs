using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static CollectaMundo.BackupRestore;
using static CollectaMundo.CardSet;

namespace CollectaMundo
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Set up varibales
        // Used for displaying images
        private string? _imageSourceUrl = string.Empty;
        private string? _imageSourceUrl2nd = string.Empty;
        public string? ImageSourceUrl
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
        public string? ImageSourceUrl2nd
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

        // Query strings to load cards into datagrids
        private readonly string allCardsQuery = "SELECT * FROM view_allCards";
        public readonly string myCollectionQuery = "SELECT * FROM view_myCollection";
        private readonly string colourQuery = "SELECT* FROM uniqueManaSymbols WHERE uniqueManaSymbol IN ('W', 'U', 'B', 'R', 'G', 'C', 'X') ORDER BY CASE uniqueManaSymbol WHEN 'W' THEN 1 WHEN 'U' THEN 2 WHEN 'B' THEN 3 WHEN 'R' THEN 4 WHEN 'G' THEN 5 WHEN 'C' THEN 6 WHEN 'X' THEN 7 END;";

        // The CardSet object which holds all the cards read from db
        private readonly List<CardSet> allCards = [];
        public List<CardSet> myCards = [];
        private readonly List<CardSet> ColorIcons = [];

        // The filter object from the FilterContext class
        private readonly FilterContext filterContext = new();
        private readonly FilterManager filterManager;

        // Object of AddToCollectionManager class to access that functionality
        private readonly AddToCollectionManager addToCollectionManager = new();

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
            DataContext = filterContext;

            // Update the statusbox with messages from methods in DownloadAndPrepareDB
            DownloadAndPrepDB.StatusMessageUpdated += UpdateStatusTextBox;
            // Update the statusbox with messages from methods in UpdateDB
            UpdateDB.StatusMessageUpdated += UpdateStatusTextBox;

            // Set up system
            Loaded += async (sender, args) =>
            {
                await LoadDataIntoUiElements();
            };


            filterManager = new FilterManager(filterContext);

            // Pick up filtering comboboxes changes
            FilterSetNameComboBox.SelectionChanged += ComboBox_SelectionChanged;
            AllOrNoneComboBox.SelectionChanged += ComboBox_SelectionChanged;
            ManaValueComboBox.SelectionChanged += ComboBox_SelectionChanged;
            ManaValueOperatorComboBox.SelectionChanged += ComboBox_SelectionChanged;
        }
        public async Task LoadDataIntoUiElements()
        {
            await DownloadAndPrepDB.CheckDatabaseExistenceAsync();

            await DBAccess.OpenConnectionAsync();

            // Start on the all cards page            
            ResetGrids();
            GridSearchAndFilterAllCards.Visibility = Visibility.Visible;
            GridFiltering.Visibility = Visibility.Visible;
            LogoSmall.Visibility = Visibility.Visible;

            var loadAllCards = LoadDataAsync(allCards, allCardsQuery, AllCardsDataGrid, false, true);
            var loadMyCollection = LoadDataAsync(myCards, myCollectionQuery, MyCollectionDatagrid, true, true);
            var loadColorIcons = LoadColorIcons(ColorIcons, colourQuery);

            await Task.WhenAll(loadAllCards, loadMyCollection, loadColorIcons);
            await FillComboBoxesAsync();

            DBAccess.CloseConnection();

            CardsToAddListView.ItemsSource = addToCollectionManager.CardItemsToAdd;
            CardsToEditListView.ItemsSource = addToCollectionManager.CardItemsToEdit;
            ShowFoilCheckBox.IsChecked = true;
        }

        #region Load data and populate UI elements
        public async Task LoadDataAsync(List<CardSet> cardList, string query, DataGrid dataGrid, bool isCardItem, bool showLoadScreen)
        {
            try
            {
                if (showLoadScreen)
                {
                    await ShowStatusWindowAsync(true);  // Show loading message                
                    CurrentInstance.StatusLabel.Content = "Loading ALL the cards ... ";
                    CurrentInstance.ProgressBar.Visibility = Visibility.Collapsed;
                    // Force the UI to update
                    Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                }

                cardList.Clear();

                List<CardSet> tempCardList = [];
                using var command = new SQLiteCommand(query, DBAccess.connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    try
                    {
                        var card = CreateCardFromReader(reader, isCardItem);
                        tempCardList.Add(card);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error while creating card: {ex.Message}");
                        throw;
                    }
                }

                cardList.AddRange(tempCardList);
                dataGrid.ItemsSource = cardList;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while loading cards: {ex.Message}");
                MessageBox.Show($"Error while loading cards: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                CurrentInstance.StatusLabel.Content = string.Empty;

                if (showLoadScreen)
                {
                    await ShowStatusWindowAsync(false);
                    CurrentInstance.ProgressBar.Visibility = Visibility.Visible;
                }
            }
        }
        private static CardSet CreateCardFromReader(DbDataReader reader, bool isCardItem)
        {
            try
            {
                var card = isCardItem ? (CardSet)new CardItem() : new CardSet();

                // Populate common properties
                card.Name = reader["Name"]?.ToString() ?? string.Empty;
                card.SetName = reader["SetName"]?.ToString() ?? string.Empty;
                card.Types = reader["Types"]?.ToString() ?? string.Empty;
                card.ManaCost = ProcessManaCost(reader["ManaCost"]?.ToString() ?? string.Empty);
                card.SuperTypes = reader["SuperTypes"]?.ToString() ?? string.Empty;
                card.SubTypes = reader["SubTypes"]?.ToString() ?? string.Empty;
                card.Type = reader["Type"]?.ToString() ?? string.Empty;
                card.Keywords = reader["Keywords"]?.ToString() ?? string.Empty;
                card.Text = reader["RulesText"]?.ToString() ?? string.Empty;
                card.ManaValue = double.TryParse(reader["ManaValue"]?.ToString(), out double manaValue) ? manaValue : 0;
                card.Language = reader["Language"]?.ToString() ?? string.Empty;
                card.Uuid = reader["Uuid"]?.ToString() ?? string.Empty;
                card.Side = reader["Side"]?.ToString() ?? string.Empty;
                card.Finishes = reader["Finishes"]?.ToString();

                // Populate raw data fields for parallel processing
                card.SetIconBytes = reader["KeyRuneImage"] as byte[];
                card.ManaCostImageBytes = reader["ManaCostImage"] as byte[];
                card.ManaCostRaw = reader["ManaCost"]?.ToString() ?? string.Empty;

                if (card is CardItem cardItem)
                {
                    cardItem.CardId = reader["CardId"] != DBNull.Value ? Convert.ToInt32(reader["CardId"]) : (int?)null;
                    cardItem.CardsOwned = Convert.ToInt32(reader["CardsOwned"]);
                    cardItem.CardsForTrade = Convert.ToInt32(reader["CardsForTrade"]);
                    cardItem.SelectedCondition = reader["Condition"]?.ToString();
                    cardItem.SelectedFinish = reader["Finish"]?.ToString();
                }

                return card;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in CreateCardFromReader: {ex.Message}");
                throw;
            }
        }
        private static string ProcessManaCost(string manaCostRaw)
        {
            return string.Join(",", manaCostRaw.Split(new[] { '{', '}' }, StringSplitOptions.RemoveEmptyEntries)).Trim(',');
        }
        public async Task LoadColorIcons(List<CardSet> cardList, string query)
        {
            try
            {
                cardList.Clear();

                List<CardSet> tempCardList = [];
                using var command = new SQLiteCommand(query, DBAccess.connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var card = CreateColorIcon(reader);
                    tempCardList.Add(card);
                }

                cardList.AddRange(tempCardList);
                FilterColorsListBoxIcons.ItemsSource = cardList;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while loading color icons: {ex.Message}");
                MessageBox.Show($"Error while loading color icons: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private static CardSet CreateColorIcon(DbDataReader reader)
        {
            try
            {
                var card = new CardSet
                {
                    ManaCostImageBytes = reader["ManaSymbolImage"] as byte[],
                    ManaCostRaw = reader["uniqueManaSymbol"]?.ToString() ?? string.Empty
                };
                return card;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in CreateColorIcon: {ex.Message}");
                throw;
            }
        }
        private Task FillComboBoxesAsync()
        {
            try
            {
                // Make sure lists are clear
                filterContext.CardNames.Clear();
                filterContext.AllSuperTypes.Clear();
                filterContext.AllTypes.Clear();
                filterContext.AllSubTypes.Clear();
                filterContext.AllColors.Clear();
                filterContext.AllKeywords.Clear();

                // Get the values to populate the comboboxes
                var cardNames = allCards.Select(card => card.Name).Distinct().ToList();
                var setNames = allCards.Select(card => card.SetName).Distinct().ToList();
                var types = allCards.Select(card => card.Types).Distinct().ToList();
                var superTypes = allCards.Select(card => card.SuperTypes).Distinct().ToList();
                var subTypes = allCards.Select(card => card.SubTypes).Distinct().ToList();
                var keywords = allCards.Select(card => card.Keywords).Distinct().ToList();

                filterContext.AllColors.AddRange(["W", "U", "B", "R", "G", "C", "X"]);

                var allOrNoneColorsOption = new List<string> { "Cards with any of these colors", "Cards with all of these colors", "Cards with none of these colors" };
                var manaValueOptions = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 1000000 };
                var manaValueCompareOptions = new List<string> { "less than", "less than/eq", "greater than", "greater than/eq", "equal to" };

                // Set up elements in supertype listbox
                filterContext.AllSuperTypes = [.. superTypes
                    .Where(type => type != null)
                    .SelectMany(type => type!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    .Select(p => p.Trim())
                    .Distinct()
                    .OrderBy(type => type)];

                // List of unwanted types. Old cards, weird types from un-sets etc. 
                var typesToRemove = new HashSet<string>
                {
                    "Eaturecray",
                    "Summon",
                    "Scariest",
                    "You'll",
                    "Ever",
                    "See",
                    "Jaguar",
                    "Dragon",
                    "Knights",
                    "Legend",
                    "instant"
                };

                // Set up elements in type listbox, removing unwanted types
                filterContext.AllTypes = [.. types
                    .Where(type => type != null)
                    .SelectMany(type => type!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    .Select(p => p.Trim())
                    .Where(p => !typesToRemove.Contains(p))  // Filter out unwanted types
                    .Distinct()
                    .OrderBy(type => type)];

                // List of unwanted subtypes. Old cards, weird types from un-sets etc. 
                var subTypesToRemove = new HashSet<string>
                {
                    "(creature",
                    "and/or",
                    "type)|Judge",
                    "The"
                };

                // Set up elements in subtype listbox
                filterContext.AllSubTypes = [.. subTypes
                    .Where(type => type != null)
                    .SelectMany(type => type!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    .Select(p => p.Trim())
                    .Where(p => !subTypesToRemove.Contains(p))  // Filter out unwanted subtypes
                    .Distinct()
                    .OrderBy(type => type)];

                // Set up elements in keywords listbox
                filterContext.AllKeywords = [.. keywords
                    .Where(keyword => keyword != null)
                    .SelectMany(keyword => keyword!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    .Select(p => p.Trim())
                    .Distinct()
                    .OrderBy(keyword => keyword)];


                foreach (var name in cardNames)
                {
                    filterContext.CardNames.Add(name);
                }

                Dispatcher.Invoke(() =>
                {
                    FilterRulesTextTextBox.Text = filterContext.RulesTextDefaultText;
                    FilterSetNameComboBox.ItemsSource = setNames.OrderBy(name => name).ToList();
                    FilterColorsListBox.ItemsSource = filterContext.AllColors;
                    AllOrNoneComboBox.ItemsSource = allOrNoneColorsOption;
                    AllOrNoneComboBox.SelectedIndex = 0;
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
                MessageBox.Show($"Error while filling comboboxes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return Task.CompletedTask;
        }
        private static void SetDefaultTextInComboBox(ComboBox comboBox, string textBoxName, string defaultText)
        {
            if (comboBox.Template.FindName(textBoxName, comboBox) is TextBox filterTextBox)
            {
                filterTextBox.Text = defaultText;
                filterTextBox.Foreground = new SolidColorBrush(Colors.Gray);
            }
        }

        #endregion

        #region Filter elements handling        
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var filteredAllCards = filterManager.ApplyFilter(allCards, "allCards");
            var filteredMyCards = filterManager.ApplyFilter(myCards, "myCards");

            AllCardsDataGrid.ItemsSource = filteredAllCards;
            MyCollectionDatagrid.ItemsSource = filteredMyCards;
        }

        private void ComboBox_DropDownOpened(object sender, EventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                try
                {
                    var (defaultText, filterTextBoxName, listBoxName) = GetComboBoxConfig(comboBox.Name);

                    if (comboBox.Template.FindName(filterTextBoxName, comboBox) is TextBox filterTextBox && (string.IsNullOrWhiteSpace(filterTextBox.Text) || filterTextBox.Text == defaultText))
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
                if (comboBox.Template.FindName(listBoxName, comboBox) is ListBox listBox)
                {
                    // Get both items source and the corresponding selected items set.
                    var (itemsSource, selectedItems) = GetDataSetAndSelection(listBoxName);
                    listBox.ItemsSource = itemsSource;

                    listBox.Dispatcher.Invoke(() =>
                    {
                        foreach (var item in itemsSource)
                        {
                            if (listBox.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem listBoxItem)
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
                    while (parent != null && parent is not ComboBox)
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
                        if (comboBox.Template.FindName(listBoxName, comboBox) is ListBox listBox)
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
                    : [.. dataSet.Distinct().OrderBy(type => type)];

                listBox.ItemsSource = filteredItems;

                listBox.Dispatcher.Invoke(() =>
                {
                    foreach (var item in filteredItems)
                    {
                        if (listBox.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem listBoxItem) // Check if listBoxItem is not null
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
            ApplyFilterSelection();
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
            Debug.WriteLine("CheckBox_Checked was clicked...");
            try
            {
                if (sender is not DependencyObject dependencyObject)
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
                            ApplyFilterSelection();
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
                if (sender is not DependencyObject dependencyObject)
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
                            ApplyFilterSelection();
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
        public static T? FindVisualChild<T>(DependencyObject obj) where T : DependencyObject // Because we use custom combobox, we need this method to find embedded elements
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
        private void FilterRulesTextButton_Click(object sender, RoutedEventArgs e) // Apply filter for rulestext freetext search
        {
            ApplyFilterSelection();
        }
        public void ApplyFilterSelection()
        {
            var filteredAllCards = filterManager.ApplyFilter(allCards, "allCards");
            var filteredMyCards = filterManager.ApplyFilter(myCards, "myCards");

            AllCardsDataGrid.ItemsSource = filteredAllCards;
            MyCollectionDatagrid.ItemsSource = filteredMyCards;
        }

        // Reset filter elements
        public void ClearFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            // Reset filter TextBoxes for each ComboBox
            ResetFilterTextBox(SuperTypesComboBox, "FilterSuperTypesTextBox", filterContext.SuperTypesDefaultText);
            ResetFilterTextBox(TypesComboBox, "FilterTypesTextBox", filterContext.TypesDefaultText);
            ResetFilterTextBox(SubTypesComboBox, "FilterSubTypesTextBox", filterContext.SubTypesDefaultText);
            ResetFilterTextBox(KeywordsComboBox, "FilterKeywordsTextBox", filterContext.KeywordsDefaultText);

            // Clear non-custom comboboxes
            FilterSetNameComboBox.SelectedIndex = -1;
            AllOrNoneComboBox.SelectedIndex = 0;
            ManaValueComboBox.SelectedIndex = -1;
            ManaValueOperatorComboBox.SelectedIndex = -1;


            var headerComboBox = FindVisualChild<ComboBox>(AllCardsDataGrid);
            if (headerComboBox != null)
            {
                headerComboBox.SelectedIndex = -1;
            }

            // Clear selections in the colors listbox
            ClearListBoxSelections(FilterColorsListBox);

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
            CardRulesTextTextBlock.Text = string.Empty;
            CardSuperTypesTextBlock.Text = string.Empty;
            CardTypesTextBlock.Text = string.Empty;
            CardSubTypesTextBlock.Text = string.Empty;
            CardKeyWordsTextBlock.Text = string.Empty;

            // Uncheck CheckBoxes if necessary
            TypesAndOrCheckBox.IsChecked = false;
            SuperTypesAndOrCheckBox.IsChecked = false;
            SubTypesAndOrCheckBox.IsChecked = false;
            KeywordsAndOrCheckBox.IsChecked = false;
            ShowFoilCheckBox.IsChecked = false;

            // Reset card images
            ImagePromoLabel.Content = string.Empty;
            ImageSetLabel.Content = string.Empty;
            ImageSourceUrl = null;
            ImageSourceUrl2nd = null;

            // Update filter label and apply filters to refresh the DataGrid            
            ApplyFilterSelection();
        }
        private static void ResetFilterTextBox(ComboBox comboBox, string textBoxName, string defaultText)
        {
            if (comboBox.Template.FindName(textBoxName, comboBox) is TextBox filterTextBox)
            {
                filterTextBox.Text = defaultText;
                filterTextBox.Foreground = new SolidColorBrush(Colors.Gray);
            }
        }
        private static void ClearListBoxSelections(ListBox listBox)
        {
            foreach (var item in listBox.Items)
            {
                if (listBox.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem container)
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
        // Show the card image for the highlighted DataGrid row
        private async void CardImageSelectionChangedHandler(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid dataGrid && dataGrid.SelectedItem is CardSet selectedCard)
            {
                if (selectedCard.Uuid != null)
                {
                    await ShowCardImage.ShowImage(selectedCard.Uuid, selectedCard.Types);
                }
            }
            else if (sender is ComboBox comboBox && comboBox.SelectedItem is UuidVersion selectedVersion && !string.IsNullOrEmpty(selectedVersion.Uuid))
            {
                await ShowCardImage.ShowImage(selectedVersion.Uuid);
            }
        }

        #endregion

        #region Pick up events for add to or edit collection 

        // Modify values in the listview
        private void IncrementCount_Click(object sender, RoutedEventArgs e)
        {
            addToCollectionManager.IncrementButtonHandler(sender, e);
        }
        private void DecrementCount_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)  // This checks if sender is a Button and assigns it to button if true
            {
                if (button.DataContext is CardSet.CardItem cardItem)
                {
                    // Determine which ListView initiated the event and pass the appropriate collection
                    ObservableCollection<CardSet.CardItem> targetCollection =
                        (CardsToEditListView.Items.Contains(cardItem)) ? addToCollectionManager.CardItemsToEdit : addToCollectionManager.CardItemsToAdd;

                    // Only decrement for CardItemsToEdit if count is above 0
                    if (targetCollection == addToCollectionManager.CardItemsToEdit)
                    {
                        if (cardItem.CardsOwned > 0)
                        {
                            addToCollectionManager.DecrementButtonHandler(sender, targetCollection);
                        }
                    }
                    else
                    {
                        addToCollectionManager.DecrementButtonHandler(sender, targetCollection);

                        // If there is nothing in CardItemsToAdd, hide listview and button
                        if (targetCollection.Count == 0)
                        {
                            AddToCollectionManager.HideCardsToAddListView(true);
                        }
                    }
                }
            }
        }
        private void CardsOwnedTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            addToCollectionManager.CardsOwnedTextHandler(sender, addToCollectionManager.CardItemsToAdd);
        }
        private void CardsForTradeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            AddToCollectionManager.CardsForTradeTextHandler(sender);
        }
        private void ListViewComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AddToCollectionManager.AdjustColumnWidths();
        }
        private void ButtonClearCardsToAdd_Click(object sender, RoutedEventArgs e)
        {
            addToCollectionManager.CardItemsToAdd.Clear();
            AddToCollectionManager.HideCardsToAddListView(true);
        }
        private void ButtonClearCardsToEdit_Click(object sender, RoutedEventArgs e)
        {
            addToCollectionManager.CardItemsToEdit.Clear();
            AddToCollectionManager.HideCardsToEditListView(true);
        }


        // Add cards to add or edit listview
        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Check if the sender is a DataGrid and has a selected item
            if (sender is DataGrid grid && grid.SelectedItem != null)
            {
                // If the source is AllCardsDataGrid, add to CardItemsToAdd. Else, add to CardItemsToEdit
                if (grid.SelectedItem is CardSet cardSetCard && grid.Name == "AllCardsDataGrid")
                {
                    AddToCollectionManager.AddOrEditCardHandler(cardSetCard, addToCollectionManager.CardItemsToAdd);
                    AddToCollectionManager.ShowCardsToAddListView();
                    grid.UnselectAll();
                }
                else if (grid.SelectedItem is CardItem cardItemCard && grid.Name == "MyCollectionDatagrid")
                {
                    AddToCollectionManager.AddOrEditCardHandler(cardItemCard, addToCollectionManager.CardItemsToEdit);
                    AddToCollectionManager.ShowCardsToEditListView();
                    grid.UnselectAll();
                }
            }
        }
        private void ButtonAddCardsToMyCollection_Click(object sender, RoutedEventArgs e)
        {
            AddToCollectionManager.ShowCardsToAddListView();
            foreach (CardSet selectedCard in AllCardsDataGrid.SelectedItems)
            {
                AddToCollectionManager.AddOrEditCardHandler(selectedCard, addToCollectionManager.CardItemsToAdd);
            }
            AllCardsDataGrid.UnselectAll();
        }
        private void ButtonAddCardsToMyCollectionWithDefaultValues_Click(object sender, RoutedEventArgs e)
        {
            var selectedCards = AllCardsDataGrid.SelectedItems.Cast<CardSet>().ToList();
            if (selectedCards.Count > 0)
            {
                AddToCollectionManager.SubmitNewCardsToCollectionWithDefaultValues(selectedCards);
                AllCardsDataGrid.UnselectAll();
            }
        }
        private void ButtonEditCardsInCollection_Click(object sender, RoutedEventArgs e)
        {
            AddToCollectionManager.ShowCardsToEditListView();
            foreach (CardSet selectedCard in MyCollectionDatagrid.SelectedItems)
            {
                AddToCollectionManager.AddOrEditCardHandler(selectedCard, addToCollectionManager.CardItemsToEdit);
            }
            MyCollectionDatagrid.UnselectAll();
        }

        // Submit cards in add or edit listviews
        private void ButtonSubmitCardsToMyCollection_Click(object sender, RoutedEventArgs e)
        {
            LogoSmall.Visibility = Visibility.Collapsed;
            addToCollectionManager.SubmitNewCardsToCollection(sender, e);
        }
        private void SubmitCardEditsInMyCollection_Click(object sender, RoutedEventArgs e)
        {
            LogoSmall.Visibility = Visibility.Collapsed;
            addToCollectionManager.SubmitEditedCardsToCollection(sender, e);
        }
        private void ButtonDeleteCardsFromCollection_Click(object sender, RoutedEventArgs e)
        {
            var selectedCards = MyCollectionDatagrid.SelectedItems.Cast<CardItem>().ToList();
            if (selectedCards.Count > 0)
            {
                AddToCollectionManager.DeleteCardsFromCollection(selectedCards);
                AllCardsDataGrid.UnselectAll();
            }
        }

        #endregion

        #region UI elements for utilities
        private async void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            await UpdateDB.CheckForUpdatesAsync();
        }
        private async void UpdateDbButton_Click(object sender, RoutedEventArgs e)
        {
            ResetGrids();
            await UpdateDB.UpdateCardDatabaseAsync();
        }
        private void UpdateStatusTextBox(string message)
        {
            Dispatcher.Invoke(() =>
            {
                StatusLabel.Content = message;
            });
        }
        private async void CreateBackupButton_Click(object sender, RoutedEventArgs e)
        {
            await CreateCsvBackupAsync();
        }

        #region Import wizard

        // Import wizard different steps button methods
        private void ImportCollectionButton_Click(object sender, RoutedEventArgs e)
        {
            Inspiredtinkering.Visibility = Visibility.Collapsed;
            GridImportWizard.Visibility = Visibility.Visible;
            GridImportStartScreen.Visibility = Visibility.Visible;
        }
        private async void BeginImportButton_Click(object sender, RoutedEventArgs e)
        {
            await BeginImportButton();
        }
        private async void ButtonIdColumnMappingNext_Click(object sender, RoutedEventArgs e)
        {
            await ButtonIdColumnMappingNext();
        }
        private void ButtonSkipIdColumnMapping_Click(object sender, RoutedEventArgs e)
        {
            ButtonSkipIdColumnMapping();
        }
        private async void ButtonNameAndSetMappingNext_Click(object sender, RoutedEventArgs e)
        {
            await ButtonNameAndSetMappingNext();
        }
        private void ButtonMultipleUuidsNext_Click(object sender, RoutedEventArgs e)
        {
            ButtonMultipleUuidsNext();
        }
        private async void ButtonAdditionalFieldsNext_Click(object sender, RoutedEventArgs e)
        {
            await ButtonAdditionalFieldsNext();
        }
        private async void ButtonConditionMappingNext_Click(object sender, RoutedEventArgs e)
        {
            await ButtonConditionMappingNext();
        }
        private async void ButtonFinishesMappingNext_Click(object sender, RoutedEventArgs e)
        {
            await ButtonFinishesMappingNext();
        }
        private void ButtonLanguageMappingNext_Click(object sender, RoutedEventArgs e)
        {
            ButtonLanguageMappingNext();
        }
        private async void ButtonImportConfirm_Click(object sender, RoutedEventArgs e)
        {
            await AddItemsToDatabaseAsync();
        }
        private async void ButtonEndImportWizard_Click(object sender, RoutedEventArgs e)
        {
            await EndImportWizard();
        }

        // Import wizards misc. buttons and helper methods
        private void ClearMappingButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.DataContext is ColumnMapping columnMapping)
                {
                    if (columnMapping.DatabaseFields != null && columnMapping.CsvHeaders != null)
                    {
                        // Clear both database and CSV header fields for IdColumnMappingListView
                        columnMapping.SelectedDatabaseField = null;
                        columnMapping.SelectedCsvHeader = null;
                    }
                    else
                    {
                        // Clear only CSV header field for other ListViews
                        columnMapping.CsvHeader = null;
                    }
                }
                else if (button.DataContext is ValueMapping valueMapping)
                {
                    valueMapping.SelectedCardSetValue = null;
                }
            }
        }
        private void SaveListOfUnimportedItems_Click(object sender, RoutedEventArgs e)
        {
            SaveUnimportedItemsToFile();
        }
        private void CancelImport_Click(object sender, RoutedEventArgs e)
        {
            EndImport();
        }

        #endregion

        #endregion

        #region Top menu navigation
        private void MenuSearchAndFilter_Click(object sender, RoutedEventArgs e)
        {
            ResetGrids();
            MenuSearchAndFilterButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5cb9ca"));
            LogoSmall.Visibility = Visibility.Visible;
            GridFiltering.Visibility = Visibility.Visible;
            GridSearchAndFilterAllCards.Visibility = Visibility.Visible;
            AddToCollectionManager.AdjustColumnWidths();
        }
        private void MenuMyCollection_Click(object sender, RoutedEventArgs e)
        {
            ResetGrids();
            MenuMyCollectionButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5cb9ca"));
            LogoSmall.Visibility = Visibility.Visible;
            GridFiltering.Visibility = Visibility.Visible;
            GridMyCollection.Visibility = Visibility.Visible;
            AddToCollectionManager.AdjustColumnWidths();
        }
        private void MenuUtilsButton_Click(object sender, RoutedEventArgs e)
        {
            ResetGrids();
            MenuUtilsButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5cb9ca"));
            GridUtilsMenu.Visibility = Visibility.Visible;
            GridUtilitiesSection.Visibility = Visibility.Visible;
        }
        public void ResetGrids()
        {
            MenuSearchAndFilterButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDDDDDD"));
            MenuMyCollectionButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDDDDDD"));
            MenuUtilsButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDDDDDD"));

            EditStatusTextBlock.Text = string.Empty;
            AddStatusTextBlock.Text = string.Empty;
            UtilsInfoLabel.Content = "";
            GridSearchAndFilterAllCards.Visibility = Visibility.Collapsed;
            GridMyCollection.Visibility = Visibility.Collapsed;
            GridUtilitiesSection.Visibility = Visibility.Collapsed;

            ImagePromoLabel.Content = string.Empty;
            ImageSetLabel.Content = string.Empty;
            ImageSourceUrl = null;
            ImageSourceUrl2nd = null;

            LogoSmall.Visibility = Visibility.Collapsed;
            GridFiltering.Visibility = Visibility.Collapsed;
            GridUtilsMenu.Visibility = Visibility.Collapsed;
            ApplyFilterSelection();
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
                        // Disable top menu buttons
                        CurrentInstance.GridTopMenu.IsEnabled = false;

                        // Show status section and hide others
                        CurrentInstance.GridContentSection.Visibility = Visibility.Collapsed;
                        CurrentInstance.GridFiltering.Visibility = Visibility.Collapsed;
                        CurrentInstance.GridCardImages.Visibility = Visibility.Collapsed;
                        CurrentInstance.GridStatus.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        CurrentInstance.GridTopMenu.IsEnabled = true;
                        CurrentInstance.GridStatus.Visibility = Visibility.Collapsed;
                        CurrentInstance.GridContentSection.Visibility = Visibility.Visible;
                        CurrentInstance.GridFiltering.Visibility = Visibility.Visible;
                        CurrentInstance.GridCardImages.Visibility = Visibility.Visible;

                    }
                });
            }
        }
    }
}