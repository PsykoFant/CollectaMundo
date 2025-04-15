using CollectaMundo.Models;
using CollectaMundo.Utilities;
using CollectaMundo.ViewModels;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;

namespace CollectaMundo.Managers
{
    public static class FilterManager
    {
        public static List<CardSet> ApplyFilter(IEnumerable<CardSet> cards, IEnumerable<FilterItemViewModel> filterCriteria)
        {
            try
            {
                if (filterCriteria == null || !filterCriteria.Any())
                {
                    return [.. cards];
                }
                return [.. cards.Where(card => filterCriteria.All(filter => filter.Matches(card)))];
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while filtering cards: {ex.Message}");
                return [.. cards];
            }
        }
        public static async Task<List<FilterDefaults>> GetFilterDefaultsFromDBAsync()
        {
            var filterDefaultsList = new List<FilterDefaults>();

            // Iterate over each filter criterion defined in your mappings.
            foreach (var entry in FilterCriteriaMappings.CriteriaMappings)
            {
                string criteriaKey = entry.Key;
                var mapping = entry.Value;
                List<string> distinctValues = [];

                // For colors, skip the query and use hardcoded values.
                if (criteriaKey.Equals("Colors", StringComparison.OrdinalIgnoreCase))
                {
                    distinctValues = ["W", "U", "B", "R", "G", "C", "X", "Colorless"];
                }
                // Text and CardsForTrade do not need default values
                else if (criteriaKey.Equals("Text", StringComparison.OrdinalIgnoreCase))
                {
                    distinctValues = [];
                }
                else if (criteriaKey.Equals("CardsForTrade", StringComparison.OrdinalIgnoreCase))
                {
                    distinctValues = [];
                }

                else
                {
                    string query = criteriaKey switch
                    {
                        "Name" => "SELECT DISTINCT name FROM cards UNION ALL SELECT DISTINCT name FROM tokens AS Name;",
                        "SetName" => "SELECT DISTINCT name AS SetName FROM sets;",
                        "Rarity" => "SELECT DISTINCT rarity AS Rarity FROM cards;",
                        "SuperTypes" => "SELECT DISTINCT supertypes FROM cards UNION ALL SELECT DISTINCT supertypes FROM tokens AS SuperTypes;",
                        "Types" => "SELECT DISTINCT types FROM cards UNION ALL SELECT DISTINCT types FROM tokens AS Types;",
                        "SubTypes" => "SELECT DISTINCT subtypes FROM cards UNION ALL SELECT DISTINCT subtypes FROM tokens AS SubTypes;",
                        "Keywords" => "SELECT DISTINCT keywords FROM cards UNION ALL SELECT DISTINCT keywords FROM tokens AS Keywords;",
                        "Finishes" => "SELECT DISTINCT finishes FROM cards UNION ALL SELECT DISTINCT finishes FROM tokens AS Finishes;",
                        "Language" => "SELECT DISTINCT language AS Language FROM myCollection;",
                        "SelectedCondition" => "SELECT DISTINCT condition AS Condition FROM myCollection;",
                        "ManaValue" => "SELECT DISTINCT manaValue AS ManaValue FROM cards;",
                        _ => throw new Exception($"Unhandled criteria key: {criteriaKey}")
                    };

                    try
                    {
                        using SQLiteCommand command = new SQLiteCommand(query, DBAccess.connection);
                        using DbDataReader reader = await command.ExecuteReaderAsync();
                        while (await reader.ReadAsync())
                        {
                            string? value = criteriaKey switch
                            {
                                "Name" => reader["Name"] as string,
                                "SetName" => reader["SetName"] as string,
                                "Rarity" => reader["Rarity"] as string,
                                "SuperTypes" => reader["SuperTypes"] as string,
                                "Types" => reader["Types"] as string,
                                "SubTypes" => reader["SubTypes"] as string,
                                "Keywords" => reader["Keywords"] as string,
                                "Finishes" => reader["Finishes"] as string,
                                "Language" => reader["Language"] as string,
                                "SelectedCondition" => reader["Condition"] as string,
                                "ManaValue" => reader["ManaValue"].ToString(),
                                _ => null
                            };

                            if (!string.IsNullOrEmpty(value))
                            {
                                distinctValues.Add(value.Trim());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error querying distinct values for {criteriaKey}: {ex.Message}");
                        continue;
                    }
                }

                // Apply cleaning and filtering.
                var removeItems = GetUnwantedItems(criteriaKey);
                bool shouldNotSplit = mapping.ShouldNotSplit;
                var cleanedValues = CleanAndFilter(distinctValues, removeItems, shouldNotSplit);

                // Special handling for Colors: Here the cleanedValues already match the hardcoded values.
                if (criteriaKey.Equals("Colors", StringComparison.OrdinalIgnoreCase))
                {
                    cleanedValues = distinctValues;
                }

                // Process numeric filters.
                List<int>? numericValues = null;
                if (mapping.Type == FilterType.Numeric)
                {
                    numericValues = [.. cleanedValues.Where(v => int.TryParse(v, out _)).Select(int.Parse)];
                }

                var filterOptions = cleanedValues.Select(value => new FilterOption(value)).ToList();

                // Determine default text.
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
            // Use a HashSet for uniqueness; using StringComparer.OrdinalIgnoreCase if needed.
            var uniqueItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Process input in a single loop.
            foreach (var item in input)
            {
                if (string.IsNullOrEmpty(item))
                    continue;

                IEnumerable<string> parts;
                if (shouldNotSplit)
                {
                    parts = [item];
                }
                else
                {
                    parts = item.Split([','], StringSplitOptions.RemoveEmptyEntries);
                }

                foreach (var part in parts)
                {
                    string trimmed = part.Trim();
                    // Skip if the trimmed string is empty or should be removed.
                    if (string.IsNullOrEmpty(trimmed))
                        continue;
                    if (removeItems != null && removeItems.Contains(trimmed))
                        continue;

                    uniqueItems.Add(trimmed);
                }
            }

            // Separate unique items into numeric and non-numeric lists.
            var numericList = new List<int>();
            var stringList = new List<string>();

            foreach (var item in uniqueItems)
            {
                if (int.TryParse(item, out int num))
                {
                    numericList.Add(num);
                }
                else
                {
                    stringList.Add(item);
                }
            }

            // Sort numeric values and convert them back to strings.
            numericList.Sort();
            var numericStrings = numericList.Select(n => n.ToString());

            // Sort string values alphabetically.
            stringList.Sort(StringComparer.OrdinalIgnoreCase);

            // Combine numeric values first, then non-numeric.
            return [.. numericStrings, .. stringList];
        }
        private static HashSet<string>? GetUnwantedItems(string criteriaKey)
        {
            return criteriaKey switch
            {
                "Types" => ["Eaturecray", "Summon", "Scariest", "You'll", "Ever", "See", "Jaguar", "Dragon", "Knights", "Legend", "instant", "Cards"],
                "SubTypes" => ["(creature", "and/or", "type)|Judge", "The", "pLAnE"],
                _ => null
            };
        }
    }
}
