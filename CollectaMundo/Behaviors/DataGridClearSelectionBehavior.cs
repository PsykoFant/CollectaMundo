using System.Windows;
using System.Windows.Controls;

namespace CollectaMundo.Behaviors
{
    public static class DataGridClearSelectionBehavior
    {
        public static readonly DependencyProperty ClearSelectionTriggerProperty =
            DependencyProperty.RegisterAttached(
                "ClearSelectionTrigger",
                typeof(int),
                typeof(DataGridClearSelectionBehavior),
                new PropertyMetadata(0, OnClearSelectionTriggerChanged));

        public static void SetClearSelectionTrigger(DependencyObject element, int value)
        {
            element.SetValue(ClearSelectionTriggerProperty, value);
        }

        public static int GetClearSelectionTrigger(DependencyObject element)
        {
            return (int)element.GetValue(ClearSelectionTriggerProperty);
        }

        private static void OnClearSelectionTriggerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataGrid dataGrid)
            {
                // Whenever the trigger value changes, clear the selection.
                dataGrid.UnselectAll();
            }
        }
    }
}
