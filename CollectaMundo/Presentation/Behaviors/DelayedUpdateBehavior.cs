using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace CollectaMundo.Presentation.Behaviors
{
    public class DelayedUpdateBehavior : Behavior<TextBox>
    {
        public static readonly DependencyProperty DelayProperty =
            DependencyProperty.Register(
                nameof(Delay),
                typeof(int),
                typeof(DelayedUpdateBehavior),
                new PropertyMetadata(500, OnDelayChanged));

        public static readonly DependencyProperty CommitCommandProperty =
            DependencyProperty.Register(
                nameof(CommitCommand),
                typeof(ICommand),
                typeof(DelayedUpdateBehavior));

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

        private DispatcherTimer? _timer;

        private static void OnDelayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DelayedUpdateBehavior b && b._timer != null)
            {
                b._timer.Interval = TimeSpan.FromMilliseconds((int)e.NewValue);
            }
        }

        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.TextChanged += AssociatedObject_TextChanged;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(Delay)
            };

            _timer.Tick += Timer_Tick;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.TextChanged -= AssociatedObject_TextChanged;

            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= Timer_Tick;
            }

            base.OnDetaching();
        }

        private void AssociatedObject_TextChanged(object sender, TextChangedEventArgs e)
        {
            _timer?.Stop();
            _timer?.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _timer?.Stop();

            var text = AssociatedObject.Text;

            if (!int.TryParse(text, out var value) || value < 0)
            {
                return;
            }

            var binding = AssociatedObject.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();

            if (CommitCommand?.CanExecute(null) == true)
            {
                CommitCommand.Execute(null);
            }
        }
    }
}
