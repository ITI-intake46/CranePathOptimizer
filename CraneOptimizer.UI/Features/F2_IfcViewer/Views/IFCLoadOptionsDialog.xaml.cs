using System;
using System.Windows;
using System.Windows.Input;
using CraneOptimizer.Domain.CreatedProject;
using System.Threading.Tasks;

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

        public CoordinateReferenceMode SelectedCoordinateMode =>
            RadIfcOrigin.IsChecked == true ? CoordinateReferenceMode.IfcOrigin : CoordinateReferenceMode.LocalOnly;

        public IFCLoadOptionsDialog(string filePath) : this(filePath, null)
        {
        }

        public IFCLoadOptionsDialog(string filePath, Task<IfcGeoreferencingResult?>? georefTask)
        {
            InitializeComponent();
            TxtFilePath.Text = filePath;

            // Initial scanning state
            RadIfcOrigin.IsEnabled = false;
            TxtStatus.Text = "Scanning georeferencing...";
            if (TryFindResource("TextSecondaryBrush") is System.Windows.Media.Brush secondaryBrush)
            {
                TxtStatus.Foreground = secondaryBrush;
            }

            if (georefTask != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var result = await georefTask;
                        Dispatcher.Invoke(() => InitializeGeoreferencing(result));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[IfcGeoreferencing] Exception during pre-scan: {ex}");
                        Dispatcher.Invoke(() => InitializeGeoreferencing(null));
                    }
                });
            }
            else
            {
                InitializeGeoreferencing(null);
            }
        }

        private void InitializeGeoreferencing(IfcGeoreferencingResult? georef)
        {
            if (georef != null && georef.HasMapConversion)
            {
                RadIfcOrigin.IsEnabled = true;
                TxtStatus.Text = "Usable IFC origin georeferencing found.";
                if (TryFindResource("SuccessBrush") is System.Windows.Media.Brush successBrush)
                {
                    TxtStatus.Foreground = successBrush;
                }
            }
            else
            {
                RadIfcOrigin.IsEnabled = false;
                RadLocalOnly.IsChecked = true;

                if (TryFindResource("WarningBrush") is System.Windows.Media.Brush warningBrush)
                {
                    TxtStatus.Foreground = warningBrush;
                }

                if (georef != null && georef.HasSiteLatLon)
                {
                    TxtStatus.Text = "IFC site latitude/longitude found as informational metadata only. It cannot activate Easting/Northing origin mapping.";
                }
                else
                {
                    TxtStatus.Text = "No usable IFC origin georeferencing was found. Local Only will be used.";
                }
            }
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
