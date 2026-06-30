using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CraneOptimizer.AI.Core.Base
{
    /// <summary>
    /// Base class for AI feature view models. It is intentionally local to the AI
    /// project so the AI layer does not depend on CraneOptimizer.UI internals.
    /// </summary>
    public class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            if (System.Windows.Application.Current == null
                || !System.Windows.Application.Current.Dispatcher.HasShutdownStarted)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
