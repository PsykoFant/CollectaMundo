using CollectaMundo.DomainLogic.Filtering.Enums;
using CollectaMundo.DomainLogic.Filtering.Models;
using System.Diagnostics;

namespace CollectaMundo.DomainLogic.Filtering
{
    public class FilteringLogic<TCard>(string criteriaKey, FilterType filterCategory, IEnumerable<string> selectedOptions, string? selectedSingleOption, int? selectedNumericValue, OperatorType operatorSelection, string defaultText) : IFilteringLogic<TCard>
    {
        public string CriteriaKey { get; } = criteriaKey;
        public FilterType FilterCategory { get; } = filterCategory;
        public IEnumerable<string> SelectedOptions { get; } = selectedOptions;
        public string? SelectedSingleOption { get; } = selectedSingleOption;
        public int? SelectedNumericValue { get; } = selectedNumericValue;
        public OperatorType OperatorSelection { get; } = operatorSelection;
        public string DefaultText { get; } = defaultText;
        public bool Matches(TCard card)
        {
            try
            {
                if (!FilterCriteriaMappings.CriteriaMappings.TryGetValue(CriteriaKey, out var mapping))
                {
                    return true;
                }

                if (CriteriaKey.Equals("Colors", StringComparison.OrdinalIgnoreCase))
                {
                    return MatchesColors(card);
                }

                if (CriteriaKey.Equals("SelectedFinish", StringComparison.OrdinalIgnoreCase))
                {
                    return MatchesExactStringOption(card, "SelectedFinish");
                }

                if (CriteriaKey.Equals("SelectedLocationDisplayName", StringComparison.OrdinalIgnoreCase))
                {
                    return MatchesLocation(card);
                }

                if (CriteriaKey.Equals("LegalFormats", StringComparison.OrdinalIgnoreCase))
                {
                    return MatchesLegalFormats(card);
                }

                var property = typeof(TCard).GetProperty(CriteriaKey);

                if (property is null)
                {
                    return true;
                }

                var value = property.GetValue(card);
                var cardValue = value?.ToString() ?? string.Empty;

                return FilterCategory switch
                {
                    FilterType.Single => MatchesSingle(cardValue),
                    FilterType.Multi => MatchesMulti(cardValue),
                    FilterType.Numeric => MatchesNumeric(cardValue),
                    _ => true
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting matches: {ex.Message}");
                return false;
            }
        }

        // Special case matching
        private bool MatchesColors(TCard card)
        {
            if (SelectedOptions == null || !SelectedOptions.Any())
            {
                return true;
            }

            var manaCost = GetPropertyValue(card, "ManaCost");
            var colors = GetPropertyValue(card, "Colors");

            var manaCostSymbols = new HashSet<string>(
                !string.IsNullOrWhiteSpace(manaCost)
                    ? manaCost.Split(',').Select(s => s.Trim())
                    : [],
                StringComparer.OrdinalIgnoreCase);

            var colorSymbols = new HashSet<string>(
                !string.IsNullOrWhiteSpace(colors)
                    ? colors.Split(',').Select(s => s.Trim())
                    : [],
                StringComparer.OrdinalIgnoreCase);

            var isColorless = string.IsNullOrWhiteSpace(colors);

            return OperatorSelection switch
            {
                OperatorType.AND => SelectedOptions.All(opt =>
                    opt.Equals("Colorless", StringComparison.OrdinalIgnoreCase) && isColorless ||
                    manaCostSymbols.Contains(opt) ||
                    colorSymbols.Contains(opt)),

                OperatorType.NOT => !SelectedOptions.Any(opt =>
                    opt.Equals("Colorless", StringComparison.OrdinalIgnoreCase) && isColorless ||
                    manaCostSymbols.Contains(opt) ||
                    colorSymbols.Contains(opt)),

                _ => SelectedOptions.Any(opt =>
                    opt.Equals("Colorless", StringComparison.OrdinalIgnoreCase) && isColorless ||
                    manaCostSymbols.Contains(opt) ||
                    colorSymbols.Contains(opt))
            };
        }
        private bool MatchesExactStringOption(TCard card, string propertyName)
        {
            if (SelectedOptions == null || !SelectedOptions.Any())
            {
                return true;
            }

            var cardValue = GetPropertyValue(card, propertyName) ?? string.Empty;

            return OperatorSelection switch
            {
                OperatorType.NOT => !SelectedOptions.Any(opt => string.Equals(opt, cardValue, StringComparison.OrdinalIgnoreCase)),

                _ => SelectedOptions.Any(opt => string.Equals(opt, cardValue, StringComparison.OrdinalIgnoreCase))
            };
        }
        private bool MatchesLocation(TCard card)
        {
            if (SelectedOptions == null || !SelectedOptions.Any())
            {
                return true;
            }

            var locationId = GetPropertyValue(card, "SelectedLocationId") ?? string.Empty;

            return OperatorSelection switch
            {
                OperatorType.NOT => !SelectedOptions.Any(opt => string.Equals(opt, locationId, StringComparison.OrdinalIgnoreCase)),
                _ => SelectedOptions.Any(opt => string.Equals(opt, locationId, StringComparison.OrdinalIgnoreCase))
            };
        }
        private bool MatchesLegalFormats(TCard card)
        {
            if (SelectedOptions == null || !SelectedOptions.Any())
            {
                return true;
            }

            var playableMaskObject = typeof(TCard).GetProperty("PlayableFormatsMask")?.GetValue(card);

            if (playableMaskObject is not ulong playableMask)
            {
                return true;
            }

            var selectedMask = SelectedOptions.Where(x => ulong.TryParse(x, out _)).Select(ulong.Parse).Aggregate(0UL, (acc, mask) => acc | mask);

            if (selectedMask == 0)
            {
                return true;
            }

            return OperatorSelection switch
            {
                OperatorType.AND => (playableMask & selectedMask) == selectedMask,
                OperatorType.NOT => (playableMask & selectedMask) == 0,
                _ => (playableMask & selectedMask) != 0
            };
        }


        // Generic matching
        private bool MatchesSingle(string cardValue)
        {
            if (string.IsNullOrWhiteSpace(SelectedSingleOption) || SelectedSingleOption == DefaultText)
            {
                return true;
            }

            return OperatorSelection switch
            {
                OperatorType.EQUALS => cardValue.Equals(SelectedSingleOption, StringComparison.OrdinalIgnoreCase),
                OperatorType.CONTAINS => cardValue.Contains(SelectedSingleOption, StringComparison.OrdinalIgnoreCase),
                _ => cardValue.Contains(SelectedSingleOption, StringComparison.OrdinalIgnoreCase)
            };
        }
        private bool MatchesMulti(string cardValue)
        {
            if (SelectedOptions == null || !SelectedOptions.Any())
            {
                return true;
            }

            return OperatorSelection switch
            {
                OperatorType.AND => SelectedOptions.All(opt => cardValue.Contains(opt, StringComparison.OrdinalIgnoreCase)),
                OperatorType.NOT => !SelectedOptions.Any(opt => cardValue.Contains(opt, StringComparison.OrdinalIgnoreCase)),
                _ => SelectedOptions.Any(opt => cardValue.Contains(opt, StringComparison.OrdinalIgnoreCase))
            };
        }
        private bool MatchesNumeric(string cardValue)
        {
            if (SelectedNumericValue is null)
            {
                return true;
            }

            if (!double.TryParse(cardValue, out var cardNumeric))
            {
                return true;
            }

            return OperatorSelection switch
            {
                OperatorType.LESS_THAN => cardNumeric < SelectedNumericValue,
                OperatorType.LESS_THAN_OR_EQUALS => cardNumeric <= SelectedNumericValue,
                OperatorType.GREATER_THAN => cardNumeric > SelectedNumericValue,
                OperatorType.GREATER_THAN_OR_EQUALS => cardNumeric >= SelectedNumericValue,
                OperatorType.EQUALS => Math.Abs(cardNumeric - (double)SelectedNumericValue) < 0.0001,
                OperatorType.NOT_EQUALS => Math.Abs(cardNumeric - (double)SelectedNumericValue) >= 0.0001,
                _ => true
            };
        }
        private static string? GetPropertyValue(TCard card, string propertyName)
        {
            var property = typeof(TCard).GetProperty(propertyName);

            if (property is null)
            {
                return null;
            }

            return property.GetValue(card)?.ToString();
        }
    }
}
