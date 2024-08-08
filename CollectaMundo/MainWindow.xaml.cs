using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        public string myCollectionQuery = @"
                    SELECT                        
                        c.name AS Name,
                        s.name AS SetName,
                        k.keyruneImage AS KeyRuneImage,
                        c.manaCost AS ManaCost,
                        u.manaCostImage AS ManaCostImage,
                        c.types AS Types,
                        c.supertypes AS SuperTypes,
                        c.subtypes AS SubTypes,
                        c.type AS Type,
                        COALESCE(cg.AggregatedKeywords, c.keywords) AS Keywords,
                        c.text AS RulesText,
                        c.manaValue AS ManaValue,
                        c.uuid AS Uuid,
                        m.id AS CardId,
                        m.count AS CardsOwned,
                        m.condition AS Condition,
                        m.language AS Language,
                        m.finish AS Finishes,
                        c.side AS Side
                    FROM
                        myCollection m
                    JOIN
                        cards c ON m.uuid = c.uuid
                    LEFT JOIN 
                        sets s ON c.setCode = s.code
                    LEFT JOIN 
                        keyruneImages k ON c.setCode = k.setCode
                    LEFT JOIN 
                        uniqueManaCostImages u ON c.manaCost = u.uniqueManaCost
                    LEFT JOIN (
                        SELECT 
                            cc.SetCode, 
                            cc.Name, 
                            GROUP_CONCAT(cc.keywords, ', ') AS AggregatedKeywords
                        FROM cards cc
                        GROUP BY cc.SetCode, cc.Name
                    ) cg ON c.SetCode = cg.SetCode AND c.Name = cg.Name
                    WHERE EXISTS (SELECT 1 FROM cards WHERE uuid = m.uuid)
                UNION ALL
                    SELECT
                        t.name AS Name,
                        s.name AS SetName,
                        k.keyruneImage AS KeyRuneImage,
                        t.manaCost AS ManaCost,
                        u.manaCostImage AS ManaCostImage,
                        t.types AS Types,
                        t.supertypes AS SuperTypes,
                        t.subtypes AS SubTypes,
                        t.type AS Type,
                        t.keywords AS Keywords,
                        t.text AS RulesText,
                        NULL AS ManaValue,  -- Tokens do not have manaValue
                        t.uuid AS Uuid,
                        m.id AS CardId,
                        m.count AS CardsOwned,
                        m.condition AS Condition,
                        m.language AS Language,
                        m.finish AS Finishes,
                        t.side AS Side
                    FROM
                        myCollection m
                    JOIN
                        tokens t ON m.uuid = t.uuid
                    LEFT JOIN 
                        sets s ON t.setCode = s.code
                    LEFT JOIN 
                        keyruneImages k ON t.setCode = k.setCode
                    LEFT JOIN 
                        uniqueManaCostImages u ON t.manaCost = u.uniqueManaCost
                    WHERE NOT EXISTS (SELECT 1 FROM cards WHERE uuid = m.uuid);
                ";
        private string allCardsQuery = @"                    
                    SELECT 
                        c.name AS Name, 
                        s.name AS SetName, 
                        k.keyruneImage AS KeyRuneImage, 
                        c.manaCost AS ManaCost, 
                        u.manaCostImage AS ManaCostImage, 
                        c.types AS Types, 
                        c.supertypes AS SuperTypes, 
                        c.subtypes AS SubTypes, 
                        c.type AS Type, 
                        COALESCE(cg.AggregatedKeywords, c.keywords) AS Keywords,
                        c.text AS RulesText, 
                        c.manaValue AS ManaValue, 
                        c.language AS Language,
                        c.uuid AS Uuid, 
                        c.finishes AS Finishes, 
                        c.side AS Side 
                    FROM cards c
                    JOIN sets s ON c.setCode = s.code
                    LEFT JOIN keyruneImages k ON c.setCode = k.setCode
                    LEFT JOIN uniqueManaCostImages u ON c.manaCost = u.uniqueManaCost
                    LEFT JOIN (
                        SELECT 
                            cc.SetCode, 
                            cc.Name, 
                            GROUP_CONCAT(cc.keywords, ', ') AS AggregatedKeywords
                        FROM cards cc
                        GROUP BY cc.SetCode, cc.Name
                    ) cg ON c.SetCode = cg.SetCode AND c.Name = cg.Name
                    WHERE c.side IS NULL OR c.side = 'a'

                    UNION ALL

                    SELECT 
                        t.name AS Name, 
                        s.name AS SetName, 
                        k.keyruneImage AS KeyRuneImage, 
                        t.manaCost AS ManaCost, 
                        u.manaCostImage AS ManaCostImage, 
                        t.types AS Types, 
                        t.supertypes AS SuperTypes, 
                        t.subtypes AS SubTypes, 
                        t.type AS Type, 
                        t.keywords AS Keywords, 
                        t.text AS RulesText, 
                        NULL AS ManaValue,  -- 'manaValue' does not exist in 'tokens'
                        t.language AS Language,
                        t.uuid AS Uuid, 
                        t.finishes AS Finishes, 
                        t.side AS Side 
                    FROM tokens t 
                    JOIN sets s ON t.setCode = s.tokenSetCode 
                    LEFT JOIN keyruneImages k ON (SELECT code FROM sets WHERE tokenSetCode = t.setCode) = k.setCode
                    LEFT JOIN uniqueManaCostImages u ON t.manaCost = u.uniqueManaCost
                    WHERE t.side IS NULL OR t.side = 'a'
                    ";

        // The CardSet object which holds all the cards read from db
        private List<CardSet> allCards = new List<CardSet>();
        public List<CardSet> myCards = new List<CardSet>();

        // The filter object from the FilterContext class
        private FilterContext filterContext = new FilterContext();
        private FilterManager filterManager;

        // Object of AddToCollectionManager class to access that functionality
        private AddToCollectionManager addToCollectionManager = new AddToCollectionManager();

        private bool isConditionMapped;
        private bool isFinishMapped;
        private bool isCardsOwnedMapped;
        private bool isCardsForTradedMapped;
        private bool isLanguageMapped;
        public List<ColumnMapping>? _mappings;

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
            Loaded += async (sender, args) =>
            {
                await LoadDataIntoUiElements();
            };


            filterManager = new FilterManager(filterContext);

            // Pick up filtering comboboxes changes
            FilterCardNameComboBox.SelectionChanged += ComboBox_SelectionChanged;
            FilterSetNameComboBox.SelectionChanged += ComboBox_SelectionChanged;
            AllOrNoneComboBox.SelectionChanged += ComboBox_SelectionChanged;
            ManaValueComboBox.SelectionChanged += ComboBox_SelectionChanged;
            ManaValueOperatorComboBox.SelectionChanged += ComboBox_SelectionChanged;
        }
        public async Task LoadDataIntoUiElements()
        {
            await DownloadAndPrepDB.CheckDatabaseExistenceAsync();
            GridSearchAndFilter.Visibility = Visibility.Visible;

            await DBAccess.OpenConnectionAsync();

            //await LoadDataAsync(allCards, allCardsQuery, AllCardsDataGrid, false);
            await LoadDataAsync(myCards, myCollectionQuery, MyCollectionDatagrid, true);
            await FillComboBoxesAsync();

            DBAccess.CloseConnection();

            CardsToAddListView.ItemsSource = addToCollectionManager.cardItemsToAdd;
            CardsToEditListView.ItemsSource = addToCollectionManager.cardItemsToEdit;
        }

        #region Filter elements handling        

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilterSelection();
        }
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
        private void filterRulesTextButton_Click(object sender, RoutedEventArgs e) // Apply filter for rulestext freetext search
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
            ResetFilterTextBox(SubTypesComboBox, "FilterSuperTypesTextBox", filterContext.SuperTypesDefaultText);
            ResetFilterTextBox(TypesComboBox, "FilterTypesTextBox", filterContext.TypesDefaultText);
            ResetFilterTextBox(SubTypesComboBox, "FilterSubTypesTextBox", filterContext.SubTypesDefaultText);
            ResetFilterTextBox(KeywordsComboBox, "FilterKeywordsTextBox", filterContext.KeywordsDefaultText);

            // Clear non-custom comboboxes
            FilterCardNameComboBox.SelectedIndex = -1;
            FilterSetNameComboBox.SelectedIndex = -1;
            AllOrNoneComboBox.SelectedIndex = 0;
            ManaValueComboBox.SelectedIndex = -1;
            ManaValueOperatorComboBox.SelectedIndex = -1;

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
            CardFrontLabel.Visibility = Visibility.Collapsed;
            CardBackLabel.Visibility = Visibility.Collapsed;
            ImageSourceUrl = null;
            ImageSourceUrl2nd = null;

            // Update filter label and apply filters to refresh the DataGrid            
            ApplyFilterSelection();
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
        // Show the card image for the highlighted DataGrid row
        private async void ShowSelectedCardImage(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid dataGrid && dataGrid.SelectedItem is CardSet selectedCard
                && !string.IsNullOrEmpty(selectedCard.Uuid) && !string.IsNullOrEmpty(selectedCard.Types))
            {
                try
                {
                    await DBAccess.OpenConnectionAsync();
                    string? scryfallId = await GetScryfallIdByUuidAsync(selectedCard.Uuid, selectedCard.Types);

                    await ShowCardImage(scryfallId, selectedCard.Uuid);
                    DBAccess.CloseConnection();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in selection changed: {ex.Message}");
                    MessageBox.Show($"Error in selection changed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Show the card image for the selected UUID from the dropdown
        private async void UuidSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is UuidVersion selectedVersion)
            {
                string? selectedUuid = selectedVersion.Uuid;
                if (selectedUuid == null)
                {
                    Debug.WriteLine("Selected UUID is null.");
                    return;
                }

                Debug.WriteLine($"Trying to show image with uuid: {selectedUuid}");
                try
                {
                    await DBAccess.OpenConnectionAsync();
                    string? scryfallId = await GetScryfallIdByUuidAsync(selectedUuid);

                    await ShowCardImage(scryfallId, selectedUuid);
                    DBAccess.CloseConnection();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in selection changed: {ex.Message}");
                    MessageBox.Show($"Error in selection changed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Method to get the Scryfall ID by UUID and type
        private async Task<string?> GetScryfallIdByUuidAsync(string uuid, string? types = null)
        {
            string query = "SELECT scryfallId FROM cardIdentifiers WHERE uuid = @uuid UNION ALL SELECT scryfallId FROM tokenIdentifiers WHERE uuid = @uuid";

            using (var command = new SQLiteCommand(query, DBAccess.connection))
            {
                command.Parameters.AddWithValue("@uuid", uuid);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return reader["scryfallId"].ToString();
                    }
                }
            }
            return null;
        }

        // Method to show the card image
        private async Task ShowCardImage(string? scryfallId, string uuid)
        {
            if (!string.IsNullOrEmpty(scryfallId) && scryfallId.Length >= 2)
            {
                char dir1 = scryfallId[0];
                char dir2 = scryfallId[1];

                string cardImageUrl = $"https://cards.scryfall.io/normal/front/{dir1}/{dir2}/{scryfallId}.jpg";
                string secondCardImageUrl = $"https://cards.scryfall.io/normal/back/{dir1}/{dir2}/{scryfallId}.jpg";

                Debug.WriteLine(scryfallId);
                Debug.WriteLine(cardImageUrl);

                // Assuming CardFrontLabel and CardBackLabel are accessible globally or within the same context
                CardFrontLabel.Visibility = Visibility.Visible;
                ImageSourceUrl = cardImageUrl;

                if (await IsDoubleSidedCardAsync(uuid))
                {
                    CardBackLabel.Visibility = Visibility.Visible;
                    ImageSourceUrl2nd = secondCardImageUrl;
                }
                else
                {
                    CardBackLabel.Visibility = Visibility.Collapsed;
                    ImageSourceUrl2nd = null;
                }
            }
        }

        // Method to check if the card is double-sided
        private async Task<bool> IsDoubleSidedCardAsync(string uuid)
        {
            string query = "SELECT side FROM cards WHERE uuid = @uuid UNION ALL SELECT side FROM tokens WHERE uuid = @uuid";
            using (var command = new SQLiteCommand(query, DBAccess.connection))
            {
                command.Parameters.AddWithValue("@uuid", uuid);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return reader["side"].ToString() == "a";
                    }
                }
            }
            return false;
        }



        #endregion

        #region Pick up events for add to or edit collection 
        private void IncrementCountHandler(object sender, RoutedEventArgs e)
        {
            addToCollectionManager.IncrementCount_Click(sender, e);
        }
        private void DecrementCountHandler(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)  // This checks if sender is a Button and assigns it to button if true
            {
                if (button.DataContext is CardSet.CardItem cardItem)
                {
                    // Determine which ListView initiated the event and pass the appropriate collection
                    ObservableCollection<CardSet.CardItem> targetCollection =
                        (CardsToEditListView.Items.Contains(cardItem)) ? addToCollectionManager.cardItemsToEdit : addToCollectionManager.cardItemsToAdd;

                    // Only decrement for cardItemsToEdit if count is above 0
                    if (targetCollection == addToCollectionManager.cardItemsToEdit)
                    {
                        if (cardItem.CardsOwned > 0)
                        {
                            addToCollectionManager.DecrementCount_Click(sender, e, targetCollection);
                        }
                    }
                    else
                    {
                        addToCollectionManager.DecrementCount_Click(sender, e, targetCollection);

                        // If there is nothing in cardItemsToAdd, hide listview and button
                        if (targetCollection.Count == 0)
                        {
                            CardsToAddListView.Visibility = Visibility.Collapsed;
                            ButtonAddCardsToMyCollection.Visibility = Visibility.Collapsed;
                        }
                    }
                }
            }
        }
        private void AddToCollectionHandler(object sender, RoutedEventArgs e)
        {
            AddStatusTextBlock.Visibility = Visibility.Collapsed;
            CardsToAddListView.Visibility = Visibility.Visible;
            ButtonAddCardsToMyCollection.Visibility = Visibility.Visible;
            addToCollectionManager.EditOrAddCard_Click(sender, e, addToCollectionManager.cardItemsToAdd);
        }
        private void EditCollectionHandler(object sender, RoutedEventArgs e)
        {
            EditStatusTextBlock.Visibility = Visibility.Collapsed;
            CardsToEditListView.Visibility = Visibility.Visible;
            ButtonEditCardsInMyCollection.Visibility = Visibility.Visible;
            addToCollectionManager.EditOrAddCard_Click(sender, e, addToCollectionManager.cardItemsToEdit);
        }
        private void ButtonAddCardsToMyCollection_Click(object sender, RoutedEventArgs e)
        {
            addToCollectionManager.SubmitNewCardsToCollection(sender, e);
        }
        private void ButtonEditCardsInMyCollection_Click(object sender, RoutedEventArgs e)
        {
            addToCollectionManager.SubmitEditedCardsToCollection(sender, e);
        }

        #endregion

        #region Load data and populate UI elements
        public async Task LoadDataAsync(List<CardSet> cardList, string query, DataGrid dataGrid, bool isCardItem)
        {
            Debug.WriteLine("Loading data asynchronously...");
            try
            {
                await ShowStatusWindowAsync(true);  // Show loading message                
                CurrentInstance.StatusLabel.Content = "Loading ALL the cards ... ";
                CurrentInstance.progressBar.Visibility = Visibility.Collapsed;
                // Force the UI to update
                Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                cardList.Clear();

                using var command = new SQLiteCommand(query, DBAccess.connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var card = CreateCardFromReader(reader, isCardItem);  // Use DbDataReader
                    cardList.Add(card);
                }

                Dispatcher.Invoke(() =>
                {
                    dataGrid.ItemsSource = cardList;
                    ICollectionView collectionView = CollectionViewSource.GetDefaultView(cardList);
                    collectionView.Refresh();
                });

                stopwatch.Stop();
                Debug.WriteLine($"Data loading method execution time: {stopwatch.ElapsedMilliseconds} ms");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while loading cards: {ex.Message}");
                MessageBox.Show($"Error while loading cards: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                CurrentInstance.StatusLabel.Content = string.Empty;
                await ShowStatusWindowAsync(false);
                CurrentInstance.progressBar.Visibility = Visibility.Visible;
            }
        }
        private CardSet CreateCardFromReader(DbDataReader reader, bool isCardItem)
        {
            var card = isCardItem ? (CardSet)new CardItem() : new CardSet();

            // Initialize common properties
            card.Name = reader["Name"]?.ToString() ?? string.Empty;
            card.SetName = reader["SetName"]?.ToString() ?? string.Empty;
            card.SetIcon = ConvertImage(reader["KeyRuneImage"] as byte[]);
            card.ManaCost = ProcessManaCost(reader["ManaCost"]?.ToString() ?? string.Empty);
            card.ManaCostImage = ConvertImage(reader["ManaCostImage"] as byte[]);
            card.Types = reader["Types"]?.ToString() ?? string.Empty;
            card.SuperTypes = reader["SuperTypes"]?.ToString() ?? string.Empty;
            card.SubTypes = reader["SubTypes"]?.ToString() ?? string.Empty;
            card.Type = reader["Type"]?.ToString() ?? string.Empty;
            card.Keywords = reader["Keywords"]?.ToString() ?? string.Empty;
            card.Text = reader["RulesText"]?.ToString() ?? string.Empty;
            card.ManaValue = double.TryParse(reader["ManaValue"]?.ToString(), out double manaValue) ? manaValue : 0;
            card.Language = reader["Language"]?.ToString() ?? string.Empty;
            card.Uuid = reader["Uuid"]?.ToString() ?? string.Empty;
            card.Side = reader["Side"]?.ToString() ?? string.Empty;
            card.Finishes = reader["Finishes"]?.ToString() ?? string.Empty;

            if (card is CardItem cardItem)
            {
                cardItem.CardId = reader["CardId"] != DBNull.Value ? Convert.ToInt32(reader["CardId"]) : (int?)null;
                cardItem.CardsOwned = Convert.ToInt32(reader["CardsOwned"]);
                cardItem.SelectedCondition = reader["Condition"]?.ToString() ?? "Near Mint";
                cardItem.SelectedFinish = reader["Finishes"]?.ToString() ?? string.Empty;
            }


            return card;
        }
        private BitmapImage? ConvertImage(byte[]? imageData)
        {
            if (imageData != null)
            {
                return ConvertByteArrayToBitmapImage(imageData);
            }
            return null;
        }
        private string ProcessManaCost(string manaCostRaw)
        {
            return string.Join(",", manaCostRaw.Split(new[] { '{', '}' }, StringSplitOptions.RemoveEmptyEntries)).Trim(',');
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
                MessageBox.Show($"Error converting byte array to BitmapImage: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return null;
        }
        private Task FillComboBoxesAsync()
        {
            try
            {
                // Make sure lists are clear
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

                filterContext.AllColors.AddRange(new[] { "W", "U", "B", "R", "G", "C", "X" });

                var allOrNoneColorsOption = new List<string> { "Cards with any of these colors", "Cards with all of these colors", "Cards with none of these colors" };
                var manaValueOptions = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 1000000 };
                var manaValueCompareOptions = new List<string> { "less than", "less than/eq", "greater than", "greater than/eq", "equal to" };

                // Set up elements in supertype listbox
                filterContext.AllSuperTypes = superTypes
                    .Where(type => type != null)
                    .SelectMany(type => type!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    .Select(p => p.Trim())
                    .Distinct()
                    .OrderBy(type => type)
                    .ToList();

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
                filterContext.AllTypes = types
                    .Where(type => type != null)
                    .SelectMany(type => type!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    .Select(p => p.Trim())
                    .Where(p => !typesToRemove.Contains(p))  // Filter out unwanted types
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
                    FilterCardNameComboBox.ItemsSource = cardNames.OrderBy(name => name).ToList();
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

        #region UI elements for utilities
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
                StatusLabel.Content = message;
            });
        }
        private async void CreateBackupButton_Click(object sender, RoutedEventArgs e)
        {
            await BackupRestore.CreateCsvBackupAsync();
        }

        // Import wizard button methods
        private async void ImportCollectionButton_Click(object sender, RoutedEventArgs e)
        {
            // Select the csv-file and create a tempImport object with the content
            await BackupRestore.ImportCsvAsync();

            PopulateIdColumnMappingListView(IdColumnMappingListView);
            GridImportWizard.Visibility = Visibility.Visible;
            GridImportIdColumnMapping.Visibility = Visibility.Visible;
        }
        private async void ButtonIdColumnMappingNext_Click(object sender, RoutedEventArgs e)
        {
            await ProcessIdColumnMappingsAsync();

            DebugAllItems();

            if (AllItemsHaveUuid())
            {
                Debug.WriteLine("All items in tempImport have uuids");
                GoToAdditionalFieldsMapping();
            }
            else
            {
                Debug.WriteLine("Not all items in tempImport have uuids");
                // Prepare the listview to map card name, set name and set code and go to the first import wizard screen
                var cardSetFields = new List<string> { "Name", "Set Name", "Set Code" };
                PopulateColumnMappingListView(NameAndSetMappingListView, cardSetFields);
                GridImportNameAndSetMapping.Visibility = Visibility.Visible;
            }
            GridImportIdColumnMapping.Visibility = Visibility.Collapsed;

        }
        private async void ButtonNameAndSetMappingNext_Click(object sender, RoutedEventArgs e)
        {

            /*
            --Assumptions--
            An item in tempImport object can have a single uuid field with value
            An item in tempImport object can have a multiple uuids field with value

            Valid scenarios
             - A single item can have single uuid and no multiple uuids
             - A single item can have no uuid and multiple uuids
             - A single item can have no uuid and no multiple uuids

            Invalid scenario:
             - A single item has single uuid field or multiple uuids fields with no value
             - A single item has both single uuid and multiple uuids fields
 
            --Actions-- 
            Depending on different combinations, three possible actions should happen:

            1. Go to Multiple uuids mapping screen (if at least one item has multiple uuids)
            2. Go to Additional fields mapping screen (if at least one item has single uuid OR all items have single uuid AND no items have multiple uuids)
            0. Error screen (if no items have single uuid AND no item has multiple uuids)

            --Possible scenarios--
            No items have have single uuid, no items have multiple uuids --> 0. Error screen
            No items have single uuid, at least one item has multiple uuids --> 1. Go to Multiple uuids mapping screen
            All items have single uuid --> 2. Go to Additional fields mapping screen
            At least one item has single uuid, no items have multiple uuids --> 2. Go to Additional fields mapping screen
            At least one item has single uuids, at least one item has multiple uuids --> 1. Go to Multiple uuids mapping screen

            --Control Flow Pseudocode--
            Assert for invalid scenarios
            All items have single uuid?
	            True: 2. Go to Additional fields mapping screen
	            False:
		            At least one item has multiple uuids?
			            True: 1. Go to Multiple uuids screen select
			            False: 
				            At least one item has single uuid?
					            True: 2. Go to Additional Fields screen
					            False: 0. Error

            */

            var mappings = NameAndSetMappingListView.Items.Cast<ColumnMapping>().ToList();

            var nameMapping = mappings.FirstOrDefault(m => m.CardSetField == "Name")?.CsvHeader;
            var setNameMapping = mappings.FirstOrDefault(m => m.CardSetField == "Set Name")?.CsvHeader;
            var setCodeMapping = mappings.FirstOrDefault(m => m.CardSetField == "Set Code")?.CsvHeader;

            // Check if "Name" and either "Set Name" or "Set Code" are mapped
            if (string.IsNullOrEmpty(nameMapping) || (string.IsNullOrEmpty(setNameMapping) && string.IsNullOrEmpty(setCodeMapping)))
            {
                MessageBox.Show("Both name and either set name or set code must be set", "Mapping Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {

                // Search for unique uuids based on selected csv-headings for card name, set, and set code
                await SearchByCardNameOrSet(mappings);

                // Assert for invalid scenarios
                AssertNoInvalidUuidFields();

                // Do all items in tempImport have single uuid
                if (AllItemsHaveUuid())
                {
                    Debug.WriteLine("All items in tempImport have single uuid");
                    GoToAdditionalFieldsMapping();
                }
                else
                {
                    // Were multiple uuids found for any items in tempImport?
                    if (AnyItemWithMultipleUuidsField())
                    {
                        Debug.WriteLine("At least one item in tempImport has multiple uuids");
                        PopulateMultipleUuidsDataGrid();
                        GridImportMultipleUuidsSelection.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        // Ok then ... were single uuid found for ANY items in tempImport?
                        if (AnyItemWithUuidField())
                        {
                            Debug.WriteLine("At least one item in tempImport have single uuid");
                            GoToAdditionalFieldsMapping();
                        }
                        // If not, the import has failed
                        else
                        {
                            tempImport.Clear();
                            AddToCollectionManager.Instance.cardItemsToAdd.Clear();
                            GridImportWizard.Visibility = Visibility.Collapsed;
                            MessageBox.Show("Was not able to map any cards in the import file the main card database", "Import failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
                GridImportNameAndSetMapping.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing mapping using card and set name and set code: {ex.Message}");
                MessageBox.Show($"Error processing mapping using card and set name and set code: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void ButtonMultipleUuidsNext_Click(object sender, RoutedEventArgs e)
        {
            // Directly retrieve items from DataGrid
            var multipleUuidsItems = new List<MultipleUuidsItem>();
            bool allSelected = true;

            // Check that all dropdowns has a value selected
            foreach (var item in MultipleUuidsDataGrid.Items)
            {
                if (item is MultipleUuidsItem multipleUuidsItem)
                {
                    // Find the corresponding DataGridRow and ComboBox
                    DataGridRow row = (DataGridRow)MultipleUuidsDataGrid.ItemContainerGenerator.ContainerFromItem(multipleUuidsItem);
                    if (row != null)
                    {
                        ComboBox? comboBox = FindVisualChild<ComboBox>(row);
                        if (comboBox != null && comboBox.SelectedItem is UuidVersion selectedVersion)
                        {
                            multipleUuidsItem.SelectedUuid = selectedVersion.Uuid;
                        }
                        else
                        {
                            allSelected = false;
                        }
                    }
                    multipleUuidsItems.Add(multipleUuidsItem);
                }
            }

            // Check if all dropdowns have a selected value
            if (!allSelected)
            {
                MessageBox.Show("Please select a version for all cards before proceeding.", "Selection Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            GridImportMultipleUuidsSelection.Visibility = Visibility.Collapsed;

            DebugImportProcess(); // Debug before updating

            // Convert to List explicitly to ensure we have a concrete collection to work with
            var multipleUuidsList = multipleUuidsItems.ToList();

            // Update tempImport and cardItemsToAdd with the uuids for the selected versions of the cards
            ProcessMultipleUuidSelections(multipleUuidsList);

            // Prepare the listview to map additional fields and make the screen visible
            var cardSetFields = new List<string> { "SelectedCondition", "SelectedFinish", "CardsOwned", "CardsForTrade", "Language" };
            PopulateColumnMappingListView(AddionalFieldsMappingListView, cardSetFields);
            GridImportAdditionalFieldsMapping.Visibility = Visibility.Visible;
        }
        private async void ButtonAdditionalFieldsNext_Click(object sender, RoutedEventArgs e)
        {
            // Instantiate mappings variable
            _mappings = AddionalFieldsMappingListView.ItemsSource as List<ColumnMapping>;

            if (_mappings == null)
            {
                MessageBox.Show("No mappings found. Please ensure you have selected the appropriate mappings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Check if "SelectedCondition", "SelectedFinish", and "Card Quantity" have a value selected
            isConditionMapped = IsFieldMapped(_mappings, "SelectedCondition");
            isFinishMapped = IsFieldMapped(_mappings, "SelectedFinish");
            isCardsOwnedMapped = IsFieldMapped(_mappings, "CardsOwned");
            isCardsForTradedMapped = IsFieldMapped(_mappings, "CardsForTrade");
            isLanguageMapped = IsFieldMapped(_mappings, "Language");

            GridImportAdditionalFieldsMapping.Visibility = Visibility.Collapsed;

            if (isCardsOwnedMapped)
            {
                UpdateCardItemsWithQuantity("CardsOwned");
            }
            else
            {
                UpdateCardItemsWithDefaultField("CardsOwned", 1);
            }

            if (isCardsForTradedMapped)
            {
                UpdateCardItemsWithQuantity("CardsForTrade");
            }
            else
            {
                UpdateCardItemsWithDefaultField("CardsForTrade", 0);
            }

            if (isConditionMapped)
            {
                await GoToMappingGeneric("SelectedCondition", MainWindow.CurrentInstance.ConditionsMappingListView, "", MainWindow.CurrentInstance.GridImportCardConditionsMapping);
            }
            else
            {
                UpdateCardItemsWithDefaultField("SelectedCondition", "Near Mint");
                if (isFinishMapped)
                {
                    await GoToMappingGeneric("SelectedFinish", MainWindow.CurrentInstance.FinishesMappingListView, "finishes", MainWindow.CurrentInstance.GridImportFinishesMapping);
                }
                else
                {
                    UpdateCardItemsWithDefaultField("SelectedFinish", "nonfoil");
                    if (isLanguageMapped)
                    {
                        await GoToMappingGeneric("Language", MainWindow.CurrentInstance.LanguageMappingListView, "language", MainWindow.CurrentInstance.GridImportLanguageMapping);
                    }
                    else
                    {
                        UpdateCardItemsWithDefaultField("Language", "English");
                        GridImportConfirm.Visibility = Visibility.Visible;
                        DebugAllItems();
                    }
                }
            }
        }
        private async void ButtonConditionMappingNext_Click(object sender, RoutedEventArgs e)
        {
            BackupRestore.UpdateCardItemsWithMappedValues(MainWindow.CurrentInstance.ConditionsMappingListView, "SelectedCondition", "Near Mint");
            GridImportCardConditionsMapping.Visibility = Visibility.Collapsed;

            if (isFinishMapped) { await GoToMappingGeneric("SelectedFinish", MainWindow.CurrentInstance.FinishesMappingListView, "finishes", MainWindow.CurrentInstance.GridImportFinishesMapping); }
            else
            {
                BackupRestore.UpdateCardItemsWithDefaultField("SelectedFinish", "nonfoil");
                if (isLanguageMapped) { await GoToMappingGeneric("Language", MainWindow.CurrentInstance.LanguageMappingListView, "language", MainWindow.CurrentInstance.GridImportLanguageMapping); }
                else
                {
                    BackupRestore.UpdateCardItemsWithDefaultField("Language", "English");
                    GridImportConfirm.Visibility = Visibility.Visible;
                    DebugAllItems();
                }
            }
        }
        private async void ButtonFinishesMappingNext_Click(object sender, RoutedEventArgs e)
        {
            BackupRestore.UpdateCardItemsWithMappedValues(MainWindow.CurrentInstance.FinishesMappingListView, "SelectedFinish", "nonfoil");
            GridImportFinishesMapping.Visibility = Visibility.Collapsed;
            if (isLanguageMapped) { await GoToMappingGeneric("Language", MainWindow.CurrentInstance.LanguageMappingListView, "language", MainWindow.CurrentInstance.GridImportLanguageMapping); }
            else
            {
                BackupRestore.UpdateCardItemsWithDefaultField("Language", "English");
                GridImportConfirm.Visibility = Visibility.Visible;
                DebugAllItems();
            }
        }
        private void ButtonLanguageMappingNext_Click(object sender, RoutedEventArgs e)
        {
            BackupRestore.UpdateCardItemsWithMappedValues(MainWindow.CurrentInstance.LanguageMappingListView, "Language", "English");
            GridImportLanguageMapping.Visibility = Visibility.Collapsed;
            GridImportConfirm.Visibility = Visibility.Visible;
            DebugAllItems();
        }
        private void GoToAdditionalFieldsMapping()
        {
            // Prepare the listview to map additional fields and make the screen visible
            var cardSetFields = new List<string> { "SelectedCondition", "SelectedFinish", "CardsOwned", "CardsForTrade", "Language" };
            BackupRestore.PopulateColumnMappingListView(AddionalFieldsMappingListView, cardSetFields);
            GridImportAdditionalFieldsMapping.Visibility = Visibility.Visible;
        }
        public async Task GoToMappingGeneric(string cardSetField, ListView listView, string tableField, Grid grid)
        {
            var mapping = MainWindow.CurrentInstance._mappings?.FirstOrDefault(m => m.CardSetField == cardSetField);
            if (mapping != null && !string.IsNullOrEmpty(mapping.CsvHeader))
            {
                await BackupRestore.InitializeMappingListViewAsync(mapping.CsvHeader, !string.IsNullOrEmpty(tableField), tableField, listView);
                grid.Visibility = Visibility.Visible;
            }
            else
            {
                Debug.WriteLine($"Mapping for {cardSetField} not found.");
            }
        }
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

        #endregion

        #region Top menu navigation
        private void MenuSearchAndFilter_Click(object sender, RoutedEventArgs e)
        {
            GridContentSection.Visibility = Visibility.Visible;
            GridUtilitiesSection.Visibility = Visibility.Collapsed;
            ResetGrids();
            GridSearchAndFilter.Visibility = Visibility.Visible;
        }
        private void MenuMyCollection_Click(object sender, RoutedEventArgs e)
        {
            ResetGrids();
            GridContentSection.Visibility = Visibility.Visible;
            GridUtilitiesSection.Visibility = Visibility.Collapsed;
            GridMyCollection.Visibility = Visibility.Visible;
        }
        private void MenuUtilsButton_Click(object sender, RoutedEventArgs e)
        {
            ResetGrids();
            //GridContentSection.Visibility = Visibility.Collapsed;
            GridFiltering.Visibility = Visibility.Collapsed;
            GridUtilsMenu.Visibility = Visibility.Visible;
            GridUtilitiesSection.Visibility = Visibility.Visible;
        }

        public void ResetGrids()
        {
            EditStatusTextBlock.Text = string.Empty;
            AddStatusTextBlock.Text = string.Empty;
            UtilsInfoLabel.Content = "";
            GridSearchAndFilter.Visibility = Visibility.Hidden;
            GridMyCollection.Visibility = Visibility.Hidden;
            GridUtilitiesSection.Visibility = Visibility.Hidden;
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
                        CurrentInstance.MenuSearchAndFilterButton.IsEnabled = false;
                        CurrentInstance.MenuMyCollectionButton.IsEnabled = false;
                        CurrentInstance.MenuDecksButton.IsEnabled = false;
                        CurrentInstance.MenuUtilsButton.IsEnabled = false;

                        // Show status section and hide others
                        CurrentInstance.GridContentSection.Visibility = Visibility.Hidden;
                        CurrentInstance.GridUtilitiesSection.Visibility = Visibility.Hidden;
                        CurrentInstance.GridStatus.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        CurrentInstance.MenuSearchAndFilterButton.IsEnabled = true;
                        CurrentInstance.MenuMyCollectionButton.IsEnabled = true;
                        CurrentInstance.MenuDecksButton.IsEnabled = true;
                        CurrentInstance.MenuUtilsButton.IsEnabled = true;

                        CurrentInstance.GridStatus.Visibility = Visibility.Hidden;
                        CurrentInstance.GridContentSection.Visibility = Visibility.Visible;

                    }
                });
            }
        }


    }
}