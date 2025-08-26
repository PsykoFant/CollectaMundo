using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Filtering;
using CollectaMundo.DomainLogic.Filtering.Models;
using System.Text.RegularExpressions;

namespace CollectaMundo.Data.Filtering
{
    public partial class FilterDefaultsLogic() : IFilterDefaultsLogic
    {
        public List<FilterDefaults> Build(IEnumerable<CardSet> allCards, IEnumerable<CardSet> myCollection)
        {
            var filterDefaultsList = new List<FilterDefaults>();

            foreach (var entry in FilterCriteriaMappings.CriteriaMappings)
            {
                var criteriaKey = entry.Key;
                var mapping = entry.Value;
                List<string> rawValues = [];

                // Handled special cases first
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
                    // Build from in-memory objects, matching the fields the SQL would return
                    switch (criteriaKey)
                    {
                        case "Name":
                            rawValues.AddRange(NotNullOrWhite(allCards.Select(c => c.Name)));
                            break;
                        case "SetName":
                            rawValues.AddRange(NotNullOrWhite(allCards.Select(c => c.SetName)));
                            break;
                        case "Rarity":
                            rawValues.AddRange(NotNullOrWhite(allCards.Select(c => c.Rarity)));
                            break;
                        case "SuperTypes":
                            rawValues.AddRange(NotNullOrWhite(allCards.Select(c => c.SuperTypes)));
                            break;
                        case "Types":
                            rawValues.AddRange(NotNullOrWhite(allCards.Select(c => c.Types)));
                            break;
                        case "SubTypes":
                            rawValues.AddRange(NotNullOrWhite(allCards.Select(c => c.SubTypes)));
                            break;
                        case "Keywords":
                            rawValues.AddRange(NotNullOrWhite(allCards.Select(c => c.Keywords)));
                            break;
                        case "Finishes":
                            rawValues.AddRange(NotNullOrWhite(allCards.Select(c => c.Finishes)));
                            break;

                        // Collection-backed fields come from myCollection (not the DB)
                        case "SelectedFinish":
                            rawValues.AddRange(NotNullOrWhite(myCollection.Select(c => c.SelectedFinish)));
                            break;
                        case "Language":
                            rawValues.AddRange(NotNullOrWhite(myCollection.Select(c => c.Language)));
                            break;
                        case "SelectedCondition":
                            rawValues.AddRange(NotNullOrWhite(myCollection.Select(c => c.SelectedCondition)));
                            break;

                        case "ManaValue":
                            // Persist as strings first; we’ll parse/sort numerics below to match repo behavior
                            rawValues.AddRange(allCards.Select(c => c.ManaValue.ToString()));
                            break;

                        default:
                            throw new Exception($"Unhandled criteria key: {criteriaKey}");
                    }

                }

                // Clean + split
                var removeItems = GetUnwantedItems(criteriaKey);
                bool shouldNotSplit = mapping.ShouldNotSplit;
                var cleanedValues = CleanAndFilter(rawValues, removeItems, shouldNotSplit);

                // Colors were hard-coded already
                if (criteriaKey.Equals("Colors", StringComparison.OrdinalIgnoreCase))
                {
                    cleanedValues = rawValues;
                }

                // Numeric list (for numeric filters)
                List<int>? numericValues = null;
                if (mapping.Type == FilterType.Numeric)
                {
                    numericValues = [.. cleanedValues.Where(v => int.TryParse(v, out _)).Select(int.Parse)];
                }

                var filterOptions = cleanedValues.Select(v => new FilterOption(v)).ToList();

                // Default text
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

                filterDefaultsList.Add(new FilterDefaults
                {
                    CriteriaKey = criteriaKey,
                    FilterOptions = filterOptions,
                    NumericCriteria = numericValues,
                    DefaultText = defaultText,
                    ReadableLabel = readableLabel
                });
            }

            return filterDefaultsList;
        }
        private static List<string> CleanAndFilter(IEnumerable<string> input, HashSet<string>? removeItems, bool shouldNotSplit)
        {
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in input)
            {
                if (string.IsNullOrWhiteSpace(item)) continue;

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
                    if (string.IsNullOrEmpty(trimmed)) continue;
                    if (removeItems != null && removeItems.Contains(trimmed)) continue;
                    unique.Add(trimmed);
                }
            }

            var numerics = new List<int>();
            var strings = new List<string>();
            foreach (var v in unique)
            {
                if (int.TryParse(v, out var n)) numerics.Add(n);
                else strings.Add(v);
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
                { "Eaturecray", "Summon", "Scariest", "You'll", "Ever", "See", "Jaguar", "Dragon", "Knights", "Legend", "instant", "Cards" },
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
                    yield return s!;
            }
        }
        // Comma NOT followed by exactly 3 digits at word boundary. To catch keywords with comma in them. E.g. "Flying, vigilance" should split, but "10,000" should not.
        private static readonly Regex SplitCommaRegex = MyRegex();

        [GeneratedRegex(@",(?!\d{3}\b)", RegexOptions.Compiled)]
        private static partial Regex MyRegex();
    }
}

