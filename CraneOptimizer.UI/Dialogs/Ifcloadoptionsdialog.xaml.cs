using System.IO;
using System.Windows;
using System.Windows.Input;

namespace WPF_IFC_test1.Dialogs
{
    // ══════════════════════════════════════════════════════════════════════════
    //  IFCLoadOptions  —  result object passed back to the caller
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Carries the user's filter choices from IFCLoadOptionsDialog to the loader.
    /// Each flag = true means "skip this IFC type during 3D rendering".
    /// </summary>
    public class IFCLoadOptions
    {
        public bool HideGrids { get; set; } = true;
        public bool HideLevels { get; set; } = true;
        public bool HideAnnotations { get; set; } = true;
        public bool HideSite { get; set; } = false;
        public bool HideSpaces { get; set; } = true;
        public bool HideOpenings { get; set; } = true;
        public bool HideVirtualElements { get; set; } = true;

        /// <summary>
        /// Returns true when at least one filter is active,
        /// so the loader can short-circuit when no filtering is needed.
        /// </summary>
        public bool AnyFilterActive =>
            HideGrids || HideLevels || HideAnnotations || HideSite ||
            HideSpaces || HideOpenings || HideVirtualElements;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  IFCLoadOptionsDialog
    // ══════════════════════════════════════════════════════════════════════════

    public partial class IFCLoadOptionsDialog : Window
    {
        // ── Result ────────────────────────────────────────────────────────────

        /// <summary>
        /// Populated when the user clicks Load. Null if cancelled.
        /// </summary>
        public IFCLoadOptions Options { get; private set; }

        // ── Constructor ───────────────────────────────────────────────────────

        public IFCLoadOptionsDialog(string filePath)
        {
            InitializeComponent();

            // Show just the filename, not the full path
            TxtFilePath.Text = Path.GetFileName(filePath);
            TxtFilePath.ToolTip = filePath;
        }

        // ── Title bar drag ────────────────────────────────────────────────────

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        // ── Button handlers ───────────────────────────────────────────────────

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            Options = new IFCLoadOptions
            {
                HideGrids = ChkHideGrids.IsChecked == true,
                HideLevels = ChkHideLevels.IsChecked == true,
                HideAnnotations = ChkHideAnnotations.IsChecked == true,
                HideSite = ChkHideSite.IsChecked == true,
                HideSpaces = ChkHideSpaces.IsChecked == true,
                HideOpenings = ChkHideOpenings.IsChecked == true,
                HideVirtualElements = ChkHideVirtualElements.IsChecked == true,
            };
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
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