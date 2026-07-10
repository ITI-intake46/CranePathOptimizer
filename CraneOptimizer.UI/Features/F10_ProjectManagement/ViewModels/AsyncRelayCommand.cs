using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.ViewModels
{
    /// <summary>
    /// An async relay command whose CanExecuteChanged wires into
    /// CommandManager.RequerySuggested so that WPF button states
    /// re-evaluate automatically when the UI thread processes commands.
    /// </summary>
    public sealed class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool>? _canExecute;
        private bool _isRunning;

        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        {
            _execute    = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
            => !_isRunning && (_canExecute?.Invoke() ?? true);

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter))
                return;

            try
            {
                _isRunning = true;
                RaiseCanExecuteChanged();
                await _execute();
            }
            finally
            {
                _isRunning = false;
                RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Wires into CommandManager.RequerySuggested so that
        /// CommandManager.InvalidateRequerySuggested() re-enables/disables buttons.
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add    => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public void RaiseCanExecuteChanged()
            => CommandManager.InvalidateRequerySuggested();
    }
}
