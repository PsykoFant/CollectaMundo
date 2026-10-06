using CollectaMundo.ApplicationServices.CardLegalities;
using CollectaMundo.ApplicationServices.KeyedDataProvider.Providers;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Filtering;
using CollectaMundo.DomainLogic.Filtering.Enums;
using CollectaMundo.DomainLogic.Filtering.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.CardModels;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace CollectaMundo.Data.Filtering
{
    public partial class FilterDefaultsLogic(ICardLegalityProviderService cardLegalityProviderService) : IFilterDefaultsLogic
    {
        private readonly ICardLegalityProviderService _cardLegalityProviderService = cardLegalityProviderService;
        public List<FilterDefaults> BuildFilters(IReadOnlyList<PrintingCard> allCards, IReadOnlyList<CollectionCard> myCollection)
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

            foreach (var criteriaKey
         in FilterCriteriaMappings.CriteriaMappings.Keys)
            {
                if (!_buildTimings.TryGetValue(
                        criteriaKey,
                        out var timing))
                {
                    continue;
                }

                Debug.WriteLine(
                    $"[FilterDefaults] {criteriaKey}: " +
                    $"extract {timing.ExtractMilliseconds} ms, " +
                    $"process {timing.BuildMilliseconds} ms, " +
                    $"raw {timing.RawValueCount}");
            }

            return [.. FilterCriteriaMappings.CriteriaMappings.Keys.Select(k => filterDefaultsDict[k])];
        }
        private FilterDefaults BuildPrintingDefault(string criteriaKey, CriteriaSpec mapping, IReadOnlyList<PrintingCard> cards)
        {
            if (criteriaKey.Equals(
                    "LegalFormats",
                    StringComparison.OrdinalIgnoreCase))
            {
                var explicitOptions =
                    _cardLegalityProviderService.Formats
                        .Select(format =>
                            new FilterOption(
                                format.Mask.ToString(),
                                format.DisplayName))
                        .ToList();

                return BuildDefaultFromRawValues(
                    criteriaKey,
                    mapping,
                    rawValues: [],
                    explicitOptions);
            }

            var extractSw = Stopwatch.StartNew();

            List<string> rawValues = criteriaKey switch
            {
                "Colors" =>
                    ["W", "U", "B", "R", "G", "C", "X", "Colorless"],

                "Text" or "Comment" or "CardsForTrade" =>
                    [],

                "ManaValue" =>
                    [.. cards.Select(c => c.ManaValue.ToString())],

                "Name" =>
                    ExtractValues(cards, c => c.Name),

                "SetName" =>
                    ExtractValues(cards, c => c.SetName),

                "Rarity" =>
                    ExtractValues(cards, c => c.Rarity),

                "SuperTypes" =>
                    ExtractValues(cards, c => c.SuperTypes),

                "Types" =>
                    ExtractValues(cards, c => c.Types),

                "SubTypes" =>
                    ExtractValues(cards, c => c.SubTypes),

                "Keywords" =>
                    ExtractValues(cards, c => c.Keywords),

                "Finishes" =>
                    ExtractValues(cards, c => c.Finishes),

                "Availability" =>
                    ExtractValues(cards, c => c.Availability),

                "GamePlayCard" =>
                    ["0", "1"],

                _ => throw new Exception(
                    $"Unhandled printing criteria key: {criteriaKey}")
            };

            extractSw.Stop();

            var buildSw = Stopwatch.StartNew();

            var result = BuildDefaultFromRawValues(
                criteriaKey,
                mapping,
                rawValues,
                explicitOptions: null);

            buildSw.Stop();

            _buildTimings[criteriaKey] =
                new FilterBuildTiming(
                    extractSw.ElapsedMilliseconds,
                    buildSw.ElapsedMilliseconds,
                    rawValues.Count);

            return result;
        }
        private readonly record struct FilterBuildTiming(
    long ExtractMilliseconds,
    long BuildMilliseconds,
    int RawValueCount);

        private readonly ConcurrentDictionary<string, FilterBuildTiming>
    _buildTimings = new();

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
                cleanedValues = CleanAndFilter(criteriaKey, rawValues, removeItems, mapping.ShouldNotSplit);

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
                ?? [.. cleanedValues.Select(v => new FilterOption(v, v))];

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
        private static List<string> CleanAndFilter(string criteriaKey, IEnumerable<string> input, HashSet<string>? removeItems, bool shouldNotSplit)
        {
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in input)
            {
                if (string.IsNullOrWhiteSpace(item))
                {
                    continue;
                }

                // Fast path:
                // If splitting is disabled OR there is no comma,
                // there is no reason to invoke the regex splitter.
                if (shouldNotSplit || item.IndexOf(',') < 0)
                {
                    AddValue(item);
                    continue;
                }

                foreach (var part in CommaSeparatedValues.SplitPreservingThousandsSeparators(item))
                {
                    AddValue(part);
                }
            }

            var numerics = new List<int>();
            var strings = new List<string>();

            foreach (var value in unique)
            {
                if (int.TryParse(value, out var numeric))
                {
                    numerics.Add(numeric);
                }
                else
                {
                    strings.Add(value);
                }
            }

            numerics.Sort();
            strings.Sort(StringComparer.OrdinalIgnoreCase);

            return [.. numerics.Select(n => n.ToString()), .. strings];

            void AddValue(string value)
            {
                var trimmed = NormalizeFilterOptionValue(criteriaKey, value.Trim());

                if (string.IsNullOrEmpty(trimmed))
                {
                    return;
                }

                if (removeItems is not null && removeItems.Contains(trimmed))
                {
                    return;
                }

                unique.Add(trimmed);
            }
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

        // Special case handling for Plane and pLAne
        private static string NormalizeFilterOptionValue(string criteriaKey, string value)
        {
            if (criteriaKey.Equals("Types", StringComparison.OrdinalIgnoreCase) && value.Equals("Plane", StringComparison.OrdinalIgnoreCase))
            {
                return "Plane";
            }

            return value;
        }
    }
}

