using System.Windows;
using System.Windows.Controls;

namespace CollectaMundo.Presentation.Behaviors
{
    public static class ListViewColumnRefreshBehavior
    {
        public static readonly DependencyProperty RefreshTriggerProperty =
            DependencyProperty.RegisterAttached(
                "RefreshTrigger",
                typeof(int),
                typeof(ListViewColumnRefreshBehavior),
                new PropertyMetadata(0, OnRefreshTriggerChanged));

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
            if (d is ListView listView)
            {
                AdjustColumnWidths(listView);
            }
        }

        private static void AdjustColumnWidths(ListView listView)
        {
            if (listView.View is GridView gridView)
            {
                // Step 1: For each column in Auto mode, capture the current width.
                foreach (var column in gridView.Columns)
                {
                    if (double.IsNaN(column.Width))
                    {
                        // Set it temporarily to its ActualWidth.
                        column.Width = column.ActualWidth;
                    }
                }

                // Force the layout to update.
                listView.UpdateLayout();

                // Step 2: Reset columns back to Auto.
                foreach (var column in gridView.Columns)
                {
                    column.Width = double.NaN;
                }
            }
        }
    }
}
