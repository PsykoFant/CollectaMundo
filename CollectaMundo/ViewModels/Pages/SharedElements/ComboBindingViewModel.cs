using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections;
using System.Windows.Input;

namespace CollectaMundo.ViewModels.Pages.SharedElements;
public sealed partial class ComboBindingViewModel(IEnumerable items, Func<object?> getter, Action<object?> setter, ICommand refreshCommand, string? displayMemberPath = null) : ObservableObject
{
    public IEnumerable Items { get; } = items;

    private readonly Func<object?> _getter = getter;
    private readonly Action<object?> _setter = setter;
    public ICommand RefreshCommand { get; } = refreshCommand;
    public string? DisplayMemberPath { get; } = displayMemberPath;

    public object? Selected
    {
        get => _getter();
        set
        {
            if (!Equals(_getter(), value))
            {
                _setter(value);
                OnPropertyChanged();
            }
        }
    }
}
