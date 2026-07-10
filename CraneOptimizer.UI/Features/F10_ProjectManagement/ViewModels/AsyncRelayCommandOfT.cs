using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.ViewModels
{
    /// <summary>
    /// Generic async relay command whose CanExecuteChanged wires into
    /// CommandManager.RequerySuggested so WPF button states re-evaluate
    /// automatically when the UI thread processes commands.
    /// </summary>
    public sealed class AsyncRelayCommand<T> : ICommand
    {
        private readonly Func<T?, Task> _execute;
        private readonly Func<T?, bool>? _canExecute;
        private bool _isRunning;

        public AsyncRelayCommand(Func<T?, Task> execute, Func<T?, bool>? canExecute = null)
        {
            _execute    = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            var p = parameter is T t ? t : default;
            return !_isRunning && (_canExecute?.Invoke(p) ?? true);
        }

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter))
                return;

            var p = parameter is T t ? t : default;
            try
            {
                _isRunning = true;
                RaiseCanExecuteChanged();
                await _execute(p);
            }
            finally
            {
                _isRunning = false;
                RaiseCanExecuteChanged();
            }
        }

        public event EventHandler? CanExecuteChanged
        {
            add    => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public void RaiseCanExecuteChanged()
            => CommandManager.InvalidateRequerySuggested();
    }
}
