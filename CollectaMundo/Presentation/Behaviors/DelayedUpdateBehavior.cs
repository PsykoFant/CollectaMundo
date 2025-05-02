using Microsoft.Xaml.Behaviors;
using System.Windows.Controls;
using System.Windows.Threading;

namespace CollectaMundo.Presentation.Behaviors
{
    public class DelayedUpdateBehavior : Behavior<TextBox>
    {
        // Delay in milliseconds – configurable via XAML.
        public int Delay { get; set; } = 500;

        private DispatcherTimer? _timer;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.TextChanged += AssociatedObject_TextChanged;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Delay) };
            _timer!.Tick += Timer_Tick;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.TextChanged -= AssociatedObject_TextChanged;
            _timer?.Stop();
            _timer!.Tick -= Timer_Tick;
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
