using CollectaMundo.Models;
using CollectaMundo.Utilities;
using CollectaMundo.ViewModels;
using System.Diagnostics;
using System.Windows;

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

        public static List<FilterDefaults> GetFilterDefaults(CardViewModel allCardsVM, CardViewModel myCollectionVM)
        {
            try
            {
                return [.. FilterCriteriaMappings.CriteriaMappings.Select(entry =>
                {
                    List<CardSet> sourceCollection = entry.Value.ListName switch{
                    "AllCards" => allCardsVM.Cards, "MyCollection" => myCollectionVM.Cards, _ =>
                    throw new Exception($"Invalid list name: {entry.Value.ListName}"), };

                    var rawValues = ExtractCriteriaValues(entry.Key, sourceCollection);
                    var removeItems = GetUnwantedItems(entry.Key);
                    bool shouldNotSplit = entry.Value.ShouldNotSplit;
                    var filteredValues = CleanAndFilter(rawValues, removeItems, shouldNotSplit);

                    // Special handling for "Colors" (add predefined values)
                    if (entry.Key == "Colors")
                    {
                        var predefinedColors = new List<string> { "W", "U", "B", "R", "G", "C", "X", "Colorless" };
                        filteredValues = [.. predefinedColors.Union(filteredValues)];
                    }

                    // Convert numeric filters to List<int>
                    List<int>? numericValues = null;
                    if (entry.Value.Type == FilterType.Numeric)
                    {
                        numericValues = [.. filteredValues.Where(v => int.TryParse(v, out _)).Select(int.Parse)];
                    }

                    // Convert string options into FilterOption objects
                    var filterOptions = filteredValues.Select(value => new FilterOption(value)).ToList();

                    // Determine default text
                    var defaultText = string.Empty;
                    if (entry.Value.Type == FilterType.Multi || entry.Key == "Text")
                    {
                        if (entry.Value.ReadableLabel == "")
                        {
                            defaultText = $"{entry.Key} ...";
                        }
                        else
                        {
                            defaultText = $"{entry.Value.ReadableLabel} ...";
                        }
                    }

                    // Determine readable label (just use CriteriaKey if empty)
                    var readableLabel = string.IsNullOrEmpty(entry.Value.ReadableLabel)
                        ? entry.Key
                        : entry.Value.ReadableLabel;

                    return new FilterDefaults
                    {
                        CriteriaKey = entry.Key,
                        NumericCriteria = numericValues, // Store numeric values for numeric filters
                        FilterOptions = filterOptions,     // Store list of filter options
                        DefaultText = defaultText,
                        ReadableLabel = readableLabel
                    };
                })];
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error Getting Filter Defaults: {ex.Message}");
                MessageBox.Show($"Error Getting Filter Defaults: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return [];  // Return an empty list if an exception occurs.
            }
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
