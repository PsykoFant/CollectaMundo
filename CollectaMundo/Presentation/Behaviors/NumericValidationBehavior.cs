using Microsoft.Xaml.Behaviors; // or System.Windows.Interactivity
using System.Windows.Controls;

namespace CollectaMundo.Presentation.Behaviors
{
    public class NumericValidationBehavior : Behavior<TextBox>
    {
        // Stores the last valid value as a string.
        private string _lastValidValue = "0";

        protected override void OnAttached()
        {
            base.OnAttached();
            _lastValidValue = string.IsNullOrWhiteSpace(AssociatedObject.Text) ? "0" : AssociatedObject.Text;
            AssociatedObject.TextChanged += AssociatedObject_TextChanged;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.TextChanged -= AssociatedObject_TextChanged;
            base.OnDetaching();
        }

        private void AssociatedObject_TextChanged(object sender, TextChangedEventArgs e)
        {
            string currentText = AssociatedObject.Text;

            // Allow blank while editing.
            if (string.IsNullOrWhiteSpace(currentText))
            {
                return;
            }

            if (int.TryParse(currentText, out int value) && value >= 0)
            {
                _lastValidValue = currentText;
                return;
            }

            RevertText();
        }

        private void RevertText()
        {
            AssociatedObject.TextChanged -= AssociatedObject_TextChanged;
            AssociatedObject.Text = _lastValidValue;
            AssociatedObject.CaretIndex = AssociatedObject.Text.Length;
            AssociatedObject.TextChanged += AssociatedObject_TextChanged;
        }
    }
}
