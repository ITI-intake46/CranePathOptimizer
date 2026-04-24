using CraneOptimizer.UI.Features.F11_DXF.ViewModels;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F11_DXF.Views
{
    /// <summary>
    /// Code-behind for DxfAlignmentDialog.xaml.
    ///
    /// The dialog is opened NON-MODALLY via Show() so the user can still
    /// interact with the 3D Helix viewport (zoom / pan / rotate) while
    /// the dialog is visible.
    ///
    /// Usage (from DxfTabViewModel):
    /// <code>
    ///   var dlg = new DxfAlignmentDialog(uploader);
    ///   dlg.Owner  = Application.Current.MainWindow;
    ///   dlg.RestoreTransform(savedArray);      // optional — restore last session
    ///   dlg.Closed += (s, e) => Save(dlg.GetTransformArray());
    ///   dlg.Show();                             // non-modal
    /// </code>
    /// </summary>
    public partial class DxfAlignmentDialog : Window
    {
        private readonly DxfAlignmentViewModel _vm;
        private readonly DxfCanvasRenderer _renderer;

        // ── Constructors ──────────────────────────────────────────────────────

        /// <summary>Opens the alignment dialog in pre-load mode (no 3-D uploader).</summary>
        public DxfAlignmentDialog()
            : this(uploader: null) { }

        /// <summary>
        /// Opens the alignment dialog with an active 3-D uploader so transform
        /// changes update the Helix viewport in real time.
        /// </summary>
        public DxfAlignmentDialog(DxfUploader? uploader)
        {
            InitializeComponent();

            _vm       = new DxfAlignmentViewModel(uploader);
            _renderer = new DxfCanvasRenderer();

            DataContext = _vm;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Restores previously saved transform values into the dialog so the user
        /// sees the same offset / scale / rotation they last entered.
        /// Must be called BEFORE Show().
        /// </summary>
        public void RestoreTransform(double[]? arr)
        {
            if (arr == null || arr.Length < 4) return;

            _vm.Transform.OffsetX         = arr[0];
            _vm.Transform.OffsetY         = arr[1];
            _vm.Transform.Scale           = arr[2];
            _vm.Transform.RotationDegrees = arr[3];
            _vm.RefreshTextBoxes();          // sync text boxes to new model values

            // Push restored values to the 3-D uploader immediately
            if (_vm.ApplyManualCommand.CanExecute(null))
                _vm.ApplyManualCommand.Execute(null);
        }

        /// <summary>Snapshot of [OffsetX, OffsetY, Scale, RotationDegrees] to persist.</summary>
        public double[] GetTransformArray() => _vm.Transform.ToArray();

        /// <summary>Returns the 4×4 alignment Matrix3D (for reading back the transform).</summary>
        public Matrix3D GetResultTransform() => _vm.GetResultTransform();

        /// <summary>File path of the DXF loaded inside the dialog (may be empty).</summary>
        public string LoadedDxfFilePath => _vm.DxfFilePath;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _renderer.Attach(DxfCanvas, _vm.Transform);
            _vm.SetRenderer(_renderer);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _renderer.Detach();
        }

        // ── Chrome handlers ───────────────────────────────────────────────────

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void CloseButton_Click(object sender, RoutedEventArgs e)
            => Close();

        // ── Button handlers ───────────────────────────────────────────────────

        /// <summary>
        /// Apply — parses text boxes, pushes transform to 3-D uploader, stays open.
        /// </summary>
        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.ApplyManualCommand.CanExecute(null))
                _vm.ApplyManualCommand.Execute(null);
        }

        /// <summary>
        /// Apply &amp; Close — applies the transform then closes the dialog.
        /// Does NOT set DialogResult (non-modal windows cannot use DialogResult).
        /// </summary>
        private void ApplyAndClose_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.ApplyManualCommand.CanExecute(null))
                _vm.ApplyManualCommand.Execute(null);
            Close();
        }

        /// <summary>Close without applying additional changes.</summary>
        private void Cancel_Click(object sender, RoutedEventArgs e)
            => Close();

        /// <summary>Escape key closes the dialog.</summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape)
                Close();
        }
    }
}
