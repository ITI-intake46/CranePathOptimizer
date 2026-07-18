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
    }
}
