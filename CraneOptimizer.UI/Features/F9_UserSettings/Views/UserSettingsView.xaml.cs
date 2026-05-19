using CraneOptimizer.Domain.Units;
using CraneOptimizer.UI.Shell.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace CraneOptimizer.UI.Features.F9_Settings.Views
{
    public partial class UserSettingsView : UserControl
    {
        public UserSettingsView()
        {
            InitializeComponent();
        }

        private void OpenUnits_Click(object sender, RoutedEventArgs e)
        {
            var dialog = App.ServiceProvider.GetRequiredService<UnitsSettingsDialog>();
            if (DataContext is MainViewModel vm)
                dialog.ProjectId = vm.CurrentProjectId;

            dialog.Owner = Window.GetWindow(this);
            dialog.ShowDialog();
        }
    }
}
