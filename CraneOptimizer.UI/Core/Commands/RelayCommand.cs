using System.Windows.Input;

namespace CraneOptimizer.UI.Core.Commands
{
    /// <summary>
    /// Non-generic RelayCommand for parameterless actions.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
            => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter)
            => _execute();

        public event EventHandler? CanExecuteChanged;

        /// <summary>
        /// Manually trigger CanExecute reevaluation
        /// </summary>
        public void RaiseCanExecuteChanged()
            => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Generic RelayCommand for commands that receive a typed parameter.
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            if (_canExecute == null) return true;

            if (parameter == null && typeof(T).IsValueType)
                return _canExecute(default);

            return _canExecute((T?)parameter);
        }

        public void Execute(object? parameter)
        {
            if (parameter == null && typeof(T).IsValueType)
            {
                _execute(default);
                return;
            }

            _execute((T?)parameter);
        }

        public event EventHandler? CanExecuteChanged;

        public void RaiseCanExecuteChanged()
            => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}