using System.Globalization;
using System.Windows.Controls;

namespace CollectaMundo
{
    public class IntegerValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return new ValidationResult(false, "Value cannot be empty.");
            }

            if (int.TryParse(value.ToString(), out int result) && result >= 0)
            {
                return ValidationResult.ValidResult;
            }
            else
            {
                return new ValidationResult(false, "Value must be a non-negative integer.");
            }
        }
    }
}
