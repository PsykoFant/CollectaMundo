using CollectaMundo.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace CollectaMundo.Presentation.Behaviors
{
    public static class DataGridColumnResizerBehavior
    {
        public static readonly DependencyProperty EnableAutoResizeProperty = DependencyProperty.RegisterAttached("EnableAutoResize", typeof(bool), typeof(DataGridColumnResizerBehavior), new PropertyMetadata(false, OnEnableAutoResizeChanged));
        public static void ForceUpdate(DataGrid dataGrid)
        {
            UpdateColumnWidths(dataGrid);
        }
        public static bool GetEnableAutoResize(DependencyObject obj) => (bool)obj.GetValue(EnableAutoResizeProperty);
        public static void SetEnableAutoResize(DependencyObject obj, bool value) => obj.SetValue(EnableAutoResizeProperty, value);

        public static readonly DependencyProperty DataGridIndexProperty = DependencyProperty.RegisterAttached("DataGridIndex", typeof(int), typeof(DataGridColumnResizerBehavior), new PropertyMetadata(-1));
        public static int GetDataGridIndex(DependencyObject obj) => (int)obj.GetValue(DataGridIndexProperty);
        public static void SetDataGridIndex(DependencyObject obj, int value) => obj.SetValue(DataGridIndexProperty, value);
        private static void OnEnableAutoResizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataGrid dataGrid)
            {
                bool enable = (bool)e.NewValue;
                if (enable)
                {
                    dataGrid.SizeChanged += OnDataGridSizeChanged;
                    // If the DataGrid is not loaded, subscribe to Loaded event.
                    if (dataGrid.IsLoaded)
                    {
                        UpdateColumnWidths(dataGrid);
                    }
                    else
                    {
                        dataGrid.Loaded += DataGrid_Loaded;
                    }
                }
                else
                {
                    dataGrid.SizeChanged -= OnDataGridSizeChanged;
                    dataGrid.Loaded -= DataGrid_Loaded;
                }
            }
        }
        private static void DataGrid_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is DataGrid dataGrid)
            {
                dataGrid.Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateColumnWidths(dataGrid);
                }), System.Windows.Threading.DispatcherPriority.Loaded);
                dataGrid.Loaded -= DataGrid_Loaded;
            }
        }
        private static void OnDataGridSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is not DataGrid dataGrid)
                return;
            UpdateColumnWidths(dataGrid);
        }
        private static void UpdateColumnWidths(DataGrid dataGrid)
        {
            // Check if running in design mode.
            if (DesignerProperties.GetIsInDesignMode(dataGrid))
                return;

            int dataGridIndex = GetDataGridIndex(dataGrid);
            if (dataGrid.DataContext is not MainWindowViewModel vm)
                return;
            var list = vm.ColumnWidths;
            if (dataGridIndex < 0 || dataGridIndex >= list.Count)
                return;

            List<int[]> paddingsList = new List<int[]>
            {
                new int[] {65, 50}, // For AllCardsDataGrid (index 0)
                new int[] {65, 50}, // For MyCollectionDataGrid (index 1)
                new int[] {65}      // For AllCardsForDecksDataGrid (index 2)
            };

            if (dataGridIndex >= paddingsList.Count)
                return;

            int[] paddings = paddingsList[dataGridIndex];
            for (int colIndex = 0; colIndex < paddings.Length; colIndex++)
            {
                if (colIndex >= list[dataGridIndex].Count)
                    continue;

                double currentWidth = dataGrid.Columns[colIndex].ActualWidth;
                double newWidth = currentWidth - paddings[colIndex];

                if (newWidth > 0 && Math.Abs(list[dataGridIndex][colIndex] - newWidth) > 0.5)
                {
                    list[dataGridIndex][colIndex] = newWidth;
                }
            }
        }

        // Kick once when the DataGrid becomes visible
        public static readonly DependencyProperty KickOnVisibleProperty =
            DependencyProperty.RegisterAttached(
                "KickOnVisible",
                typeof(bool),
                typeof(DataGridColumnResizerBehavior),
                new PropertyMetadata(false, OnKickOnVisibleChanged));

        public static void SetKickOnVisible(DependencyObject d, bool value) =>
            d.SetValue(KickOnVisibleProperty, value);

        public static bool GetKickOnVisible(DependencyObject d) =>
            (bool)d.GetValue(KickOnVisibleProperty);

        private static void OnKickOnVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid dg) return;

            if ((bool)e.NewValue)
            {
                // subscribe
                dg.IsVisibleChanged += Dg_IsVisibleChanged;
            }
            else
            {
                // unsubscribe
                dg.IsVisibleChanged -= Dg_IsVisibleChanged;
            }
        }

        private static void Dg_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var dg = sender as DataGrid;
            if (dg is null) return;

            if (dg.IsVisible)
            {
                // Run after layout so ActualWidth is valid
                dg.Dispatcher.BeginInvoke(
                    new Action(() => UpdateColumnWidths(dg)),
                    System.Windows.Threading.DispatcherPriority.Render);

                // One-shot: remove the handler to avoid repeated kicks
                dg.IsVisibleChanged -= Dg_IsVisibleChanged;
            }
        }
    }
}
