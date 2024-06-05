using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace CardboardHoarder
{
    public class FilterManager
    {
        private FilterContext filterContext;

        public FilterManager(FilterContext context)
        {
            this.filterContext = context;
        }
        public IEnumerable<CardSet> ApplyFilter(IEnumerable<CardSet> cards, string listName)
        {
            try
            {
                var filteredCards = cards.AsEnumerable();

                string cardFilter = MainWindow.CurrentInstance.FilterCardNameComboBox.SelectedItem?.ToString() ?? string.Empty;
                string setFilter = MainWindow.CurrentInstance.FilterSetNameComboBox.SelectedItem?.ToString() ?? string.Empty;
                string rulesTextFilter = MainWindow.CurrentInstance.FilterRulesTextTextBox.Text;
                bool useAnd = MainWindow.CurrentInstance.AllOrNoneComboBox.SelectedIndex == 1;
                bool exclude = MainWindow.CurrentInstance.AllOrNoneComboBox.SelectedIndex == 2;
                string compareOperator = MainWindow.CurrentInstance.ManaValueOperatorComboBox.SelectedItem?.ToString() ?? string.Empty;
                double.TryParse(MainWindow.CurrentInstance.ManaValueComboBox.SelectedItem?.ToString(), out double manaValueCompare);

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
                MessageBox.Show($"Error while filtering datagrid: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return Enumerable.Empty<CardSet>();
            }
        }
        private IEnumerable<CardSet> FilterByText(IEnumerable<CardSet> cards, string cardFilter, string setFilter, string rulesTextFilter)
        {
            var filteredCards = cards;
            if (!string.IsNullOrEmpty(cardFilter))
            {
                filteredCards = filteredCards.Where(card => card.Name != null && card.Name.IndexOf(cardFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            if (!string.IsNullOrEmpty(setFilter))
            {
                filteredCards = filteredCards.Where(card => card.SetName != null && card.SetName.IndexOf(setFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            if (!string.IsNullOrEmpty(rulesTextFilter) && rulesTextFilter != filterContext.RulesTextDefaultText)
            {
                filteredCards = filteredCards.Where(card => card.Text != null && card.Text.IndexOf(rulesTextFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            return filteredCards;
        }
        private IEnumerable<CardSet> FilterByCardProperty(IEnumerable<CardSet> cards, HashSet<string> selectedCriteria, bool useAnd, Func<CardSet, string> propertySelector, bool exclude = false)
        {
            if (cards == null)
            {
                return Enumerable.Empty<CardSet>();
            }

            if (selectedCriteria == null || selectedCriteria.Count == 0)
            {
                return cards;
            }

            return cards.Where(card =>
            {
                var propertyValue = propertySelector(card);
                var criteria = propertyValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());

                bool match = useAnd ? selectedCriteria.All(c => criteria.Contains(c)) : selectedCriteria.Any(c => criteria.Contains(c));
                return exclude ? !match : match;
            });
        }
        private IEnumerable<CardSet> FilterByManaValue(IEnumerable<CardSet> cards, string compareOperator, double manaValueCompare)
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
        private IEnumerable<CardSet> FilterByIncludeFoil(IEnumerable<CardSet> cards)
        {
            bool includeFoil = MainWindow.CurrentInstance.ShowFoilCheckBox.IsChecked ?? false;
            return cards.Where(card =>
            {
                // Check if 'Finishes' column contains only 'foil' or 'etched'
                var finishes = card.Finishes?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(f => f.Trim()).ToList() ?? new List<string>();

                // If 'includeFoil' is false, filter out cards where 'Finishes' contains only 'foil' or 'etched'
                if (!includeFoil)
                {
                    return finishes.Any(finish => finish != "foil" && finish != "etched");
                }

                // If 'includeFoil' is true, include all cards
                return true;
            });
        }
        private void UpdateFilterLabel()
        {
            if (MainWindow.CurrentInstance.FilterRulesTextTextBox.Text != filterContext.RulesTextDefaultText)
            {
                MainWindow.CurrentInstance.CardRulesTextTextBlock.Text = $"Rulestext: \"{MainWindow.CurrentInstance.FilterRulesTextTextBox.Text}\"";
            }

            UpdateLabelContent(filterContext.SelectedSuperTypes, MainWindow.CurrentInstance.CardSuperTypesTextBlock, MainWindow.CurrentInstance.SuperTypesAndOrCheckBox.IsChecked ?? false, "Card supertypes");
            UpdateLabelContent(filterContext.SelectedTypes, MainWindow.CurrentInstance.CardTypesTextBlock, MainWindow.CurrentInstance.TypesAndOrCheckBox.IsChecked ?? false, "Card types");
            UpdateLabelContent(filterContext.SelectedSubTypes, MainWindow.CurrentInstance.CardSubTypesTextBlock, MainWindow.CurrentInstance.SubTypesAndOrCheckBox.IsChecked ?? false, "Card subtypes");
            UpdateLabelContent(filterContext.SelectedKeywords, MainWindow.CurrentInstance.CardKeyWordsTextBlock, MainWindow.CurrentInstance.KeywordsAndOrCheckBox.IsChecked ?? false, "Keywords");
        }
        private void UpdateLabelContent(HashSet<string> selectedItems, TextBlock targetTextBlock, bool useAnd, string prefix)
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
    }
}
