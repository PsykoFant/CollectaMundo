using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;

namespace CollectaMundo.Presentation.Behaviors
{
    public class ComboBoxUnselector : Behavior<ComboBox>
    {
        public static readonly DependencyProperty ClearTriggerProperty =
            DependencyProperty.Register(nameof(ClearTrigger), typeof(bool), typeof(ComboBoxUnselector),
                new PropertyMetadata(false, OnClearTriggerChanged));

        public bool ClearTrigger
        {
            get => (bool)GetValue(ClearTriggerProperty);
            set => SetValue(ClearTriggerProperty, value);
        }

        private static void OnClearTriggerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ComboBoxUnselector behavior && behavior.AssociatedObject is ComboBox combo)
            {
                if ((bool)e.NewValue)
                {
                    combo.SelectedItem = null;
                }
            }
        }
    }
}
