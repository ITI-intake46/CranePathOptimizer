using CraneOptimizer.UI.Features.F3_CraneSetup.Views;
using CraneOptimizer.UI.Shell.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace CraneOptimizer.UI.F10_ProjectManagement.Views
{
    /// <summary>
    /// Interaction logic for HomeTabView.xaml
    /// </summary>
    public partial class HomeTabView : UserControl
    {
        public static readonly RoutedEvent ProjectSettingsRequestedEvent = EventManager.RegisterRoutedEvent(
            nameof(ProjectSettingsRequested),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(HomeTabView));

        public event RoutedEventHandler ProjectSettingsRequested
        {
            add => AddHandler(ProjectSettingsRequestedEvent, value);
            remove => RemoveHandler(ProjectSettingsRequestedEvent, value);
        }

        public HomeTabView()
        {
            InitializeComponent();
        }

        private void ProjectSettings_Click(object sender, RoutedEventArgs e)
            => RaiseEvent(new RoutedEventArgs(ProjectSettingsRequestedEvent, this));

        private async void ImportLoadChart_Click(object sender, RoutedEventArgs e)
            => await CranesTabView.OpenLoadChartImportAsync(
                this,
                DataContext as MainViewModel);

        private async void CraneCatalog_Click(object sender, RoutedEventArgs e)
            => await CranesTabView.OpenCraneCatalogAsync(
                this,
                DataContext as MainViewModel);
    }
}
