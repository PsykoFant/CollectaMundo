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

                // Define the query explicitly using a switch statement on the criteria key.
                string query = criteriaKey switch
                {
                    "Name" => "SELECT DISTINCT Name FROM view_allCards;",
                    "SetName" => "SELECT DISTINCT SetName FROM view_allCards;",
                    "Text" => "SELECT DISTINCT RulesText FROM view_allCards;",
                    "Colors" => "SELECT DISTINCT Colors FROM view_allCards;", // Adjust if needed.
                    "Rarity" => "SELECT DISTINCT Rarity FROM view_allCards;",
                    "SuperTypes" => "SELECT DISTINCT SuperTypes FROM view_allCards;",
                    "Types" => "SELECT DISTINCT Type FROM view_allCards;",
                    "SubTypes" => "SELECT DISTINCT SubTypes FROM view_allCards;",
                    "Keywords" => "SELECT DISTINCT Keywords FROM view_allCards;",
                    "Finishes" => "SELECT DISTINCT Finishes FROM view_allCards;",
                    "Language" => "SELECT DISTINCT Language FROM view_myCollection;",
                    "SelectedCondition" => "SELECT DISTINCT Condition FROM view_myCollection;",
                    "ManaValue" => "SELECT DISTINCT ManaValue FROM view_allCards;",
                    "CardsForTrade" => "SELECT DISTINCT CardsForTrade FROM view_myCollection;",
                    _ => throw new Exception($"Unhandled criteria key: {criteriaKey}")
                };

                var distinctValues = new List<string>();

                try
                {
                    using SQLiteCommand command = new(query, DBAccess.connection);
                    using DbDataReader reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        // Retrieve the value using the column name that corresponds to the criteria.
                        // For example, for "Text", we read from "RulesText" column.
                        string? value = criteriaKey switch
                        {
                            "Name" => reader["Name"] as string,
                            "SetName" => reader["SetName"] as string,
                            "Text" => reader["RulesText"] as string,
                            "Colors" => reader["Colors"] as string,
                            "Rarity" => reader["Rarity"] as string,
                            "SuperTypes" => reader["SuperTypes"] as string,
                            "Types" => reader["Type"] as string,
                            "SubTypes" => reader["SubTypes"] as string,
                            "Keywords" => reader["Keywords"] as string,
                            "Finishes" => reader["Finishes"] as string,
                            "Language" => reader["Language"] as string,
                            "SelectedCondition" => reader["Condition"] as string,
                            "ManaValue" => reader["ManaValue"].ToString(),
                            "CardsForTrade" => reader["CardsForTrade"].ToString(),
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

                // Apply cleaning and filtering.
                var removeItems = GetUnwantedItems(criteriaKey);
                bool shouldNotSplit = mapping.ShouldNotSplit;
                var cleanedValues = CleanAndFilter(distinctValues, removeItems, shouldNotSplit);

                // Special handling for Colors: add predefined colors if needed.
                if (criteriaKey.Equals("Colors", StringComparison.OrdinalIgnoreCase))
                {
                    var predefinedColors = new List<string> { "W", "U", "B", "R", "G", "C", "X", "Colorless" };
                    cleanedValues = predefinedColors.Union(cleanedValues).ToList();
                }

                // Process numeric filters by converting values to int as needed.
                List<int>? numericValues = null;
                if (mapping.Type == FilterType.Numeric)
                {
                    numericValues = cleanedValues
                        .Where(v => int.TryParse(v, out _))
                        .Select(int.Parse)
                        .ToList();
                }

                var filterOptions = cleanedValues.Select(value => new FilterOption(value)).ToList();

                // Determine default text.
                string defaultText = string.IsNullOrWhiteSpace(mapping.ReadableLabel)
                    ? $"{criteriaKey} ..."
                    : $"{mapping.ReadableLabel} ...";

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


        private static List<string> ExtractCriteriaValues(string propertyName, List<CardSet> sourceCollection)
        {
            var propertyInfo = typeof(CardSet).GetProperty(propertyName);
            if (propertyInfo == null)
            {
                Debug.WriteLine($"[ERROR] Property '{propertyName}' not found in CardSet.");
                return [];
            }

            return [.. sourceCollection
                .Select(card => propertyInfo.GetValue(card))
                .Where(value => value != null)
                .SelectMany(value =>
                {
                    return value switch
                    {
                        string str => [str],                     // String properties
                        List<string> strList => strList,         // List<string> properties
                        double dbl => [dbl.ToString()],          // Convert numeric values to strings
                        int num => [num.ToString()],             // Convert int values to strings
                        _ => []                                  // Ignore unsupported types
                    };
                }).Distinct()];
        }
        private static List<string> CleanAndFilter(IEnumerable<string> input, HashSet<string>? removeItems, bool shouldNotSplit)
        {
            char[] separatorArray = [','];

            var processedItems = input
                .Where(item => !string.IsNullOrEmpty(item)) // Ensure we don't process null/empty values
                .SelectMany<string, string>(item => shouldNotSplit
                    ? new List<string> { item } // Keep whole string if shouldNotSplit == true
                    : item.Split(separatorArray, StringSplitOptions.RemoveEmptyEntries)) // Otherwise, split by comma
                .Select(item => item.Trim())
                .Where(item => removeItems == null || !removeItems.Contains(item)) // Apply filter
                .Distinct()
                .ToList();

            // Separate Numeric & Non-Numeric Values
            var numericValues = processedItems
                .Where(v => int.TryParse(v, out _))       // Extract integer values
                .Select(int.Parse)                        // Convert to int
                .OrderBy(n => n)                          // Sort Numerically
                .Select(n => n.ToString())                // Convert back to string for compatibility
                .ToList();

            var stringValues = processedItems
                .Where(v => !int.TryParse(v, out _))      // Extract non-numeric values
                .OrderBy(v => v)                          // Sort Alphabetically
                .ToList();

            return [.. numericValues, .. stringValues]; // Keep numeric values first
        }
        private static HashSet<string>? GetUnwantedItems(string criteriaKey)
        {
            return criteriaKey switch
            {
                "Types" => ["Eaturecray", "Summon", "Scariest", "You'll", "Ever", "See", "Jaguar", "Dragon", "Knights", "Legend", "instant", "Cards"],
                "SubTypes" => ["(creature", "and/or", "type)|Judge", "The"],
                _ => null
            };
        }
    }
}
