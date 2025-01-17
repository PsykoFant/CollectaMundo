using CollectaMundo.Models;
using ServiceStack;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using static CollectaMundo.Models.CardSet;

namespace CollectaMundo
{
    public class FilterManager
    {
        //public string WhichDropdown = string.Empty;

        #region Filtering
        public static void ApplyFilter(IEnumerable<CardSet> cards, DataGrid dataGrid)
        {
            try
            {
                if (MainWindow.CurrentInstance._isStartup)
                {
                    return;
                }

                // ✅ Define the filter criteria with individual operators for each property
                var filterCriteriaMultiple = new Dictionary<string, (Func<CardSet, string?> propertySelector, HashSet<string> selectedCriteria, int filterMode)>
                {
                    { "Rarities", (card => card.Rarity, MainWindow.CurrentInstance.filterSelections.SelectedRarity, MainWindow.CurrentInstance.filterSelections.RarityOperator) },
                    { "SuperTypes", (card => card.SuperTypes, MainWindow.CurrentInstance.filterSelections.SelectedSuperTypes, MainWindow.CurrentInstance.filterSelections.SuperTypesOperator) },
                    { "Types", (card => card.Types, MainWindow.CurrentInstance.filterSelections.SelectedTypes, MainWindow.CurrentInstance.filterSelections.TypesOperator) },
                    { "SubTypes", (card => card.SubTypes, MainWindow.CurrentInstance.filterSelections.SelectedSubTypes, MainWindow.CurrentInstance.filterSelections.SubTypesOperator) },
                    { "Keywords", (card => card.Keywords, MainWindow.CurrentInstance.filterSelections.SelectedKeywords, MainWindow.CurrentInstance.filterSelections.KeywordsOperator) },
                    { "Finishes", (card => card.Finishes, MainWindow.CurrentInstance.filterSelections.SelectedFinishes, MainWindow.CurrentInstance.filterSelections.FinishesOperator) }
                };

                var singleFilterCriteria = new Dictionary<string, (Func<CardSet, string?> propertySelector, string? selectedValue)>
                {
                    { "Name", (card => card.Name, MainWindow.CurrentInstance.filterSelections.SelectedName) },
                    { "Set", (card => card.SetName, MainWindow.CurrentInstance.filterSelections.SelectedSetName) },
                };


                var filteredCards = FilterByMultipleProperties(cards, filterCriteriaMultiple);

                filteredCards = FilterBySingleProperty(filteredCards, singleFilterCriteria);


                //var (cardFilter, setFilter) = GetDropdownFilters(WhichDropdown);
                //string rulesTextFilter = MainWindow.CurrentInstance.FilterRulesTextTextBox.Text ?? string.Empty;


                //// Filtering by card name, set name, and rules text
                ////filteredCards = FilterByText(filteredCards, cardFilter, setFilter, rulesTextFilter);

                //// Apply mana value filter
                //filteredCards = FilterByManaValue(filteredCards);

                filteredCards = FilterByColor(filteredCards, MainWindow.CurrentInstance.filterSelections.SelectedColors, MainWindow.CurrentInstance.AllOrNoneComboBox.SelectedIndex);

                // Apply specific filters for list contexts
                //filteredCards = datagridName switch
                //{
                //    "myCards" => ApplyMyCardsSpecificFilters(filteredCards),
                //    _ => filteredCards
                //};

                var finalFilteredCards = filteredCards.ToList();


                // UI stuff

                // Update the sort order while updating ItemsSource
                SaveAndRestoreSort(dataGrid, () =>
                {
                    dataGrid.ItemsSource = finalFilteredCards;
                });


                UpdateCardCount(dataGrid.Name, finalFilteredCards.Count);
                UpdateFilterSummary(filterCriteriaMultiple, singleFilterCriteria);

                //return finalFilteredCards;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while filtering datagrid: {ex.Message}");
                _ = MessageBox.Show($"Error while filtering datagrid: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                //return [];
                return;
            }
        }
        private static IEnumerable<CardSet> FilterBySingleProperty(IEnumerable<CardSet> cards, Dictionary<string, (Func<CardSet, string?> propertySelector, string? selectedValue)> singleFilterCriteria)
        {
            if (cards == null || singleFilterCriteria == null || singleFilterCriteria.Count == 0)
            {
                return cards; // Return unfiltered if no criteria
            }

            return cards.Where(card =>
            {
                foreach (var (_, (propertySelector, selectedValue)) in singleFilterCriteria)
                {
                    if (!string.IsNullOrWhiteSpace(selectedValue))
                    {
                        var propertyValue = propertySelector(card) ?? string.Empty;

                        // ✅ Skip this filter if the card property is empty or null
                        if (string.IsNullOrWhiteSpace(propertyValue))
                        {
                            continue; // Do not exclude the card; move to the next filter
                        }

                        // ✅ Exclude card if it doesn't match the selected value
                        if (!propertyValue.Equals(selectedValue, StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                    }
                }
                return true; // Include card if all applicable single-value filters match
            });
        }
        private static IEnumerable<CardSet> FilterByMultipleProperties(IEnumerable<CardSet> cards, Dictionary<string, (Func<CardSet, string?> propertySelector, HashSet<string> selectedCriteria, int filterMode)> filterCriteria)
        {
            if (cards == null || filterCriteria == null || filterCriteria.Count == 0)
            {
                return cards;
            }

            return cards.Where(card =>
            {
                bool result = true;

                foreach (var (_, (propertySelector, selectedCriteria, filterMode)) in filterCriteria)
                {
                    // ✅ Skip filters with no selected criteria
                    if (selectedCriteria == null || selectedCriteria.Count == 0)
                    {
                        continue; // Skip this filter, do not affect filtering
                    }

                    // ✅ Apply filtering only when criteria are present
                    bool matches = filterMode switch
                    {
                        0 => MatchesCriteria(card, propertySelector, selectedCriteria),         // OR Mode
                        1 => selectedCriteria.All(c => MatchesCriteria(card, propertySelector, new HashSet<string> { c })), // AND Mode
                        2 => !MatchesCriteria(card, propertySelector, selectedCriteria),        // NOT Mode
                        _ => false
                    };

                    // ✅ If any filter fails in AND mode, exclude the card
                    if (!matches)
                    {
                        result = false;
                        break;
                    }
                }

                return result;
            });
        }
        private static bool MatchesCriteria(CardSet card, Func<CardSet, string?> propertySelector, HashSet<string> filterValues)
        {
            var propertyValue = propertySelector(card) ?? string.Empty;

            var propertyItems = new HashSet<string>(
                propertyValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                             .Select(p => p.Trim())
            );

            return filterValues.Any(filterValue => propertyItems.Contains(filterValue));
        }
        private static (string cardFilter, string setFilter) GetDropdownFilters(string whichDropdown)
        {
            // Helper function to find a ComboBox by its tag in the specified DataGrid
            ComboBox? GetComboBoxByTag(DataGrid dataGrid, string tagName) =>
                MainWindow.FindVisualChildren<ComboBox>(dataGrid)
                          .FirstOrDefault(cb => cb.Tag?.ToString() == tagName);

            string cardFilter = string.Empty;
            string setFilter = string.Empty;

            // Determine which dropdown to process
            switch (whichDropdown)
            {
                case "AllCards":
                    cardFilter = GetComboBoxByTag(MainWindow.CurrentInstance.AllCardsDataGrid, "AllCardsName")?.SelectedItem?.ToString() ?? string.Empty;
                    setFilter = GetComboBoxByTag(MainWindow.CurrentInstance.AllCardsDataGrid, "AllCardsSet")?.SelectedItem?.ToString() ?? string.Empty;
                    break;

                case "MyCollection":
                    cardFilter = GetComboBoxByTag(MainWindow.CurrentInstance.MyCollectionDataGrid, "MyCollectionName")?.SelectedItem?.ToString() ?? string.Empty;
                    setFilter = GetComboBoxByTag(MainWindow.CurrentInstance.MyCollectionDataGrid, "MyCollectionSet")?.SelectedItem?.ToString() ?? string.Empty;
                    break;

                case "AllCardsForDecks":
                    cardFilter = GetComboBoxByTag(MainWindow.CurrentInstance.AllCardsForDecksDataGrid, "AllCardsForDecksName")?.SelectedItem?.ToString() ?? string.Empty;
                    break;

                default:
                    Debug.WriteLine($"Unknown dropdown type: {whichDropdown}");
                    break;
            }

            return (cardFilter, setFilter);
        }
        //private static IEnumerable<CardSet> FilterByText(IEnumerable<CardSet> cards, string cardFilter, string setFilter, string rulesTextFilter)
        //{
        //    var filteredCards = cards;
        //    if (!string.IsNullOrEmpty(cardFilter))
        //    {
        //        filteredCards = filteredCards.Where(card => card.Name != null && card.Name.Contains(cardFilter, StringComparison.OrdinalIgnoreCase));
        //    }
        //    if (!string.IsNullOrEmpty(setFilter))
        //    {
        //        filteredCards = filteredCards.Where(card => card.SetName != null && card.SetName.Equals(setFilter, StringComparison.OrdinalIgnoreCase));
        //    }
        //    if (!string.IsNullOrEmpty(rulesTextFilter) && rulesTextFilter != MainWindow.CurrentInstance.filterSelections.RulesTextDefaultText)
        //    {
        //        filteredCards = filteredCards.Where(card => card.Text != null && card.Text.Contains(rulesTextFilter, StringComparison.OrdinalIgnoreCase));
        //    }
        //    return filteredCards;
        //}
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


        /// <summary>
        /// General filter method for non-color properties using criteria matching.
        /// </summary>
        private static IEnumerable<CardSet> FilterByCardProperty(IEnumerable<CardSet>? cards, HashSet<string>? selectedCriteria, bool useAnd, Func<CardSet, string?> propertySelector, bool exclude = false)
        {
            if (cards == null || propertySelector == null || selectedCriteria == null || selectedCriteria.Count == 0)
            {
                return cards ?? [];
            }

            return cards.Where(card =>
            {
                var propertyValue = propertySelector(card) ?? string.Empty;
                var criteria = propertyValue.Split([','], StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());

                bool match = useAnd
                    ? selectedCriteria.All(c => criteria.Any(crit => crit.Contains(c)))
                    : selectedCriteria.Any(c => criteria.Any(crit => crit.Contains(c)));

                return exclude ? !match : match;
            });
        }
        private static IEnumerable<CardSet> ApplyMyCardsSpecificFilters(IEnumerable<CardSet> cards)
        {
            var filteredCardItems = cards.OfType<CardInCollection>();

            // Handle "Cards for Trade" and "Cards Not for Trade"
            bool showForTrade = MainWindow.CurrentInstance.CheckBoxCardsForTrade.IsChecked ?? false;
            bool showNotForTrade = MainWindow.CurrentInstance.CheckBoxCardsNotForTrade.IsChecked ?? false;

            if (showForTrade)
            {
                filteredCardItems = filteredCardItems.Where(card => card.CardsForTrade > 0);
            }

            if (showNotForTrade)
            {
                filteredCardItems = filteredCardItems.Where(card => card.CardsForTrade == 0);
            }

            // Apply language filter
            var languageFilteredItems = FilterByCardProperty(filteredCardItems.Cast<CardSet>(), MainWindow.CurrentInstance.filterSelections.SelectedLanguages, false, card => card.Language);

            return languageFilteredItems.OfType<CardInCollection>().Cast<CardSet>();
        }
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
        private static void UpdateFilterSummary(
    Dictionary<string, (Func<CardSet, string?> propertySelector, HashSet<string> selectedCriteria, int filterMode)> multipleFilterCriteria,
    Dictionary<string, (Func<CardSet, string?> propertySelector, string? selectedValue)> singleFilterCriteria)
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
    }
}
