using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace CollectaMundo.Behaviors
{
    public static class DataGridSelectedItemsBehavior
    {
        public static readonly DependencyProperty BoundSelectedItemsProperty =
            DependencyProperty.RegisterAttached(
                "BoundSelectedItems",
                typeof(IList),
                typeof(DataGridSelectedItemsBehavior),
                new PropertyMetadata(null, OnBoundSelectedItemsChanged));

        public static void SetBoundSelectedItems(DependencyObject element, IList value)
        {
            element.SetValue(BoundSelectedItemsProperty, value);
        }

        public static IList GetBoundSelectedItems(DependencyObject element)
        {
            return (IList)element.GetValue(BoundSelectedItemsProperty);
        }

        private static void OnBoundSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataGrid dataGrid)
            {
                // Removed unsubscribe since we don't have a named handler.
                if (e.NewValue is INotifyCollectionChanged newCollection)
                {
                    newCollection.CollectionChanged += (s, args) =>
                    {
                        // When the bound collection is cleared, unselect all rows in the DataGrid.
                        if (args.Action == NotifyCollectionChangedAction.Reset ||
                            (args.NewItems == null && dataGrid.SelectedItems.Count > 0))
                        {
                            dataGrid.UnselectAll();
                        }
                    };
                }
                dataGrid.SelectionChanged -= DataGrid_SelectionChanged;
                dataGrid.SelectionChanged += DataGrid_SelectionChanged;
            }
        }

        private static void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid dataGrid)
            {
                var boundSelectedItems = GetBoundSelectedItems(dataGrid);
                if (boundSelectedItems == null)
                    return;

                boundSelectedItems.Clear();
                foreach (var item in dataGrid.SelectedItems)
                {
                    boundSelectedItems.Add(item);
                }
            }
        }
    }
}
