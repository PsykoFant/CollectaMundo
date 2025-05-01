using CollectaMundo.Models;
using CollectaMundo.Utilities;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using static CollectaMundo.MainWindow;

namespace CollectaMundo.Domain
{
    public class FilterCriterion(string criteriaKey, FilterType filterCategory, IEnumerable<string> selectedOptions, string? selectedSingleOption, int? selectedNumericValue, MainWindow.OperatorType operatorSelection, string defaultText) : IFilterCriterion
    {
        public string CriteriaKey { get; } = criteriaKey;
        public FilterType FilterCategory { get; } = filterCategory;
        public IEnumerable<string> SelectedOptions { get; } = selectedOptions;
        public string? SelectedSingleOption { get; } = selectedSingleOption;
        public int? SelectedNumericValue { get; } = selectedNumericValue;
        public OperatorType OperatorSelection { get; } = operatorSelection;
        public string DefaultText { get; } = defaultText;

        public bool Matches(CardSet card)
        {
            try
            {
                // Look up the mapping for this filter.
                if (!FilterCriteriaMappings.CriteriaMappings.TryGetValue(CriteriaKey, out var mapping))
                {
                    return true; // No mapping? Then don't filter on this criterion.
                }

                // Special case for color filtering
                if (CriteriaKey.Equals("Colors", StringComparison.OrdinalIgnoreCase))
                {
                    // Build sets for mana cost and colors.
                    var manaCostSymbols = new HashSet<string>(
                        card.ManaCost != null ? card.ManaCost.Split(',').Select(s => s.Trim()) : [],
                        StringComparer.OrdinalIgnoreCase);
                    var colorSymbols = new HashSet<string>(
                        card.Colors != null ? card.Colors.Split(',').Select(s => s.Trim()) : [],
                        StringComparer.OrdinalIgnoreCase);

                    // "Colorless" means no colors are specified.
                    bool isColorless = string.IsNullOrWhiteSpace(card.Colors);

                    // For multi-select color filtering, use the selected options.
                    if (SelectedOptions == null || !SelectedOptions.Any())
                    {
                        return true;
                    }

                    switch (OperatorSelection)
                    {
                        case OperatorType.AND:
                            // Every selected color must be present (if "Colorless" is selected, card must be colorless).
                            return SelectedOptions.All(opt =>
                                opt.Equals("Colorless", StringComparison.OrdinalIgnoreCase) && isColorless ||
                                manaCostSymbols.Contains(opt) ||
                                colorSymbols.Contains(opt)
                            );
                        case OperatorType.NOT:
                            // No selected color should be present.
                            return !SelectedOptions.Any(opt =>
                                opt.Equals("Colorless", StringComparison.OrdinalIgnoreCase) && isColorless ||
                                manaCostSymbols.Contains(opt) ||
                                colorSymbols.Contains(opt)
                            );
                        default: // OR case (or any other operator)
                            return SelectedOptions.Any(opt =>
                                opt.Equals("Colorless", StringComparison.OrdinalIgnoreCase) && isColorless ||
                                manaCostSymbols.Contains(opt) ||
                                colorSymbols.Contains(opt)
                            );
                    }
                }

                // Special case for SelectedFinish: perform an exact match.
                if (CriteriaKey.Equals("SelectedFinish", StringComparison.OrdinalIgnoreCase))
                {
                    if (SelectedOptions == null || !SelectedOptions.Any())
                    {
                        return true;
                    }

                    // Use the card's finish value. Adjust this if your card uses a different property.
                    string cardFinish = card.SelectedFinish ?? string.Empty;
                    switch (OperatorSelection)
                    {
                        case OperatorType.OR:
                            // Exact match required.
                            return SelectedOptions.Any(opt =>
                                string.Equals(opt, cardFinish, StringComparison.OrdinalIgnoreCase));
                        case OperatorType.NOT:
                            return !SelectedOptions.Any(opt =>
                                string.Equals(opt, cardFinish, StringComparison.OrdinalIgnoreCase));
                        default:
                            return true;
                    }
                }

                // For other filter types, use your existing logic.
                // First, try to get the property using the mapping's Property value.
                string propertyName = CriteriaKey;
                // Optionally also try this.CriteriaKey if necessary:
                PropertyInfo? property = typeof(CardSet).GetProperty(propertyName)
                                      ?? typeof(CardSet).GetProperty(CriteriaKey);

                if (property == null)
                {
                    return true;
                }

                object? value = property.GetValue(card);
                string cardValue = value?.ToString() ?? "";

                switch (FilterCategory)
                {
                    case FilterType.Single:
                        if (string.IsNullOrWhiteSpace(SelectedSingleOption) || SelectedSingleOption == DefaultText)
                        {
                            return true;
                        }

                        return cardValue.Contains(SelectedSingleOption, StringComparison.OrdinalIgnoreCase);

                    case FilterType.Multi:
                        if (SelectedOptions == null || !SelectedOptions.Any())
                        {
                            return true;
                        }

                        if (OperatorSelection == OperatorType.AND)
                        {
                            return SelectedOptions.All(opt => cardValue.IndexOf(opt, StringComparison.OrdinalIgnoreCase) >= 0);
                        }
                        else if (OperatorSelection == OperatorType.NOT)
                        {
                            return !SelectedOptions.Any(opt => cardValue.IndexOf(opt, StringComparison.OrdinalIgnoreCase) >= 0);
                        }
                        else // default OR
                        {
                            return SelectedOptions.Any(opt => cardValue.IndexOf(opt, StringComparison.OrdinalIgnoreCase) >= 0);
                        }

                    case FilterType.Numeric:
                        if (SelectedNumericValue == null)
                        {
                            return true;
                        }

                        if (double.TryParse(cardValue, out double cardNumeric))
                        {
                            switch (OperatorSelection)
                            {
                                case OperatorType.LESS_THAN:
                                    return cardNumeric < SelectedNumericValue;
                                case OperatorType.LESS_THAN_OR_EQUALS:
                                    return cardNumeric <= SelectedNumericValue;
                                case OperatorType.GREATER_THAN:
                                    return cardNumeric > SelectedNumericValue;
                                case OperatorType.GREATER_THAN_OR_EQUALS:
                                    return cardNumeric >= SelectedNumericValue;
                                case OperatorType.EQUALS:
                                    return Math.Abs(cardNumeric - (double)SelectedNumericValue) < 0.0001;
                                case OperatorType.NOT_EQUALS:
                                    return Math.Abs(cardNumeric - (double)SelectedNumericValue) >= 0.0001;
                                default:
                                    return true;
                            }
                        }
                        return true;

                    default:
                        return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting matches: {ex.Message}");
                MessageBox.Show($"Error getting matches: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
}
