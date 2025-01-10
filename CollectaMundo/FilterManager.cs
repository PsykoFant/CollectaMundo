using CollectaMundo.Models;
using ServiceStack;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using static CollectaMundo.Models.CardSet;
using LinqHashSet = System.Linq.Enumerable;

namespace CollectaMundo
{
    public class FilterManager(FilterContext context)
    {
        public string WhichDropdown = string.Empty;
        private readonly FilterContext filterContext = context;



        private static readonly char[] separator = [','];

        #region Filtering
        public IEnumerable<CardSet> ApplyFilter(IEnumerable<CardSet> cards, string listName)
        {
            try
            {
                if (MainWindow.CurrentInstance._isStartup)
                {
                    return cards;
                }

                var filteredCards = cards.AsEnumerable();

                var (cardFilter, setFilter) = GetDropdownFilters(WhichDropdown);
                string rulesTextFilter = MainWindow.CurrentInstance.FilterRulesTextTextBox.Text ?? string.Empty;

                // Filtering by card name, set name, and rules text
                filteredCards = FilterByText(filteredCards, cardFilter, setFilter, rulesTextFilter);

                // Apply mana value filter
                filteredCards = FilterByManaValue(filteredCards);

                // Determine values of color compare combobox
                filterContext.AndOrSettings["Colors"] = MainWindow.CurrentInstance.AllOrNoneComboBox.SelectedIndex == 1;
                bool exclude = MainWindow.CurrentInstance.AllOrNoneComboBox.SelectedIndex == 2;

                // Apply shared property filters
                filteredCards = ApplySharedPropertyFilters(filteredCards, exclude);

                // Apply specific filters for list contexts
                filteredCards = listName switch
                {
                    "myCards" => ApplyMyCardsSpecificFilters(filteredCards),
                    "allCards" => ApplyAllCardsSpecificFilters(filteredCards),
                    _ => filteredCards
                };

                var finalFilteredCards = filteredCards.ToList();


                UpdateCardCount(listName, finalFilteredCards.Count);
                UpdateFilterSummary();

                return finalFilteredCards;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while filtering datagrid: {ex.Message}");
                _ = MessageBox.Show($"Error while filtering datagrid: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return [];
            }
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
        private IEnumerable<CardSet> FilterByText(IEnumerable<CardSet> cards, string cardFilter, string setFilter, string rulesTextFilter)
        {
            var filteredCards = cards;
            if (!string.IsNullOrEmpty(cardFilter))
            {
                filteredCards = filteredCards.Where(card => card.Name != null && card.Name.Contains(cardFilter, StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrEmpty(setFilter))
            {
                filteredCards = filteredCards.Where(card => card.SetName != null && card.SetName.Equals(setFilter, StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrEmpty(rulesTextFilter) && rulesTextFilter != filterContext.RulesTextDefaultText)
            {
                filteredCards = filteredCards.Where(card => card.Text != null && card.Text.Contains(rulesTextFilter, StringComparison.OrdinalIgnoreCase));
            }
            return filteredCards;
        }
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
        private IEnumerable<CardSet> ApplySharedPropertyFilters(IEnumerable<CardSet> cards, bool exclude)
        {
            var filterMap = new Dictionary<Func<CardSet, string?>, (HashSet<string>, string)>
            {
                { card => card.Types, (filterContext.SelectedTypes, "Types") },
                { card => card.SuperTypes, (filterContext.SelectedSuperTypes, "SuperTypes") },
                { card => card.SubTypes, (filterContext.SelectedSubTypes, "SubTypes") },
                { card => card.Keywords, (filterContext.SelectedKeywords, "Keywords") },
                { card => card.Rarity, (filterContext.SelectedRarity, "Rarity") }
            };

            // Apply color filtering based on combobox selection
            cards = FilterByColor(cards, filterContext.SelectedColors, MainWindow.CurrentInstance.AllOrNoneComboBox.SelectedIndex);

            // Apply other shared property filters
            foreach (var (propertySelector, (selectedCriteria, propertyKey)) in filterMap)
            {
                bool useAnd = filterContext.AndOrSettings.TryGetValue(propertyKey, out bool andOrValue) && andOrValue;
                cards = FilterByCardProperty(cards, selectedCriteria, useAnd, propertySelector, exclude);
            }

            return cards;
        }

        public static IEnumerable<CardSet> FilterByColor(IEnumerable<CardSet> cards, HashSet<string> selectedColors, int filterMode)
        {
            if (cards == null || selectedColors == null || selectedColors.Count == 0)
            {
                return cards;
            }

            return cards.Where(card =>
            {
                var manaCostSymbols = LinqHashSet.ToHashSet(card.ManaCost?.Split(',')
                    .Select(c => c.Trim()) ?? Enumerable.Empty<string>());

                var colorSymbols = LinqHashSet.ToHashSet(card.Colors?.Split(',')
                    .Select(c => c.Trim()) ?? Enumerable.Empty<string>());

                bool manaCostMatch = false;
                bool colorMatch = false;

                if (selectedColors.Contains("C") || selectedColors.Contains("X"))
                {
                    var manaCostCriteria = LinqHashSet.ToHashSet(selectedColors.Intersect(new[] { "C", "X" }));
                    manaCostMatch = filterMode switch
                    {
                        0 => manaCostSymbols.Overlaps(manaCostCriteria),
                        1 => manaCostCriteria.All(manaCostSymbols.Contains),
                        2 => !manaCostSymbols.Overlaps(manaCostCriteria),
                        _ => false
                    };
                }

                if (selectedColors.Overlaps(new[] { "W", "U", "B", "R", "G", "Colorless" }))
                {
                    bool colorlessMatch = selectedColors.Contains("Colorless") && string.IsNullOrWhiteSpace(card.Colors);
                    var coloredCriteria = LinqHashSet.ToHashSet(selectedColors.Where(c => c != "Colorless"));

                    colorMatch = filterMode switch
                    {
                        0 => colorSymbols.Overlaps(coloredCriteria) || colorlessMatch,
                        1 => coloredCriteria.All(colorSymbols.Contains) && colorlessMatch,
                        2 => !colorSymbols.Overlaps(coloredCriteria) && !colorlessMatch,
                        _ => false
                    };
                }

                return filterMode switch
                {
                    0 => manaCostMatch || colorMatch,
                    1 => manaCostMatch && colorMatch,
                    2 => !manaCostMatch && !colorMatch,
                    _ => false
                };
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
                var criteria = propertyValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());

                bool match = useAnd
                    ? selectedCriteria.All(c => criteria.Any(crit => crit.Contains(c)))
                    : selectedCriteria.Any(c => criteria.Any(crit => crit.Contains(c)));

                return exclude ? !match : match;
            });
        }

        public static void DebugCardsWithEmptyOrNullColors(List<CardSet> cards)
        {
            if (cards == null || cards.Count == 0)
            {
                Debug.WriteLine("The card list is empty or null.");
                return;
            }

            // Filter cards where Colors is null or an empty string
            var filteredCards = cards
                .Where(card => string.IsNullOrWhiteSpace(card.Colors))
                .ToList();

            if (filteredCards.Count == 0)
            {
                Debug.WriteLine("No cards found where Colors is null or empty.");
                return;
            }

            // Output the names of the matching cards
            Debug.WriteLine($"Cards with empty or null Colors (Total: {filteredCards.Count}):");
            foreach (var card in filteredCards)
            {
                Debug.WriteLine($"- {card.Name} (Colors: '{card.Colors ?? "null"}')");
            }
        }




        private IEnumerable<CardSet> ApplyMyCardsSpecificFilters(IEnumerable<CardSet> cards)
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

            // Apply filters for specific properties
            if (filterContext.SelectedConditions.Count > 0)
            {
                filteredCardItems = filteredCardItems.Where(card =>
                    card.SelectedCondition != null && filterContext.SelectedConditions.Contains(card.SelectedCondition));
            }

            if (filterContext.SelectedFinishes.Count > 0)
            {
                filteredCardItems = filteredCardItems.Where(card =>
                    card.SelectedFinish != null && filterContext.SelectedFinishes.Contains(card.SelectedFinish));
            }

            // Apply language filter
            var languageFilteredItems = FilterByCardProperty(filteredCardItems.Cast<CardSet>(), filterContext.SelectedLanguages, false, card => card.Language);

            return languageFilteredItems.OfType<CardInCollection>().Cast<CardSet>();
        }
        private IEnumerable<CardSet> ApplyAllCardsSpecificFilters(IEnumerable<CardSet> cards)
        {
            return FilterByCardProperty(cards, filterContext.SelectedFinishes, MainWindow.CurrentInstance.FinishesAndOrCheckBox.IsChecked ?? false, card => card.Finishes);
        }

        #endregion

        #region Filter UI updates
        private static void UpdateCardCount(string listName, int count)
        {
            if (listName == "allCards")
            {
                MainWindow.CurrentInstance.AllCardsCountLabel.Content = $"Showing: {count} cards out of total {MainWindow.CurrentInstance.allCards.Count} cards.";
            }
            else if (listName == "myCards")
            {
                MainWindow.CurrentInstance.MyCardsCountLabel.Content = $"Showing: {count} cards out of total {MainWindow.CurrentInstance.myCards.Count} cards in your collection.";
            }
        }
        private void UpdateFilterSummary()
        {
            // Create a StringBuilder to build the filter summary
            StringBuilder filterSummary = new();

            // Check and add the filter rules text
            if (MainWindow.CurrentInstance.FilterRulesTextTextBox.Text != filterContext.RulesTextDefaultText && MainWindow.CurrentInstance.FilterRulesTextTextBox.Text != string.Empty)
            {
                filterSummary.Append($"Rulestext: \"{MainWindow.CurrentInstance.FilterRulesTextTextBox.Text}\" \u2022 ");
            }

            // Update the summary text with selected filter options
            AppendFilterContent(filterContext.SelectedSuperTypes, MainWindow.CurrentInstance.SuperTypesAndOrCheckBox.IsChecked ?? false, "Card supertypes", filterSummary);
            AppendFilterContent(filterContext.SelectedTypes, MainWindow.CurrentInstance.TypesAndOrCheckBox.IsChecked ?? false, "Card types", filterSummary);
            AppendFilterContent(filterContext.SelectedSubTypes, MainWindow.CurrentInstance.SubTypesAndOrCheckBox.IsChecked ?? false, "Card subtypes", filterSummary);
            AppendFilterContent(filterContext.SelectedKeywords, MainWindow.CurrentInstance.KeywordsAndOrCheckBox.IsChecked ?? false, "Keywords", filterSummary);
            AppendFilterContent(filterContext.SelectedFinishes, MainWindow.CurrentInstance.FinishesAndOrCheckBox.IsChecked ?? false, "Finishes", filterSummary);
            AppendFilterContent(filterContext.SelectedRarity, false, "Rarities", filterSummary);
            AppendFilterContent(filterContext.SelectedLanguages, false, "Languages", filterSummary);
            AppendFilterContent(filterContext.SelectedConditions, false, "Conditions", filterSummary);

            // Remove the last separator if there is any content
            if (filterSummary.Length > 0 && filterSummary.ToString().EndsWith("\u2022 "))
            {
                filterSummary.Remove(filterSummary.Length - 3, 3);
            }

            // Set the consolidated filter summary to the FilterSummaryTextBlock
            MainWindow.CurrentInstance.FilterSummaryTextBlock.Text = filterSummary.ToString();
        }
        private static void AppendFilterContent(HashSet<string> selectedItems, bool useAnd, string prefix, StringBuilder filterSummary)
        {
            if (selectedItems.Count > 0)
            {
                string conjunction = useAnd ? " AND " : " OR ";
                string content = $"{prefix}: {string.Join(conjunction, selectedItems)}";
                filterSummary.Append($"{content} \u2022 ");
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
