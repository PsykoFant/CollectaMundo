using CollectaMundo.ApplicationServices;
using CollectaMundo.Data;
using CollectaMundo.DomainLogic;
using CollectaMundo.DomainLogic.Models;
using CollectaMundo.Presentation.Behaviors;
using CollectaMundo.ViewModels;
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

        private MainWindowViewModel VM => (MainWindowViewModel)DataContext;

        private readonly ICardListService _cardListService;


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

        // Flag to track startup phase
        public bool _isStartup = true;
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

        // Read the price retailer from appsettings.json
        public string? appsettingsRetailer = JsonAppSettings.GetSetting("PriceInfo:Retailer") as string;

        #endregion
        public MainWindow()
        {
            InitializeComponent();
            _currentInstance = this;

            // create your two little helpers exactly once
            var settings = new JsonAppSettings();             // implements IAppSettings
            var dbFactory = new DbConnectionFactory(settings); // implements IDbConnectionFactory

            // build your repository & service stack
            var cardListRepo = new CardListRepository(dbFactory);
            var cardListService = new CardListService(cardListRepo);
            var filterDefaultsRepo = new FilterDefaultsRepository(dbFactory);
            var filterDefaultsSvc = new FilterDefaultsService(filterDefaultsRepo);
            var filteringService = new FilteringService(filterDefaultsRepo);
            var editCollRepo = new EditCollectionRepository(dbFactory);
            var editCollLogic = new EditCollectionLogic(editCollRepo);
            var editCollSvc = new EditCollectionService(editCollRepo, editCollLogic);

            // now hand them all straight into your ViewModel
            var vm = new MainWindowViewModel(
                settings,
                dbFactory,
                cardListService,
                filterDefaultsSvc,
                filteringService,
                editCollSvc
            );

            DataContext = vm;

            Loaded += async (sender, args) =>
            {
                await ShowStatusWindowAsync(true, "Just a quick system integrity check …");
                await DownloadAndPrepDB.SystemIntegrityCheckAsync();
                await vm.InitializeAsync();
                _isStartup = false;
            };

            DownloadAndPrepDB.StatusMessageUpdated += UpdateStatusTextBox;
            UpdateDB.StatusMessageUpdated += UpdateStatusTextBox;
        }

        #region Load data and populate UI elements
        public async Task LoadDataIntoUiElements()
        {
            await ShowStatusWindowAsync(true, "Loading ALL the cards ...");

            await DBAccess.OpenConnectionAsync();

            await _cardListService.LoadAllCardsAsync(VM.AllCardsVM.Cards);
            await _cardListService.LoadMyCollectionAsync(VM.MyCollectionVM.Cards);
            await _cardListService.LoadAllCardsForDecksAsync(VM.AllCardsForDecksVM.Cards);
            await _cardListService.LoadAllCardsInDecksAsync(VM.AllCardsInDecksVM.Cards);
            await _cardListService.LoadColorIconsAsync(VM.ColorIcons.Cards);

            await VM.FilterVM.InitializeFilterDefaultsAsync();

            Task loadDecks = LoadAllDecksAsync();
            Task populateAllFormatsList = PopulateAllFormatsListAsync();
            await Task.WhenAll(loadDecks, populateAllFormatsList);

            DBAccess.CloseConnection();

            CardPriceUtilities.UpdateDataGridHeaders(AllCardsDataGrid);
            CardPriceUtilities.UpdateDataGridHeaders(MyCollectionDataGrid);

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
                JsonAppSettings.UpdatePriceInfo(null, retailer);
                appsettingsRetailer = retailer;
            }

            // Update the db views to load prices from the selected retailer
            await DownloadAndPrepDB.CreateViews();

            Task loadAllCards = _cardListService.LoadAllCardsAsync(VM.AllCardsVM.Cards);
            Task loadMyCollection = _cardListService.LoadAllCardsAsync(VM.MyCollectionVM.Cards);

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
        public void ResetGrids()
        {
            // Reset top menu
            //MenuSearchAndFilterButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDDDDDD"));
            //MenuMyCollectionButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDDDDDD"));
            //MenuDecksButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDDDDDD"));
            //MenuUtilsButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDDDDDD"));

            // Reset content section UI
            //GridSearchAndFilterAllCards.Visibility = Visibility.Collapsed;
            //GridMyCollection.Visibility = Visibility.Collapsed;
            //GridDecks.Visibility = Visibility.Collapsed;
            //GridUtilitiesSection.Visibility = Visibility.Collapsed;

            //// Reset side menu options
            //GridFiltering.Visibility = Visibility.Collapsed;
            //GridUtilsMenu.Visibility = Visibility.Collapsed;

            // Reset filtering and add/edit cards UI
            //AddCardsVM.StatusMessage = string.Empty;
            //EditCardsVM.StatusMessage = string.Empty;
            UtilsInfoLabel.Content = "";


            // Reset filter UI specific to my collection 
            //LanguageComboBox.Visibility = Visibility.Collapsed;
            //LanguageOperatorComboBox.Visibility = Visibility.Collapsed;
            //SelectedFinishComboBox.Visibility = Visibility.Collapsed;
            //SelectedFinishOperatorComboBox.Visibility = Visibility.Collapsed;
            //SelectedConditionComboBox.Visibility = Visibility.Collapsed;
            //SelectedConditionOperatorComboBox.Visibility = Visibility.Collapsed;
            //CheckBoxCardsForTrade.Visibility = Visibility.Collapsed;
            //CheckBoxCardsNotForTrade.Visibility = Visibility.Collapsed;

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
