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
                        m.trade AS CardsForTrade,
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
                        m.trade AS CardsForTrade,
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

            await DBAccess.OpenConnectionAsync();

            // Start on the all cards page            
            ResetGrids();
            GridSearchAndFilterAllCards.Visibility = Visibility.Visible;
            GridFiltering.Visibility = Visibility.Visible;

            await LoadDataAsync(allCards, allCardsQuery, AllCardsDataGrid, false);
            await LoadDataAsync(myCards, myCollectionQuery, MyCollectionDatagrid, true);
            await FillComboBoxesAsync();

            DBAccess.CloseConnection();

            CardsToAddListView.ItemsSource = addToCollectionManager.cardItemsToAdd;
            CardsToEditListView.ItemsSource = addToCollectionManager.cardItemsToEdit;
        }

        /* To do
         * Bugs:
         *      håndter sprog, hvis man forsøger at importere kort, der ikke er på det sprog
         *      Tilføj kort fra alle korts filter - håndter trade
         *      plus og minus til trade...
         * Performance optimer load kort         
         * Refaktorer installer oprettelse
         */

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
            ImagePromoLabel.Content = string.Empty;
            ImageSetLabel.Content = string.Empty;
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
        private async void CardImageSelectionChangedHandler(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid dataGrid && dataGrid.SelectedItem is CardSet selectedCard)
            {
                if (selectedCard.Uuid != null)
                {
                    Debug.WriteLine($"Trying to show image with uuid: {selectedCard.Uuid}");
                    await ShowCardImage.ShowImage(selectedCard.Uuid, selectedCard.Types);
                }
            }
            else if (sender is ComboBox comboBox && comboBox.SelectedItem is UuidVersion selectedVersion && !string.IsNullOrEmpty(selectedVersion.Uuid))
            {
                Debug.WriteLine($"Trying to show image with uuid: {selectedVersion.Uuid}");
                await ShowCardImage.ShowImage(selectedVersion.Uuid);
            }
        }

        #endregion

        #region Pick up events for add to or edit collection 
        private void IncrementCount_Click(object sender, RoutedEventArgs e)
        {
            addToCollectionManager.IncrementCountHandler(sender, e);
        }
        private void DecrementCount_Click(object sender, RoutedEventArgs e)
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
                            addToCollectionManager.DecrementCountHandler(sender, e, targetCollection);
                        }
                    }
                    else
                    {
                        addToCollectionManager.DecrementCountHandler(sender, e, targetCollection);

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
        private void CardsOwnedTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            addToCollectionManager.CardsOwnedTextHandler(sender, e, addToCollectionManager.cardItemsToAdd);
        }
        private void CardsForTradeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            addToCollectionManager.CardsForTradeTextHandler(sender, e, addToCollectionManager.cardItemsToAdd);
        }

        private void AddCardToCollection_Click(object sender, RoutedEventArgs e)
        {
            AddStatusTextBlock.Visibility = Visibility.Collapsed;
            CardsToAddListView.Visibility = Visibility.Visible;
            ButtonAddCardsToMyCollection.Visibility = Visibility.Visible;
            addToCollectionManager.AddOrEditCardHandler(sender, e, addToCollectionManager.cardItemsToAdd);
        }
        private void EditCardInCollection_Click(object sender, RoutedEventArgs e)
        {
            EditStatusTextBlock.Visibility = Visibility.Collapsed;
            CardsToEditListView.Visibility = Visibility.Visible;
            ButtonEditCardsInMyCollection.Visibility = Visibility.Visible;
            addToCollectionManager.AddOrEditCardHandler(sender, e, addToCollectionManager.cardItemsToEdit);
        }
        private void ListViewComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AddToCollectionManager.AdjustColumnWidths();
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
            card.Finishes = reader["Finishes"]?.ToString();

            if (card is CardItem cardItem)
            {
                cardItem.CardId = reader["CardId"] != DBNull.Value ? Convert.ToInt32(reader["CardId"]) : (int?)null;
                cardItem.CardsOwned = Convert.ToInt32(reader["CardsOwned"]);
                cardItem.CardsForTrade = Convert.ToInt32(reader["CardsForTrade"]);
                cardItem.SelectedCondition = reader["Condition"]?.ToString();
                cardItem.SelectedFinish = reader["Finishes"]?.ToString();
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
            GridFiltering.Visibility = Visibility.Visible;
            GridSearchAndFilterAllCards.Visibility = Visibility.Visible;
            AddToCollectionManager.AdjustColumnWidths();
        }
        private void MenuMyCollection_Click(object sender, RoutedEventArgs e)
        {
            ResetGrids();
            GridFiltering.Visibility = Visibility.Visible;
            GridMyCollection.Visibility = Visibility.Visible;
            AddToCollectionManager.AdjustColumnWidths();
        }
        private void MenuUtilsButton_Click(object sender, RoutedEventArgs e)
        {
            ResetGrids();
            GridUtilsMenu.Visibility = Visibility.Visible;
            GridUtilitiesSection.Visibility = Visibility.Visible;
        }
        public void ResetGrids()
        {
            EditStatusTextBlock.Text = string.Empty;
            AddStatusTextBlock.Text = string.Empty;
            UtilsInfoLabel.Content = "";
            GridSearchAndFilterAllCards.Visibility = Visibility.Collapsed;
            GridMyCollection.Visibility = Visibility.Collapsed;
            GridUtilitiesSection.Visibility = Visibility.Collapsed;

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
                        CurrentInstance.MenuSearchAndFilterButton.IsEnabled = false;
                        CurrentInstance.MenuMyCollectionButton.IsEnabled = false;
                        CurrentInstance.MenuDecksButton.IsEnabled = false;
                        CurrentInstance.MenuUtilsButton.IsEnabled = false;

                        // Show status section and hide others
                        CurrentInstance.GridContentSection.Visibility = Visibility.Collapsed;
                        CurrentInstance.GridUtilitiesSection.Visibility = Visibility.Collapsed;
                        CurrentInstance.GridStatus.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        CurrentInstance.MenuSearchAndFilterButton.IsEnabled = true;
                        CurrentInstance.MenuMyCollectionButton.IsEnabled = true;
                        CurrentInstance.MenuDecksButton.IsEnabled = true;
                        CurrentInstance.MenuUtilsButton.IsEnabled = true;

                        CurrentInstance.GridStatus.Visibility = Visibility.Collapsed;
                        CurrentInstance.GridContentSection.Visibility = Visibility.Visible;

                    }
                });
            }
        }
    }
}