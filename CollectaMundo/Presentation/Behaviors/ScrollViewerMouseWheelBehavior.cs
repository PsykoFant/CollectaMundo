using Microsoft.Xaml.Behaviors;
using System.Windows.Controls;
using System.Windows.Input;


namespace CollectaMundo.Presentation.Behaviors
{
    public sealed class ScrollViewerMouseWheelBehavior : Behavior<ScrollViewer>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.PreviewMouseWheel += OnPreviewMouseWheel;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.PreviewMouseWheel -= OnPreviewMouseWheel;
            base.OnDetaching();
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            AssociatedObject.ScrollToVerticalOffset(AssociatedObject.VerticalOffset - e.Delta);
            e.Handled = true;
        }
    }
}
