using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace CollectaMundo.Presentation.Behaviors
{
    public sealed class ClearSelectionOnBackgroundClickBehavior : Behavior<FrameworkElement>
    {
        public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(ClearSelectionOnBackgroundClickBehavior));

        public ICommand? Command
        {
            get => (ICommand?)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        protected override void OnAttached()
        {
            AssociatedObject.PreviewMouseDown += OnPreviewMouseDown;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.PreviewMouseDown -= OnPreviewMouseDown;
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (IsInteractiveElement(e.OriginalSource as DependencyObject))
            {
                return;
            }

            if (Command?.CanExecute(null) == true)
            {
                Command.Execute(null);
            }
        }

        private static bool IsInteractiveElement(DependencyObject? source)
        {
            while (source is not null)
            {
                if (source is DataGrid ||
                    source is DataGridRow ||
                    source is DataGridCell ||
                    source is Button ||
                    source is TextBox ||
                    source is ComboBox ||
                    source is ScrollBar)
                {
                    return true;
                }

                source = System.Windows.Media.VisualTreeHelper.GetParent(source);
            }

            return false;
        }
    }
}
