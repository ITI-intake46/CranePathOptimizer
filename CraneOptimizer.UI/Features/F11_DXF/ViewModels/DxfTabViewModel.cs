using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Features.F11_DXF.Views;
using HelixToolkit.Wpf;
using Microsoft.Win32;
using System;
using System.IO;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F11_DXF.ViewModels
{
    /// <summary>
    /// Self-contained ViewModel for the DXF ribbon tab.
    /// Has no dependency on IFC modules.
    /// 
    /// Workflow:
    ///   1. User clicks "Upload DXF" → file picker → path stored, status updated.
    ///   2. User clicks "Align"      → DxfAlignmentDialog opens → transform stored.
    /// </summary>
    public class DxfTabViewModel : ViewModelBase
    {
        // ── Backing fields ────────────────────────────────────────────────
        private DxfUploader? _uploader;
        private string? _loadedFilePath;
        private string _loadedFileName = string.Empty;
        private string _statusMessage  = "No DXF loaded";
        private bool   _hasDxfLoaded;
        private Matrix3D? _pendingTransform;

        // ── Alignment dialog state ───────────────────────────────────────────
        // Persisted between sessions so the user’s last offset/scale/rotation
        // is restored when the dialog is re-opened.
        private double[] _savedTransformArray = { 0, 0, 1.0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 };
        private bool _hasSavedTransformForLoadedFile;

        // Non-modal dialog reference — reuse if already open
        private DxfAlignmentDialog? _openDialog;

        // ── Commands ──────────────────────────────────────────────────────
        public ICommand UploadDxfCommand      { get; }
        public ICommand AlignCommand          { get; }

        // Stubs — visual only, no backend logic yet
        public ICommand AddAnotherDxfCommand  { get; }
        public ICommand DeleteCommand         { get; }
        public ICommand SelectCommand         { get; }
        public ICommand SelectAllCommand      { get; }

        // ── Events ────────────────────────────────────────────────────────
        /// <summary>
        /// Fired after a DXF file has been successfully loaded into the viewport.
        /// Subscribers (e.g. OsnapGeometryBridge) use this to refresh snap geometry.
        /// </summary>
        public event Action<DxfUploader>? DxfLoaded;

        public DxfUploader? CurrentUploader => _uploader;

        // ── Constructor ───────────────────────────────────────────────────
        public DxfTabViewModel()
        {
            UploadDxfCommand     = new RelayCommand(ExecuteUploadDxf);
            AlignCommand         = new RelayCommand(ExecuteAlign, () => HasDxfLoaded);

            // Stub commands — registered but do nothing yet
            AddAnotherDxfCommand = new RelayCommand(() => { });
            DeleteCommand        = new RelayCommand(() => { });
            SelectCommand        = new RelayCommand(() => { });
            SelectAllCommand     = new RelayCommand(() => { });
        }

        /// <summary>
        /// Called by MainWindow after the Helix viewport is ready.
        /// Creates the DxfUploader that renders DXF lines into the 3-D scene.
        /// Pattern mirrors IRenderingService.Initialize(IfcViewer).
        /// </summary>
        public void Initialize(HelixViewport3D viewport)
        {
            if (_uploader != null)
                return;

            _uploader = new DxfUploader(viewport);
        }

        // ── Bindable Properties ───────────────────────────────────────────

        /// <summary>Display name of the currently loaded DXF file (filename only).</summary>
        public string LoadedFileName
        {
            get => _loadedFileName;
            private set { _loadedFileName = value; OnPropertyChanged(); }
        }

        /// <summary>Status text shown in the tab's status strip.</summary>
        public string StatusMessage
        {
            get => _statusMessage;
            private set { _statusMessage = value; OnPropertyChanged(); }
        }

        /// <summary>True when a DXF file has been loaded. Controls Align button enabled state.</summary>
        public bool HasDxfLoaded
        {
            get => _hasDxfLoaded;
            private set { _hasDxfLoaded = value; OnPropertyChanged(); }
        }

        /// <summary>The transform matrix produced by the last alignment session.</summary>
        public Matrix3D? PendingTransform
        {
            get => _pendingTransform;
            private set { _pendingTransform = value; OnPropertyChanged(); }
        }

        // ── Command Implementations ───────────────────────────────────────

        /// <summary>
        /// Upload DXF:
        ///   1. Opens a file picker → user selects a .dxf file.
        ///   2. Calls _uploader.LoadDxf() → renders DXF lines into the 3D viewport immediately.
        ///   3. Immediately opens the DxfAlignmentDialog (with the loaded uploader) for alignment.
        /// Mirrors the reference ZoneModuleController.LoadDxf() pattern exactly.
        /// </summary>
        private void ExecuteUploadDxf()
        {
            var picker = new OpenFileDialog
            {
                Title  = "Select DXF File",
                Filter = "DXF Files (*.dxf)|*.dxf|All Files (*.*)|*.*"
            };

            if (picker.ShowDialog() != true) return;

            bool isDifferentFile = !string.Equals(_loadedFilePath, picker.FileName, StringComparison.OrdinalIgnoreCase);
            if (isDifferentFile)
            {
                _savedTransformArray = new[] { 0, 0, 1.0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 };
                _hasSavedTransformForLoadedFile = false;
            }

            _loadedFilePath = picker.FileName;
            LoadedFileName  = Path.GetFileName(picker.FileName);
            StatusMessage   = $"Loading: {LoadedFileName}...";

            // ── Render into the 3D Helix viewport immediately ─────────────
            if (_uploader != null)
            {
                bool success;
                try   { success = _uploader.LoadDxf(_loadedFilePath); }
                catch (Exception ex)
                {
                    StatusMessage = $"⚠ DXF load failed: {ex.Message}";
                    return;
                }

                if (!success)
                {
                    StatusMessage = $"⚠ Could not load DXF: {_uploader.LastLoadDiagnostic}";
                    System.Windows.MessageBox.Show(
                        $"Could not load DXF file.\n\n{_uploader.LastLoadDiagnostic}\n\n" +
                        "Ensure the file is AutoCAD 2010+ DXF format.",
                        "DXF Load Failed",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    return;
                }
            }

            HasDxfLoaded  = true;
            StatusMessage = $"Loaded: {LoadedFileName}";

            // Notify subscribers (e.g. OsnapGeometryBridge) that snap geometry is ready
            if (_uploader != null)
                DxfLoaded?.Invoke(_uploader);

            // Open the alignment dialog immediately — uploader is now pre-loaded
            ExecuteAlign();
        }

        /// <summary>
        /// Align — opens the DXF alignment dialog as a NON-MODAL window so the user
        /// can still zoom / pan / rotate the Helix 3-D viewport while it is open.
        ///
        /// Behaviour:
        ///  • If already open  → bring it to front.
        ///  • If closed        → create new dialog, restore last transform, show non-modal.
        ///  • On close         → save transform array so next open shows same values.
        /// </summary>
        private void ExecuteAlign()
        {
            // If the dialog is already open, just bring it to the foreground
            if (_openDialog != null)
            {
                _openDialog.Activate();
                return;
            }

            if (_uploader == null) return;

            try
            {
                var dlg = new DxfAlignmentDialog(_uploader);
                dlg.Owner = System.Windows.Application.Current.MainWindow;

                // Restore the values the user entered last time
                if (_hasSavedTransformForLoadedFile)
                    dlg.RestoreTransform(_savedTransformArray);

                // Save transform + clear reference when user closes the dialog
                dlg.Closed += (s, e) =>
                {
                    _savedTransformArray = dlg.GetTransformArray();
                    _hasSavedTransformForLoadedFile = true;
                    PendingTransform = dlg.GetResultTransform();
                    _openDialog = null;
                    StatusMessage = $"Aligned: {LoadedFileName}";
                };

                _openDialog = dlg;
                dlg.Show();   // non-modal — viewport stays interactive
            }
            catch (Exception ex)
            {
                StatusMessage = $"Alignment error: {ex.Message}";
            }
        }
    }
}
