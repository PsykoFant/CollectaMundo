using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace CollectaMundo.Presentation.Behaviors
{
    public static class DataGridSelectedItemsBehavior
    {
        public static readonly DependencyProperty SyncedSelectedItemsProperty =
            DependencyProperty.RegisterAttached(
                "SyncedSelectedItems",
                typeof(IList),
                typeof(DataGridSelectedItemsBehavior),
                new PropertyMetadata(null, OnSyncedSelectedItemsChanged));

        public static void SetSyncedSelectedItems(DependencyObject element, IList? value)
        {
            element.SetValue(SyncedSelectedItemsProperty, value);
        }

        public static IList? GetSyncedSelectedItems(DependencyObject element)
        {
            return (IList?)element.GetValue(SyncedSelectedItemsProperty);
        }

        private static readonly DependencyProperty IsSubscribedProperty =
            DependencyProperty.RegisterAttached(
                "IsSubscribed",
                typeof(bool),
                typeof(DataGridSelectedItemsBehavior),
                new PropertyMetadata(false));

        private static void OnSyncedSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid dataGrid)
                return;

            bool isSubscribed = (bool)dataGrid.GetValue(IsSubscribedProperty);

            if (!isSubscribed)
            {
                dataGrid.SelectionChanged += DataGrid_SelectionChanged;
                dataGrid.SetValue(IsSubscribedProperty, true);
            }

            SyncFromGrid(dataGrid);
        }

        private static void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid dataGrid)
            {
                SyncFromGrid(dataGrid);
            }
        }

        private static void SyncFromGrid(DataGrid dataGrid)
        {
            IList? target = GetSyncedSelectedItems(dataGrid);
            if (target is null)
                return;

            target.Clear();

            foreach (var item in dataGrid.SelectedItems)
            {
                target.Add(item);
            }
        }
    }
}
