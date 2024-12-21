using CollectaMundo.Models;
using ServiceStack;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using static CollectaMundo.Models.CardSet;

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


                // Extract mana value filter parameters
                var manaValueFilterParams = GetManaValueFilterParameters();

                // Apply mana value filter
                filteredCards = FilterByManaValue(filteredCards, manaValueFilterParams);


                bool useAnd = MainWindow.CurrentInstance.AllOrNoneComboBox.SelectedIndex == 1;
                bool exclude = MainWindow.CurrentInstance.AllOrNoneComboBox.SelectedIndex == 2;

                // Filter by colors
                filteredCards = FilterByCardProperty(filteredCards, filterContext.SelectedColors, useAnd, card => card.ManaCost, exclude);

                // Filter by listbox selections
                filteredCards = FilterByCardProperty(filteredCards, filterContext.SelectedTypes, MainWindow.CurrentInstance.TypesAndOrCheckBox.IsChecked ?? false, card => card.Types);
                filteredCards = FilterByCardProperty(filteredCards, filterContext.SelectedSuperTypes, MainWindow.CurrentInstance.SuperTypesAndOrCheckBox.IsChecked ?? false, card => card.SuperTypes);
                filteredCards = FilterByCardProperty(filteredCards, filterContext.SelectedSubTypes, MainWindow.CurrentInstance.SubTypesAndOrCheckBox.IsChecked ?? false, card => card.SubTypes);
                filteredCards = FilterByCardProperty(filteredCards, filterContext.SelectedKeywords, MainWindow.CurrentInstance.KeywordsAndOrCheckBox.IsChecked ?? false, card => card.Keywords);
                filteredCards = FilterByCardProperty(filteredCards, filterContext.SelectedRarity, false, card => card.Rarity);

                if (listName == "myCards")
                {
                    // Filter by CardsForTrade if relevant checkboxes are checked
                    bool showForTrade = MainWindow.CurrentInstance.CheckBoxCardsForTrade.IsChecked ?? false;
                    bool showNotForTrade = MainWindow.CurrentInstance.CheckBoxCardsNotForTrade.IsChecked ?? false;

                    var filteredCardItems = filteredCards.OfType<CardInCollection>();

                    // If "Cards for Trade" is checked, filter for cards with CardsForTrade > 0
                    if (showForTrade)
                    {
                        filteredCardItems = filteredCardItems.Where(cardItem => cardItem.CardsForTrade > 0);
                    }

                    // If "Cards Not for Trade" is checked, filter for cards with CardsForTrade == 0
                    if (showNotForTrade)
                    {
                        filteredCardItems = filteredCardItems.Where(cardItem => cardItem.CardsForTrade == 0);
                    }

                    // Apply filter for SelectedCondition property
                    if (filterContext.SelectedConditions.Count != 0)
                    {
                        filteredCardItems = filteredCardItems.Where(cardItem =>
                            cardItem.SelectedCondition != null && filterContext.SelectedConditions.Contains(cardItem.SelectedCondition));
                    }

                    // Apply filter for SelectedFinish property
                    if (filterContext.SelectedFinishes.Count != 0)
                    {
                        filteredCardItems = filteredCardItems.Where(cardItem =>
                            cardItem.SelectedFinish != null && filterContext.SelectedFinishes.Contains(cardItem.SelectedFinish));
                    }

                    // Apply language filter, then cast the result back to IEnumerable<CardInCollection>
                    var languageFilteredItems = FilterByCardProperty(filteredCardItems.Cast<CardSet>(), filterContext.SelectedLanguages, false, card => card.Language);

                    // Cast back to CardInCollection after language filtering
                    filteredCardItems = languageFilteredItems.OfType<CardInCollection>();

                    // Cast back to CardSet after all filtering
                    filteredCards = filteredCardItems.Cast<CardSet>();
                }
                else if (listName == "allCards")
                {
                    filteredCards = FilterByCardProperty(filteredCards, filterContext.SelectedFinishes, MainWindow.CurrentInstance.FinishesAndOrCheckBox.IsChecked ?? false, card => card.Finishes);
                }

                var finalFilteredCards = filteredCards.ToList();
                UpdateFilterSummary();

                if (listName == "allCards")
                {
                    MainWindow.CurrentInstance.AllCardsCountLabel.Content = $"Showing: {finalFilteredCards.Count} cards out of total {MainWindow.CurrentInstance.allCards.Count} cards.";
                }
                else if (listName == "myCards")
                {
                    MainWindow.CurrentInstance.MyCardsCountLabel.Content = $"Showing: {finalFilteredCards.Count} cards out of total {MainWindow.CurrentInstance.myCards.Count} cards in your collection.";
                }

                return finalFilteredCards;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while filtering datagrid: {ex.Message}");
                _ = MessageBox.Show($"Error while filtering datagrid: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return [];
            }
        }

        /// <summary>
        /// Helper methods for filtering by text
        /// </summary>
        /// <param name="cards"></param>
        /// <param name="cardFilter"></param>
        /// <param name="setFilter"></param>
        /// <param name="rulesTextFilter"></param>
        /// <returns></returns>
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
        private static (string cardFilter, string setFilter) GetDropdownFilters(string whichDropdown)
        {
            string cardFilter = string.Empty;
            string setFilter = string.Empty;

            if (whichDropdown == "AllCards")
            {
                cardFilter = GetComboBoxByTag(MainWindow.CurrentInstance.AllCardsDataGrid, "AllCardsName")?.SelectedItem?.ToString() ?? string.Empty;
                setFilter = GetComboBoxByTag(MainWindow.CurrentInstance.AllCardsDataGrid, "AllCardsSet")?.SelectedItem?.ToString() ?? string.Empty;
            }
            else if (whichDropdown == "MyCollection")
            {
                cardFilter = GetComboBoxByTag(MainWindow.CurrentInstance.MyCollectionDataGrid, "MyCollectionName")?.SelectedItem?.ToString() ?? string.Empty;
                setFilter = GetComboBoxByTag(MainWindow.CurrentInstance.MyCollectionDataGrid, "MyCollectionSet")?.SelectedItem?.ToString() ?? string.Empty;
            }
            else if (whichDropdown == "AllCardsForDecks")
            {
                cardFilter = GetComboBoxByTag(MainWindow.CurrentInstance.AllCardsForDecksDataGrid, "AllCardsForDecksName")?.SelectedItem?.ToString() ?? string.Empty;
            }

            return (cardFilter, setFilter);
        }
        private static ComboBox? GetComboBoxByTag(DataGrid dataGrid, string tagName)
        {
            return MainWindow.FindVisualChildren<ComboBox>(dataGrid)
                             .FirstOrDefault(cb => cb.Tag?.ToString() == tagName);
        }

        /// <summary>
        /// Filter by mana value
        /// </summary>
        /// <param name="cards"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        private static IEnumerable<CardSet> FilterByManaValue(IEnumerable<CardSet> cards, (string compareOperator, double manaValueCompare) parameters)
        {
            var (compareOperator, manaValueCompare) = parameters;

            if (string.IsNullOrEmpty(compareOperator))
            {
                return cards; // No filtering if operator is not specified
            }

            return cards.Where(card => CompareManaValue(card.ManaValue, compareOperator, manaValueCompare));
        }
        private static (string compareOperator, double manaValueCompare) GetManaValueFilterParameters()
        {
            string compareOperator = MainWindow.CurrentInstance.ManaValueOperatorComboBox.SelectedItem?.ToString() ?? string.Empty;

            if (!double.TryParse(MainWindow.CurrentInstance.ManaValueComboBox.SelectedItem?.ToString(), out double manaValueCompare))
            {
                Debug.WriteLine("Invalid mana value comparison value. Defaulting to 0.");
            }

            return (compareOperator, manaValueCompare);
        }
        private static bool CompareManaValue(double cardManaValue, string compareOperator, double manaValueCompare)
        {
            return compareOperator switch
            {
                "less than" => cardManaValue < manaValueCompare,
                "greater than" => cardManaValue > manaValueCompare,
                "less than/eq" => cardManaValue <= manaValueCompare,
                "greater than/eq" => cardManaValue >= manaValueCompare,
                "equal to" => cardManaValue == manaValueCompare,
                _ => true, // Default: no filtering
            };
        }


        private static IEnumerable<CardSet> FilterByCardProperty(IEnumerable<CardSet>? cards, HashSet<string>? selectedCriteria, bool useAnd, Func<CardSet, string?> propertySelector, bool exclude = false)
        {
            if (cards == null || propertySelector == null)
            {
                return [];
            }

            if (selectedCriteria == null || selectedCriteria.Count == 0)
            {
                return cards;
            }

            return cards.Where(card =>
            {
                var propertyValue = propertySelector(card) ?? string.Empty;  // Avoid nulls in property values
                var criteria = propertyValue.Split(separator, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());

                // Modify match logic to check for substring matches in each criterion
                bool match = useAnd
                    ? selectedCriteria.All(c => criteria.Any(crit => crit.Contains(c)))
                    : selectedCriteria.Any(c => criteria.Any(crit => crit.Contains(c)));

                return exclude ? !match : match;
            });
        }


        #endregion

        #region Filter UI updates
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
