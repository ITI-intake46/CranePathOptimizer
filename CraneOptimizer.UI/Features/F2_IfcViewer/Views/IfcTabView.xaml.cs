using System.Windows.Controls;

namespace CraneOptimizer.UI.Features.F2_IfcViewer.Views
{
    /// <summary>
    /// Interaction logic for IfcTabView.xaml
    /// </summary>
    public partial class IfcTabView : UserControl
    {
        public IfcTabView()
        {
            InitializeComponent();
        }

        private void MoveIfcButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (MoveIfcButton.IsEnabled)
                MoveIfcPopup.IsOpen = true;
        }

        private void MoveIfcOption_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(
                new System.Action(() => MoveIfcPopup.IsOpen = false));
        }
    }
}
