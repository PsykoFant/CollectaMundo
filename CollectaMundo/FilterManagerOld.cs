using CollectaMundo.Models;
using ServiceStack;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CollectaMundo
{
    public class FilterManagerOld
    {
        #region Filtering
        public static void ApplyFilter(IEnumerable<CardSet> cards, DataGrid dataGrid)
        {
            //try
            //{
            //    if (MainWindow.CurrentInstance._isStartup) return;

            //    Debug.WriteLine($"Applying filter to DataGrid: {dataGrid.Name}, Total cards before filtering: {cards.Count()}");

            //    // Create strongly-typed filter criteria
            //    var filterCriteria = MainWindow.CurrentInstance.filterSelections
            //        .Select(fs => fs.ToFilterCriteria())
            //        .ToList();

            //    Debug.WriteLine($"Total filters to apply: {filterCriteria.Count}");

            //    // **Check if the field exists in the current list**
            //    var validFilters = filterCriteria
            //        .Where(filter => PropertyExistsInList(filter.CriteriaKey, cards))
            //        .ToList();

            //    Debug.WriteLine($"Valid filters remaining after property check: {validFilters.Count}");

            //    // **If no valid filters remain, return the unfiltered list**
            //    if (validFilters.Count == 0)
            //    {
            //        Debug.WriteLine($"No valid filters found for DataGrid {dataGrid.Name}, returning unfiltered cards.");
            //        return;
            //    }

            //    // Apply filters
            //    var filteredCards = FilterCardsByUnifiedCriteria(cards, validFilters);
            //    var finalFilteredCards = filteredCards.ToList();

            //    Debug.WriteLine($"Total cards after filtering: {finalFilteredCards.Count}");

            //    // Update DataGrid
            //    SaveAndRestoreSort(dataGrid, () =>
            //    {
            //        dataGrid.ItemsSource = finalFilteredCards;
            //    });

            //    // ✅ Now uses `FilterViewModel` to update count
            //    MainWindow.CurrentInstance.FilterVM.UpdateCardCount(dataGrid.Name, finalFilteredCards.Count);

            //    // ✅ Update `FilterViewModel` instead of UI directly
            //    MainWindow.CurrentInstance.FilterVM.UpdateSummary(validFilters);
            //}
            //catch (Exception ex)
            //{
            //    Debug.WriteLine($"Error while filtering datagrid: {ex.Message}");
            //    _ = MessageBox.Show($"Error while filtering datagrid: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            //}
        }
        //private static IEnumerable<CardSet> FilterCardsByUnifiedCriteria(IEnumerable<CardSet> cards, IEnumerable<BaseFilterCriteria> filterCriteria)
        //{
        //    return cards.Where(card => filterCriteria.All(filter => filter.Matches(card)));
        //}
        //private static bool PropertyExistsInList(string? criteriaKey, IEnumerable<CardSet> cards)
        //{
        //    if (string.IsNullOrEmpty(criteriaKey))
        //    {
        //        Debug.WriteLine("PropertyExistsInList: criteriaKey is null or empty.");
        //        return false;
        //    }

        //    // Get the property mapping

        //    // commented out - referenced the old CriteriaKeyToPropertyMap
        //    //if (!MainWindow.CurrentInstance.CriteriaKeyToPropertyMap.TryGetValue(criteriaKey, out var propertyName))
        //    //{
        //    //    Debug.WriteLine($"PropertyExistsInList: No property mapping found for criteriaKey: {criteriaKey}");
        //    //    return false;
        //    //}

        //    // commented out - referenced the old CriteriaKeyToPropertyMap
        //    // Check if the property exists on at least one card in the list AND has a non-null value
        //    //bool hasValidProperty = cards.Any(card =>
        //    //{
        //    //    var property = card.GetType().GetProperty(propertyName);
        //    //    if (property == null) return false; // Property does not exist on this card type

        //    //    var value = property.GetValue(card);
        //    //    return value != null; // Ensures that the property is actually set on at least one object
        //    //});

        //    //return hasValidProperty;
        //}

        //public static IEnumerable<CardSet> FilterByColor(IEnumerable<CardSet> cards, HashSet<string> selectedColors, int filterMode)
        //{
        //    if (cards == null)
        //    {
        //        Debug.WriteLine("Warning: Card collection is null.");
        //        return []; // Return an empty collection instead of null
        //    }

        //    if (selectedColors == null || selectedColors.Count == 0)
        //    {
        //        return cards; // Cards are returned unfiltered when no colors are selected
        //    }

        //    return cards.Where(card =>
        //    {
        //        // Prepare collections for easier matching
        //        var manaCostSymbols = new HashSet<string>(card.ManaCost?.Split(',').Select(c => c.Trim()) ?? []);
        //        var colorSymbols = new HashSet<string>(card.Colors?.Split(',').Select(c => c.Trim()) ?? []);

        //        bool manaCostMatch = false;
        //        bool colorMatch = false;

        //        // Check for "C" and "X" in ManaCost
        //        if (selectedColors.Contains("C") || selectedColors.Contains("X"))
        //        {
        //            var manaCostCriteria = new HashSet<string>(selectedColors.Intersect(["C", "X"]));
        //            manaCostMatch = manaCostCriteria.All(manaCostSymbols.Contains);
        //        }

        //        // Check for colored mana and "Colorless" in Colors
        //        bool colorlessMatch = selectedColors.Contains("Colorless") && string.IsNullOrWhiteSpace(card.Colors);
        //        if (selectedColors.Overlaps(["W", "U", "B", "R", "G", "Colorless"]))
        //        {
        //            var coloredCriteria = new HashSet<string>(selectedColors.Where(c => c != "Colorless"));
        //            colorMatch = coloredCriteria.All(colorSymbols.Contains) || colorlessMatch;
        //        }

        //        // ✅ FIX: Stricter "ALL" logic to enforce both conditions simultaneously
        //        return filterMode switch
        //        {
        //            0 => selectedColors.Any(c => manaCostSymbols.Contains(c) || colorSymbols.Contains(c) ||
        //                    (c == "Colorless" && string.IsNullOrWhiteSpace(card.Colors))), // ANY
        //            1 => selectedColors.All(c =>
        //                    (c == "Colorless" && string.IsNullOrWhiteSpace(card.Colors)) ||
        //                    manaCostSymbols.Contains(c) ||
        //                    colorSymbols.Contains(c)), // ALL: Both conditions must be met
        //            2 => !selectedColors.Any(c => manaCostSymbols.Contains(c) || colorSymbols.Contains(c) ||
        //                    (c == "Colorless" && string.IsNullOrWhiteSpace(card.Colors))), // NONE
        //            _ => false
        //        };
        //    });
        //}

        #endregion

        #region Filter UI updates
        public static void SaveAndRestoreSort(DataGrid dataGrid, Action updateItemsSource)
        {
            // Step 1: Save current sort descriptions
            var sortDescriptions = dataGrid.Items.SortDescriptions.ToList();
            var sortedColumns = dataGrid.Columns
                .Where(column => column.SortDirection.HasValue)
                .ToDictionary(column => column, column => column.SortDirection);

            // Step 2: Perform the update (reset ItemsSource)
            updateItemsSource?.Invoke();

            // Step 3: Restore sort descriptions
            dataGrid.Items.SortDescriptions.Clear();
            foreach (var sortDescription in sortDescriptions)
            {
                dataGrid.Items.SortDescriptions.Add(sortDescription);
            }

            // Restore column sort directions
            foreach (var column in sortedColumns)
            {
                column.Key.SortDirection = column.Value;
            }
        }

        #endregion

        #region Filter Helper Methods

        /// <summary>
        /// Find children of a dependency object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static T? FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            try
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                    if (child is T correctChild)
                    {
                        return correctChild;
                    }

                    T? childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                    {
                        return childOfChild;
                    }
                }
            }
            catch (Exception ex)
            {
                // Optionally log the exception if needed
                Debug.WriteLine($"An error occurred while searching for visual child: {ex}");
            }

            return null;
        }

        /// <summary>
        /// Find all children of a dependency object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="depObj"></param>
        /// <returns></returns>
        public static List<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            List<T> children = [];
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null)
                    {
                        if (child is T t)
                        {
                            children.Add(t);
                        }

                        // Recursive call only if child is not null
                        children.AddRange(FindVisualChildren<T>(child));
                    }
                }
            }

            return children;
        }

        #endregion
    }
}