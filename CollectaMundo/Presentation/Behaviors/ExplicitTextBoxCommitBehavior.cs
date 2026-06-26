using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CollectaMundo.Presentation.Behaviors
{
    public sealed class ExplicitTextBoxCommitBehavior : Behavior<TextBox>
    {
        public ICommand? CommitCommand
        {
            get => (ICommand?)GetValue(CommitCommandProperty);
            set => SetValue(CommitCommandProperty, value);
        }
        public static readonly DependencyProperty CommitCommandProperty = DependencyProperty.Register(nameof(CommitCommand), typeof(ICommand), typeof(ExplicitTextBoxCommitBehavior));
        protected override void OnAttached()
        {
            AssociatedObject.LostFocus += OnCommit;
            AssociatedObject.KeyDown += OnKeyDown;
        }
        protected override void OnDetaching()
        {
            AssociatedObject.LostFocus -= OnCommit;
            AssociatedObject.KeyDown -= OnKeyDown;
        }
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (CanCommitCurrentText())
                {
                    Commit();
                }

                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                RestoreTargetFromSource();

                AssociatedObject.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));

                e.Handled = true;
            }
        }
        private void OnCommit(object? sender, RoutedEventArgs e)
        {
            if (CanCommitCurrentText())
            {
                Commit();
                return;
            }

            RestoreTargetFromSource();
        }
        private bool CanCommitCurrentText()
        {
            return int.TryParse(AssociatedObject.Text, out var value) && value >= 0;
        }
        private void Commit()
        {
            AssociatedObject.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

            if (CommitCommand?.CanExecute(null) == true)
            {
                CommitCommand.Execute(null);
            }
        }
        private void RestoreTargetFromSource()
        {
            AssociatedObject.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
        }
    }
}
