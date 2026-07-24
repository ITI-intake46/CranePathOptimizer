using System;
using System.Windows;
using System.Windows.Input;
using CraneOptimizer.Domain.CreatedProject;
using CraneOptimizer.UI.Features.F2_IfcViewer.DTOs;
using CraneOptimizer.UI.Features.F2_IfcViewer.Services;
using CraneOptimizer.UI.Features.F2_IfcViewer.ViewModels;
using System.Threading.Tasks;

namespace CraneOptimizer.UI.Features.F2_IfcViewer.Views
{
    public partial class IFCLoadOptionsDialog : Window
    {
        public IfcOptionsDialogViewModel ViewModel { get; }

        public bool HideGrids => ViewModel.HideGrids;
        public bool HideLevels => ViewModel.HideLevels;
        public bool HideAnnotations => ViewModel.HideAnnotations;
        public bool HideSite => ViewModel.HideSite;
        public bool HideSpaces => ViewModel.HideSpaces;
        public bool HideOpenings => ViewModel.HideOpenings;
        public bool HideVirtualElements => ViewModel.HideVirtualElements;

        public CoordinateReferenceMode SelectedCoordinateMode =>
            RadIfcOrigin.IsChecked == true ? CoordinateReferenceMode.IfcOrigin : CoordinateReferenceMode.LocalOnly;

        public IFCLoadOptionsDialog(string filePath) : this(filePath, null)
        {
        }

        public IFCLoadOptionsDialog(string filePath, Task<IfcGeoreferencingResult?>? georefTask)
            : this(
                IfcOptionsMode.Load,
                filePath,
                georefTask,
                IfcVisibilityOptionsCatalog.CreateDefaultOptions(),
                IfcVisibilityOptionsCatalog.Definitions)
        {
        }

        private IFCLoadOptionsDialog(
            IfcOptionsMode mode,
            string? filePath,
            Task<IfcGeoreferencingResult?>? georefTask,
            IfcLoadOptionsDto currentOptions,
            IReadOnlyList<IfcVisibilityOptionDefinition> optionDefinitions)
        {
            InitializeComponent();

            ViewModel = new IfcOptionsDialogViewModel(mode, filePath, currentOptions, optionDefinitions);
            DataContext = ViewModel;
            ApplyCoordinateMode(currentOptions.CoordinateMode);

            if (ViewModel.CoordinateReferenceVisibility == Visibility.Collapsed)
                CoordinateReferencePanel.Visibility = Visibility.Collapsed;

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
                        await Dispatcher.InvokeAsync(() => InitializeGeoreferencing(result));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[IfcGeoreferencing] Exception during pre-scan: {ex}");
                        await Dispatcher.InvokeAsync(() => InitializeGeoreferencing(null));
                    }
                });
            }
            else
            {
                InitializeGeoreferencing(null);
            }
        }

        public static IFCLoadOptionsDialog CreateLoadOptionsDialog(
            string filePath,
            Task<IfcGeoreferencingResult?>? georefTask,
            IfcLoadOptionsDto currentOptions,
            IReadOnlyList<IfcVisibilityOptionDefinition> optionDefinitions)
        {
            return new IFCLoadOptionsDialog(
                IfcOptionsMode.Load,
                filePath,
                georefTask,
                currentOptions,
                optionDefinitions);
        }

        public static IFCLoadOptionsDialog CreateDisplayOptionsDialog(
            string? modelPath,
            IfcLoadOptionsDto currentOptions,
            IReadOnlyList<IfcVisibilityOptionDefinition> optionDefinitions)
        {
            return new IFCLoadOptionsDialog(
                IfcOptionsMode.Display,
                modelPath,
                null,
                currentOptions,
                optionDefinitions);
        }

        public IfcLoadOptionsDto GetOptions()
        {
            ViewModel.CoordinateMode = SelectedCoordinateMode;
            return ViewModel.ToOptions();
        }

        private void ApplyCoordinateMode(CoordinateReferenceMode mode)
        {
            RadIfcOrigin.IsChecked = mode == CoordinateReferenceMode.IfcOrigin;
            RadLocalOnly.IsChecked = mode != CoordinateReferenceMode.IfcOrigin;
        }

        private void InitializeGeoreferencing(IfcGeoreferencingResult? georef)
        {
            if (ViewModel.Mode == IfcOptionsMode.Display)
                return;

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
            ViewModel.CoordinateMode = SelectedCoordinateMode;
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
            ViewModel.SelectAll();
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearAll();
        }
    }
}
