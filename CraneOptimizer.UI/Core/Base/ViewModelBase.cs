using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace CraneOptimizer.UI.Core.Base
{
    /// <summary>
    /// Base ViewModel. Suppresses PropertyChanged notifications during application
    /// shutdown to prevent WPF's binding engine from evaluating expressions against
    /// a partially-disposed object graph, which would yield DependencyProperty.UnsetValue
    /// and crash the rendering pipeline.
    /// </summary>
    public class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            // Do not raise notifications after the Dispatcher has been shut down.
            // At that point the UI has already been torn down and any attempt to
            // propagate a binding update would touch a dead rendering pipeline.
            if (System.Windows.Application.Current == null
                || !System.Windows.Application.Current.Dispatcher.HasShutdownStarted)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
