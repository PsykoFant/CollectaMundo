using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace CollectaMundo.Presentation.Behaviors
{
    public sealed class NumericTextBoxCommitBehavior : Behavior<TextBox>
    {
        public static readonly DependencyProperty DelayProperty = DependencyProperty.Register(nameof(Delay), typeof(int), typeof(NumericTextBoxCommitBehavior), new PropertyMetadata(500, OnDelayChanged));
        public static readonly DependencyProperty CommitCommandProperty = DependencyProperty.Register(nameof(CommitCommand), typeof(ICommand), typeof(NumericTextBoxCommitBehavior));

        private DispatcherTimer? _timer;
        public int Delay
        {
            get => (int)GetValue(DelayProperty);
            set => SetValue(DelayProperty, value);
        }
        public ICommand? CommitCommand
        {
            get => (ICommand?)GetValue(CommitCommandProperty);
            set => SetValue(CommitCommandProperty, value);
        }
        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.TextChanged += OnTextChanged;
            AssociatedObject.LostFocus += OnLostFocus;
            AssociatedObject.KeyDown += OnKeyDown;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(Delay)
            };

            _timer.Tick += OnTimerTick;
        }
        protected override void OnDetaching()
        {
            AssociatedObject.TextChanged -= OnTextChanged;
            AssociatedObject.LostFocus -= OnLostFocus;
            AssociatedObject.KeyDown -= OnKeyDown;

            if (_timer is not null)
            {
                _timer.Stop();
                _timer.Tick -= OnTimerTick;
            }

            base.OnDetaching();
        }
        private static void OnDelayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NumericTextBoxCommitBehavior b && b._timer is not null)
            {
                b._timer.Interval = TimeSpan.FromMilliseconds((int)e.NewValue);
            }
        }
        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            _timer?.Stop();
            _timer?.Start();
        }
        private void OnTimerTick(object? sender, EventArgs e)
        {
            _timer?.Stop();

            if (CanCommitCurrentText())
            {
                Commit();
            }
        }
        private void OnLostFocus(object? sender, RoutedEventArgs e)
        {
            _timer?.Stop();

            if (CanCommitCurrentText())
            {
                Commit();
                return;
            }

            RestoreTargetFromSource();
        }
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _timer?.Stop();

                if (CanCommitCurrentText())
                {
                    Commit();
                }

                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                _timer?.Stop();

                RestoreTargetFromSource();
                AssociatedObject.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));

                e.Handled = true;
            }
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
