using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Views;
using System.Windows;

namespace CraneOptimizer.UI.Core.Services
{
    public sealed class DeleteConfirmationService : IDeleteConfirmationService
    {
        public bool ConfirmDelete(string title, string message, string actionText)
        {
            System.Windows.Application? application = System.Windows.Application.Current;
            if (application == null)
                return false;

            Window? owner = application.Windows
                .OfType<Window>()
                .FirstOrDefault(window => window.IsActive)
                ?? application.MainWindow;

            var dialog = new DeleteConfirmationWindow(title, message, actionText);
            if (owner != null)
                dialog.Owner = owner;

            return dialog.ShowDialog() == true;
        }
    }
}
