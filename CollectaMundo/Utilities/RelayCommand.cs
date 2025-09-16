using System.Windows.Input;

namespace CollectaMundo.Utilities
{
    public class RelayCommand<T>(Action<T> execute, Predicate<T>? canExecute = null) : ICommand
    {
        private readonly Action<T> _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        private readonly Predicate<T>? _canExecute = canExecute;

        public bool CanExecute(object? parameter)
        {
            if (_canExecute == null)
            {
                return true;
            }

            if (parameter == null && typeof(T).IsValueType)
            {
                return _canExecute(default!);
            }

            return _canExecute((T)parameter!);
        }

        public void Execute(object? parameter)
        {
            _execute((T)parameter!);
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
