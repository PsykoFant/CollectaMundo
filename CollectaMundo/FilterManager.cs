using CollectaMundo.Models;
using ServiceStack;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static CollectaMundo.MainWindow;
using static CollectaMundo.Models.CardSet;

namespace CollectaMundo
{
    public class FilterManager
    {
        #region Filtering
        public static void ApplyFilter(IEnumerable<CardSet> cards, DataGrid dataGrid)
        {
            try
            {
                if (MainWindow.CurrentInstance._isStartup)
                {
                    return;
                }

                // Build filter criteria for multiple properties
                var filterCriteriaMultiple = MainWindow.CurrentInstance.filterSelections
                    .Where(fs => fs.MultipleCriteria != null && fs.MultipleCriteria.Count > 0)
                    .ToDictionary(
                        fs => fs.CriteriaKey!,
                        fs => (
                            ResolveStringPropertySelector(fs.CriteriaKey!),
                            fs.MultipleCriteria!,
                            (int)fs.Operator
                        )
                    );

                // Build filter criteria for single properties
                var singleFilterCriteria = MainWindow.CurrentInstance.filterSelections
                    .Where(fs => !string.IsNullOrWhiteSpace(fs.SingleCriteria))
                    .ToDictionary(
                        fs => fs.CriteriaKey!,
                        fs => (
                            propertySelector: ResolveStringPropertySelector(fs.CriteriaKey!),
                            selectedValue: fs.SingleCriteria!
                        )
                    ) as Dictionary<string, (Func<CardSet, string?> propertySelector, string? selectedValue)>;

                // Build filter criteria for numeric properties only
                var numericCriteriaKeys = new[] { "ManaValue" }; // Add all numeric CriteriaKeys here
                var numberCriteria = MainWindow.CurrentInstance.filterSelections
                    .Where(fs => numericCriteriaKeys.Contains(fs.CriteriaKey) && fs.NumberCriteria != 0)
                    .ToDictionary(
                        fs => fs.CriteriaKey!,
                        fs => (
                            ResolveNumericPropertySelector(fs.CriteriaKey!),
                            (fs.NumberCriteria, fs.Operator)
                        )
                    );


                // Apply filters
                var filteredCards = FilterByMultipleProperties(cards, filterCriteriaMultiple);
                filteredCards = FilterBySingleProperty(filteredCards, singleFilterCriteria);
                filteredCards = FilterByNumber(filteredCards, numberCriteria);

                var finalFilteredCards = filteredCards.ToList();

                // Update the DataGrid
                SaveAndRestoreSort(dataGrid, () =>
                {
                    dataGrid.ItemsSource = finalFilteredCards;
                });

                UpdateCardCount(dataGrid.Name, finalFilteredCards.Count);
                UpdateFilterSummary(filterCriteriaMultiple, singleFilterCriteria);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while filtering datagrid: {ex.Message}");
                _ = MessageBox.Show($"Error while filtering datagrid: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            static Func<CardSet, string?> ResolveStringPropertySelector(string criteriaKey)
            {
                var property = typeof(CardSet).GetProperty(criteriaKey)
                              ?? typeof(CardInCollection).GetProperty(criteriaKey)
                              ?? typeof(CardInDeck).GetProperty(criteriaKey);

                if (property == null)
                {
                    throw new InvalidOperationException($"Property '{criteriaKey}' not found on any supported types.");
                }

                return card =>
                {
                    try
                    {
                        return property.GetValue(card) as string;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error accessing property '{criteriaKey}' on '{card.GetType()}': {ex.Message}");
                        return null;
                    }
                };
            }


            static Func<CardSet, double?> ResolveNumericPropertySelector(string criteriaKey)
            {
                // Handle numeric values, explicitly checking for valid conversion
                var property = typeof(CardSet).GetProperty(criteriaKey)
                              ?? typeof(CardInCollection).GetProperty(criteriaKey)
                              ?? typeof(CardInDeck).GetProperty(criteriaKey);

                if (property == null || property.PropertyType != typeof(double))
                {
                    Debug.WriteLine($"Numeric property '{criteriaKey}' not found or not of type double.");
                    return _ => null;
                }

                return card =>
                {
                    try
                    {
                        return property.GetValue(card) as double?;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error accessing numeric property '{criteriaKey}': {ex.Message}");
                        return null;
                    }
                };
            }

        }
        private static IEnumerable<CardSet> FilterBySingleProperty(IEnumerable<CardSet> cards, Dictionary<string, (Func<CardSet, string?> propertySelector, string? selectedValue)> singleFilterCriteria)
        {
            if (cards == null || singleFilterCriteria == null || singleFilterCriteria.Count == 0)
            {
                return cards!; // Return unfiltered if no criteria
            }

            return cards.Where(card =>
            {
                foreach (var (_, (propertySelector, selectedValue)) in singleFilterCriteria)
                {
                    if (!string.IsNullOrWhiteSpace(selectedValue))
                    {
                        var propertyValue = propertySelector(card) ?? string.Empty;

                        if (!propertyValue.Contains(selectedValue, StringComparison.OrdinalIgnoreCase))
                        {
                            return false; // Exclude card if it doesn't match the single-value filter
                        }
                    }
                }

                return true; // Include card if all single-value filters match
            });
        }
        private static IEnumerable<CardSet> FilterByMultipleProperties(IEnumerable<CardSet> cards, Dictionary<string, (Func<CardSet, string?> propertySelector, HashSet<string> selectedCriteria, int filterMode)> filterCriteria)
        {
            if (cards == null || filterCriteria == null || filterCriteria.Count == 0)
            {
                return cards!;
            }

            return cards.Where(card =>
            {
                foreach (var (_, (propertySelector, selectedCriteria, filterMode)) in filterCriteria)
                {
                    // Skip filters with no selected criteria
                    if (selectedCriteria == null || selectedCriteria.Count == 0)
                    {
                        continue;
                    }

                    // Apply filtering logic based on the mode
                    bool matches = filterMode switch
                    {
                        0 => MatchesCriteria(card, propertySelector, selectedCriteria),         // OR Mode
                        1 => selectedCriteria.All(c => MatchesCriteria(card, propertySelector, new HashSet<string> { c })), // AND Mode
                        2 => !MatchesCriteria(card, propertySelector, selectedCriteria),        // NOT Mode
                        _ => false
                    };

                    // If a filter fails in AND mode, exclude the card
                    if (!matches)
                    {
                        return false;
                    }
                }

                return true;
            });

            static bool MatchesCriteria(CardSet card, Func<CardSet, string?> propertySelector, HashSet<string> filterValues)
            {
                var propertyValue = propertySelector(card) ?? string.Empty;

                var propertyItems = new HashSet<string>(
                    propertyValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                 .Select(p => p.Trim())
                );

                return filterValues.Any(filterValue => propertyItems.Contains(filterValue));
            }
        }
        private static IEnumerable<CardSet> FilterByNumber(IEnumerable<CardSet> cards, Dictionary<string, (Func<CardSet, double?> propertySelector, (double value, OperatorType operatorType))> numberCriteria)
        {
            if (cards == null || numberCriteria == null || numberCriteria.Count == 0)
            {
                return cards!; // Return unfiltered if no criteria
            }

            return cards.Where(card =>
            {
                foreach (var (criteriaKey, (propertySelector, (value, operatorType))) in numberCriteria)
                {
                    var propertyValue = propertySelector(card);

                    if (propertyValue == null)
                    {
                        return false; // Exclude cards with null values
                    }

                    bool matches = operatorType switch
                    {
                        OperatorType.LESS_THAN => propertyValue < value,
                        OperatorType.LESS_THAN_OR_EQUALS => propertyValue <= value,
                        OperatorType.GREATER_THAN => propertyValue > value,
                        OperatorType.GREATER_THAN_OR_EQUALS => propertyValue >= value,
                        OperatorType.EQUALS => Math.Abs(propertyValue.Value - value) < 0.0001,
                        OperatorType.NOT_EQUALS => Math.Abs(propertyValue.Value - value) >= 0.0001,
                        _ => false
                    };

                    if (!matches)
                    {
                        return false; // Exclude card if the condition fails
                    }
                }

                return true; // Include card if all conditions are met
            });
        }



        public static void DebugFilterSelections(List<FilterSelections> filterSelections)
        {
            try
            {
                Debug.WriteLine("FilterSelections Debug Output:");
                Debug.WriteLine(new string('-', 50));

                foreach (var filter in filterSelections)
                {
                    Debug.WriteLine($"CriteriaKey: {filter.CriteriaKey}");
                    Debug.WriteLine($"Operator: {filter.Operator}");
                    Debug.WriteLine($"SingleCriteria: {filter.SingleCriteria ?? "null"}");
                    Debug.WriteLine($"NumberCriteria: {filter.NumberCriteria}");

                    if (filter.MultipleCriteria != null && filter.MultipleCriteria.Count > 0)
                    {
                        Debug.WriteLine("MultipleCriteria: " + string.Join(", ", filter.MultipleCriteria));
                    }
                    else
                    {
                        Debug.WriteLine("MultipleCriteria: Empty or null");
                    }

                    Debug.WriteLine(new string('-', 50));
                }

                Debug.WriteLine("End of FilterSelections Debug Output");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in DebugFilterSelections: {ex.Message}");
            }
        }


        //filteredCards = FilterByColor(filteredCards, MainWindow.CurrentInstance.filterSelections.SelectedColors, MainWindow.CurrentInstance.filterSelections.ColorOperator);

        private static IEnumerable<CardSet> FilterByManaValue(IEnumerable<CardSet> cards)
        {
            // Retrieve filter parameters from the UI
            string compareOperator = MainWindow.CurrentInstance.ManaValueOperatorComboBox.SelectedItem?.ToString() ?? string.Empty;

            if (!double.TryParse(MainWindow.CurrentInstance.ManaValueComboBox.SelectedItem?.ToString(), out double manaValueCompare))
            {
                Debug.WriteLine("Invalid mana value comparison value. Defaulting to 0.");
                manaValueCompare = 0; // Default to 0 if parsing fails
            }

            // No filtering if the operator is not specified
            if (string.IsNullOrEmpty(compareOperator))
            {
                return cards;
            }

            // Perform filtering
            return cards.Where(card => compareOperator switch
            {
                "less than" => card.ManaValue < manaValueCompare,
                "greater than" => card.ManaValue > manaValueCompare,
                "less than/eq" => card.ManaValue <= manaValueCompare,
                "greater than/eq" => card.ManaValue >= manaValueCompare,
                "equal to" => card.ManaValue == manaValueCompare,
                _ => true // Default: no filtering
            });
        }


        //private static IEnumerable<CardSet> ApplyMyCardsSpecificFilters(IEnumerable<CardSet> cards)
        //{
        //    var filteredCardItems = cards.OfType<CardInCollection>();

        //    // Handle "Cards for Trade" and "Cards Not for Trade"
        //    bool showForTrade = MainWindow.CurrentInstance.CheckBoxCardsForTrade.IsChecked ?? false;
        //    bool showNotForTrade = MainWindow.CurrentInstance.CheckBoxCardsNotForTrade.IsChecked ?? false;

        //    if (showForTrade)
        //    {
        //        filteredCardItems = filteredCardItems.Where(card => card.CardsForTrade > 0);
        //    }

        //    if (showNotForTrade)
        //    {
        //        filteredCardItems = filteredCardItems.Where(card => card.CardsForTrade == 0);
        //    }

        //    // Apply language filter
        //    var languageFilteredItems = FilterByCardProperty(filteredCardItems.Cast<CardSet>(), MainWindow.CurrentInstance.filterSelections.SelectedLanguages, false, card => card.Language);

        //    return languageFilteredItems.OfType<CardInCollection>().Cast<CardSet>();
        //}
        public static IEnumerable<CardSet> FilterByColor(IEnumerable<CardSet> cards, HashSet<string> selectedColors, int filterMode)
        {
            if (cards == null)
            {
                Debug.WriteLine("Warning: Card collection is null.");
                return []; // Return an empty collection instead of null
            }

            if (selectedColors == null || selectedColors.Count == 0)
            {
                return cards; // Cards are returned unfiltered when no colors are selected
            }

            return cards.Where(card =>
            {
                // Prepare collections for easier matching
                var manaCostSymbols = new HashSet<string>(card.ManaCost?.Split(',').Select(c => c.Trim()) ?? []);
                var colorSymbols = new HashSet<string>(card.Colors?.Split(',').Select(c => c.Trim()) ?? []);

                bool manaCostMatch = false;
                bool colorMatch = false;

                // Check for "C" and "X" in ManaCost
                if (selectedColors.Contains("C") || selectedColors.Contains("X"))
                {
                    var manaCostCriteria = new HashSet<string>(selectedColors.Intersect(["C", "X"]));
                    manaCostMatch = manaCostCriteria.All(manaCostSymbols.Contains);
                }

                // Check for colored mana and "Colorless" in Colors
                bool colorlessMatch = selectedColors.Contains("Colorless") && string.IsNullOrWhiteSpace(card.Colors);
                if (selectedColors.Overlaps(["W", "U", "B", "R", "G", "Colorless"]))
                {
                    var coloredCriteria = new HashSet<string>(selectedColors.Where(c => c != "Colorless"));
                    colorMatch = coloredCriteria.All(colorSymbols.Contains) || colorlessMatch;
                }

                // ✅ FIX: Stricter "ALL" logic to enforce both conditions simultaneously
                return filterMode switch
                {
                    0 => selectedColors.Any(c => manaCostSymbols.Contains(c) || colorSymbols.Contains(c) ||
                            (c == "Colorless" && string.IsNullOrWhiteSpace(card.Colors))), // ANY
                    1 => selectedColors.All(c =>
                            (c == "Colorless" && string.IsNullOrWhiteSpace(card.Colors)) ||
                            manaCostSymbols.Contains(c) ||
                            colorSymbols.Contains(c)), // ALL: Both conditions must be met
                    2 => !selectedColors.Any(c => manaCostSymbols.Contains(c) || colorSymbols.Contains(c) ||
                            (c == "Colorless" && string.IsNullOrWhiteSpace(card.Colors))), // NONE
                    _ => false
                };
            });
        }

        #endregion

        #region Filter UI updates
        private static void UpdateCardCount(string datagridName, int count)
        {
            if (datagridName == "AllCardsDataGrid")
            {
                MainWindow.CurrentInstance.AllCardsCountLabel.Content = $"Showing: {count} cards out of total {MainWindow.CurrentInstance.allCards.Count} cards.";
            }
            else if (datagridName == "MyCollectionDataGrid")
            {
                MainWindow.CurrentInstance.MyCardsCountLabel.Content = $"Showing: {count} cards out of total {MainWindow.CurrentInstance.myCards.Count} cards in your collection.";
            }
        }
        private static void UpdateFilterSummary(Dictionary<string, (Func<CardSet, string?> propertySelector, HashSet<string> selectedCriteria, int filterMode)> multipleFilterCriteria, Dictionary<string, (Func<CardSet, string?> propertySelector, string? selectedValue)> singleFilterCriteria)
        {
            try
            {
                StringBuilder filterSummary = new();

                // Add single property filters
                foreach (var (key, (_, selectedValue)) in singleFilterCriteria)
                {
                    if (!string.IsNullOrWhiteSpace(selectedValue))
                    {
                        filterSummary.Append($"{key}: \"{selectedValue}\" AND ");
                    }
                }

                // Add multiple property filters
                foreach (var (filterKey, (propertySelector, selectedCriteria, filterMode)) in multipleFilterCriteria)
                {
                    if (selectedCriteria == null || selectedCriteria.Count == 0)
                    {
                        continue;
                    }

                    string operatorSymbol = filterMode switch
                    {
                        0 => "OR",
                        1 => "AND",
                        2 => "NOT",
                        _ => string.Empty
                    };

                    string filterSegment = filterMode == 2
                        ? string.Join(", ", selectedCriteria.Select(c => $"NOT {c}"))
                        : string.Join($" {operatorSymbol} ", selectedCriteria);

                    filterSummary.Append($"{{{filterSegment}}} AND ");
                }

                if (filterSummary.Length > 5)
                {
                    filterSummary.Remove(filterSummary.Length - 5, 5); // Remove trailing " AND "
                }

                MainWindow.CurrentInstance.FilterSummaryTextBlock.Text = filterSummary.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while updating filter summary: {ex.Message}");
            }
        }


        // Update the object to which the width of the combobox is bound
        public static void DataGrid_LayoutUpdated(int dataGridIndex)
        {
            if (dataGridIndex < 0 || dataGridIndex >= MainWindow.CurrentInstance.ColumnWidths.Count)
            {
                return; // Protect against out-of-range errors
            }

            // Define paddings for each datagrid. Ensure this list matches the number of columns for each DataGrid.
            List<int[]> paddingsList =
            [
                [65, 50], // Paddings for AllCardsDataGrid
                [65, 50], // Paddings for MyCollectionDataGrid
                [65]      // Padding for AllCardsForDecksDataGrid (only one column to adjust)
            ];

            if (dataGridIndex >= paddingsList.Count)
            {
                return; // Protect against out-of-range errors when accessing paddingsList
            }

            var paddings = paddingsList[dataGridIndex];
            DataGrid currentDataGrid = dataGridIndex switch
            {
                0 => MainWindow.CurrentInstance.AllCardsDataGrid,
                1 => MainWindow.CurrentInstance.MyCollectionDataGrid,
                2 => MainWindow.CurrentInstance.AllCardsForDecksDataGrid,
                _ => throw new ArgumentOutOfRangeException(nameof(dataGridIndex), "Invalid DataGrid index.")
            };

            if (currentDataGrid == null)
            {
                return;
            }

            for (int colIndex = 0; colIndex < paddings.Length; colIndex++)
            {
                if (colIndex >= MainWindow.CurrentInstance.ColumnWidths[dataGridIndex].Count || colIndex >= paddings.Length)
                {
                    continue; // Protect against out-of-range errors when column widths or paddings list is shorter than the number of actual columns
                }

                double currentWidth = currentDataGrid.Columns[colIndex].ActualWidth;
                double newWidth = currentWidth - paddings[colIndex]; // Apply specific padding for each column

                // Check for a significant change before updating
                if (newWidth > 0 && Math.Abs(MainWindow.CurrentInstance.ColumnWidths[dataGridIndex][colIndex] - newWidth) > 0.5)
                {
                    MainWindow.CurrentInstance.ColumnWidths[dataGridIndex][colIndex] = newWidth;
                }
            }
        }

        // Save column sort selections
        public static void SaveAndRestoreSort(DataGrid dataGrid, Action updateItemsSource)
        {
            // Step 1: Save current sort descriptions
            var sortDescriptions = dataGrid.Items.SortDescriptions.ToList();
            var sortedColumns = dataGrid.Columns
                .Where(column => column.SortDirection.HasValue)
                .ToDictionary(column => column, column => column.SortDirection);

            // Step 2: Perform the update (reset ItemsSource)
            updateItemsSource?.Invoke();

            // Step 3: Restore sort descriptions
            dataGrid.Items.SortDescriptions.Clear();
            foreach (var sortDescription in sortDescriptions)
            {
                dataGrid.Items.SortDescriptions.Add(sortDescription);
            }

            // Restore column sort directions
            foreach (var column in sortedColumns)
            {
                column.Key.SortDirection = column.Value;
            }
        }

        #endregion

        #region Filter Helper Methods
        /// <summary>
        /// Generic method for getting the data to populate the listbox with, including already selected items
        /// </summary>
        /// <param name="listBoxName"></param>
        /// <param name="filterSelections"></param>
        /// <param name="filterDefaults"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static (IEnumerable<string> items, HashSet<string> selectedItems) GetDataSetAndSelection(string listBoxName, List<FilterSelections> filterSelections, List<FilterDefaults> filterDefaults)
        {
            IEnumerable<string> itemsSource;
            HashSet<string> selectedItemsSet;

            var filterDefault = filterDefaults.FirstOrDefault(fd => $"Filter{fd.CriteriaKey}ListBox" == listBoxName);
            if (filterDefault != null)
            {
                itemsSource = filterDefault.AllCriteria;
                selectedItemsSet = filterSelections.FirstOrDefault(fs => fs.CriteriaKey == filterDefault.CriteriaKey)?.MultipleCriteria ?? [];
            }
            else
            {
                throw new InvalidOperationException($"ListBox name not recognized: {listBoxName}");
            }

            return (itemsSource.Distinct().OrderBy(type => type).ToList(), selectedItemsSet);
        }

        /// <summary>
        /// Get default text, textbox name and listboxname for a custom combobox
        /// </summary>
        /// <param name="comboBoxName"></param>
        /// <param name="filterDefaults"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static (string defaultText, string textBoxName, string listBoxName) GetComboBoxConfig(string comboBoxName, List<FilterDefaults> filterDefaults)
        {
            // Extract the CriteriaKey from the ComboBox name
            var criteriaKey = comboBoxName.Replace("ComboBox", "");

            // Find the matching FilterDefaults object
            var filterDefault = filterDefaults.FirstOrDefault(fd => fd.CriteriaKey == criteriaKey);

            if (filterDefault != null)
            {
                // Dynamically construct TextBox and ListBox names
                string textBoxName = $"Filter{criteriaKey}TextBox";
                string listBoxName = $"Filter{criteriaKey}ListBox";
                string defaultText = filterDefault.DefaultText ?? $"Filter {criteriaKey} ...";

                return (defaultText, textBoxName, listBoxName);
            }

            throw new InvalidOperationException($"Configuration not found for ComboBox: {comboBoxName}");
        }

        /// <summary>
        /// Finds the parent for an object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="child"></param>
        /// <returns></returns>
        public static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject? parentObject = VisualTreeHelper.GetParent(child);

            while (parentObject != null && parentObject is not T)
            {
                parentObject = VisualTreeHelper.GetParent(parentObject);
            }

            return parentObject as T;
        }

        /// <summary>
        /// Find children of a dependency object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static T? FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
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

        /// <summary>
        /// Find all children of a dependency object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="depObj"></param>
        /// <returns></returns>
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
    }
}
