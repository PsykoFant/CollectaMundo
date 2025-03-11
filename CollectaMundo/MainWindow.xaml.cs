using CollectaMundo.Behaviors;
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

namespace CollectaMundo
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Set up varibales
        public CardViewModel CardVM { get; }
        public FilterViewModel? FilterVM { get; private set; }

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

        // Query strings to load cards into datagrids
        public readonly string allCardsQuery = "SELECT * FROM view_allCards";
        public readonly string myCollectionQuery = "SELECT * FROM view_myCollection;";
        public readonly string allCardsForDecksQuery = "SELECT * FROM view_allCardsForDecks;";

        // Flag to track startup phase
        public bool _isStartup = true;

        // The CardSet object which holds all the cards read from db
        //public readonly List<CardSet> allCards = [];
        public readonly List<CardSet> myCards = [];
        public readonly List<CardSet> allCardsForDecks = [];
        public readonly List<CardSet> cardsInDecks = [];

        public enum DataGridContext
        {
            AllCards,
            MyCollection,
            AllCardsForDecks,
            CardsInDecks
        }
        public enum OperatorType
        {
            // Basic logical operators
            OR = 0,
            AND = 1,
            NOT = 2,

            // Comparison operators
            EQUALS = 3,
            NOT_EQUALS = 4,
            GREATER_THAN = 5,
            LESS_THAN = 6,
            GREATER_THAN_OR_EQUALS = 7,
            LESS_THAN_OR_EQUALS = 8,

            // Range operators
            IN_RANGE = 9,
            NOT_IN_RANGE = 10,

            // String-specific operators
            CONTAINS = 11,
            DOES_NOT_CONTAIN = 12,
            STARTS_WITH = 13,
            ENDS_WITH = 14,

            // Special operators
            IS_NULL = 15,
            IS_NOT_NULL = 16,

            // Unknown or default
            Unknown = -1
        }

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

        public MainWindow()
        {
            InitializeComponent();
            _currentInstance = this;
            CardVM = new CardViewModel();

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
        //    FilterManagerOld testFilterManager = new FilterManagerOld();

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
        //private void RunTest(FilterManagerOld testFilterManager, HashSet<string> selectedColors, int filterMode, string testName, int expectedCount)
        //{
        //    //AllOrNoneComboBox.SelectedIndex = filterMode;

        //    // Directly modify the test FilterDefaults without needing public access
        //    typeof(FilterManagerOld)
        //        .GetField("filterSelections", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
        //        .SetValue(testFilterManager, new FilterSelections { SelectedColors = selectedColors });

        //    List<CardSet> result = FilterManagerOld.FilterByColor(testCards, selectedColors, filterMode).ToList();
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

            await CardViewModel.PopulateCardDataGridAsync(CardVM.AllCards, CardVM.AllCardsView, allCardsQuery, DataGridContext.AllCards);
            await CardViewModel.PopulateCardDataGridAsync(CardVM.MyCollection, CardVM.MyCollectionView, myCollectionQuery, DataGridContext.MyCollection);
            await CardViewModel.PopulateCardDataGridAsync(CardVM.allCardsForDecks, CardVM.AllCardsForDecksView, allCardsForDecksQuery, DataGridContext.AllCardsForDecks);
            await CardVM.LoadColorIconsAsync();

            OnPropertyChanged(nameof(CardVM)); // This ensures CardVM bindings refresh

            // Assign the new FilterVM object AFTER data is available
            FilterVM = new FilterViewModel(CardVM);
            OnPropertyChanged(nameof(FilterVM)); // Force UI refresh so bindings update

            Task loadDecks = LoadAllDecksAsync();
            Task populateAllFormatsList = PopulateAllFormatsListAsync();
            await Task.WhenAll(loadDecks, populateAllFormatsList);

            DBAccess.CloseConnection();

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

        #endregion

        #region Filter elements handling        
        // When combobox textboxes get focus/defocus        
        private void FilterTextTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is FilterItemViewModel filterItem)
            {
                if (e.Key == Key.Escape)
                {
                    filterItem.FreetextSearch = filterItem.DefaultText;
                    textBox.Foreground = new SolidColorBrush(Colors.Gray);

                    // Use Dispatcher to remove focus with a small delay
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        // Kill logical focus
                        FocusManager.SetFocusedElement(FocusManager.GetFocusScope(textBox), null);
                        // Kill keyboard focus
                        Keyboard.ClearFocus();
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }

                filterItem.HandleKeyPress(e.Key);
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
                if (button.DataContext is CardSet cardItem)
                {
                    // Determine which ListView initiated the event and pass the appropriate collection
                    ObservableCollection<CardSet> targetCollection =
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
                else if (grid.SelectedItem is CardSet cardItemCard && grid.Name == "MyCollectionDataGrid")
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
            List<CardSet> selectedCards = MyCollectionDataGrid.SelectedItems.Cast<CardSet>().ToList();
            if (selectedCards.Count > 0)
            {
                addToCollectionManager.DeleteCardsFromCollection(selectedCards);
            }
        }
        private void ButtonSetCardsForTrade_Click(object sender, RoutedEventArgs e)
        {
            List<CardSet> selectedCards = MyCollectionDataGrid.SelectedItems.Cast<CardSet>().ToList();
            if (selectedCards.Count > 0)
            {
                addToCollectionManager.SetCardsForTrade(selectedCards, true);
                MyCollectionDataGrid.UnselectAll();
            }
        }
        private void ButtonSetNoneForTrade_Click(object sender, RoutedEventArgs e)
        {
            List<CardSet> selectedCards = MyCollectionDataGrid.SelectedItems.Cast<CardSet>().ToList();
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
                HeadlineDecks.Content = "Deck Editor";
                GridTopMenu.IsEnabled = false;
                GridFiltering.Visibility = Visibility.Visible;
                FilterSummaryScrollViewer.Visibility = Visibility.Visible;

                _ = MyCollectionDataGrid.Dispatcher.BeginInvoke(new Action(() =>
                {
                    DataGridColumnResizerBehavior.ForceUpdate(MyCollectionDataGrid);
                }), System.Windows.Threading.DispatcherPriority.Loaded);

                GridDecksOverview.Visibility = Visibility.Collapsed;
                GridDeckEditor.Visibility = Visibility.Visible;
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
            ResetGrids();
            HeadlineDecks.Content = "Deck Management";
            GridTopMenu.IsEnabled = true;
            GridDecks.Visibility = Visibility.Visible;
            GridDeckEditor.Visibility = Visibility.Collapsed;
            GridDecksOverview.Visibility = Visibility.Visible;
        }

        // Deck Editor Methods

        // Cancel edits by clicking outside edited element
        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
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

            Task loadAllCards = CardViewModel.PopulateCardDataGridAsync(CardVM.AllCards, CardVM.AllCardsView, allCardsQuery, DataGridContext.AllCards);
            Task loadMyCollection = CardViewModel.PopulateCardDataGridAsync(CardVM.MyCollection, CardVM.MyCollectionView, myCollectionQuery, DataGridContext.MyCollection);

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

            MyCollectionDataGrid.Dispatcher.BeginInvoke(new Action(() =>
            {
                DataGridColumnResizerBehavior.ForceUpdate(AllCardsDataGrid);
            }), System.Windows.Threading.DispatcherPriority.Loaded);

            GridFiltering.Visibility = Visibility.Visible;
            GridSearchAndFilterAllCards.Visibility = Visibility.Visible;
            AddToCollectionManager.AdjustColumnWidths();
        }
        private void MenuMyCollection_Click(object sender, RoutedEventArgs e)
        {
            ResetGrids();

            MyCollectionDataGrid.Dispatcher.BeginInvoke(new Action(() =>
            {
                DataGridColumnResizerBehavior.ForceUpdate(MyCollectionDataGrid);
            }), System.Windows.Threading.DispatcherPriority.Loaded);

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
            // Reset top menu
            MenuSearchAndFilterButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDDDDDD"));
            MenuMyCollectionButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDDDDDD"));
            MenuDecksButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDDDDDD"));
            MenuUtilsButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDDDDDD"));

            // Reset content section UI
            GridSearchAndFilterAllCards.Visibility = Visibility.Collapsed;
            GridMyCollection.Visibility = Visibility.Collapsed;
            GridDecks.Visibility = Visibility.Collapsed;
            GridUtilitiesSection.Visibility = Visibility.Collapsed;

            // Reset side menu options
            GridFiltering.Visibility = Visibility.Collapsed;
            GridUtilsMenu.Visibility = Visibility.Collapsed;

            // Reset filtering and add/edit cards UI
            EditStatusTextBlock.Text = string.Empty;
            AddStatusTextBlock.Text = string.Empty;
            UtilsInfoLabel.Content = "";
            FilterSummaryScrollViewer.Visibility = Visibility.Collapsed;
            LogoSmall.Visibility = Visibility.Collapsed;

            // Reset filter UI specific to my collection 
            LanguageComboBox.Visibility = Visibility.Collapsed;
            LanguageOperatorComboBox.Visibility = Visibility.Collapsed;
            SelectedConditionComboBox.Visibility = Visibility.Collapsed;
            SelectedConditionOperatorComboBox.Visibility = Visibility.Collapsed;
            CheckBoxCardsForTrade.Visibility = Visibility.Collapsed;
            CheckBoxCardsNotForTrade.Visibility = Visibility.Collapsed;

            // Reset image UI
            ImagePromoLabel.Content = string.Empty;
            ImageSetLabel.Content = string.Empty;
            ImageSourceUrl = null;
            ImageSourceUrl2nd = null;
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
