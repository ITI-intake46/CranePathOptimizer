using CraneOptimizer.UI.Core.Interfaces;
using Microsoft.Win32;
using System.Windows;

namespace CraneOptimizer.UI.Core.Services
{
    public class DialogService : IDialogService
    {
        public string? OpenFile(string filter)
        {
            var dialog = new OpenFileDialog
            {
                Filter = filter
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public void ShowMessage(string message)
        {
            MessageBox.Show(message);
        }

        public bool Confirm(string message, string title)
        {
            return MessageBox.Show(
                message,
                title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

        public void ShowNonBlockingError(string message, string title)
        {
            var window = new Window
            {
                Title = title,
                Width = 420,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Content = new System.Windows.Controls.TextBlock
                {
                    Text = message,
                    Margin = new Thickness(20),
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            window.Show();
        }
    }
}
