using CollectaMundo.ApplicationServices.KeyedDataProvider.Providers;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Filtering;
using CollectaMundo.DomainLogic.Filtering.Enums;
using CollectaMundo.DomainLogic.Filtering.Models;
using CollectaMundo.DomainLogic.Shared.CardModels;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace CollectaMundo.Data.Filtering
{
    public partial class FilterDefaultsLogic() : IFilterDefaultsLogic
    {
        public List<FilterDefaults> Build(IReadOnlyList<PrintingCard> allCards, IReadOnlyList<CollectionCard> myCollection)
        {
            var filterDefaultsDict = new ConcurrentDictionary<string, FilterDefaults>();

            Parallel.ForEach(FilterCriteriaMappings.CriteriaMappings, entry =>
            {
                var criteriaKey = entry.Key;
                var mapping = entry.Value;

                var filterDefaults = mapping.DataSource == FilterDataSource.Collection
                    ? BuildCollectionDefault(criteriaKey, mapping, myCollection)
                    : BuildPrintingDefault(criteriaKey, mapping, allCards);

                filterDefaultsDict[criteriaKey] = filterDefaults;
            });

            return [.. FilterCriteriaMappings.CriteriaMappings.Keys.Select(k => filterDefaultsDict[k])];
        }

        private static FilterDefaults BuildPrintingDefault(string criteriaKey, CriteriaSpec mapping, IReadOnlyList<PrintingCard> cards)
        {
            var rawValues = criteriaKey switch
            {
                "Colors" => ["W", "U", "B", "R", "G", "C", "X", "Colorless"],
                "Text" or "Comment" or "CardsForTrade" => [],
                "ManaValue" => [.. cards.Select(c => c.ManaValue.ToString())],
                "Name" => ExtractValues(cards, c => c.Name),
                "SetName" => ExtractValues(cards, c => c.SetName),
                "Rarity" => ExtractValues(cards, c => c.Rarity),
                "SuperTypes" => ExtractValues(cards, c => c.SuperTypes),
                "Types" => ExtractValues(cards, c => c.Types),
                "SubTypes" => ExtractValues(cards, c => c.SubTypes),
                "Keywords" => ExtractValues(cards, c => c.Keywords),
                "Finishes" => ExtractValues(cards, c => c.Finishes),

                _ => throw new Exception($"Unhandled printing criteria key: {criteriaKey}")
            };

            return BuildDefaultFromRawValues(criteriaKey, mapping, rawValues, explicitOptions: null);
        }
        private static FilterDefaults BuildCollectionDefault(string criteriaKey, CriteriaSpec mapping, IReadOnlyList<CollectionCard> cards)
        {
            if (!mapping.GenerateFilterOptions)
            {
                return BuildDefaultFromRawValues(criteriaKey, mapping, rawValues: [], explicitOptions: null);
            }

            if (criteriaKey.Equals("SelectedLocationDisplayName", StringComparison.OrdinalIgnoreCase))
            {
                var explicitOptions = cards
                    .Where(c =>
                        c.SelectedLocationId is not null &&
                        !string.IsNullOrWhiteSpace(c.SelectedLocationDisplayName))
                    .GroupBy(c => c.SelectedLocationId!.Value)
                    .Select(g => new FilterOption(
                        g.Key.ToString(),
                        g.First().SelectedLocationDisplayName!))
                    .OrderBy(o => o.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return BuildDefaultFromRawValues(criteriaKey, mapping, rawValues: [], explicitOptions);
            }

            if (mapping.CollectionOptionExtractor is null)
            {
                throw new Exception(
                    $"Collection criteria key '{criteriaKey}' generates options but has no CollectionOptionExtractor.");
            }

            var rawValues = cards
                .Select(mapping.CollectionOptionExtractor)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!)
                .ToList();

            return BuildDefaultFromRawValues(criteriaKey, mapping, rawValues, explicitOptions: null);
        }
        private static FilterDefaults BuildDefaultFromRawValues(string criteriaKey, CriteriaSpec mapping, List<string> rawValues, List<FilterOption>? explicitOptions)
        {
            var cleanedValues = rawValues;

            if (explicitOptions is null)
            {
                var removeItems = GetUnwantedItems(criteriaKey);
                cleanedValues = CleanAndFilter(rawValues, removeItems, mapping.ShouldNotSplit);

                if (criteriaKey.Equals("SetName", StringComparison.OrdinalIgnoreCase))
                {
                    cleanedValues = SortSetNamesByReleaseDate(cleanedValues);
                }

                if (criteriaKey.Equals("Colors", StringComparison.OrdinalIgnoreCase))
                {
                    cleanedValues = rawValues;
                }
            }

            List<int>? numericValues = null;

            if (mapping.Type == FilterType.Numeric)
            {
                numericValues = [.. cleanedValues.Where(v => int.TryParse(v, out _)).Select(int.Parse)];
            }

            var filterOptions = explicitOptions
                ?? cleanedValues.Select(v => new FilterOption(v, v)).ToList();

            var defaultText = string.Empty;

            if (mapping.Type == FilterType.Multi || criteriaKey.Equals("Text", StringComparison.OrdinalIgnoreCase) || criteriaKey.Equals("Comment", StringComparison.OrdinalIgnoreCase))
            {
                defaultText = string.IsNullOrWhiteSpace(mapping.ReadableLabel)
                    ? $"{criteriaKey} ..."
                    : $"{mapping.ReadableLabel} ...";
            }

            var readableLabel = string.IsNullOrEmpty(mapping.ReadableLabel)
                ? criteriaKey
                : mapping.ReadableLabel;

            return new FilterDefaults
            {
                CriteriaKey = criteriaKey,
                FilterOptions = filterOptions,
                NumericCriteria = numericValues,
                DefaultText = defaultText,
                ReadableLabel = readableLabel
            };
        }
        private static List<string> ExtractValues<T>(IReadOnlyList<T> cards, Func<T, string?> selector)
        {
            var values = new List<string>();

            foreach (var card in cards)
            {
                var value = selector(card);

                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value);
                }
            }

            return values;
        }
        private static List<string> SortSetNamesByReleaseDate(List<string> setNames)
        {
            if (CardDataProviders.SetMetaProvider is not ValueProvider<string, SetDto> provider)
            {
                return setNames;
            }

            var releaseDateMap = provider.Values
                .Where(s => !string.IsNullOrWhiteSpace(s.Name) && s.ReleaseDate.HasValue)
                .ToDictionary(
                    s => s.Name!,
                    s => s.ReleaseDate!.Value,
                    StringComparer.OrdinalIgnoreCase);

            return
            [
                .. setNames.OrderByDescending(name => releaseDateMap.TryGetValue(name, out var date)
                ? date
                : DateTime.MinValue)
            ];
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

        // Comma NOT followed by exactly 3 digits at word boundary. To catch keywords with comma in them. E.g. "Flying, vigilance" should split, but "10,000" should not.
        private static readonly Regex SplitCommaRegex = MyRegex();

        [GeneratedRegex(@",(?!\d{3}\b)", RegexOptions.Compiled)]
        private static partial Regex MyRegex();
    }
}

