using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;

namespace CollectaMundo.ViewModels.Pages.SharedElements;
public sealed partial class NumericBindingViewModel(Func<int> getter, Action<int> setter, ICommand? changedCommand = null, int? min = null, int? max = null, Func<int?>? maxGetter = null, int delayMs = 0) : ObservableObject
{
    private readonly Func<int> _getter = getter;
    private readonly Action<int> _setter = setter;
    private readonly Func<int?>? _maxGetter = maxGetter;

    public ICommand? ChangedCommand { get; } = changedCommand;
    public int? Min { get; } = min;
    public int? Max { get; } = max;
    public int DelayMs { get; } = delayMs;

    public int Value
    {
        get => _getter();
        set
        {
            if (_getter() == value)
            {
                return;
            }

            var v = value;

            if (Min.HasValue && v < Min.Value)
            {
                v = Min.Value;
            }

            // Clamp against dynamic max (owned)
            var dynMax = _maxGetter?.Invoke();
            if (dynMax.HasValue && v > dynMax.Value)
            {
                v = dynMax.Value;
            }

            // Clamp against static max
            if (Max.HasValue && v > Max.Value)
            {
                v = Max.Value;
            }

            _setter(v);
            OnPropertyChanged();
        }
    }
    public void NotifyValueChanged() => OnPropertyChanged(nameof(Value));
}
