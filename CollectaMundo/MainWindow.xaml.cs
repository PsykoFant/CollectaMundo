using CollectaMundo.Models;
using ServiceStack;
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
using static CollectaMundo.Models.CardSet;

namespace CollectaMundo
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Set up varibales
        // Used for displaying images
        private string? _imageSourceUrl = string.Empty;
        private string? _imageSourceUrl2nd = string.Empty;

        // Location of user's "Downloads" folder
        public readonly static string currentUserFolders = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
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
        public readonly string allCardsQuery = "SELECT * FROM view_allCards";
        public readonly string myCollectionQuery = "SELECT * FROM view_myCollection;";
        public readonly string allCardsForDecksQuery = "SELECT * FROM view_allCardsForDecks;";
        private readonly string colourQuery = "SELECT* FROM uniqueManaSymbols WHERE uniqueManaSymbol IN ('W', 'U', 'B', 'R', 'G', 'C', 'X') ORDER BY CASE uniqueManaSymbol WHEN 'W' THEN 1 WHEN 'U' THEN 2 WHEN 'B' THEN 3 WHEN 'R' THEN 4 WHEN 'G' THEN 5 WHEN 'C' THEN 6 WHEN 'X' THEN 7 END;";

        // Flag to track startup phase
        public bool _isStartup = true;

        // The CardSet object which holds all the cards read from db
        public readonly List<CardSet> allCards = [];
        public readonly List<CardSet> myCards = [];
        public readonly List<CardSet> allCardsForDecks = [];
        private readonly List<CardSet> ColorIcons = [];

        // The filter object from the FilterContext class
        private readonly FilterContext filterContext = new();
        private readonly FilterManager filterManager;

        // Objects for deck management
        public readonly List<Deck> allDecks = [];
        public Deck CurrentDeck { get; set; } = new Deck();
        public List<string> allFormats = [];

        // Common variables used for deck edits
        TextBox textBoxToEdit = new();
        Button editButton = new();
        Button saveButton = new();
        Button cancelButton = new();
        string columnToEdit = string.Empty;

        // Object of AddToCollectionManager class to access that functionality
        private readonly AddToCollectionManager addToCollectionManager = new();
        public ObservableCollection<ObservableCollection<double>> ColumnWidths { get; set; } = [[50, 50], [50, 50], [50]];

        // Read the price retailer from appsettings.json
        public string? appsettingsRetailer = ConfigurationManager.GetSetting("PriceInfo:Retailer") as string;

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
            filterManager = new FilterManager(filterContext);

            // Set up system
            Loaded += async (sender, args) =>
            {
                await ShowStatusWindowAsync(true, "Just a quick system integrity check ...");
                await DownloadAndPrepDB.SystemIntegrityCheckAsync();
                await LoadDataIntoUiElements();
                _isStartup = false; // Set flag to false after initial load
            };

            // Update the statusbox with messages from methods in DownloadAndPrepareDB and UpdateDB
            DownloadAndPrepDB.StatusMessageUpdated += UpdateStatusTextBox;
            UpdateDB.StatusMessageUpdated += UpdateStatusTextBox;

            // Subscribe to column width changes
            AllCardsDataGrid.LayoutUpdated += (s, e) => FilterManager.DataGrid_LayoutUpdated(0);
            MyCollectionDataGrid.LayoutUpdated += (s, e) => FilterManager.DataGrid_LayoutUpdated(1);
            AllCardsForDecksDataGrid.LayoutUpdated += (s, e) => FilterManager.DataGrid_LayoutUpdated(2);

            // Pick up filtering combobox changes
            AllOrNoneComboBox.SelectionChanged += ComboBox_SelectionChanged;
            ManaValueComboBox.SelectionChanged += ComboBox_SelectionChanged;
            ManaValueOperatorComboBox.SelectionChanged += ComboBox_SelectionChanged;
        }

        #region Load data and populate UI elements
        public async Task LoadDataIntoUiElements()
        {
            await ShowStatusWindowAsync(true, "Loading ALL the cards ...");

            await DBAccess.OpenConnectionAsync();

            Task loadAllCards = PopulateCardDataGridAsync(allCards, allCardsQuery, AllCardsDataGrid, false, false);
            Task loadMyCollection = PopulateCardDataGridAsync(myCards, myCollectionQuery, MyCollectionDataGrid, true, false);
            Task loadCardsForDecks = PopulateCardDataGridAsync(allCardsForDecks, allCardsForDecksQuery, AllCardsForDecksDataGrid, false, true);
            Task loadColorIcons = LoadColorIcons(ColorIcons, colourQuery);
            Task loadDecks = LoadAllDecksAsync();
            Task populateAllFormatsList = PopulateAllFormatsListAsync();

            await Task.WhenAll(loadAllCards, loadMyCollection, loadColorIcons, loadDecks, populateAllFormatsList, loadCardsForDecks);

            DBAccess.CloseConnection();

            await PopulateFilterUiElements();

            CardPriceUtilities.UpdateDataGridHeaders(AllCardsDataGrid);
            CardPriceUtilities.UpdateDataGridHeaders(MyCollectionDataGrid);

            CardsToAddListView.ItemsSource = addToCollectionManager.CardItemsToAdd;
            CardsToEditListView.ItemsSource = addToCollectionManager.CardItemsToEdit;

            // Start on the search and filter all cards page            
            ResetGrids();
            MenuSearchAndFilterButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5cb9ca"));
            LogoSmall.Visibility = Visibility.Visible;
            GridFiltering.Visibility = Visibility.Visible;
            GridSearchAndFilterAllCards.Visibility = Visibility.Visible;
            FilterSummaryScrollViewer.Visibility = Visibility.Visible;

            await ShowStatusWindowAsync(false);
        }
        public static async Task PopulateCardDataGridAsync(List<CardSet> cardList, string query, DataGrid dataGrid, bool isCardItem, bool isAllCardsItem)
        {
            try
            {
                cardList.Clear();

                List<CardSet> tempCardList = [];
                using SQLiteCommand command = new(query, DBAccess.connection);
                using DbDataReader reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    try
                    {
                        CardSet card = CreateCardFromReader(reader, isCardItem, isAllCardsItem);
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
        }
        private static CardSet CreateCardFromReader(DbDataReader reader, bool isCardItem, bool isAllCardsItem)
        {
            // Utility to process ManaCost string
            static string ProcessManaCost(string manaCostRaw)
            {
                char[] separator = ['{', '}'];
                return string.Join(",", manaCostRaw.Split(separator, StringSplitOptions.RemoveEmptyEntries)).Trim(',');
            }

            // Utility to safely retrieve field values
            static T? GetFieldValue<T>(DbDataReader reader, string columnName)
            {
                if (reader[columnName] == DBNull.Value)
                {
                    return default;
                }

                object value = reader[columnName];

                // Explicit conversion for specific cases
                if (typeof(T) == typeof(int?) && value is long longValue)
                {
                    return (T)(object)(int?)longValue;
                }

                return (T)value;
            }

            // Utility to parse nullable decimal price fields
            static decimal? ParsePrice(string priceColumn, DbDataReader reader)
            {
                return decimal.TryParse(reader[priceColumn]?.ToString(), out decimal price) ? price : null;
            }

            // Utility to parse nullable DateTime fields
            static DateTime? ParseDate(string? dateRaw)
            {
                return DateTime.TryParse(dateRaw, out DateTime parsedDate) ? parsedDate : null;
            }

            try
            {
                // Instantiate appropriate type
                CardSet card = isCardItem ? new CardItem() : new CardSet();

                // Populate common properties
                card.Name = GetFieldValue<string>(reader, "Name") ?? string.Empty;
                card.Types = GetFieldValue<string>(reader, "Types") ?? string.Empty;
                card.ManaCost = ProcessManaCost(GetFieldValue<string>(reader, "ManaCost") ?? string.Empty);
                card.SuperTypes = GetFieldValue<string>(reader, "SuperTypes") ?? string.Empty;
                card.SubTypes = GetFieldValue<string>(reader, "SubTypes") ?? string.Empty;
                card.Type = GetFieldValue<string>(reader, "Type") ?? string.Empty;
                card.Keywords = GetFieldValue<string>(reader, "Keywords") ?? string.Empty;
                card.Text = GetFieldValue<string>(reader, "RulesText") ?? string.Empty;
                card.ManaValue = GetFieldValue<double?>(reader, "ManaValue") ?? 0;
                card.Side = GetFieldValue<string>(reader, "Side") ?? string.Empty;
                card.ManaCostImageBytes = GetFieldValue<byte[]>(reader, "ManaCostImage");
                card.ManaCostRaw = GetFieldValue<string>(reader, "ManaCost") ?? string.Empty;

                if (!isAllCardsItem)
                {
                    card.Language = GetFieldValue<string>(reader, "Language") ?? string.Empty;
                    card.Uuid = GetFieldValue<string>(reader, "Uuid") ?? string.Empty;
                    card.SetName = GetFieldValue<string>(reader, "SetName") ?? string.Empty;
                    card.Rarity = GetFieldValue<string>(reader, "Rarity") ?? string.Empty;
                    card.Finishes = GetFieldValue<string>(reader, "Finishes");
                    card.ReleaseDate = ParseDate(GetFieldValue<string>(reader, "ReleaseDate"));

                    // Populate raw data fields for parallel processing
                    card.SetIconBytes = GetFieldValue<byte[]>(reader, "KeyRuneImage");
                }

                // Populate CardItem-specific properties
                if (card is CardItem cardItem)
                {
                    cardItem.CardId = GetFieldValue<int?>(reader, "CardId");
                    cardItem.CardsOwned = GetFieldValue<int?>(reader, "CardsOwned") ?? 0;
                    cardItem.CardsForTrade = GetFieldValue<int?>(reader, "CardsForTrade") ?? 0;
                    cardItem.SelectedCondition = GetFieldValue<string>(reader, "Condition");
                    cardItem.SelectedFinish = GetFieldValue<string>(reader, "Finish");

                    cardItem.CardItemPrice = cardItem.SelectedFinish switch
                    {
                        "foil" => ParsePrice("FoilPrice", reader),
                        "etched" => ParsePrice("EtchedPrice", reader),
                        _ => ParsePrice("NormalPrice", reader)
                    };
                }
                else if (!isAllCardsItem)
                {
                    // Populate price fields for non-all-cards items
                    card.NormalPrice = ParsePrice("NormalPrice", reader);
                    card.FoilPrice = ParsePrice("FoilPrice", reader);
                    card.EtchedPrice = ParsePrice("EtchedPrice", reader);
                }

                return card;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error in CreateCardFromReader: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"Error in CreateCardFromReader: {ex.Message}");
                throw;
            }
        }
        private async Task LoadColorIcons(List<CardSet> cardList, string query)
        {
            try
            {
                cardList.Clear();

                List<CardSet> tempCardList = [];
                using SQLiteCommand command = new(query, DBAccess.connection);
                using DbDataReader reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    CardSet card = CreateColorIcon(reader);
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
                CardSet card = new()
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
        public async Task LoadAllDecksAsync()
        {
            try
            {
                // SQL query to fetch all decks
                string query = "SELECT id, deckName, deckDescription, targetFormat FROM myDecks";

                using var command = new SQLiteCommand(query, DBAccess.connection); // Use your database connection
                using var reader = await command.ExecuteReaderAsync();

                // Clear existing decks to avoid duplicates
                allDecks.Clear();

                while (await reader.ReadAsync())
                {
                    // Map the database row to the Deck object
                    var deck = new Deck
                    {
                        DeckId = reader.GetInt32(0),
                        DeckName = reader.IsDBNull(1) ? null : reader.GetString(1),
                        Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                        TargetFormat = reader.IsDBNull(3) ? null : reader.GetString(3)
                    };

                    // Add the deck to the allDecks list
                    allDecks.Add(deck);
                }

                // Bind the list to the ListView
                MyDecksListView.ItemsSource = null; // Reset the source to force update
                MyDecksListView.ItemsSource = allDecks;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading decks: {ex.Message}");
                MessageBox.Show($"Error loading decks: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async Task PopulateAllFormatsListAsync()
        {
            try
            {
                // Query to fetch column names, excluding 'uuid'
                string query = @"PRAGMA table_info(cardLegalities);";

                using SQLiteCommand command = new(query, DBAccess.connection);
                using var reader = (SQLiteDataReader)await command.ExecuteReaderAsync();
                var columnNames = new List<string>();

                while (await reader.ReadAsync())
                {
                    string columnName = reader["name"]?.ToString() ?? string.Empty;

                    // Exclude 'uuid' column
                    if (!string.Equals(columnName, "uuid", StringComparison.OrdinalIgnoreCase))
                    {
                        columnNames.Add(columnName);
                    }
                }

                // Assign to allFormats as an array and change first letter to capital
                allFormats = [.. columnNames];
                allFormats = allFormats.Select(s => char.ToUpper(s[0]) + s.Substring(1)).ToList();
                allFormats.Insert(0, "Casual/kitchen table");

                // Update ComboBox ItemsSource on the UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    NewDeckFormatComboBox.ItemsSource = allFormats;
                    ExistingDeckFormatComboBox.ItemsSource = allFormats;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error populating formats list: {ex.Message}");
            }
        }
        public Task PopulateFilterUiElements()
        {
            try
            {
                // Clear existing filter context lists
                filterContext.AllSuperTypes.Clear();
                filterContext.AllTypes.Clear();
                filterContext.AllSubTypes.Clear();
                filterContext.AllColors.Clear();
                filterContext.AllKeywords.Clear();
                filterContext.AllFinishes.Clear();
                filterContext.AllLanguages.Clear();
                filterContext.AllConditions.Clear();
                filterContext.AllRarities.Clear();

                // Setup common lists
                filterContext.AllColors.AddRange(["W", "U", "B", "R", "G", "C", "X"]);
                List<string> allOrNoneColorsOption = ["Cards with any of these colors", "Cards with all of these colors", "Cards with none of these colors"];
                List<int> manaValueOptions = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 1000000];
                List<string> manaValueCompareOptions = ["less than", "less than/eq", "greater than", "greater than/eq", "equal to"];

                // Split strings by commas and clean data
                char[] separatorArray = [','];

                // Define reusable helper function for cleaning lists
                IEnumerable<string> CleanAndFilter(IEnumerable<string?> input, HashSet<string>? removeItems = null)
                {
                    return input
                        .Where(item => !string.IsNullOrEmpty(item))
                        .SelectMany(item => item!.Split(separatorArray, StringSplitOptions.RemoveEmptyEntries))
                        .Select(item => item.Trim())
                        .Where(item => removeItems == null || !removeItems.Contains(item))
                        .Distinct()
                        .OrderBy(item => item);
                }

                // Set up unwanted types and subtypes
                HashSet<string> typesToRemove = ["Eaturecray", "Summon", "Scariest", "You'll", "Ever", "See", "Jaguar", "Dragon", "Knights", "Legend", "instant", "Cards"];
                HashSet<string> subTypesToRemove = ["(creature", "and/or", "type)|Judge", "The"];

                // Populate the filtered data into context
                filterContext.AllSuperTypes.AddRange(CleanAndFilter(allCards.Select(card => card.SuperTypes)));
                filterContext.AllTypes.AddRange(CleanAndFilter(allCards.Select(card => card.Types), typesToRemove));
                filterContext.AllSubTypes.AddRange(CleanAndFilter(allCards.Select(card => card.SubTypes), subTypesToRemove));
                filterContext.AllKeywords.AddRange(CleanAndFilter(allCards.Select(card => card.Keywords)));
                filterContext.AllFinishes.AddRange(CleanAndFilter(allCards.Select(card => card.Finishes)));
                filterContext.AllRarities.AddRange(CleanAndFilter(allCards.Select(card => card.Rarity)));
                filterContext.AllLanguages.AddRange(CleanAndFilter(myCards.Select(card => card.Language)));
                filterContext.AllConditions.AddRange(CleanAndFilter(myCards.OfType<CardItem>().Select(card => card.SelectedCondition)));

                Dispatcher.Invoke(() =>
                {
                    // Update DataGrid ComboBoxes
                    UpdateComboBoxSource(AllCardsDataGrid, "AllCardsName", allCards.Select(card => card.Name).Distinct().ToList());
                    UpdateComboBoxSource(AllCardsDataGrid, "AllCardsSet", allCards.Select(card => card.SetName).Distinct().ToList());
                    UpdateComboBoxSource(MyCollectionDataGrid, "MyCollectionName", allCards.Select(card => card.Name).Distinct().ToList());
                    UpdateComboBoxSource(MyCollectionDataGrid, "MyCollectionSet", allCards.Select(card => card.SetName).Distinct().ToList());
                    UpdateComboBoxSource(AllCardsForDecksDataGrid, "AllCardsForDecksName", allCardsForDecks.Select(card => card.Name).Distinct().ToList());

                    // Set Filter Options
                    FilterRulesTextTextBox.Text = filterContext.RulesTextDefaultText;
                    FilterColorsListBox.ItemsSource = filterContext.AllColors;
                    AllOrNoneComboBox.ItemsSource = allOrNoneColorsOption;
                    AllOrNoneComboBox.SelectedIndex = 0;
                    ManaValueComboBox.ItemsSource = manaValueOptions;
                    ManaValueComboBox.SelectedIndex = -1;
                    ManaValueOperatorComboBox.ItemsSource = manaValueCompareOptions;
                    ManaValueOperatorComboBox.SelectedIndex = -1;

                    // Set default text for other comboboxes
                    SetDefaultTextInComboBox(SuperTypesComboBox, "FilterSuperTypesTextBox", filterContext.SuperTypesDefaultText);
                    SetDefaultTextInComboBox(TypesComboBox, "FilterTypesTextBox", filterContext.TypesDefaultText);
                    SetDefaultTextInComboBox(SubTypesComboBox, "FilterSubTypesTextBox", filterContext.SubTypesDefaultText);
                    SetDefaultTextInComboBox(KeywordsComboBox, "FilterKeywordsTextBox", filterContext.KeywordsDefaultText);
                    SetDefaultTextInComboBox(FinishesComboBox, "FilterFinishesTextBox", filterContext.FinishesDefaultText);
                    SetDefaultTextInComboBox(RarityComboBox, "FilterRarityTextBox", filterContext.RarityDefaultText);
                    SetDefaultTextInComboBox(LanguagesComboBox, "FilterLanguagesTextBox", filterContext.LanguagesDefaultText);
                    SetDefaultTextInComboBox(ConditionsComboBox, "FilterConditionsTextBox", filterContext.ConditionsDefaultText);

                    PriceRetailerUiUpdates();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while filling comboboxes: {ex.Message}");
                MessageBox.Show($"Error while filling comboboxes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return Task.CompletedTask;
        }
        public static List<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            List<T> children = [];
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null)
                    {
                        if (child is T t)
                        {
                            children.Add(t);
                        }

                        // Recursive call only if child is not null
                        children.AddRange(FindVisualChildren<T>(child));
                    }
                }
            }

            return children;
        }
        private static void UpdateComboBoxSource(DataGrid dataGrid, string tag, List<string?> dataSource)
        {
            List<ComboBox> headerComboBoxes = FindVisualChildren<ComboBox>(dataGrid);
            foreach (ComboBox comboBox in headerComboBoxes)
            {
                if (comboBox.Tag?.ToString() == tag)
                {
                    comboBox.ItemsSource = dataSource.OrderBy(name => name).ToList();
                }
            }
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
            void ResetOtherComboBoxSelections(string currentCategory)
            {
                var categoryComboBoxMappings = new Dictionary<string, DataGrid>
            {
                { "AllCards", AllCardsDataGrid },
                { "MyCollection", MyCollectionDataGrid },
                { "AllCardsForDecks", AllCardsForDecksDataGrid }
            };

                foreach (var category in categoryComboBoxMappings)
                {
                    if (category.Key != currentCategory)
                    {
                        var headerComboBoxes = FindVisualChildren<ComboBox>(category.Value);
                        foreach (ComboBox headerComboBox in headerComboBoxes)
                        {
                            headerComboBox.SelectedIndex = -1;
                        }
                    }
                }
            }

            if (sender is ComboBox comboBox)
            {
                // Map ComboBox names to filter categories
                var comboBoxCategoryMap = new Dictionary<string, string>
                {
                    { "AllCardsNameComboBox", "AllCards" },
                    { "AllCardsSetComboBox", "AllCards" },
                    { "MyCollectionNameComboBox", "MyCollection" },
                    { "MyCollectionSetComboBox", "MyCollection" },
                    { "AllCardsForDecksNameComboBox", "AllCardsForDecks" }
                };

                // Identify category and reset other ComboBox selections
                if (comboBoxCategoryMap.TryGetValue(comboBox.Name, out string? category))
                {
                    filterManager.WhichDropdown = category;

                    // Reset selections in unrelated categories
                    ResetOtherComboBoxSelections(category);
                }
            }

            // Apply filters and update data sources
            AllCardsDataGrid.ItemsSource = filterManager.ApplyFilter(allCards, "allCards");
            MyCollectionDataGrid.ItemsSource = filterManager.ApplyFilter(myCards, "myCards");
            AllCardsForDecksDataGrid.ItemsSource = filterManager.ApplyFilter(allCardsForDecks, "allCardsForDecks");
        }
        private void ComboBox_DropDownOpened(object sender, EventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                try
                {
                    (string defaultText, string filterTextBoxName, string listBoxName) = GetComboBoxConfig(comboBox.Name);

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
                    (IEnumerable<string> itemsSource, HashSet<string> selectedItems) = GetDataSetAndSelection(listBoxName);
                    listBox.ItemsSource = itemsSource;

                    listBox.Dispatcher.Invoke(() =>
                    {
                        foreach (string item in itemsSource)
                        {
                            if (listBox.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem listBoxItem)
                            {
                                CheckBox? checkBox = FindVisualChild<CheckBox>(listBoxItem);
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
                case "FilterFinishesListBox":
                    itemsSource = filterContext.AllFinishes;
                    selectedItemsSet = filterContext.SelectedFinishes;
                    break;
                case "FilterRarityListBox":
                    itemsSource = filterContext.AllRarities;
                    selectedItemsSet = filterContext.SelectedRarity;
                    break;
                case "FilterLanguagesListBox":
                    itemsSource = filterContext.AllLanguages;
                    selectedItemsSet = filterContext.SelectedLanguages;
                    break;
                case "FilterConditionsListBox":
                    itemsSource = filterContext.AllConditions;
                    selectedItemsSet = filterContext.SelectedConditions;
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
                    while (parent is not null and not ComboBox)
                    {
                        parent = VisualTreeHelper.GetParent(parent);
                    }

                    // Explicitly check for null before casting
                    if (parent is ComboBox comboBox)
                    {
                        // Get configuration for this specific ComboBox
                        (string defaultText, string _, string listBoxName) = GetComboBoxConfig(comboBox.Name);

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
                (IEnumerable<string> dataSet, HashSet<string> selectedItems) = GetDataSetAndSelection(listBox.Name);

                List<string> filteredItems = !string.IsNullOrWhiteSpace(filterText)
                    ? dataSet.Where(type => type.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0).ToList()
                    : [.. dataSet.Distinct().OrderBy(type => type)];

                listBox.ItemsSource = filteredItems;

                listBox.Dispatcher.Invoke(() =>
                {
                    foreach (string item in filteredItems)
                    {
                        if (listBox.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem listBoxItem) // Check if listBoxItem is not null
                        {
                            CheckBox? checkBox = FindVisualChild<CheckBox>(listBoxItem);
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
                "FinishesComboBox" => (filterContext.FinishesDefaultText, "FilterFinishesTextBox", "FilterFinishesListBox"),
                "RarityComboBox" => (filterContext.RarityDefaultText, "FilterRarityTextBox", "FilterRarityListBox"),
                "LanguagesComboBox" => (filterContext.LanguagesDefaultText, "FilterLanguagesTextBox", "FilterLanguagesListBox"),
                "ConditionsComboBox" => (filterContext.ConditionsDefaultText, "FilterConditionsTextBox", "FilterConditionsListBox"),
                _ => throw new InvalidOperationException($"Configuration not found for ComboBox: {comboBoxName}")
            };
        }
        private void AndOrCheckBox_Toggled(object sender, RoutedEventArgs e)
        {
            // Unsubscribe from Checked/Unchecked events to avoid recursive triggering
            CheckBoxCardsForTrade.Checked -= AndOrCheckBox_Toggled;
            CheckBoxCardsForTrade.Unchecked -= AndOrCheckBox_Toggled;
            CheckBoxCardsNotForTrade.Checked -= AndOrCheckBox_Toggled;
            CheckBoxCardsNotForTrade.Unchecked -= AndOrCheckBox_Toggled;

            try
            {
                // If 'CheckBoxCardsForTrade' is toggled
                if (sender == CheckBoxCardsForTrade)
                {
                    // If 'CheckBoxCardsNotForTrade' is checked, uncheck it
                    if (CheckBoxCardsNotForTrade.IsChecked == true)
                    {
                        CheckBoxCardsNotForTrade.IsChecked = false;
                    }
                }
                // If 'CheckBoxCardsNotForTrade' is toggled
                else if (sender == CheckBoxCardsNotForTrade)
                {
                    // If 'CheckBoxCardsForTrade' is checked, uncheck it
                    if (CheckBoxCardsForTrade.IsChecked == true)
                    {
                        CheckBoxCardsForTrade.IsChecked = false;
                    }
                }

                // Apply filter and update label after toggling the checkbox
                ApplyFilterSelection();
            }
            finally
            {
                // Re-subscribe to Checked/Unchecked events
                CheckBoxCardsForTrade.Checked += AndOrCheckBox_Toggled;
                CheckBoxCardsForTrade.Unchecked += AndOrCheckBox_Toggled;
                CheckBoxCardsNotForTrade.Checked += AndOrCheckBox_Toggled;
                CheckBoxCardsNotForTrade.Unchecked += AndOrCheckBox_Toggled;
            }
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
                        "FilterFinishesTextBox" => filterContext.FinishesDefaultText,
                        "FilterRarityTextBox" => filterContext.RarityDefaultText,
                        "FilterRulesTextTextBox" => filterContext.RulesTextDefaultText,
                        "FilterLanguagesTextBox" => filterContext.LanguagesDefaultText,
                        "FilterConditionsTextBox" => filterContext.ConditionsDefaultText,
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
                        "FilterFinishesTextBox" => filterContext.FinishesDefaultText,
                        "FilterRarityTextBox" => filterContext.RarityDefaultText,
                        "FilterRulesTextTextBox" => filterContext.RulesTextDefaultText,
                        "FilterLanguagesTextBox" => filterContext.LanguagesDefaultText,
                        "FilterConditionsTextBox" => filterContext.ConditionsDefaultText,
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
                if (sender is not DependencyObject dependencyObject)
                {
                    return; // Exit if casting failed
                }

                CheckBox? checkBox = FindVisualChild<CheckBox>(dependencyObject);

                if (checkBox != null && checkBox.Content is ContentPresenter contentPresenter)
                {
                    string? label = contentPresenter.Content as string;
                    if (!string.IsNullOrEmpty(label))
                    {
                        HashSet<string>? targetCollection = checkBox.Tag switch
                        {
                            "Type" => filterContext.SelectedTypes,
                            "SuperType" => filterContext.SelectedSuperTypes,
                            "SubType" => filterContext.SelectedSubTypes,
                            "Keywords" => filterContext.SelectedKeywords,
                            "Finishes" => filterContext.SelectedFinishes,
                            "Rarity" => filterContext.SelectedRarity,
                            "Colors" => filterContext.SelectedColors,
                            "Languages" => filterContext.SelectedLanguages,
                            "Conditions" => filterContext.SelectedConditions,
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

                CheckBox? checkBox = FindVisualChild<CheckBox>(dependencyObject);
                if (checkBox != null && checkBox.Content is ContentPresenter contentPresenter)
                {
                    string? label = contentPresenter.Content as string;
                    if (!string.IsNullOrEmpty(label))
                    {
                        HashSet<string>? targetCollection = checkBox.Tag switch
                        {
                            "Type" => filterContext.SelectedTypes,
                            "SuperType" => filterContext.SelectedSuperTypes,
                            "SubType" => filterContext.SelectedSubTypes,
                            "Keywords" => filterContext.SelectedKeywords,
                            "Finishes" => filterContext.SelectedFinishes,
                            "Rarity" => filterContext.SelectedRarity,
                            "Colors" => filterContext.SelectedColors,
                            "Languages" => filterContext.SelectedLanguages,
                            "Conditions" => filterContext.SelectedConditions,
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
                    case "Finishes":
                        checkBox.IsChecked = filterContext.SelectedFinishes.Contains(dataContext);
                        break;
                    case "Rarity":
                        checkBox.IsChecked = filterContext.SelectedRarity.Contains(dataContext);
                        break;
                    case "Colors":
                        checkBox.IsChecked = filterContext.SelectedColors.Contains(dataContext);
                        break;
                    case "Languages":
                        checkBox.IsChecked = filterContext.SelectedLanguages.Contains(dataContext);
                        break;
                    case "Conditions":
                        checkBox.IsChecked = filterContext.SelectedConditions.Contains(dataContext);
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
            IEnumerable<CardSet> filteredAllCards = filterManager.ApplyFilter(allCards, "allCards");
            IEnumerable<CardSet> filteredMyCards = filterManager.ApplyFilter(myCards, "myCards");
            IEnumerable<CardSet> filteredAllCardsForDecks = filterManager.ApplyFilter(allCardsForDecks, "allCardsForDecks");

            // Save and restore sort descriptions for both DataGrids
            FilterManager.SaveAndRestoreSort(AllCardsDataGrid, () =>
            {
                AllCardsDataGrid.ItemsSource = filteredAllCards;
            });
            FilterManager.SaveAndRestoreSort(MyCollectionDataGrid, () =>
            {
                MyCollectionDataGrid.ItemsSource = filteredMyCards;
            });
            FilterManager.SaveAndRestoreSort(AllCardsForDecksDataGrid, () =>
            {
                AllCardsForDecksDataGrid.ItemsSource = filteredAllCardsForDecks;
            });
        }

        // Reset filter elements
        public void ClearFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            // Reset filter TextBoxes for each ComboBox
            ResetFilterTextBox(SuperTypesComboBox, "FilterSuperTypesTextBox", filterContext.SuperTypesDefaultText);
            ResetFilterTextBox(TypesComboBox, "FilterTypesTextBox", filterContext.TypesDefaultText);
            ResetFilterTextBox(SubTypesComboBox, "FilterSubTypesTextBox", filterContext.SubTypesDefaultText);
            ResetFilterTextBox(KeywordsComboBox, "FilterKeywordsTextBox", filterContext.KeywordsDefaultText);
            ResetFilterTextBox(FinishesComboBox, "FilterFinishesTextBox", filterContext.FinishesDefaultText);
            ResetFilterTextBox(RarityComboBox, "FilterRarityTextBox", filterContext.RarityDefaultText);
            ResetFilterTextBox(LanguagesComboBox, "FilterLanguagesTextBox", filterContext.LanguagesDefaultText);
            ResetFilterTextBox(ConditionsComboBox, "FilterConditionsTextBox", filterContext.ConditionsDefaultText);

            // Clear non-custom comboboxes
            AllOrNoneComboBox.SelectedIndex = 0;
            ManaValueComboBox.SelectedIndex = -1;
            ManaValueOperatorComboBox.SelectedIndex = -1;

            // Find and clear all ComboBoxes in the DataGrid header
            var headerComboBoxesAllCards = FindVisualChildren<ComboBox>(AllCardsDataGrid);
            foreach (ComboBox headerComboBox in headerComboBoxesAllCards)
            {
                headerComboBox.SelectedIndex = -1;
            }
            var headerComboBoxesMyCollection = FindVisualChildren<ComboBox>(MyCollectionDataGrid);
            foreach (ComboBox headerComboBox in headerComboBoxesMyCollection)
            {
                headerComboBox.SelectedIndex = -1;
            }
            var headerComboBoxesAllCardsForDecks = FindVisualChildren<ComboBox>(AllCardsForDecksDataGrid);
            foreach (ComboBox headerComboBox in headerComboBoxesAllCardsForDecks)
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
            filterContext.SelectedFinishes.Clear();
            filterContext.SelectedRarity.Clear();
            filterContext.SelectedColors.Clear();
            filterContext.SelectedLanguages.Clear();
            filterContext.SelectedConditions.Clear();

            // Clear rulestext textbox
            FilterRulesTextTextBox.Text = filterContext.RulesTextDefaultText;
            FilterRulesTextTextBox.Foreground = new SolidColorBrush(Colors.Gray);

            // Uncheck CheckBoxes if necessary
            TypesAndOrCheckBox.IsChecked = false;
            SuperTypesAndOrCheckBox.IsChecked = false;
            SubTypesAndOrCheckBox.IsChecked = false;
            KeywordsAndOrCheckBox.IsChecked = false;
            FinishesAndOrCheckBox.IsChecked = false;
            CheckBoxCardsForTrade.IsChecked = false;
            CheckBoxCardsNotForTrade.IsChecked = false;

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
            foreach (object? item in listBox.Items)
            {
                if (listBox.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem container)
                {
                    CheckBox? checkBox = FindVisualChild<CheckBox>(container);
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

            // Show image from a highlighted row in a datagrid
            if (sender is DataGrid dataGrid && dataGrid.SelectedItem is CardSet selectedCard)
            {
                if (selectedCard.Uuid != null)
                {
                    await ShowCardImage.ShowImage(selectedCard.Uuid);
                }
                else if (selectedCard.Name != null)
                {
                    await ShowCardImage.ShowImage(null, selectedCard.Name);
                }
            }

            // Show image from import wizards (choose between versions)
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
        private void AddCardsToListView(object sender, MouseButtonEventArgs e)
        {
            // Check if the sender is a DataGrid and has a selected item
            if (sender is DataGrid grid && grid.SelectedItem != null)
            {
                // If the source is AllCardsDataGrid, add to CardItemsToAdd. Else, add to CardItemsToEdit
                if (grid.SelectedItem is CardSet cardSetCard && grid.Name == "AllCardsDataGrid")
                {
                    AddToCollectionManager.AddOrEditCardHandler(cardSetCard, addToCollectionManager.CardItemsToAdd);
                    AddToCollectionManager.ShowCardsToAddListView();
                }
                else if (grid.SelectedItem is CardItem cardItemCard && grid.Name == "MyCollectionDataGrid")
                {
                    AddToCollectionManager.AddOrEditCardHandler(cardItemCard, addToCollectionManager.CardItemsToEdit);
                    AddToCollectionManager.ShowCardsToEditListView();
                }
                grid.UnselectAll();
            }
        }
        private void ButtonAddCardsToMyCollection_Click(object sender, RoutedEventArgs e)
        {
            AddToCollectionManager.AddCardsToListView(AllCardsDataGrid, AddToCollectionManager.ShowCardsToAddListView, addToCollectionManager.CardItemsToAdd);
        }
        private void ButtonEditCardsInCollection_Click(object sender, RoutedEventArgs e)
        {
            AddToCollectionManager.AddCardsToListView(MyCollectionDataGrid, AddToCollectionManager.ShowCardsToEditListView, addToCollectionManager.CardItemsToEdit);
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

        // Right-click actions 
        private void ButtonAddCardsToMyCollectionWithDefaultValues_Click(object sender, RoutedEventArgs e)
        {
            List<CardSet> selectedCards = AllCardsDataGrid.SelectedItems.Cast<CardSet>().ToList();
            if (selectedCards.Count > 0)
            {
                AddToCollectionManager.SubmitNewCardsToCollectionWithDefaultValues(selectedCards);
                AllCardsDataGrid.UnselectAll();
            }
        }
        private void ButtonDeleteCardsFromCollection_Click(object sender, RoutedEventArgs e)
        {
            List<CardItem> selectedCards = MyCollectionDataGrid.SelectedItems.Cast<CardItem>().ToList();
            if (selectedCards.Count > 0)
            {
                addToCollectionManager.DeleteCardsFromCollection(selectedCards);
            }
        }
        private void ButtonSetCardsForTrade_Click(object sender, RoutedEventArgs e)
        {
            List<CardItem> selectedCards = MyCollectionDataGrid.SelectedItems.Cast<CardItem>().ToList();
            if (selectedCards.Count > 0)
            {
                addToCollectionManager.SetCardsForTrade(selectedCards, true);
                MyCollectionDataGrid.UnselectAll();
            }
        }
        private void ButtonSetNoneForTrade_Click(object sender, RoutedEventArgs e)
        {
            List<CardItem> selectedCards = MyCollectionDataGrid.SelectedItems.Cast<CardItem>().ToList();
            if (selectedCards.Count > 0)
            {
                addToCollectionManager.SetCardsForTrade(selectedCards, false);
                MyCollectionDataGrid.UnselectAll();
            }
        }

        #endregion

        #region Deck Management
        // Adding new deck
        private void AddDeckButton_Click(object sender, RoutedEventArgs e)
        {
            AddDeckButton.Visibility = Visibility.Collapsed;
            GridAddNewDeckForm.Visibility = Visibility.Visible;
        }
        private async void SubmitNewDeckButton_Click(object sender, RoutedEventArgs e)
        {
            await DeckManager.SubmitNewDeck();
        }
        private void CancelNewDeckButton_Click(object sender, RoutedEventArgs e)
        {
            AddDeckNameTextBox.Text = string.Empty;
            AddDeckDescriptionTextBox.Text = string.Empty;
            NewDeckFormatComboBox.SelectedIndex = -1;
            AddDeckButton.Visibility = Visibility.Visible;
            GridAddNewDeckForm.Visibility = Visibility.Collapsed;
        }
        private async void DeleteDeckButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is Deck deckFromButton)
            {
                // Show a confirmation dialog
                MessageBoxResult result = MessageBox.Show(
                    $"Are you sure you want to delete the deck '{deckFromButton.DeckName}'?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    await DeckManager.DeleteDeck(deckFromButton.DeckId);

                    // Reload deck list
                    await DBAccess.OpenConnectionAsync();
                    await LoadAllDecksAsync();
                    DBAccess.CloseConnection();
                }
            }
        }

        // Open deck editor window
        private async void OpenAndEditDeck(object sender, RoutedEventArgs e)
        {
            Deck? selectedDeck = null;

            // If the user double-clicks on a deck
            if (sender is ListView grid && grid.SelectedItem is Deck deckFromListView)
            {
                selectedDeck = deckFromListView;
                grid.UnselectAll();
            }

            // If the user clicks the edit button for a deck
            else if (sender is Button button && button.DataContext is Deck deckFromButton)
            {
                selectedDeck = deckFromButton;
            }

            if (selectedDeck != null)
            {
                await DeckManager.LoadDeck(selectedDeck.DeckId);
            }
        }
        private async void BackToDeckOverviewButton_Click(object sender, RoutedEventArgs e)
        {
            // Cancel all edits if there are some
            CancelDeckEdit(DeckNameTextBox, EditDeckNameButton, SaveDeckNameButton, CancelDeckNameEditButton, CurrentDeck.DeckName);
            CancelDeckEdit(DeckDescriptionTextBox, EditDeckDescriptionButton, SaveDeckDescriptionButton, CancelDeckDescriptionEditButton, CurrentDeck.Description);
            CancelDeckEdit(DeckFormatTextBox, EditDeckFormatButton, SaveDeckFormatButton, CancelDeckFormatEditButton, $"Target format: {CurrentDeck.TargetFormat}");

            // Reload deck list
            await DBAccess.OpenConnectionAsync();
            await LoadAllDecksAsync();
            DBAccess.CloseConnection();

            // Reset UI elements
            HeadlineDecks.Content = "Deck Management";
            GridDeckEditor.Visibility = Visibility.Collapsed;
            GridFiltering.Visibility = Visibility.Collapsed;
            GridCardImages.Visibility = Visibility.Collapsed;
            GridTopMenu.IsEnabled = true;
            GridDecksOverview.Visibility = Visibility.Visible;
        }

        // Deck Editor Methods

        // Cancel edits by clicking outside edited element
        private void Window_PreviewMouseDown_CancelEdits(object sender, MouseButtonEventArgs e)
        {
            bool anEditTextBoxHasFocus = false;
            bool weAreClickingDeckNameElements = false;
            bool weAreEditingDeckDescription = false;
            bool weAreEditingDeckFormat = false;

            // Check if we are editing something
            if (DeckNameTextBox.IsFocused || DeckDescriptionTextBox.IsFocused || DeckFormatTextBox.Visibility == Visibility.Collapsed) { anEditTextBoxHasFocus = true; }

            // Determine what we are editing
            if (DeckNameTextBox.IsMouseOver || SaveDeckNameButton.IsMouseOver) { weAreClickingDeckNameElements = true; }
            if (DeckDescriptionTextBox.IsMouseOver || SaveDeckDescriptionButton.IsMouseOver) { weAreEditingDeckDescription = true; }
            if (ExistingDeckFormatComboBox.IsMouseOver || SaveDeckFormatButton.IsMouseOver) { weAreEditingDeckFormat = true; }

            if (anEditTextBoxHasFocus)
            {
                if (!weAreClickingDeckNameElements && !ExistingDeckFormatComboBox.IsMouseOver)
                {
                    CancelDeckEdit(DeckNameTextBox, EditDeckNameButton, SaveDeckNameButton, CancelDeckNameEditButton, CurrentDeck.DeckName);
                }
                if (!weAreEditingDeckDescription && !ExistingDeckFormatComboBox.IsMouseOver)
                {
                    CancelDeckEdit(DeckDescriptionTextBox, EditDeckDescriptionButton, SaveDeckDescriptionButton, CancelDeckDescriptionEditButton, CurrentDeck.Description);
                }
                if (!weAreEditingDeckFormat)
                {
                    CancelDeckEdit(DeckFormatTextBox, EditDeckFormatButton, SaveDeckFormatButton, CancelDeckFormatEditButton, $"Target format: {CurrentDeck.TargetFormat}");
                }
            }

        }

        // Turn on element to edit
        private void EditDeckInfoButton_Click(object sender, RoutedEventArgs e)
        {
            void HandleEditing(TextBox currentTextBox, Button currentEditButton, Button currentSaveButton, Button currentCancelButton)
            {
                textBoxToEdit = currentTextBox;
                editButton = currentEditButton;
                saveButton = currentSaveButton;
                cancelButton = currentCancelButton;
            }

            if (sender is TextBox textBox)
            {
                if (textBox.Name == "DeckNameTextBox")
                {
                    HandleEditing(DeckNameTextBox, EditDeckNameButton, SaveDeckNameButton, CancelDeckNameEditButton);
                }
                else if (textBox.Name == "DeckDescriptionTextBox")
                {
                    HandleEditing(DeckDescriptionTextBox, EditDeckDescriptionButton, SaveDeckDescriptionButton, CancelDeckDescriptionEditButton);
                }
                else if (textBox.Name == "DeckFormatTextBox")
                {
                    HandleEditing(DeckFormatTextBox, EditDeckFormatButton, SaveDeckFormatButton, CancelDeckFormatEditButton);
                }
            }
            else if (sender is Button button)
            {
                if (button.Name == "EditDeckNameButton")
                {
                    HandleEditing(DeckNameTextBox, EditDeckNameButton, SaveDeckNameButton, CancelDeckNameEditButton);
                }
                else if (button.Name == "EditDeckDescriptionButton")
                {
                    HandleEditing(DeckDescriptionTextBox, EditDeckDescriptionButton, SaveDeckDescriptionButton, CancelDeckDescriptionEditButton);
                }
                else if (button.Name == "EditDeckFormatButton")
                {
                    HandleEditing(DeckFormatTextBox, EditDeckFormatButton, SaveDeckFormatButton, CancelDeckFormatEditButton);
                }
            }

            // Enable editing for the selected text box
            if (textBoxToEdit.Name == "DeckFormatTextBox")
            {
                textBoxToEdit.Visibility = Visibility.Collapsed;
                ExistingDeckFormatComboBox.Visibility = Visibility.Visible;
            }
            else
            {
                textBoxToEdit.IsReadOnly = false;
                textBoxToEdit.Background = new SolidColorBrush(Colors.White);
                textBoxToEdit.Focus();
                textBoxToEdit.SelectAll();
            }

            // Adjust visibility of buttons
            editButton.Visibility = Visibility.Hidden;
            saveButton.Visibility = Visibility.Visible;
            cancelButton.Visibility = Visibility.Visible;
        }

        // Pick up icon events
        private async void SaveDeckInfoButton_Click(object sender, RoutedEventArgs e)
        {
            string? textToUpdate = string.Empty;

            if (sender is Button button)
            {
                saveButton = button;

                if (button.Name == "SaveDeckNameButton")
                {
                    textToUpdate = DeckNameTextBox.Text;
                    textBoxToEdit = DeckNameTextBox;
                    editButton = EditDeckNameButton;
                    cancelButton = CancelDeckNameEditButton;
                    columnToEdit = "deckName";
                }
                else if (button.Name == "SaveDeckDescriptionButton")
                {
                    textToUpdate = DeckDescriptionTextBox.Text;
                    textBoxToEdit = DeckDescriptionTextBox;
                    editButton = EditDeckDescriptionButton;
                    cancelButton = CancelDeckDescriptionEditButton;
                    columnToEdit = "deckDescription";
                }
                else if (button.Name == "SaveDeckFormatButton")
                {
                    textToUpdate = ExistingDeckFormatComboBox.SelectedItem.ToString();
                    textBoxToEdit = DeckFormatTextBox;
                    editButton = EditDeckFormatButton;
                    cancelButton = CancelDeckFormatEditButton;
                    columnToEdit = "targetFormat";
                }
            }

            if (await DeckManager.UpdateDeckInfo(columnToEdit, textToUpdate?.Trim() ?? String.Empty))
            {
                CurrentDeck.DeckName = DeckNameTextBox.Text;
                CurrentDeck.Description = DeckDescriptionTextBox.Text;
                CurrentDeck.TargetFormat = ExistingDeckFormatComboBox.SelectedItem.ToString();
                HideDeckEditTextBox(textBoxToEdit, editButton, saveButton, cancelButton);
            }

        }
        private void CancelDeckEditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.Name == "CancelDeckNameEditButton")
                {
                    CancelDeckEdit(DeckNameTextBox, EditDeckNameButton, SaveDeckNameButton, button, CurrentDeck.DeckName);
                }
                else if (button.Name == "CancelDeckDescriptionEditButton")
                {
                    CancelDeckEdit(DeckDescriptionTextBox, EditDeckDescriptionButton, SaveDeckDescriptionButton, button, CurrentDeck.Description);
                }
                else if (button.Name == "CancelDeckFormatEditButton")
                {
                    CancelDeckEdit(DeckFormatTextBox, EditDeckFormatButton, SaveDeckFormatButton, button, $"Target format: {CurrentDeck.TargetFormat}");
                }
            }
        }

        // When a textbox has focus, pick up keystrokes like "Enter" and "Escape"
        private async void DeckInfoTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBoxToEdit = textBox;

                if (textBox.Name == "DeckNameTextBox")
                {
                    editButton = EditDeckNameButton;
                    saveButton = SaveDeckNameButton;
                    cancelButton = CancelDeckNameEditButton;
                    columnToEdit = "deckName";
                }
                else if (textBox.Name == "DeckDescriptionTextBox")
                {
                    editButton = EditDeckDescriptionButton;
                    saveButton = SaveDeckDescriptionButton;
                    cancelButton = CancelDeckDescriptionEditButton;
                    columnToEdit = "deckDescription";
                }
            }

            // Save by pressing enter
            if (e.Key == Key.Enter)
            {
                if (await DeckManager.UpdateDeckInfo(columnToEdit, textBoxToEdit.Text?.Trim() ?? String.Empty))
                {
                    CurrentDeck.DeckName = DeckNameTextBox.Text;
                    CurrentDeck.Description = DeckDescriptionTextBox.Text;
                    HideDeckEditTextBox(textBoxToEdit, editButton, saveButton, cancelButton);
                }
            }

            // Cancel by pressing escape
            else if (e.Key == Key.Escape)
            {
                string? originalTextBoxValue = textBoxToEdit.Name == "DeckNameTextBox"
                    ? CurrentDeck.DeckName
                    : CurrentDeck.Description;

                CancelDeckEdit(textBoxToEdit, editButton, saveButton, cancelButton, originalTextBoxValue);
            }
        }

        // Shared methods
        private static void CancelDeckEdit(TextBox textBoxToEdit, Button editButton, Button saveButton, Button cancelButton, string? originalValue)
        {
            textBoxToEdit.Text = originalValue;
            HideDeckEditTextBox(textBoxToEdit, editButton, saveButton, cancelButton);
        }
        private static void HideDeckEditTextBox(TextBox textBox, Button editButton, Button saveButton, Button cancelButton)
        {

            // Reset the TextBox value to its original value
            if (textBox.Name == "DeckFormatTextBox")
            {
                CurrentInstance.ExistingDeckFormatComboBox.Visibility = Visibility.Collapsed;
                textBox.Visibility = Visibility.Visible;
                textBox.Text = $"Target format: {CurrentInstance.CurrentDeck.TargetFormat}";
            }

            textBox.IsReadOnly = true;
            textBox.Background = null;
            Keyboard.ClearFocus();
            editButton.Visibility = Visibility.Visible;
            saveButton.Visibility = Visibility.Hidden;
            cancelButton.Visibility = Visibility.Hidden;
        }

        #endregion


        #region UI elements for utilities
        private async void CreateBackupButton_Click(object sender, RoutedEventArgs e)
        {
            ResetUtilsMenu();
            await CreateCsvBackupAsync();
        }
        private void ImportCollectionButton_Click(object sender, RoutedEventArgs e)
        {
            Inspiredtinkering.Visibility = Visibility.Collapsed;
            UtilsInfoLabel.Content = string.Empty;
            GridImportWizard.Visibility = Visibility.Visible;
            GridImportStartScreen.Visibility = Visibility.Visible;
        }
        private async void UpdatePricesButton_Click(object sender, RoutedEventArgs e)
        {
            ResetUtilsMenu();
            await CardPriceUtilities.UpdatePricesAsync();
        }
        private async void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            ResetUtilsMenu();
            await UpdateDB.CheckForDbUpdatesAsync();
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
        private void ResetUtilsMenu()
        {
            GridImportWizard.Visibility = Visibility.Collapsed;
            Inspiredtinkering.Visibility = Visibility.Visible;
            UtilsInfoLabel.Content = string.Empty;
        }
        private async void RetailSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isStartup)
            {
                return;
            }

            await ShowStatusWindowAsync(true, "Reloading cards prices from selected retailer ... ");

            await Task.Delay(100);

            await DBAccess.OpenConnectionAsync();

            if (RetailSelector.SelectedItem is ComboBoxItem selectedItem)
            {
                // Determine the selected retailer based on the ComboBoxItem content
                string retailer = selectedItem.Content switch
                {
                    "Cardmarket" => "cardmarket",
                    "Card Kingdom" => "cardkingdom",
                    "Cardsphere" => "cardsphere",
                    "TCG Player" => "tcgplayer",
                    "Cardhoarder" => "cardhoarder",
                    _ => throw new NotImplementedException()
                };

                // Update the retailer in appsettings
                ConfigurationManager.UpdatePriceInfo(null, retailer);
                appsettingsRetailer = retailer;
            }

            // Update the db views to load prices from the selected retailer
            await DownloadAndPrepDB.CreateViews();

            Task loadAllCards = PopulateCardDataGridAsync(allCards, allCardsQuery, AllCardsDataGrid, false, false);
            Task loadMyCollection = PopulateCardDataGridAsync(myCards, myCollectionQuery, MyCollectionDataGrid, true, false);

            await Task.WhenAll(loadAllCards, loadMyCollection);

            CardPriceUtilities.UpdateDataGridHeaders(AllCardsDataGrid);
            CardPriceUtilities.UpdateDataGridHeaders(MyCollectionDataGrid);

            DBAccess.CloseConnection();

            await ShowStatusWindowAsync(false);
        }
        public void PriceRetailerUiUpdates()
        {
            string retailer = appsettingsRetailer switch
            {
                "cardmarket" => "Cardmarket",
                "cardkingdom" => "Card Kingdom",
                "cardsphere" => "Cardsphere",
                "tcgplayer" => "TCG Player",
                "cardhoarder" => "Cardhoarder",
                _ => throw new NotImplementedException()
            };

            // Find the ComboBoxItem with the matching content
            var itemToSelect = RetailSelector.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => item.Content.ToString() == retailer);

            // If we found the item, set it as the selected item
            if (itemToSelect != null)
            {
                RetailSelector.SelectedItem = itemToSelect;
            }
        }

        #region Import wizard

        // Import wizard different steps button methods
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
            FilterSummaryScrollViewer.Visibility = Visibility.Visible;
            LogoSmall.Visibility = Visibility.Visible;
            GridFiltering.Visibility = Visibility.Visible;
            GridSearchAndFilterAllCards.Visibility = Visibility.Visible;
            AddToCollectionManager.AdjustColumnWidths();
        }
        private void MenuMyCollection_Click(object sender, RoutedEventArgs e)
        {
            ResetGrids();
            MenuMyCollectionButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5cb9ca"));
            FilterSummaryScrollViewer.Visibility = Visibility.Visible;
            LogoSmall.Visibility = Visibility.Visible;
            GridFiltering.Visibility = Visibility.Visible;
            GridMyCollection.Visibility = Visibility.Visible;
            LanguagesComboBox.Visibility = Visibility.Visible;
            ConditionsComboBox.Visibility = Visibility.Visible;
            CheckBoxCardsForTrade.Visibility = Visibility.Visible;
            CheckBoxCardsNotForTrade.Visibility = Visibility.Visible;

            AddToCollectionManager.AdjustColumnWidths();
        }
        private void MenuDecks_Click(object sender, RoutedEventArgs e)
        {
            ResetGrids();
            MenuDecksButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5cb9ca"));
            GridDecks.Visibility = Visibility.Visible;
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
            MenuDecksButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDDDDDD"));
            MenuUtilsButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDDDDDD"));

            EditStatusTextBlock.Text = string.Empty;
            AddStatusTextBlock.Text = string.Empty;
            UtilsInfoLabel.Content = "";
            FilterSummaryScrollViewer.Visibility = Visibility.Collapsed;
            GridSearchAndFilterAllCards.Visibility = Visibility.Collapsed;
            GridMyCollection.Visibility = Visibility.Collapsed;
            GridDecks.Visibility = Visibility.Collapsed;
            GridUtilitiesSection.Visibility = Visibility.Collapsed;
            LanguagesComboBox.Visibility = Visibility.Collapsed;
            ConditionsComboBox.Visibility = Visibility.Collapsed;
            CheckBoxCardsForTrade.Visibility = Visibility.Collapsed;
            CheckBoxCardsNotForTrade.Visibility = Visibility.Collapsed;

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
        public static async Task ShowStatusWindowAsync(bool statusScreenIsVisible, string? statusLabelContent = null, bool progressBarVisible = false)
        {
            if (CurrentInstance != null)
            {
                await CurrentInstance.Dispatcher.InvokeAsync(() =>
                {
                    if (statusScreenIsVisible)
                    {
                        // Disable top menu buttons
                        CurrentInstance.GridTopMenu.IsEnabled = false;

                        // Show status section and hide others
                        CurrentInstance.GridContentSection.Visibility = Visibility.Collapsed;
                        CurrentInstance.GridSideMenu.Visibility = Visibility.Collapsed;
                        CurrentInstance.GridCardImages.Visibility = Visibility.Collapsed;
                        CurrentInstance.GridStatus.Visibility = Visibility.Visible;

                        if (progressBarVisible)
                        {
                            CurrentInstance.ProgressBar.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            CurrentInstance.ProgressBar.Visibility = Visibility.Collapsed;
                        }

                        CurrentInstance.StatusLabel.Content = statusLabelContent;
                    }
                    else
                    {
                        CurrentInstance.GridTopMenu.IsEnabled = true;
                        CurrentInstance.GridStatus.Visibility = Visibility.Collapsed;
                        CurrentInstance.GridContentSection.Visibility = Visibility.Visible;
                        CurrentInstance.GridSideMenu.Visibility = Visibility.Visible;
                        CurrentInstance.GridCardImages.Visibility = Visibility.Visible;
                    }
                });
                CurrentInstance.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            }
        }

    }
}
