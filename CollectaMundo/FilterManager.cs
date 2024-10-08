using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace CollectaMundo
{
    public class FilterManager(FilterContext context)
    {
        private readonly FilterContext filterContext = context;

        #region Filtering
        public IEnumerable<CardSet> ApplyFilter(IEnumerable<CardSet> cards, string listName)
        {
            try
            {
                var filteredCards = cards.AsEnumerable();

                // Find all ComboBoxes and then find the specific ones by their tags
                var headerComboBoxes = MainWindow.FindVisualChildren<ComboBox>(MainWindow.CurrentInstance.AllCardsDataGrid);
                ComboBox? nameComboBox = headerComboBoxes.FirstOrDefault(cb => cb.Tag?.ToString() == "AllCardsName");
                ComboBox? setComboBox = headerComboBoxes.FirstOrDefault(cb => cb.Tag?.ToString() == "AllCardsSet");

                string cardFilter = nameComboBox?.SelectedItem?.ToString() ?? string.Empty;
                string setFilter = setComboBox?.SelectedItem?.ToString() ?? string.Empty;

                string rulesTextFilter = MainWindow.CurrentInstance.FilterRulesTextTextBox.Text ?? string.Empty;
                bool useAnd = MainWindow.CurrentInstance.AllOrNoneComboBox.SelectedIndex == 1;
                bool exclude = MainWindow.CurrentInstance.AllOrNoneComboBox.SelectedIndex == 2;
                string compareOperator = MainWindow.CurrentInstance.ManaValueOperatorComboBox.SelectedItem?.ToString() ?? string.Empty;
                _ = double.TryParse(MainWindow.CurrentInstance.ManaValueComboBox.SelectedItem?.ToString(), out double manaValueCompare);

                // Filter by mana value
                filteredCards = FilterByManaValue(filteredCards, compareOperator, manaValueCompare);

                // Filtering by card name, set name, and rules text
                filteredCards = FilterByText(filteredCards, cardFilter, setFilter, rulesTextFilter);

                // Filter by colors
                filteredCards = FilterByCardProperty(filteredCards, filterContext.SelectedColors, useAnd, card => card.ManaCost, exclude);

                // Filter by listbox selections
                filteredCards = FilterByCardProperty(filteredCards, filterContext.SelectedTypes, MainWindow.CurrentInstance.TypesAndOrCheckBox.IsChecked ?? false, card => card.Types);
                filteredCards = FilterByCardProperty(filteredCards, filterContext.SelectedSuperTypes, MainWindow.CurrentInstance.SuperTypesAndOrCheckBox.IsChecked ?? false, card => card.SuperTypes);
                filteredCards = FilterByCardProperty(filteredCards, filterContext.SelectedSubTypes, MainWindow.CurrentInstance.SubTypesAndOrCheckBox.IsChecked ?? false, card => card.SubTypes);
                filteredCards = FilterByCardProperty(filteredCards, filterContext.SelectedKeywords, MainWindow.CurrentInstance.KeywordsAndOrCheckBox.IsChecked ?? false, card => card.Keywords);

                // Filter for including/excluding cards based on foil or etched finishes
                filteredCards = FilterByIncludeFoil(filteredCards);

                var finalFilteredCards = filteredCards.ToList();
                UpdateFilterLabel();

                if (listName == "allCards")
                {
                    MainWindow.CurrentInstance.AllCardsCountLabel.Content = $"Cards shown: {finalFilteredCards.Count}";
                }
                else
                {
                    MainWindow.CurrentInstance.MyCardsCountLabel.Content = $"Cards shown: {finalFilteredCards.Count}";
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
        private static IEnumerable<CardSet> FilterByCardProperty(IEnumerable<CardSet>? cards, HashSet<string> selectedCriteria, bool useAnd, Func<CardSet, string> propertySelector, bool exclude = false)
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
                var criteria = propertyValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());

                bool match = useAnd ? selectedCriteria.All(c => criteria.Contains(c)) : selectedCriteria.Any(c => criteria.Contains(c));
                return exclude ? !match : match;
            });
        }
        private static IEnumerable<CardSet> FilterByManaValue(IEnumerable<CardSet> cards, string compareOperator, double manaValueCompare)
        {
            if (MainWindow.CurrentInstance.ManaValueComboBox.SelectedIndex != -1 && MainWindow.CurrentInstance.ManaValueOperatorComboBox.SelectedIndex != -1)
            {
                return cards.Where(card =>
                {
                    return compareOperator switch
                    {
                        "less than" => card.ManaValue < manaValueCompare,
                        "greater than" => card.ManaValue > manaValueCompare,
                        "less than/eq" => card.ManaValue <= manaValueCompare,
                        "greater than/eq" => card.ManaValue >= manaValueCompare,
                        "equal to" => card.ManaValue == manaValueCompare,
                        _ => true,  // If no valid operator is selected, don't filter on ManaValue
                    };
                });
            }
            else
            {
                // If conditions for filtering are not met, return all cards unfiltered.
                return cards;
            }
        }
        private static IEnumerable<CardSet> FilterByIncludeFoil(IEnumerable<CardSet> cards)
        {
            bool includeFoil = MainWindow.CurrentInstance.ShowFoilCheckBox.IsChecked ?? false;
            return cards.Where(card =>
            {
                // Check if 'Finishes' column contains only 'foil' or 'etched'
                var finishes = card.Finishes?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(f => f.Trim()).ToList() ?? [];

                // If 'includeFoil' is false, filter out cards where 'Finishes' contains only 'foil' or 'etched'
                if (!includeFoil)
                {
                    return finishes.Any(finish => finish != "foil" && finish != "etched");
                }

                // If 'includeFoil' is true, include all cards
                return true;
            });
        }

        #endregion

        #region Filter UI updates
        private void UpdateFilterLabel()
        {
            if (MainWindow.CurrentInstance.FilterRulesTextTextBox.Text != filterContext.RulesTextDefaultText && MainWindow.CurrentInstance.FilterRulesTextTextBox.Text != string.Empty)
            {
                MainWindow.CurrentInstance.CardRulesTextTextBlock.Text = $"Rulestext: \"{MainWindow.CurrentInstance.FilterRulesTextTextBox.Text}\"";
            }

            UpdateLabelContent(filterContext.SelectedSuperTypes, MainWindow.CurrentInstance.CardSuperTypesTextBlock, MainWindow.CurrentInstance.SuperTypesAndOrCheckBox.IsChecked ?? false, "Card supertypes");
            UpdateLabelContent(filterContext.SelectedTypes, MainWindow.CurrentInstance.CardTypesTextBlock, MainWindow.CurrentInstance.TypesAndOrCheckBox.IsChecked ?? false, "Card types");
            UpdateLabelContent(filterContext.SelectedSubTypes, MainWindow.CurrentInstance.CardSubTypesTextBlock, MainWindow.CurrentInstance.SubTypesAndOrCheckBox.IsChecked ?? false, "Card subtypes");
            UpdateLabelContent(filterContext.SelectedKeywords, MainWindow.CurrentInstance.CardKeyWordsTextBlock, MainWindow.CurrentInstance.KeywordsAndOrCheckBox.IsChecked ?? false, "Keywords");
        }
        private static void UpdateLabelContent(HashSet<string> selectedItems, TextBlock targetTextBlock, bool useAnd, string prefix)
        {
            if (selectedItems.Count > 0)
            {
                string conjunction = useAnd ? " AND " : " OR ";
                string content = $"{prefix}: {string.Join(conjunction, selectedItems)}";
                targetTextBlock.Text = content;
            }
            else
            {
                targetTextBlock.Text = string.Empty;
            }
        }

        // Update the object to which the width of the combobox is bound
        public static void DataGrid_LayoutUpdated(int dataGridIndex)
        {
            if (dataGridIndex < 0 || dataGridIndex >= MainWindow.CurrentInstance.ColumnWidths.Count)
            {
                return; // Protect against out-of-range errors
            }

            // Define paddings for each column. Ensure this list matches the number of columns you have.
            int[] paddings = [65, 50];

            var currentDataGrid = dataGridIndex == 0 ? MainWindow.CurrentInstance.AllCardsDataGrid : MainWindow.CurrentInstance.MyCollectionDataGrid;

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

        #endregion
    }
}
