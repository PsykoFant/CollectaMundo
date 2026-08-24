using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace CollectaMundo.Presentation.Behaviors
{
    public static class ColumnWidthRefreshBehavior
    {
        public static readonly DependencyProperty RefreshTriggerProperty = DependencyProperty.RegisterAttached("RefreshTrigger", typeof(int), typeof(ColumnWidthRefreshBehavior), new PropertyMetadata(0, OnRefreshTriggerChanged));

        public static void SetRefreshTrigger(DependencyObject element, int value)
        {
            element.SetValue(RefreshTriggerProperty, value);
        }

        public static int GetRefreshTrigger(DependencyObject element)
        {
            return (int)element.GetValue(RefreshTriggerProperty);
        }

        private static void OnRefreshTriggerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DispatcherObject dispatcherObject)
            {
                return;
            }

            dispatcherObject.Dispatcher.BeginInvoke(() => RefreshColumns(d), DispatcherPriority.Loaded);
        }

        private static void RefreshColumns(DependencyObject control)
        {
            switch (control)
            {
                case ListView listView:
                    RefreshListViewColumns(listView);
                    break;

                case DataGrid dataGrid:
                    RefreshDataGridColumns(dataGrid);
                    break;
            }
        }

        private static void RefreshListViewColumns(ListView listView)
        {
            if (listView.View is not GridView gridView)
            {
                return;
            }

            foreach (var column in gridView.Columns)
            {
                if (double.IsNaN(column.Width))
                {
                    column.Width = column.ActualWidth;
                }
            }

            listView.UpdateLayout();

            foreach (var column in gridView.Columns)
            {
                column.Width = double.NaN;
            }
        }

        private static void RefreshDataGridColumns(DataGrid dataGrid)
        {
            var contentSizedColumns = dataGrid.Columns.Where(column => column.Width.IsAuto || column.Width.IsSizeToCells || column.Width.IsSizeToHeader)
                .Select(column => new
                {
                    Column = column,
                    OriginalWidth = column.Width
                }).ToList();

            foreach (var item in contentSizedColumns)
            {
                item.Column.Width = new DataGridLength(item.Column.ActualWidth, DataGridLengthUnitType.Pixel);
            }

            dataGrid.UpdateLayout();

            foreach (var item in contentSizedColumns)
            {
                item.Column.Width = item.OriginalWidth;
            }
        }
    }
}
