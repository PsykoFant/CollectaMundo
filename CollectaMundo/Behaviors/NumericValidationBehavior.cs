using Microsoft.Xaml.Behaviors; // or System.Windows.Interactivity
using System.Windows.Controls;

namespace CollectaMundo.Behaviors
{
    public class NumericValidationBehavior : Behavior<TextBox>
    {
        private string _lastValidValue = "0";

        protected override void OnAttached()
        {
            base.OnAttached();
            // Initialize with the current text (or default to "0")
            _lastValidValue = AssociatedObject.Text;
            AssociatedObject.TextChanged += AssociatedObject_TextChanged;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.TextChanged -= AssociatedObject_TextChanged;
            base.OnDetaching();
        }

        private void AssociatedObject_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Try to parse the current text as an integer
            if (int.TryParse(AssociatedObject.Text, out int result) && result >= 0)
            {
                // Valid numeric input – update the stored valid value.
                _lastValidValue = AssociatedObject.Text;
            }
            else
            {
                // Invalid input: revert to the last valid value.
                // Temporarily remove the handler to avoid recursion.
                AssociatedObject.TextChanged -= AssociatedObject_TextChanged;
                AssociatedObject.Text = _lastValidValue;
                AssociatedObject.CaretIndex = AssociatedObject.Text.Length;
                AssociatedObject.TextChanged += AssociatedObject_TextChanged;
            }
        }
    }
}
