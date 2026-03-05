using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace CollectaMundo.Presentation.Behaviors
{
    public class DelayedUpdateBehavior : Behavior<TextBox>
    {
        public static readonly DependencyProperty DelayProperty = DependencyProperty.Register(nameof(Delay), typeof(int), typeof(DelayedUpdateBehavior), new PropertyMetadata(500, OnDelayChanged));

        public int Delay
        {
            get => (int)GetValue(DelayProperty);
            set => SetValue(DelayProperty, value);
        }

        private static void OnDelayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DelayedUpdateBehavior b && b._timer != null)
            {
                b._timer.Interval = TimeSpan.FromMilliseconds((int)e.NewValue);
            }
        }

        private DispatcherTimer? _timer;

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
            var binding = AssociatedObject.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();
        }
    }

}
