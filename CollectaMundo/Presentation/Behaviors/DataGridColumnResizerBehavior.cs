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
            if (dataGridIndex < 0 || dataGridIndex >= MainWindow.CurrentInstance.ColumnWidths.Count)
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
                if (colIndex >= MainWindow.CurrentInstance.ColumnWidths[dataGridIndex].Count)
                    continue;

                double currentWidth = dataGrid.Columns[colIndex].ActualWidth;
                double newWidth = currentWidth - paddings[colIndex];

                if (newWidth > 0 && Math.Abs(MainWindow.CurrentInstance.ColumnWidths[dataGridIndex][colIndex] - newWidth) > 0.5)
                {
                    MainWindow.CurrentInstance.ColumnWidths[dataGridIndex][colIndex] = newWidth;
                }
            }
        }
    }
}
