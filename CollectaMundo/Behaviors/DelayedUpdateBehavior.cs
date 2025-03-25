using Microsoft.Xaml.Behaviors; // Or System.Windows.Interactivity, depending on your package
using System.Windows.Controls;
using System.Windows.Threading;

namespace CollectaMundo.Behaviors
{
    public class DelayedUpdateBehavior : Behavior<TextBox>
    {
        // Delay in milliseconds (default 500ms)
        public int Delay { get; set; } = 500;

        private DispatcherTimer? _timer;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.TextChanged += AssociatedObject_TextChanged;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Delay) };
            _timer.Tick += Timer_Tick;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.TextChanged -= AssociatedObject_TextChanged;
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= Timer_Tick;
            }
        }

        private void AssociatedObject_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Restart the timer on each keystroke
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Start();
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            _timer?.Stop();
            // Force update of the binding source
            var binding = AssociatedObject.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();
        }
    }
}
