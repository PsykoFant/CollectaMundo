using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace CollectaMundo.Presentation.Behaviors
{
    public static class DataGridPersistSortBehavior
    {
        public static readonly DependencyProperty EnableProperty = DependencyProperty.RegisterAttached("Enable", typeof(bool), typeof(DataGridPersistSortBehavior),
                new PropertyMetadata(false, OnEnableChanged));

        public static void SetEnable(DependencyObject obj, bool value) => obj.SetValue(EnableProperty, value);
        public static bool GetEnable(DependencyObject obj) => (bool)obj.GetValue(EnableProperty);

        private static readonly DependencyProperty StoredSortsProperty = DependencyProperty.RegisterAttached("StoredSorts", typeof(List<SortDescription>), typeof(DataGridPersistSortBehavior),
                new PropertyMetadata(null));
        private static void SetStoredSorts(DependencyObject obj, List<SortDescription>? value) => obj.SetValue(StoredSortsProperty, value);
        private static List<SortDescription>? GetStoredSorts(DependencyObject obj) => (List<SortDescription>?)obj.GetValue(StoredSortsProperty);
        private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid grid) return;

            if ((bool)e.NewValue)
            {
                grid.Sorting += Grid_Sorting;
                grid.TargetUpdated += Grid_TargetUpdated; // requires NotifyOnTargetUpdated=True
            }
            else
            {
                grid.Sorting -= Grid_Sorting;
                grid.TargetUpdated -= Grid_TargetUpdated;
            }
        }
        private static void Grid_Sorting(object? sender, DataGridSortingEventArgs e)
        {
            // Let DataGrid perform its sort first, then capture the final SortDescriptions.
            if (sender is not DataGrid grid) return;
            grid.Dispatcher.BeginInvoke(new Action(() =>
            {
                var view = CollectionViewSource.GetDefaultView(grid.ItemsSource);
                if (view is null) return;

                var snapshot = view.SortDescriptions.ToList();
                SetStoredSorts(grid, snapshot);
            }));
        }
        private static void Grid_TargetUpdated(object? sender, DataTransferEventArgs e)
        {
            if (sender is not DataGrid grid || e.Property != ItemsControl.ItemsSourceProperty) return;

            var stored = GetStoredSorts(grid);
            if (stored is null || stored.Count == 0) return;

            var view = CollectionViewSource.GetDefaultView(grid.ItemsSource);
            if (view is null) return;

            using (view.DeferRefresh())
            {
                view.SortDescriptions.Clear();
                foreach (var sd in stored)
                    view.SortDescriptions.Add(sd);
            }

            // Update column glyphs
            foreach (var col in grid.Columns)
            {
                var sd = stored.FirstOrDefault(s => s.PropertyName == (col.SortMemberPath ?? string.Empty));
                col.SortDirection = sd.PropertyName is null
                    ? null
                    : sd.Direction;
            }
        }
    }
}
