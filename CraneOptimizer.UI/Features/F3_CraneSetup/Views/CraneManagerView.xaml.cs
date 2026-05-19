using System.Windows;
using System.Windows.Controls;

namespace CraneOptimizer.UI.Features.F3_CraneSetup.Views
{
    /// <summary>
    /// Interaction logic for CraneManagerView.xaml
    /// </summary>
    public partial class CraneManagerView : UserControl
    {
        public CraneManagerView()
        {
            InitializeComponent();
        }

        private void BodyAngleTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                var tb = (TextBox)sender;
                tb.MoveFocus(new System.Windows.Input.TraversalRequest(
                    System.Windows.Input.FocusNavigationDirection.Next));
            }
        }
    }
}
