using CollectaMundo.Models;
using Microsoft.Xaml.Behaviors; // or System.Windows.Interactivity
using System.Globalization;
using System.Windows.Controls;

namespace CollectaMundo.Behaviors
{
    public class NumericValidationBehavior : Behavior<TextBox>
    {
        // Stores the last valid value as a string.
        private string _lastValidValue = "0";

        protected override void OnAttached()
        {
            base.OnAttached();
            // Initialize _lastValidValue with the current text, or "0" if empty.
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
            // Ensure the DataContext is a CardSet.
            if (AssociatedObject.DataContext is CardSet card)
            {
                string currentText = AssociatedObject.Text;
                // If the text is empty, revert.
                if (string.IsNullOrWhiteSpace(currentText))
                {
                    RevertText();
                    return;
                }

                // Try to parse the text.
                if (int.TryParse(currentText, out int value))
                {
                    // Enforce: value must be >= 0.
                    if (value < 0)
                    {
                        RevertText();
                    }
                    // Enforce: CardsForTrade (input) cannot exceed CardsOwned.
                    else if (value > card.CardsOwned)
                    {
                        // Clamp to card.CardsOwned.
                        SetText(card.CardsOwned.ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        // Input is valid – update last valid value.
                        _lastValidValue = currentText;
                    }
                }
                else
                {
                    // Not a valid integer: revert.
                    RevertText();
                }
            }
        }

        private void RevertText()
        {
            // Temporarily remove the event handler to avoid recursion.
            AssociatedObject.TextChanged -= AssociatedObject_TextChanged;
            AssociatedObject.Text = _lastValidValue;
            AssociatedObject.CaretIndex = AssociatedObject.Text.Length;
            AssociatedObject.TextChanged += AssociatedObject_TextChanged;
        }

        private void SetText(string newText)
        {
            AssociatedObject.TextChanged -= AssociatedObject_TextChanged;
            AssociatedObject.Text = newText;
            AssociatedObject.CaretIndex = AssociatedObject.Text.Length;
            _lastValidValue = newText;
            AssociatedObject.TextChanged += AssociatedObject_TextChanged;
        }
    }
}
