using System.Windows;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F2_IfcViewer.Views
{
    public partial class IFCLoadOptionsDialog : Window
    {
        // ── Result properties (read by caller) ────────────────────────────
        public bool HideGrids => ChkHideGrids.IsChecked == true;
        public bool HideLevels => ChkHideLevels.IsChecked == true;
        public bool HideAnnotations => ChkHideAnnotations.IsChecked == true;
        public bool HideSite => ChkHideSite.IsChecked == true;
        public bool HideSpaces => ChkHideSpaces.IsChecked == true;
        public bool HideOpenings => ChkHideOpenings.IsChecked == true;
        public bool HideVirtualElements => ChkHideVirtualElements.IsChecked == true;

        public IFCLoadOptionsDialog(string filePath)
        {
            InitializeComponent();
            TxtFilePath.Text = filePath;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            => DragMove();

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            ChkHideGrids.IsChecked = true;
            ChkHideLevels.IsChecked = true;
            ChkHideAnnotations.IsChecked = true;
            ChkHideSite.IsChecked = true;
            ChkHideSpaces.IsChecked = true;
            ChkHideOpenings.IsChecked = true;
            ChkHideVirtualElements.IsChecked = true;
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            ChkHideGrids.IsChecked = false;
            ChkHideLevels.IsChecked = false;
            ChkHideAnnotations.IsChecked = false;
            ChkHideSite.IsChecked = false;
            ChkHideSpaces.IsChecked = false;
            ChkHideOpenings.IsChecked = false;
            ChkHideVirtualElements.IsChecked = false;
        }
    }
}
