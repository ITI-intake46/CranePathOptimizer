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
    }
}
