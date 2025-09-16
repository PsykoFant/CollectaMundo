using CollectaMundo.ApplicationServices.CardLists.CardLookups.Providers;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Filtering;
using CollectaMundo.DomainLogic.Filtering.Models;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace CollectaMundo.Data.Filtering
{
    public partial class FilterDefaultsLogic() : IFilterDefaultsLogic
    {
        public List<FilterDefaults> Build(IEnumerable<CardSet> allCards, IEnumerable<CardSet> myCollection)
        {
            var filterDefaultsDict = new ConcurrentDictionary<string, FilterDefaults>();

            Parallel.ForEach(FilterCriteriaMappings.CriteriaMappings, entry =>
            {
                var criteriaKey = entry.Key;
                var mapping = entry.Value;
                List<string> rawValues = [];

                // Special case handling
                if (criteriaKey.Equals("Colors", StringComparison.OrdinalIgnoreCase))
                {
                    rawValues = ["W", "U", "B", "R", "G", "C", "X", "Colorless"];
                }
                else if (criteriaKey.Equals("Text", StringComparison.OrdinalIgnoreCase) || criteriaKey.Equals("CardsForTrade", StringComparison.OrdinalIgnoreCase))
                {
                    rawValues = [];
                }
                else
                {
                    switch (criteriaKey)
                    {
                        case "Name":
                            foreach (var c in allCards)
                            {
                                if (!string.IsNullOrWhiteSpace(c.Name))
                                {
                                    rawValues.Add(c.Name);
                                }
                            }

                            break;

                        case "SetName":
                            foreach (var c in allCards)
                            {
                                if (!string.IsNullOrWhiteSpace(c.SetName))
                                {
                                    rawValues.Add(c.SetName);
                                }
                            }

                            break;

                        case "Rarity":
                            foreach (var c in allCards)
                            {
                                if (!string.IsNullOrWhiteSpace(c.Rarity))
                                {
                                    rawValues.Add(c.Rarity);
                                }
                            }

                            break;

                        case "SuperTypes":
                            foreach (var c in allCards)
                            {
                                if (!string.IsNullOrWhiteSpace(c.SuperTypes))
                                {
                                    rawValues.Add(c.SuperTypes);
                                }
                            }

                            break;

                        case "Types":
                            foreach (var c in allCards)
                            {
                                if (!string.IsNullOrWhiteSpace(c.Types))
                                {
                                    rawValues.Add(c.Types);
                                }
                            }

                            break;

                        case "SubTypes":
                            foreach (var c in allCards)
                            {
                                if (!string.IsNullOrWhiteSpace(c.SubTypes))
                                {
                                    rawValues.Add(c.SubTypes);
                                }
                            }

                            break;

                        case "Keywords":
                            foreach (var c in allCards)
                            {
                                if (!string.IsNullOrWhiteSpace(c.Keywords))
                                {
                                    rawValues.Add(c.Keywords);
                                }
                            }

                            break;

                        case "Finishes":
                            foreach (var c in allCards)
                            {
                                if (!string.IsNullOrWhiteSpace(c.Finishes))
                                {
                                    rawValues.Add(c.Finishes);
                                }
                            }

                            break;

                        case "SelectedFinish":
                            foreach (var c in myCollection)
                            {
                                if (!string.IsNullOrWhiteSpace(c.SelectedFinish))
                                {
                                    rawValues.Add(c.SelectedFinish);
                                }
                            }

                            break;

                        case "Language":
                            foreach (var c in myCollection)
                            {
                                if (!string.IsNullOrWhiteSpace(c.Language))
                                {
                                    rawValues.Add(c.Language);
                                }
                            }

                            break;

                        case "SelectedCondition":
                            foreach (var c in myCollection)
                            {
                                if (!string.IsNullOrWhiteSpace(c.SelectedCondition))
                                {
                                    rawValues.Add(c.SelectedCondition);
                                }
                            }

                            break;

                        case "ManaValue":
                            foreach (var c in allCards)
                            {
                                rawValues.Add(c.ManaValue.ToString());
                            }

                            break;

                        default:
                            throw new Exception($"Unhandled criteria key: {criteriaKey}");
                    }

                }

                var removeItems = GetUnwantedItems(criteriaKey);
                bool shouldNotSplit = mapping.ShouldNotSplit;
                var cleanedValues = CleanAndFilter(rawValues, removeItems, shouldNotSplit);

                // Special sorting for SetName by release date descending
                if (criteriaKey.Equals("SetName", StringComparison.OrdinalIgnoreCase) && CardSet.SetMetaProvider is ValueProvider<string, SetDto> provider)
                {
                    var releaseDateMap = provider.Values.Where(s => !string.IsNullOrWhiteSpace(s.Name) && s.ReleaseDate.HasValue).ToDictionary(s => s.Name, s => s.ReleaseDate!.Value, StringComparer.OrdinalIgnoreCase);
                    cleanedValues = [.. cleanedValues.OrderByDescending(name => releaseDateMap.TryGetValue(name, out var date) ? date : DateTime.MinValue)];
                }

                // Skip cleaning for hardcoded
                if (criteriaKey.Equals("Colors", StringComparison.OrdinalIgnoreCase))
                {
                    cleanedValues = rawValues;
                }

                List<int>? numericValues = null;
                if (mapping.Type == FilterType.Numeric)
                {
                    numericValues = [.. cleanedValues.Where(v => int.TryParse(v, out _)).Select(int.Parse)];
                }

                var filterOptions = cleanedValues.Select(v => new FilterOption(v)).ToList();

                string defaultText = string.Empty;
                if (mapping.Type == FilterType.Multi || criteriaKey == "Text")
                {
                    defaultText = string.IsNullOrWhiteSpace(mapping.ReadableLabel)
                        ? $"{criteriaKey} ..."
                        : $"{mapping.ReadableLabel} ...";
                }

                string readableLabel = string.IsNullOrEmpty(mapping.ReadableLabel)
                    ? criteriaKey
                    : mapping.ReadableLabel;

                filterDefaultsDict[criteriaKey] = new FilterDefaults
                {
                    CriteriaKey = criteriaKey,
                    FilterOptions = filterOptions,
                    NumericCriteria = numericValues,
                    DefaultText = defaultText,
                    ReadableLabel = readableLabel
                };
            });

            return [.. FilterCriteriaMappings.CriteriaMappings.Keys.Select(k => filterDefaultsDict[k])];

        }
        private static List<string> CleanAndFilter(IEnumerable<string> input, HashSet<string>? removeItems, bool shouldNotSplit)
        {
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in input)
            {
                if (string.IsNullOrWhiteSpace(item))
                {
                    continue;
                }

                IEnumerable<string> parts;

                if (shouldNotSplit)
                {
                    parts = [item];
                }
                else
                {
                    // Use regex split instead of naive Split(',')
                    parts = SplitCommaRegex.Split(item);
                }

                foreach (var p in parts)
                {
                    string trimmed = p.Trim();
                    if (string.IsNullOrEmpty(trimmed))
                    {
                        continue;
                    }

                    if (removeItems != null && removeItems.Contains(trimmed))
                    {
                        continue;
                    }

                    unique.Add(trimmed);
                }
            }

            var numerics = new List<int>();
            var strings = new List<string>();
            foreach (var v in unique)
            {
                if (int.TryParse(v, out var n))
                {
                    numerics.Add(n);
                }
                else
                {
                    strings.Add(v);
                }
            }

            numerics.Sort();
            strings.Sort(StringComparer.OrdinalIgnoreCase);

            return [.. numerics.Select(n => n.ToString()), .. strings];
        }

        // Remove weirdo types from unsets etc. 
        private static HashSet<string>? GetUnwantedItems(string criteriaKey)
        {
            return criteriaKey switch
            {
                "Types" => new(StringComparer.OrdinalIgnoreCase)
                { "Eaturecray", "Summon", "Scariest", "You'll", "Ever", "See", "Jaguar", "Dragon", "Knights", "Legend", "Cards" },
                "SubTypes" => new(StringComparer.OrdinalIgnoreCase)
                { "(creature", "and/or", "type)|Judge", "The", "pLAnE" },
                _ => null
            };
        }
        private static IEnumerable<string> NotNullOrWhite(IEnumerable<string?> src)
        {
            // Works for IEnumerable<string> and IEnumerable<string?> alike.
            foreach (var s in src)
            {
                if (!string.IsNullOrWhiteSpace(s))
                {
                    yield return s!;
                }
            }
        }
        // Comma NOT followed by exactly 3 digits at word boundary. To catch keywords with comma in them. E.g. "Flying, vigilance" should split, but "10,000" should not.
        private static readonly Regex SplitCommaRegex = MyRegex();

        [GeneratedRegex(@",(?!\d{3}\b)", RegexOptions.Compiled)]
        private static partial Regex MyRegex();
    }
}

