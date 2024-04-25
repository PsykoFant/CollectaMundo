using System.Diagnostics;
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
        public IEnumerable<CardSet> ApplyFilter(IEnumerable<CardSet> cards)
        {
            try
            {
                var filteredCards = cards.AsEnumerable();

                string cardFilter = MainWindow.CurrentInstance.filterCardNameComboBox.SelectedItem?.ToString() ?? string.Empty;
                string setFilter = MainWindow.CurrentInstance.filterSetNameComboBox.SelectedItem?.ToString() ?? string.Empty;
                string rulesTextFilter = MainWindow.CurrentInstance.FilterRulesTextTextBox.Text;
                bool useAnd = MainWindow.CurrentInstance.allOrNoneComboBox.SelectedIndex == 1;
                bool exclude = MainWindow.CurrentInstance.allOrNoneComboBox.SelectedIndex == 2;
                string compareOperator = MainWindow.CurrentInstance.ManaValueOperatorComboBox.SelectedItem?.ToString() ?? string.Empty;
                double.TryParse(MainWindow.CurrentInstance.ManaValueComboBox.SelectedItem?.ToString(), out double manaValueCompare);
                Debug.WriteLine(setFilter);

                // Filter by mana value
                filteredCards = FilterByManaValue(filteredCards, compareOperator, manaValueCompare);

                // Filtering by card name, set name, and rules text
                filteredCards = FilterByText(filteredCards, cardFilter, setFilter, rulesTextFilter);

                // Filter by colors
                filteredCards = FilterByCriteria(filteredCards, filterContext.SelectedColors, useAnd, card => card.ManaCost, exclude);

                // Filter by listbox selections
                filteredCards = FilterByCriteria(filteredCards, filterContext.SelectedTypes, MainWindow.CurrentInstance.typesAndOr.IsChecked ?? false, card => card.Types);
                filteredCards = FilterByCriteria(filteredCards, filterContext.SelectedSuperTypes, MainWindow.CurrentInstance.superTypesAndOr.IsChecked ?? false, card => card.SuperTypes);
                filteredCards = FilterByCriteria(filteredCards, filterContext.SelectedSubTypes, MainWindow.CurrentInstance.subTypesAndOr.IsChecked ?? false, card => card.SubTypes);
                filteredCards = FilterByCriteria(filteredCards, filterContext.SelectedKeywords, MainWindow.CurrentInstance.keywordsAndOr.IsChecked ?? false, card => card.Keywords);

                var finalFilteredCards = filteredCards.ToList();
                UpdateFilterLabel();
                return finalFilteredCards;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while filtering datagrid: {ex.Message}");
                return Enumerable.Empty<CardSet>();
            }
        }
        private IEnumerable<CardSet> FilterByText(IEnumerable<CardSet> cards, string cardFilter, string setFilter, string rulesTextFilter)
        {
            try
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
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while filtering cards: {ex.Message}");
                return Enumerable.Empty<CardSet>();
            }
        }
        private IEnumerable<CardSet> FilterByCriteria(IEnumerable<CardSet> cards, HashSet<string> selectedCriteria, bool useAnd, Func<CardSet, string> propertySelector, bool exclude = false)
        {
            if (cards == null)
            {
                return Enumerable.Empty<CardSet>();
            }

            if (selectedCriteria == null || selectedCriteria.Count == 0)
            {
                return cards;
            }

            try
            {
                return cards.Where(card =>
                {
                    var propertyValue = propertySelector(card);
                    var criteria = propertyValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());

                    bool match = useAnd ? selectedCriteria.All(c => criteria.Contains(c)) : selectedCriteria.Any(c => criteria.Contains(c));
                    return exclude ? !match : match;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while filtering cards: {ex.Message}");
                return Enumerable.Empty<CardSet>();
            }
        }
        private IEnumerable<CardSet> FilterByManaValue(IEnumerable<CardSet> cards, string compareOperator, double manaValueCompare)
        {
            try
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
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while filtering cards: {ex.Message}");
                return Enumerable.Empty<CardSet>();
            }
        }
        private void UpdateFilterLabel()
        {
            if (MainWindow.CurrentInstance.FilterRulesTextTextBox.Text != filterContext.RulesTextDefaultText)
            {
                MainWindow.CurrentInstance.CardRulesTextLabel.Content = $"Rulestext: \"{MainWindow.CurrentInstance.FilterRulesTextTextBox.Text}\"";
            }

            UpdateLabelContent(filterContext.SelectedTypes, MainWindow.CurrentInstance.CardTypeLabel, MainWindow.CurrentInstance.typesAndOr.IsChecked ?? false, "Card types");
            UpdateLabelContent(filterContext.SelectedSuperTypes, MainWindow.CurrentInstance.CardSuperTypesLabel, MainWindow.CurrentInstance.superTypesAndOr.IsChecked ?? false, "Card supertypes");
            UpdateLabelContent(filterContext.SelectedSubTypes, MainWindow.CurrentInstance.CardSubTypeLabel, MainWindow.CurrentInstance.subTypesAndOr.IsChecked ?? false, "Card subtypes");
            UpdateLabelContent(filterContext.SelectedKeywords, MainWindow.CurrentInstance.CardKeywordsLabel, MainWindow.CurrentInstance.keywordsAndOr.IsChecked ?? false, "Keywords");
        }
        private void UpdateLabelContent(HashSet<string> selectedItems, Label targetLabel, bool useAnd, string prefix)
        {
            if (selectedItems.Count > 0)
            {
                string conjunction = useAnd ? " AND " : " OR ";
                string content = $"{prefix}: {string.Join(conjunction, selectedItems)}";
                targetLabel.Content = content;
            }
            else
            {
                targetLabel.Content = string.Empty;
            }
        }
    }
}
