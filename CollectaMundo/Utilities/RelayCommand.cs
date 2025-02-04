using System.Windows.Input;

namespace CollectaMundo.Utilities
{
    public class RelayCommand<T>(Action<T> execute, Predicate<T>? canExecute = null) : ICommand
    {
        private readonly Action<T> _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        private readonly Predicate<T>? _canExecute = canExecute;

        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || (parameter is T param && _canExecute(param));
        }

        public void Execute(object? parameter)
        {
            if (parameter is T param)
            {
                _execute(param);
            }
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
