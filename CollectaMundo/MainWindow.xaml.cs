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
        public readonly List<CardSet> cardsInDecks = [];
        private readonly List<CardSet> ColorIcons = [];

        public enum DataGridContext
        {
            AllCards,
            MyCollection,
            AllCardsForDecks,
            CardsInDecks
        }
        public enum OperatorType
        {
            OR = 0,
            AND = 1,
            NOT = 2,
            Unknown = -1
        }


        // The object which holds the filter selections
        public List<FilterSelections> filterSelections = [];
        public List<FilterDefaults> filterDefaults = [];


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
            ManaValueComboBox.SelectionChanged += DataGridHeaderComboBox_SelectionChanged;
            ManaValueOperatorComboBox.SelectionChanged += DataGridHeaderComboBox_SelectionChanged;

            // Run some tests
            //InitializeTestCards();
            // Inject filterSelections only for testing purposes
            //FilterDefaults testContext = new();

            //RunFilterTests();
        }

        #region Tests

        // Test objects
        //private readonly List<CardSet> testCards = [];
        //private void InitializeTestCards()
        //{
        //    testCards.AddRange(new List<CardSet>
        //    {
        //        new() { Name = "Black Lotus", Colors = "", ManaCost = "0" },
        //        new() { Name = "Sol Ring", Colors = "", ManaCost = "1" },
        //        new() { Name = "Lightning Bolt", Colors = "R", ManaCost = "R" },
        //        new() { Name = "Traben Inspector", Colors = "W", ManaCost = "W" },
        //        new() { Name = "Eldrazi Ravager", Colors = "", ManaCost = "5,C" },
        //        new() { Name = "Island", Colors = "", ManaCost = "" },
        //        new() { Name = "Dromoka's Command", Colors = "G, W", ManaCost = "G,W" },
        //        new() { Name = "Biomass Mutation", Colors = "G, U", ManaCost = "X,G/U,G/U" },
        //        new() { Name = "Suffer The Past", Colors = "B", ManaCost = "X,B" },
        //        new() { Name = "Kozilek's Command", Colors = "", ManaCost = "X,C,C" },
        //    });
        //}

        /// <summary>
        /// Runs all the tests for the FilterByColor method in the FilterManager class.
        /// </summary>
        //public void RunFilterTests()
        //{
        //    Debug.WriteLine("Starting Filter Tests...");

        //    // c og x test

        //    _ = new FilterDefaults();
        //    FilterManager testFilterManager = new FilterManager();

        //    // Test 1: Select single color / ANY
        //    RunTest(testFilterManager, ["R"], 0, "Test 1: Single color / ANY", 1);
        //    // Test 2: Select two colors / ANY
        //    RunTest(testFilterManager, ["W", "R"], 0, "Test 2: Two colors / ANY", 3);
        //    // Test 3: Select two colors / NONE
        //    RunTest(testFilterManager, ["W", "R"], 2, "Test 3: Two colors / NONE", 7);
        //    // Test 4: Select single color and X/C / ANY
        //    RunTest(testFilterManager, ["R", "C"], 0, "Test 4: Single color and X/C / ANY", 3);
        //    // Test 5: Select single color and X/C / NONE
        //    RunTest(testFilterManager, ["R", "C"], 2, "Test 5: Single color and X/C / NONE", 7);
        //    // Test 6: Select two colors / ALL
        //    RunTest(testFilterManager, ["G", "U"], 1, "Test 6: Two colors / ALL", 1);
        //    // Test 7: Select single color and X/C / ALL
        //    RunTest(testFilterManager, ["G", "X"], 1, "Test 7: Single color and X/C / ALL", 1);
        //    // Test 8: Select two colors and X/C / ALL
        //    RunTest(testFilterManager, ["G", "U", "X"], 1, "Test 8: Single color and X/C / ALL", 1);
        //    // Test 9: Select three colors and X/C / ALL
        //    RunTest(testFilterManager, ["G", "U", "B", "X"], 1, "Test 9: 3 colors and X/C / ALL", 0);
        //    // Test 10: Select Colorless / ANY
        //    RunTest(testFilterManager, ["Colorless"], 0, "Test 10: Colorless / ANY", 5);
        //    RunTest(testFilterManager, ["Colorless", "X"], 2, "Test 11: Colorless and X/ NONE", 3);
        //    RunTest(testFilterManager, ["Colorless", "C"], 1, "Test 12: Colorless and C/ ALL", 2);
        //    RunTest(testFilterManager, ["Colorless", "R"], 1, "Test 13: Colorless and R/ ALL", 0);
        //    RunTest(testFilterManager, ["Colorless", "C", "X"], 1, "Test 14: Colorless and both C and X/ ALL", 1);


        //    Debug.WriteLine("Filter Tests Completed.");
        //}

        /// <summary>
        /// Helper method to execute and log results for a single test case.
        /// </summary>
        //private void RunTest(FilterManager testFilterManager, HashSet<string> selectedColors, int filterMode, string testName, int expectedCount)
        //{
        //    //AllOrNoneComboBox.SelectedIndex = filterMode;

        //    // Directly modify the test FilterDefaults without needing public access
        //    typeof(FilterManager)
        //        .GetField("filterSelections", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
        //        .SetValue(testFilterManager, new FilterSelections { SelectedColors = selectedColors });

        //    List<CardSet> result = FilterManager.FilterByColor(testCards, selectedColors, filterMode).ToList();
        //    Debug.WriteLine($"{testName} -> Expected: {expectedCount}, Actual: {result.Count}");

        //    if (result.Count == expectedCount)
        //    {
        //        Debug.WriteLine("Test Passed!");
        //    }
        //    else
        //    {
        //        Debug.WriteLine("TEST FAILED!");
        //        foreach (CardSet? card in result)
        //        {
        //            Debug.WriteLine($"  - {card.Name}, Colors: {card.Colors ?? "null"}, ManaCost: {card.ManaCost}");
        //        }
        //    }
        //}


        #endregion

        #region Load data and populate UI elements
        public async Task LoadDataIntoUiElements()
        {
            await ShowStatusWindowAsync(true, "Loading ALL the cards ...");

            await DBAccess.OpenConnectionAsync();

            Task loadAllCards = PopulateCardDataGridAsync(allCards, allCardsQuery, DataGridContext.AllCards);
            Task loadMyCollection = PopulateCardDataGridAsync(myCards, myCollectionQuery, DataGridContext.MyCollection);
            Task loadCardsForDecks = PopulateCardDataGridAsync(allCardsForDecks, allCardsForDecksQuery, DataGridContext.AllCardsForDecks);
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
        public static async Task PopulateCardDataGridAsync(List<CardSet> cardList, string query, DataGridContext context)
        {
            try
            {
                cardList.Clear();

                DataGrid dataGrid = new();

                switch (context)
                {
                    case DataGridContext.AllCards:
                        dataGrid = CurrentInstance.AllCardsDataGrid;
                        break;

                    case DataGridContext.MyCollection:
                        dataGrid = CurrentInstance.MyCollectionDataGrid;
                        break;

                    case DataGridContext.AllCardsForDecks:
                        dataGrid = CurrentInstance.AllCardsForDecksDataGrid;
                        break;

                    case DataGridContext.CardsInDecks:
                        dataGrid = CurrentInstance.DeckDataGrid;
                        break;
                }

                Debug.WriteLine($"Populating {dataGrid.Name} ...");

                List<CardSet> tempCardList = [];
                using SQLiteCommand command = new(query, DBAccess.connection);
                using DbDataReader reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    try
                    {
                        CardSet card = CreateCardFromReader(reader, context);
                        tempCardList.Add(card);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error while creating card: {ex.Message}");
                        throw;
                    }
                }

                cardList.AddRange(tempCardList);
                dataGrid.ItemsSource = null; // Clear any current binding
                dataGrid.ItemsSource = cardList; // Bind/rebind
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while loading cards: {ex.Message}");
                MessageBox.Show($"Error while loading cards: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private static CardSet CreateCardFromReader(DbDataReader reader, DataGridContext context)
        {
            try
            {
                // Instantiate appropriate type
                CardSet card = context switch
                {
                    DataGridContext.AllCards => new PricedCardSet(),
                    DataGridContext.MyCollection => new CardInCollection(),
                    DataGridContext.CardsInDecks => new CardInDeck(),
                    _ => new CardSet()
                };

                // for all CardSet lists 
                card.Name = GetFieldValue<string>(reader, "Name") ?? string.Empty;
                card.ManaCost = ProcessManaCost(GetFieldValue<string>(reader, "ManaCost") ?? string.Empty);
                card.Colors = GetFieldValue<string>(reader, "Colors") ?? string.Empty;
                card.Type = GetFieldValue<string>(reader, "Type") ?? string.Empty;
                card.ManaValue = GetFieldValue<double?>(reader, "ManaValue") ?? 0;
                card.ManaCostImageBytes = GetFieldValue<byte[]>(reader, "ManaCostImage");
                card.ManaCostRaw = GetFieldValue<string>(reader, "ManaCost") ?? string.Empty;

                // for all CardSet lists except cardsInDecks
                if (context != DataGridContext.CardsInDecks)
                {
                    card.Types = GetFieldValue<string>(reader, "Types") ?? string.Empty;
                    card.SuperTypes = GetFieldValue<string>(reader, "SuperTypes") ?? string.Empty;
                    card.SubTypes = GetFieldValue<string>(reader, "SubTypes") ?? string.Empty;
                    card.Keywords = GetFieldValue<string>(reader, "Keywords") ?? string.Empty;
                    card.Text = GetFieldValue<string>(reader, "RulesText") ?? string.Empty;
                    card.Side = GetFieldValue<string>(reader, "Side") ?? string.Empty;
                }

                // for all CardSet lists except allCardsForDecks or cardsInDecks
                if (context != DataGridContext.AllCardsForDecks && context != DataGridContext.CardsInDecks)
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

                // Only for myCards and cardsInDecks lists
                if (context == DataGridContext.MyCollection || context == DataGridContext.CardsInDecks)
                {
                    card.CardId = GetFieldValue<int?>(reader, "CardId");
                }

                // Only fiels specific to certain lists
                switch (card)
                {
                    case PricedCardSet pricedCard:
                        pricedCard.NormalPrice = GetFieldValue<decimal?>(reader, "NormalPrice");
                        pricedCard.FoilPrice = GetFieldValue<decimal?>(reader, "FoilPrice");
                        pricedCard.EtchedPrice = GetFieldValue<decimal?>(reader, "EtchedPrice");
                        break;

                    case CardInCollection cardInCollection:
                        cardInCollection.CardsOwned = GetFieldValue<int?>(reader, "CardsOwned") ?? 0;
                        cardInCollection.CardsForTrade = GetFieldValue<int?>(reader, "CardsForTrade") ?? 0;
                        cardInCollection.SelectedCondition = GetFieldValue<string>(reader, "Condition");
                        cardInCollection.SelectedFinish = GetFieldValue<string>(reader, "Finish");
                        cardInCollection.CardInCollectionPrice = cardInCollection.SelectedFinish switch
                        {
                            "foil" => ParsePrice("FoilPrice", reader),
                            "etched" => ParsePrice("EtchedPrice", reader),
                            _ => ParsePrice("NormalPrice", reader)
                        };
                        break;

                    case CardInDeck cardInDeck:
                        cardInDeck.Count = GetFieldValue<int?>(reader, "Count") ?? 0;
                        break;
                }

                return card;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error in CreateCardFromReader when trying to create lists for {context}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"Error in CreateCardFromReader: {ex.Message}");
                throw;
            }
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

                using SQLiteCommand command = new SQLiteCommand(query, DBAccess.connection); // Use your database connection
                using DbDataReader reader = await command.ExecuteReaderAsync();

                // Clear existing decks to avoid duplicates
                allDecks.Clear();

                while (await reader.ReadAsync())
                {
                    // Map the database row to the Deck object
                    Deck deck = new Deck
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
                using SQLiteDataReader reader = (SQLiteDataReader)await command.ExecuteReaderAsync();
                List<string> columnNames = [];

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
                // Setup common lists
                List<string> allColors = ["W", "U", "B", "R", "G", "C", "X", "Colorless"];
                List<int> manaValueOptions = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 1000000];
                List<string> manaValueCompareOptions = ["less than", "less than/eq", "greater than", "greater than/eq", "equal to"];

                // Set up unwanted types and subtypes
                HashSet<string> typesToRemove = ["Eaturecray", "Summon", "Scariest", "You'll", "Ever", "See", "Jaguar", "Dragon", "Knights", "Legend", "instant", "Cards"];
                HashSet<string> subTypesToRemove = ["(creature", "and/or", "type)|Judge", "The"];

                // Define the criteria keys
                List<string> criteriaKeys =
                [
                    "Colors",
                    "Rarity",
                    "SuperTypes",
                    "Types",
                    "SubTypes",
                    "Keywords",
                    "Text",
                    "Finishes",
                    "Language",
                    "SelectedCondition"
                ];

                // Clear existing filter context lists by re-initializing it
                filterDefaults = [];

                // Initialize the filterDefaults list dynamically
                filterDefaults = criteriaKeys
                    .Select(criteriaKey => new FilterDefaults { CriteriaKey = criteriaKey })
                    .ToList();


                // Populate the filtered data dynamically
                foreach (var filter in filterDefaults)
                {
                    // Determine the source data and processing logic based on CriteriaKey
                    filter.AllCriteria = filter.CriteriaKey switch
                    {
                        "Colors" => allColors,
                        "Rarity" => CleanAndFilter(allCards.Select(card => card.Rarity)).ToList(),
                        "SuperTypes" => CleanAndFilter(allCards.Select(card => card.SuperTypes)).ToList(),
                        "Types" => CleanAndFilter(allCards.Select(card => card.Types), typesToRemove).ToList(),
                        "SubTypes" => CleanAndFilter(allCards.Select(card => card.SubTypes), subTypesToRemove).ToList(),
                        "Keywords" => CleanAndFilter(allCards.Select(card => card.Keywords)).ToList(),
                        "Finishes" => CleanAndFilter(allCards.Select(card => card.Finishes)).ToList(),
                        "Language" => CleanAndFilter(myCards.Select(card => card.Language)).ToList(),
                        "SelectedCondition" => CleanAndFilter(myCards.OfType<CardInCollection>().Select(card => card.SelectedCondition)).ToList(),
                        _ => [] // Default case if CriteriaKey is not recognized
                    };


                    // Optionally, set DefaultText dynamically
                    filter.DefaultText = filter.CriteriaKey switch
                    {
                        "Rarity" => "Filter rarity ...",
                        "SuperTypes" => "Filter supertypes ...",
                        "Types" => "Filter card types ...",
                        "SubTypes" => "Filter subtypes ...",
                        "Keywords" => "Filter keywords ...",
                        "Text" => "Filter rulestext ...",
                        "Finishes" => "Filter finishes ...",
                        "Language" => "Filter languages ...",
                        "SelectedCondition" => "Filter conditions ...",
                        _ => "Filter criteria ..."
                    };
                }


                Dispatcher.Invoke(() =>
                {
                    // Update DataGrid ComboBoxes
                    UpdateComboBoxSource(AllCardsDataGrid, "AllCardsName", allCards.Select(card => card.Name).Distinct().ToList());
                    UpdateComboBoxSource(AllCardsDataGrid, "AllCardsSet", allCards.Select(card => card.SetName).Distinct().ToList());
                    UpdateComboBoxSource(MyCollectionDataGrid, "MyCollectionName", allCards.Select(card => card.Name).Distinct().ToList());
                    UpdateComboBoxSource(MyCollectionDataGrid, "MyCollectionSet", allCards.Select(card => card.SetName).Distinct().ToList());
                    UpdateComboBoxSource(AllCardsForDecksDataGrid, "AllCardsForDecksName", allCardsForDecks.Select(card => card.Name).Distinct().ToList());

                    // Set Filter Options
                    //FilterRulesTextTextBox.Text = filterDefaults.RulesTextDefaultText;

                    var colorsFilter = filterDefaults.FirstOrDefault(fc => fc.CriteriaKey == "Colors");
                    if (colorsFilter != null)
                    {
                        FilterColorsListBox.ItemsSource = colorsFilter.AllCriteria;
                    }

                    ManaValueComboBox.ItemsSource = manaValueOptions;
                    ManaValueOperatorComboBox.ItemsSource = manaValueCompareOptions;

                    // Set default values for comboboxes on startup
                    if (_isStartup)
                    {
                        ManaValueOperatorComboBox.SelectedIndex = 3;
                        ManaValueComboBox.SelectedIndex = 0;
                    }

                    // Set default text for other comboboxes
                    foreach (var filter in filterDefaults)
                    {
                        // Dynamically retrieve ComboBox and TextBox names based on CriteriaKey
                        string comboBoxName = $"{filter.CriteriaKey}ComboBox";
                        string textBoxName = $"Filter{filter.CriteriaKey}TextBox";

                        // Find the ComboBox by name
                        if (FindName(comboBoxName) is ComboBox comboBox)
                        {
                            // Find the TextBox within the ComboBox template
                            if (comboBox.Template.FindName(textBoxName, comboBox) is TextBox filterTextBox)
                            {
                                // Set the default text and style
                                filterTextBox.Text = filter.DefaultText ?? $"Filter {filter.CriteriaKey} ...";
                                filterTextBox.Foreground = new SolidColorBrush(Colors.Gray);
                            }
                        }
                        else if (FindName(textBoxName) is TextBox textBox) // Directly locate the TextBox by name
                        {
                            // Set the default text and style
                            textBox.Text = filter.DefaultText ?? $"Filter {filter.CriteriaKey} ...";
                            textBox.Foreground = new SolidColorBrush(Colors.Gray);
                        }
                    }



                    PriceRetailerUiUpdates();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while filling comboboxes: {ex.Message}");
                MessageBox.Show($"Error while filling comboboxes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return Task.CompletedTask;

            // Define reusable helper function for cleaning lists
            IEnumerable<string> CleanAndFilter(IEnumerable<string?> input, HashSet<string>? removeItems = null)
            {
                // Split strings by commas and clean data
                char[] separatorArray = [','];

                return input
                    .Where(item => !string.IsNullOrEmpty(item))
                    .SelectMany(item => item!.Split(separatorArray, StringSplitOptions.RemoveEmptyEntries))
                    .Select(item => item.Trim())
                    .Where(item => removeItems == null || !removeItems.Contains(item))
                    .Distinct()
                    .OrderBy(item => item);
            }

            static void UpdateComboBoxSource(DataGrid dataGrid, string tag, List<string?> dataSource)
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
        }

        #endregion

        #region Filter elements handling        
        private void DataGridHeaderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isStartup) { return; }

            if (sender is ComboBox comboBox)
            {

                if (comboBox.Name.Contains("Name", StringComparison.OrdinalIgnoreCase))
                {
                    // Retrieve or create the FilterSelections object for "Name"
                    var nameFilterSelection = filterSelections.FirstOrDefault(fs => fs.CriteriaKey == "Name");
                    if (nameFilterSelection == null)
                    {
                        nameFilterSelection = new FilterSelections { CriteriaKey = "Name" };
                        filterSelections.Add(nameFilterSelection);
                    }

                    // Update the SingleCriteria field with the selected value
                    nameFilterSelection.SingleCriteria = comboBox.SelectedItem?.ToString();
                    // Trigger filtering
                    ApplyFiltersToAllLists();
                }
                else if (comboBox.Name.Contains("Set", StringComparison.OrdinalIgnoreCase))
                {
                    // Retrieve or create the FilterSelections object for "Set"
                    var setFilterSelection = filterSelections.FirstOrDefault(fs => fs.CriteriaKey == "SetName");
                    if (setFilterSelection == null)
                    {
                        setFilterSelection = new FilterSelections { CriteriaKey = "SetName" };
                        filterSelections.Add(setFilterSelection);
                    }

                    // Update the SingleCriteria field with the selected value
                    setFilterSelection.SingleCriteria = comboBox.SelectedItem?.ToString();
                    // Trigger filtering
                    FilterManager.ApplyFilter(allCards, AllCardsDataGrid);
                    FilterManager.ApplyFilter(myCards, MyCollectionDataGrid);
                }

                // Find the parent DataGrid for the current ComboBox
                DataGrid? parentDataGrid = FindParent<DataGrid>(comboBox);

                // If a parent DataGrid is found, reset selections in other DataGrids
                if (parentDataGrid != null)
                {
                    ResetOtherDataGridSelections(parentDataGrid);
                }

            }

            void ResetOtherDataGridSelections(DataGrid currentDataGrid)
            {
                // List all DataGrids
                List<DataGrid> allDataGrids =
                [
                    AllCardsDataGrid,
                    MyCollectionDataGrid,
                    AllCardsForDecksDataGrid
                ];

                // Iterate through other DataGrids and reset their ComboBox selections
                foreach (DataGrid dataGrid in allDataGrids.Where(dg => dg != currentDataGrid))
                {
                    List<ComboBox> headerComboBoxes = FindVisualChildren<ComboBox>(dataGrid);
                    foreach (ComboBox headerComboBox in headerComboBoxes)
                    {
                        headerComboBox.SelectedIndex = -1;
                    }
                }
            }
        }
        private void OperatorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isStartup) { return; }

            OperatorType operatorSelection = OperatorType.Unknown;

            if (sender is ComboBox comboBox)
            {
                // Determine the selected operator
                if (comboBox.SelectedItem is ComboBoxItem selectedItem)
                {
                    string? selectedText = selectedItem.Content.ToString();
                    operatorSelection = selectedText switch
                    {
                        "OR" => OperatorType.OR,
                        "AND" => OperatorType.AND,
                        "NOT" => OperatorType.NOT,
                        _ => OperatorType.Unknown
                    };
                }

                // Derive the CriteriaKey from the ComboBox name
                string? criteriaKey = comboBox.Name.Replace("OperatorComboBox", string.Empty);

                if (!string.IsNullOrEmpty(criteriaKey))
                {
                    // Retrieve or create the FilterSelections object for this CriteriaKey
                    var filterSelection = filterSelections.FirstOrDefault(fs => fs.CriteriaKey == criteriaKey);
                    if (filterSelection == null)
                    {
                        filterSelection = new FilterSelections { CriteriaKey = criteriaKey };
                        filterSelections.Add(filterSelection);
                    }

                    // Update the Operator field
                    filterSelection.Operator = operatorSelection;
                }
            }
            ApplyFiltersToAllLists();
        }
        private void AndOrCheckBox_Toggled(object sender, RoutedEventArgs e)
        {
            // Avoid recursive triggering
            CheckBoxCardsForTrade.Checked -= AndOrCheckBox_Toggled;
            CheckBoxCardsForTrade.Unchecked -= AndOrCheckBox_Toggled;
            CheckBoxCardsNotForTrade.Checked -= AndOrCheckBox_Toggled;
            CheckBoxCardsNotForTrade.Unchecked -= AndOrCheckBox_Toggled;

            try
            {
                if (sender is CheckBox toggledCheckBox)
                {
                    // Identify which property to update in AndOrSettings
                    string propertyName = toggledCheckBox.Name switch
                    {
                        "CheckBoxCardsForTrade" => "CardsForTrade",
                        "CheckBoxCardsNotForTrade" => "CardsNotForTrade",
                        _ => string.Empty
                    };

                    // If 'CheckBoxCardsForTrade' is toggled
                    if (toggledCheckBox == CheckBoxCardsForTrade)
                    {
                        // If 'CheckBoxCardsNotForTrade' is checked, uncheck it
                        if (CheckBoxCardsNotForTrade.IsChecked == true)
                        {
                            CheckBoxCardsNotForTrade.IsChecked = false;
                        }
                    }
                    // If 'CheckBoxCardsNotForTrade' is toggled
                    else if (toggledCheckBox == CheckBoxCardsNotForTrade)
                    {
                        // If 'CheckBoxCardsForTrade' is checked, uncheck it
                        if (CheckBoxCardsForTrade.IsChecked == true)
                        {
                            CheckBoxCardsForTrade.IsChecked = false;
                        }
                    }
                }

                // Apply filter and update label after toggling the checkbox
                ApplyFiltersToAllLists();
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
        private void FilterRulesTextButton_Click(object sender, RoutedEventArgs e)
        {
            FilterRulesText();
        }
        private void FilterTextTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            // Filter by pressing enter
            if (e.Key == Key.Enter)
            {
                FilterRulesText();
            }
        }
        private void FilterRulesText()
        {
            var filterTextEntry = filterSelections.FirstOrDefault(ft => ft.CriteriaKey == "Text");
            if (filterTextEntry == null)
            {
                filterTextEntry = new FilterSelections { CriteriaKey = "Text" };
                filterSelections.Add(filterTextEntry);
            }

            // Update the SingleCriteria field with the selected value
            filterTextEntry.SingleCriteria = FilterTextTextBox.Text;

            ApplyFiltersToAllLists();
        }


        // When a combobox checkbox item is checked or unchecked
        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            HandleCheckCheckOrUncheck(sender, (collection, label) => collection.Add(label));
        }
        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            HandleCheckCheckOrUncheck(sender, (collection, label) => collection.Remove(label));
        }
        private void HandleCheckCheckOrUncheck(object sender, Action<HashSet<string>, string> action)
        {
            try
            {
                if (sender is not DependencyObject dependencyObject)
                {
                    return; // Exit if casting failed
                }

                // Attempt to find the CheckBox and retrieve its Tag and Content
                CheckBox? checkBox = FindVisualChild<CheckBox>(dependencyObject);
                if (checkBox == null || checkBox.Tag is not string criteriaKey || checkBox.Content is not ContentPresenter contentPresenter)
                {
                    return; // Exit if required data is unavailable
                }

                string? label = contentPresenter.Content as string;
                if (string.IsNullOrEmpty(label))
                {
                    return; // Exit if no label is present
                }

                // Ensure the FilterSelections object for this CriteriaKey exists
                var targetFilterSelection = filterSelections.FirstOrDefault(fs => fs.CriteriaKey == criteriaKey);
                if (targetFilterSelection == null)
                {
                    targetFilterSelection = new FilterSelections { CriteriaKey = criteriaKey };
                    filterSelections.Add(targetFilterSelection);
                }

                // Perform the action (Add/Remove) on MultipleCriteria
                action(targetFilterSelection.MultipleCriteria, label);

                // Trigger filter update
                ApplyFiltersToAllLists();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in HandleCheckCheckOrUncheck: {ex.Message}");
            }
        }


        // Every time a dynamically populated filter combobox is opened, it is populated with the correct values, including selected items
        private void DynamicallyPopulatedComboBox_DropDownOpened(object sender, EventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                try
                {
                    (string defaultText, string filterTextBoxName, string listBoxName) = FilterManager.GetComboBoxConfig(comboBox.Name, filterDefaults);

                    if (comboBox.Template.FindName(filterTextBoxName, comboBox) is TextBox filterTextBox && (string.IsNullOrWhiteSpace(filterTextBox.Text) || filterTextBox.Text == defaultText))
                    {
                        PopulateListBoxWithValues(comboBox, listBoxName);
                        filterTextBox.Foreground = new SolidColorBrush(Colors.Gray);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in DynamicallyPopulatedComboBox_DropDownOpened: {ex.Message}");
                }
            }
            void PopulateListBoxWithValues(ComboBox comboBox, string listBoxName)
            {
                if (comboBox.Template.FindName(listBoxName, comboBox) is ListBox listBox)
                {
                    // Get both items source and the corresponding selected items set.
                    (IEnumerable<string> itemsSource, HashSet<string> selectedItems) = FilterManager.GetDataSetAndSelection(listBoxName, filterSelections, filterDefaults);
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
        }
        private void CheckBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is string dataContext)
            {
                // Retrieve the CriteriaKey from the CheckBox's Tag
                string? criteriaKey = checkBox.Tag as string;

                if (!string.IsNullOrEmpty(criteriaKey))
                {
                    // Find the FilterSelections object corresponding to the CriteriaKey
                    var targetFilterSelection = filterSelections.FirstOrDefault(fs => fs.CriteriaKey == criteriaKey);

                    if (targetFilterSelection != null)
                    {
                        // Check if the dataContext exists in the MultipleCriteria collection
                        checkBox.IsChecked = targetFilterSelection.MultipleCriteria.Contains(dataContext);
                    }
                }
            }
        }

        // Filter checkbox elements in the embedded listbox based text typed in the embedded testbox
        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                try
                {
                    // Finding the parent ComboBox by traversing up the visual tree
                    var parent = FindParent<ComboBox>(textBox);

                    // Explicitly check for null before casting
                    if (parent is ComboBox comboBox)
                    {
                        // Get configuration for this specific ComboBox
                        (string defaultText, string _, string listBoxName) = FilterManager.GetComboBoxConfig(comboBox.Name, filterDefaults);

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

            void UpdateListBoxItems(ListBox listBox, string filterText) // This method updates the listbox items based on text typed in FilterTextBox
            {
                (IEnumerable<string> dataSet, HashSet<string> selectedItems) = FilterManager.GetDataSetAndSelection(listBox.Name, filterSelections, filterDefaults);

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
        }

        // When combobox textboxes get focus/defocus        
        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                HandleTextBoxFocus(
                    textBox,
                    (tb, defaultText) => tb.Text == defaultText, // Condition
                    (tb, _) =>
                    {
                        tb.Text = string.Empty;
                        tb.Foreground = new SolidColorBrush(Colors.Black);
                    } // Action
                );
            }
        }
        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                HandleTextBoxFocus(
                    textBox,
                    (tb, _) => string.IsNullOrWhiteSpace(tb.Text), // Condition
                    (tb, defaultText) =>
                    {
                        tb.Text = defaultText;
                        tb.Foreground = new SolidColorBrush(Colors.Gray);
                    } // Action
                );
            }
        }
        private void HandleTextBoxFocus(TextBox textBox, Func<TextBox, string, bool> condition, Action<TextBox, string> action)
        {
            try
            {
                string defaultText;

                // Special case for FilterTextTextBox
                if (textBox.Name == "FilterTextTextBox")
                {
                    var textFilter = filterDefaults.FirstOrDefault(fd => fd.CriteriaKey == "Text");
                    defaultText = textFilter?.DefaultText ?? "Oops, something went wrong ...";
                }
                else
                {
                    // Find the parent ComboBox dynamically
                    var parentComboBox = FindParent<ComboBox>(textBox) ?? throw new InvalidOperationException($"No parent ComboBox found for TextBox: {textBox.Name}");

                    // Get the default text dynamically using the ComboBox's name
                    var config = FilterManager.GetComboBoxConfig(parentComboBox.Name, filterDefaults);
                    defaultText = config.defaultText;
                }

                // Apply condition and action
                if (condition(textBox, defaultText))
                {
                    action(textBox, defaultText);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in HandleTextBoxFocus: {ex.Message}");
            }
        }

        public void ApplyFiltersToAllLists()
        {
            FilterManager.ApplyFilter(allCards, AllCardsDataGrid);
            FilterManager.ApplyFilter(myCards, MyCollectionDataGrid);
            FilterManager.ApplyFilter(allCardsForDecks, AllCardsForDecksDataGrid);
        }

        // Reset filter elements
        public void ClearFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isStartup) { return; }

            // Reset filter TextBoxes for each ComboBox
            foreach (var filter in filterDefaults)
            {
                // Construct ComboBox and TextBox names dynamically
                string comboBoxName = $"{filter.CriteriaKey}ComboBox";
                string textBoxName = $"Filter{filter.CriteriaKey}TextBox";

                // Find the ComboBox and reset its corresponding TextBox
                if (FindName(comboBoxName) is ComboBox comboBox)
                {
                    ResetFilterTextBox(comboBox, textBoxName, filter.DefaultText ?? $"Filter {filter.CriteriaKey} ...");
                }
            }

            // Clear non-custom comboboxes
            ManaValueOperatorComboBox.SelectedIndex = 3;
            ManaValueComboBox.SelectedIndex = 0;

            // Find and clear all ComboBoxes in the DataGrid header
            List<ComboBox> headerComboBoxesAllCards = FindVisualChildren<ComboBox>(AllCardsDataGrid);
            foreach (ComboBox headerComboBox in headerComboBoxesAllCards)
            {
                headerComboBox.SelectedIndex = -1;
            }
            List<ComboBox> headerComboBoxesMyCollection = FindVisualChildren<ComboBox>(MyCollectionDataGrid);
            foreach (ComboBox headerComboBox in headerComboBoxesMyCollection)
            {
                headerComboBox.SelectedIndex = -1;
            }
            List<ComboBox> headerComboBoxesAllCardsForDecks = FindVisualChildren<ComboBox>(AllCardsForDecksDataGrid);
            foreach (ComboBox headerComboBox in headerComboBoxesAllCardsForDecks)
            {
                headerComboBox.SelectedIndex = -1;
            }

            // Reset all the operator comboboxes
            foreach (var cb in FindAllOperatorComboBoxes())
            {
                cb.SelectedIndex = 0;
            }

            // Clear selections in the colors listbox
            ClearListBoxSelections(FilterColorsListBox);

            // Clear the internal HashSets by re-initializing the object
            filterSelections = [];

            // Clear rulestext textbox
            FilterTextTextBox.Text = filterDefaults.FirstOrDefault(fd => fd.CriteriaKey == "Text")?.DefaultText ?? "Oops, something went wrong ...";
            FilterTextTextBox.Foreground = new SolidColorBrush(Colors.Gray);

            // Uncheck CheckBoxes if necessary
            CheckBoxCardsForTrade.IsChecked = false;
            CheckBoxCardsNotForTrade.IsChecked = false;

            // Reset card images
            ImagePromoLabel.Content = string.Empty;
            ImageSetLabel.Content = string.Empty;
            ImageSourceUrl = null;
            ImageSourceUrl2nd = null;

            // Update filter label and apply filters to refresh the DataGrid            
            ApplyFiltersToAllLists();

            // Local helper functions
            static void ResetFilterTextBox(ComboBox comboBox, string textBoxName, string defaultText)
            {
                if (comboBox.Template.FindName(textBoxName, comboBox) is TextBox filterTextBox)
                {
                    filterTextBox.Text = defaultText;
                    filterTextBox.Foreground = new SolidColorBrush(Colors.Gray);
                }
            }
            static void ClearListBoxSelections(ListBox listBox)
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
            IEnumerable<ComboBox> FindAllOperatorComboBoxes()
            {
                var comboBoxes = new List<ComboBox>();
                TraverseVisualTree(this, comboBoxes);
                return comboBoxes.Where(cb => cb.Tag?.ToString() == "OperatorComboBox");

                static void TraverseVisualTree(DependencyObject parent, List<ComboBox> comboBoxes)
                {
                    if (parent == null)
                    {
                        return;
                    }

                    for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                    {
                        var child = VisualTreeHelper.GetChild(parent, i);

                        if (child is ComboBox comboBox)
                        {
                            comboBoxes.Add(comboBox);
                        }

                        TraverseVisualTree(child, comboBoxes);
                    }
                }
            }
        }

        // Filtering helper methods



        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject? parentObject = VisualTreeHelper.GetParent(child);

            while (parentObject != null && parentObject is not T)
            {
                parentObject = VisualTreeHelper.GetParent(parentObject);
            }

            return parentObject as T;
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
                if (button.DataContext is CardSet.CardInCollection cardItem)
                {
                    // Determine which ListView initiated the event and pass the appropriate collection
                    ObservableCollection<CardSet.CardInCollection> targetCollection =
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
                else if (grid.SelectedItem is CardInCollection cardItemCard && grid.Name == "MyCollectionDataGrid")
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
            List<CardInCollection> selectedCards = MyCollectionDataGrid.SelectedItems.Cast<CardInCollection>().ToList();
            if (selectedCards.Count > 0)
            {
                addToCollectionManager.DeleteCardsFromCollection(selectedCards);
            }
        }
        private void ButtonSetCardsForTrade_Click(object sender, RoutedEventArgs e)
        {
            List<CardInCollection> selectedCards = MyCollectionDataGrid.SelectedItems.Cast<CardInCollection>().ToList();
            if (selectedCards.Count > 0)
            {
                addToCollectionManager.SetCardsForTrade(selectedCards, true);
                MyCollectionDataGrid.UnselectAll();
            }
        }
        private void ButtonSetNoneForTrade_Click(object sender, RoutedEventArgs e)
        {
            List<CardInCollection> selectedCards = MyCollectionDataGrid.SelectedItems.Cast<CardInCollection>().ToList();
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
            try
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
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in open and edit deck: {ex}");
                MessageBox.Show($"Error in open and edit deck: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        // Add a card to a deck
        private async void AddCardToDeck(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGrid { SelectedItem: CardSet cardSetCard } || CurrentDeck == null || string.IsNullOrWhiteSpace(cardSetCard.Name))
            {
                return;
            }

            await DeckManager.SubmitCardToDeck(cardSetCard.Name, CurrentDeck.DeckId);
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

            Task loadAllCards = PopulateCardDataGridAsync(allCards, allCardsQuery, DataGridContext.AllCards);
            Task loadMyCollection = PopulateCardDataGridAsync(myCards, myCollectionQuery, DataGridContext.MyCollection);

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
            ComboBoxItem? itemToSelect = RetailSelector.Items
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
            LanguageComboBox.Visibility = Visibility.Visible;
            LanguageOperatorComboBox.Visibility = Visibility.Visible;
            SelectedConditionComboBox.Visibility = Visibility.Visible;
            SelectedConditionOperatorComboBox.Visibility = Visibility.Visible;
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
            LanguageComboBox.Visibility = Visibility.Collapsed;
            LanguageOperatorComboBox.Visibility = Visibility.Collapsed;
            SelectedConditionComboBox.Visibility = Visibility.Collapsed;
            SelectedConditionOperatorComboBox.Visibility = Visibility.Collapsed;
            CheckBoxCardsForTrade.Visibility = Visibility.Collapsed;
            CheckBoxCardsNotForTrade.Visibility = Visibility.Collapsed;

            ImagePromoLabel.Content = string.Empty;
            ImageSetLabel.Content = string.Empty;
            ImageSourceUrl = null;
            ImageSourceUrl2nd = null;

            LogoSmall.Visibility = Visibility.Collapsed;
            GridFiltering.Visibility = Visibility.Collapsed;
            GridUtilsMenu.Visibility = Visibility.Collapsed;

            ApplyFiltersToAllLists();
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
