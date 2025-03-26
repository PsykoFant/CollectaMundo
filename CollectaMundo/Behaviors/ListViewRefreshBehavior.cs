using System.Windows;
using System.Windows.Controls;

namespace CollectaMundo.Behaviors
{
    public static class ListViewRefreshBehavior
    {
        public static readonly DependencyProperty RefreshTriggerProperty =
            DependencyProperty.RegisterAttached(
                "RefreshTrigger",
                typeof(int),
                typeof(ListViewRefreshBehavior),
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
                // Refresh the items collection.
                listView.Items.Refresh();
            }
        }
    }
}
