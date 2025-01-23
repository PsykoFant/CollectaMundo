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

                // Create the filter criteria dictionary
                var filterCriteriaDictionary = new FilterCriteriaDictionary(MainWindow.CurrentInstance.filterSelections);
                // Apply filters
                var filteredCards = FilterCardsByUnifiedCriteria(cards, filterCriteriaDictionary);

                var finalFilteredCards = filteredCards.ToList();

                // Update the DataGrid
                SaveAndRestoreSort(dataGrid, () =>
                {
                    dataGrid.ItemsSource = finalFilteredCards;
                });

                UpdateCardCount(dataGrid.Name, finalFilteredCards.Count);
                UpdateFilterSummary(filterCriteriaDictionary);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while filtering datagrid: {ex.Message}");
                _ = MessageBox.Show($"Error while filtering datagrid: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private static IEnumerable<CardSet> FilterCardsByUnifiedCriteria(IEnumerable<CardSet> cards, FilterCriteriaDictionary filterCriteriaDictionary)
        {
            return cards.Where(card =>
            {
                foreach (var (key, (stringSelector, numericSelector, multiCriteria, singleCriteria, numCriteria, operatorType)) in filterCriteriaDictionary.Criteria)
                {
                    // Apply multiple criteria
                    if (multiCriteria != null && multiCriteria.Count > 0)
                    {
                        bool matches = operatorType switch
                        {
                            OperatorType.OR => multiCriteria.Any(c => MatchesCriteria(card, stringSelector, c)),
                            OperatorType.AND => multiCriteria.All(c => MatchesCriteria(card, stringSelector, c)),
                            OperatorType.NOT => !multiCriteria.Any(c => MatchesCriteria(card, stringSelector, c)),
                            _ => true
                        };

                        if (!matches) return false;
                    }

                    // Apply single criteria
                    if (!string.IsNullOrWhiteSpace(singleCriteria))
                    {
                        var propertyValue = stringSelector?.Invoke(card) ?? string.Empty;
                        if (!propertyValue.Contains(singleCriteria, StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                    }

                    // Apply numeric criteria
                    if (numCriteria.HasValue)
                    {
                        var (value, opType) = numCriteria.Value;
                        var propertyValue = numericSelector?.Invoke(card);
                        if (!EvaluateNumericCondition(propertyValue, value, opType))
                        {
                            return false;
                        }
                    }
                }

                return true;
            });
        }
        private static bool MatchesCriteria(CardSet card, Func<CardSet, string?>? selector, string value)
        {
            return selector?.Invoke(card)?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;
        }
        private static bool EvaluateNumericCondition(double? propertyValue, double value, OperatorType operatorType)
        {
            if (!propertyValue.HasValue) return false;

            return operatorType switch
            {
                OperatorType.LESS_THAN => propertyValue < value,
                OperatorType.LESS_THAN_OR_EQUALS => propertyValue <= value,
                OperatorType.GREATER_THAN => propertyValue > value,
                OperatorType.GREATER_THAN_OR_EQUALS => propertyValue >= value,
                OperatorType.EQUALS => Math.Abs(propertyValue.Value - value) < 0.0001,
                OperatorType.NOT_EQUALS => Math.Abs(propertyValue.Value - value) >= 0.0001,
                _ => false
            };
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
        //public static IEnumerable<CardSet> FilterByColor(IEnumerable<CardSet> cards, HashSet<string> selectedColors, int filterMode)
        //{
        //    if (cards == null)
        //    {
        //        Debug.WriteLine("Warning: Card collection is null.");
        //        return []; // Return an empty collection instead of null
        //    }

        //    if (selectedColors == null || selectedColors.Count == 0)
        //    {
        //        return cards; // Cards are returned unfiltered when no colors are selected
        //    }

        //    return cards.Where(card =>
        //    {
        //        // Prepare collections for easier matching
        //        var manaCostSymbols = new HashSet<string>(card.ManaCost?.Split(',').Select(c => c.Trim()) ?? []);
        //        var colorSymbols = new HashSet<string>(card.Colors?.Split(',').Select(c => c.Trim()) ?? []);

        //        bool manaCostMatch = false;
        //        bool colorMatch = false;

        //        // Check for "C" and "X" in ManaCost
        //        if (selectedColors.Contains("C") || selectedColors.Contains("X"))
        //        {
        //            var manaCostCriteria = new HashSet<string>(selectedColors.Intersect(["C", "X"]));
        //            manaCostMatch = manaCostCriteria.All(manaCostSymbols.Contains);
        //        }

        //        // Check for colored mana and "Colorless" in Colors
        //        bool colorlessMatch = selectedColors.Contains("Colorless") && string.IsNullOrWhiteSpace(card.Colors);
        //        if (selectedColors.Overlaps(["W", "U", "B", "R", "G", "Colorless"]))
        //        {
        //            var coloredCriteria = new HashSet<string>(selectedColors.Where(c => c != "Colorless"));
        //            colorMatch = coloredCriteria.All(colorSymbols.Contains) || colorlessMatch;
        //        }

        //        // ✅ FIX: Stricter "ALL" logic to enforce both conditions simultaneously
        //        return filterMode switch
        //        {
        //            0 => selectedColors.Any(c => manaCostSymbols.Contains(c) || colorSymbols.Contains(c) ||
        //                    (c == "Colorless" && string.IsNullOrWhiteSpace(card.Colors))), // ANY
        //            1 => selectedColors.All(c =>
        //                    (c == "Colorless" && string.IsNullOrWhiteSpace(card.Colors)) ||
        //                    manaCostSymbols.Contains(c) ||
        //                    colorSymbols.Contains(c)), // ALL: Both conditions must be met
        //            2 => !selectedColors.Any(c => manaCostSymbols.Contains(c) || colorSymbols.Contains(c) ||
        //                    (c == "Colorless" && string.IsNullOrWhiteSpace(card.Colors))), // NONE
        //            _ => false
        //        };
        //    });
        //}

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
        private static void UpdateFilterSummary(FilterCriteriaDictionary filterCriteriaDictionary)
        {
            try
            {
                StringBuilder filterSummary = new();

                foreach (var (key, (stringSelector, numericSelector, multiCriteria, singleCriteria, numCriteria, operatorType)) in filterCriteriaDictionary.Criteria)
                {
                    // Add single criteria
                    if (!string.IsNullOrWhiteSpace(singleCriteria))
                    {
                        filterSummary.Append($"{key}: \"{singleCriteria}\" AND ");
                    }

                    // Add multiple criteria
                    if (multiCriteria != null && multiCriteria.Count > 0)
                    {
                        string operatorSymbol = operatorType switch
                        {
                            OperatorType.OR => "OR",
                            OperatorType.AND => "AND",
                            OperatorType.NOT => "NOT",
                            _ => string.Empty
                        };

                        string filterSegment = operatorType == OperatorType.NOT
                            ? string.Join(", ", multiCriteria.Select(c => $"NOT {c}"))
                            : string.Join($" {operatorSymbol} ", multiCriteria);

                        filterSummary.Append($"{{{filterSegment}}} AND ");
                    }

                    // Add numeric criteria
                    if (numCriteria.HasValue)
                    {
                        var (value, opType) = numCriteria.Value;
                        string operatorSymbol = opType switch
                        {
                            OperatorType.LESS_THAN => "<",
                            OperatorType.LESS_THAN_OR_EQUALS => "<=",
                            OperatorType.GREATER_THAN => ">",
                            OperatorType.GREATER_THAN_OR_EQUALS => ">=",
                            OperatorType.EQUALS => "==",
                            OperatorType.NOT_EQUALS => "!=",
                            _ => string.Empty
                        };

                        filterSummary.Append($"{key} {operatorSymbol} {value} AND ");
                    }
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
    public class FilterCriteriaDictionary(List<FilterSelections> filterSelections)
    {
        public Dictionary<string, (Func<CardSet, string?>? StringPropertySelector, Func<CardSet, double?>? NumericPropertySelector, HashSet<string>? MultipleCriteria, string? SingleCriteria, (double value, OperatorType operatorType)? NumericCriteria, OperatorType? OperatorType)> Criteria
        { get; private set; } = filterSelections.ToDictionary(
                fs => fs.CriteriaKey!,
                fs => (
                    StringPropertySelector: fs.CriteriaKey != null ? ResolveStringPropertySelector(fs.CriteriaKey!) : null,
                    NumericPropertySelector: fs.CriteriaKey != null ? ResolveNumericPropertySelector(fs.CriteriaKey!) : null,
                    MultipleCriteria: fs.MultipleCriteria ?? null, // Ensure nullability alignment
                    SingleCriteria: fs.SingleCriteria ?? null,
                    NumericCriteria: fs.NumberCriteria != -1
                        ? (fs.NumberCriteria, fs.Operator) as (double value, OperatorType operatorType)?
                        : null,
                    OperatorType: fs.Operator != OperatorType.Unknown ? fs.Operator : (OperatorType?)null
                )
            );
        private static Func<CardSet, string?> ResolveStringPropertySelector(string criteriaKey)
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
                    return property.GetValue(card) as string; // Safe cast to string
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error accessing property '{criteriaKey}' on '{card.GetType()}': {ex.Message}");
                    return null;
                }
            };
        }
        private static Func<CardSet, double?> ResolveNumericPropertySelector(string criteriaKey)
        {
            var property = typeof(CardSet).GetProperty(criteriaKey)
                          ?? typeof(CardInCollection).GetProperty(criteriaKey)
                          ?? typeof(CardInDeck).GetProperty(criteriaKey);

            if (property == null || property.PropertyType != typeof(double))
            {
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
}