using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Features.F11_DXF.Models;
using CraneOptimizer.UI.Features.F11_DXF.Services;
using Microsoft.Win32;
using netDxf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Features.F11_DXF.Views;
using System.Collections.ObjectModel;


namespace CraneOptimizer.UI.Features.F11_DXF.ViewModels
{
    public sealed class DxfAlignmentViewModel : ViewModelBase
    {
        // ── Injected collaborators ────────────────────────────────────────────

        private readonly DxfUploader? _uploader;
        private readonly DxfAlignmentService _service = new();
        private DxfCanvasRenderer? _canvasRenderer;

        // ── Domain model ──────────────────────────────────────────────────────

        public DxfTransformModel Transform { get; } = new();

        // ── Undo stack ────────────────────────────────────────────────────────

        private readonly Stack<object> _undoStack = new();
        private const int MaxUndoDepth = 20;

        // ── Loaded document ───────────────────────────────────────────────────

        private DxfDocument? _loadedDocument;

        // ── Empty layers fallback ─────────────────────────────────────────────

        private static readonly ObservableCollection<DxfLayerInfo> _emptyLayers = new();

        // ═════════════════════════════════════════════════════════════════════
        //  Constructor
        // ═════════════════════════════════════════════════════════════════════

        public DxfAlignmentViewModel(
            DxfUploader? uploader = null,
            DxfCanvasRenderer? canvasRenderer = null)
        {
            _uploader = uploader;
            _canvasRenderer = canvasRenderer;

            Transform.PropertyChanged += (_, _) => SyncTextFromModel();
            Transform.PropertyChanged += (_, _) => OnPropertyChanged(nameof(TransformSummary));

            LoadDxfCommand = new RelayCommand(ExecuteLoadDxf);
            ApplyManualCommand = new RelayCommand(ExecuteApplyManual);
            ApplyPointMatchCommand = new RelayCommand(ExecuteApplyPointMatch);
            AutoFitCommand = new RelayCommand(
                ExecuteAutoFit,
                () => _canvasRenderer != null && !_canvasRenderer.RawBounds.IsEmpty);
            ResetCommand = new RelayCommand(ExecuteReset);
            UndoCommand = new RelayCommand(ExecuteUndo, () => CanUndo);

            SyncTextFromModel();
        }

        // ── SetRenderer (called by View after Loaded) ─────────────────────────

        public void SetRenderer(DxfCanvasRenderer renderer)
        {
            _canvasRenderer = renderer;
            CommandManager.InvalidateRequerySuggested();
            OnPropertyChanged(nameof(Layers));
        }

        // ── GetResultTransform (called by Dialog after ShowDialog) ─────────────

        public Matrix3D GetResultTransform()
        {
            double s = Transform.Scale;
            double rad = -Transform.RotationRadians;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);

            return new Matrix3D(
                s * cos, -s * sin, 0, 0,
                s * sin, s * cos, 0, 0,
                0, 0, 1, 0,
                Transform.OffsetX, Transform.OffsetY, 0, 1);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Commands
        // ═════════════════════════════════════════════════════════════════════

        public ICommand LoadDxfCommand { get; }
        public ICommand ApplyManualCommand { get; }
        public ICommand ApplyPointMatchCommand { get; }
        public ICommand AutoFitCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand UndoCommand { get; }

        // ═════════════════════════════════════════════════════════════════════
        //  Bindable properties — file info
        // ═════════════════════════════════════════════════════════════════════

        private string _dxfFilePath = "";
        public string DxfFilePath
        {
            get => _dxfFilePath;
            private set { _dxfFilePath = value ?? ""; OnPropertyChanged(); }
        }

        private string _dxfFileName = "";
        public string DxfFileName
        {
            get => _dxfFileName;
            private set { _dxfFileName = value ?? ""; OnPropertyChanged(); }
        }

        private string _dxfInfo = "";
        public string DxfInfo
        {
            get => _dxfInfo;
            private set { _dxfInfo = value ?? ""; OnPropertyChanged(); }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Bindable properties — status / feedback
        // ═════════════════════════════════════════════════════════════════════

        private string _statusMessage = "No DXF file loaded.";
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value ?? ""; OnPropertyChanged(); }
        }

        private string _validationError = "";
        public string ValidationError
        {
            get => _validationError;
            set { _validationError = value ?? ""; OnPropertyChanged(); }
        }

        private bool _isDxfLoading;
        public bool IsDxfLoading
        {
            get => _isDxfLoading;
            private set { _isDxfLoading = value; OnPropertyChanged(); }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Bindable properties — transform text boxes
        // ═════════════════════════════════════════════════════════════════════

        private string _offsetXText = "0.000";
        private string _offsetYText = "0.000";
        private string _scaleText = "1.0000";
        private string _rotationText = "0.00";

        public string OffsetXText
        {
            get => _offsetXText;
            set { _offsetXText = value; OnPropertyChanged(); }
        }

        public string OffsetYText
        {
            get => _offsetYText;
            set { _offsetYText = value; OnPropertyChanged(); }
        }

        public string ScaleText
        {
            get => _scaleText;
            set { _scaleText = value; OnPropertyChanged(); }
        }

        public string RotationText
        {
            get => _rotationText;
            set { _rotationText = value; OnPropertyChanged(); }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Bindable properties — point-match reference fields
        // ═════════════════════════════════════════════════════════════════════

        private string _dxfRefXText = "0";
        private string _dxfRefYText = "0";
        private string _ifcRefXText = "0";
        private string _ifcRefYText = "0";

        public string DxfRefXText { get => _dxfRefXText; set { _dxfRefXText = value; OnPropertyChanged(); } }
        public string DxfRefYText { get => _dxfRefYText; set { _dxfRefYText = value; OnPropertyChanged(); } }
        public string IfcRefXText { get => _ifcRefXText; set { _ifcRefXText = value; OnPropertyChanged(); } }
        public string IfcRefYText { get => _ifcRefYText; set { _ifcRefYText = value; OnPropertyChanged(); } }

        // ═════════════════════════════════════════════════════════════════════
        //  Bindable properties — visual / overlay
        // ═════════════════════════════════════════════════════════════════════

        public double Opacity
        {
            get => Transform.Opacity;
            set { Transform.Opacity = value; OnPropertyChanged(); PushToUploader(); }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Bindable properties — computed / read-only
        // ═════════════════════════════════════════════════════════════════════

        public string TransformSummary =>
            $"Offset({Transform.OffsetX:F2}, {Transform.OffsetY:F2})  " +
            $"Scale={Transform.Scale:F4}  Rot={Transform.RotationDegrees:F1}°";

        public bool CanUndo => _undoStack.Count > 0;

        public ObservableCollection<DxfLayerInfo> Layers =>
            _canvasRenderer?.Layers ?? _emptyLayers;

        // ═════════════════════════════════════════════════════════════════════
        //  Command execute methods
        // ═════════════════════════════════════════════════════════════════════

        private void ExecuteLoadDxf()
        {
            IsDxfLoading = true;
            ValidationError = "";

            try
            {
                var dlg = new OpenFileDialog
                {
                    Title = "Open DXF Site Plan",
                    Filter = "DXF files (*.dxf)|*.dxf|All files (*.*)|*.*"
                };

                if (dlg.ShowDialog() != true) return;

                var doc = DxfDocument.Load(dlg.FileName);

                if (doc == null)
                {
                    StatusMessage = "⚠  Failed to parse DXF (null document returned).";
                    return;
                }

                _loadedDocument = doc;
                DxfFilePath = dlg.FileName;
                DxfFileName = Path.GetFileName(dlg.FileName);

                int total =
                    doc.Entities.Lines.Count() +
                    doc.Entities.Polylines2D.Count() +
                    doc.Entities.Arcs.Count() +
                    doc.Entities.Circles.Count();

                long fileSizeKb = new FileInfo(dlg.FileName).Length / 1024;
                DxfInfo = $"{total:N0} entities  •  {doc.Layers.Count} layers  •  {fileSizeKb:F0} KB";

                _canvasRenderer?.LoadDocument(doc);
                OnPropertyChanged(nameof(Layers));

                if (_uploader != null)
                {
                    PushToUploader();
                    StatusMessage = $"✓  Loaded: {DxfFileName}";
                }
                else
                {
                    StatusMessage = $"✓  Loaded: {DxfFileName}  " +
                                    "(IFC not yet loaded — transform will apply when IFC opens)";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"⚠  Error loading DXF: {ex.Message}";
            }
            finally
            {
                IsDxfLoading = false;
            }
        }

        private void ExecuteApplyManual()
        {
            PushUndoSnapshot();

            bool ok = _service.TryApplyManual(
                Transform,
                OffsetXText, OffsetYText, ScaleText, RotationText,
                out string error);

            if (!ok) { ValidationError = error; return; }

            ValidationError = "";
            PushToUploader();
            _canvasRenderer?.FitToView();
            StatusMessage = $"✓  Transform applied — {TransformSummary}";
        }

        private void ExecuteApplyPointMatch()
        {
            if (!double.TryParse(DxfRefXText, out double dxfX) ||
                !double.TryParse(DxfRefYText, out double dxfY))
            {
                ValidationError = "DXF reference coordinates must be numeric values.";
                return;
            }

            if (!double.TryParse(IfcRefXText, out double ifcX) ||
                !double.TryParse(IfcRefYText, out double ifcY))
            {
                ValidationError = "IFC reference coordinates must be numeric values.";
                return;
            }

            ValidationError = "";
            PushUndoSnapshot();
            _service.ApplyPointMatch(Transform, (dxfX, dxfY), (ifcX, ifcY));

            PushToUploader();
            _canvasRenderer?.FitToView();
            StatusMessage =
                $"✓  Point-match applied — offset: ({Transform.OffsetX:F2}, {Transform.OffsetY:F2}) m";
        }

        private void ExecuteAutoFit()
        {
            if (_canvasRenderer == null || _canvasRenderer.RawBounds.IsEmpty) return;

            PushUndoSnapshot();

            double sc = _service.SuggestScale(_canvasRenderer.RawBounds, 500.0);
            (double tx, double ty) = _service.SuggestCenterOffset(
                _canvasRenderer.RawBounds, sc, 0d, 0d);

            Transform.Scale = sc;
            Transform.OffsetX = tx;
            Transform.OffsetY = ty;

            SyncTextFromModel();
            PushToUploader();
            _canvasRenderer.FitToView();
            StatusMessage = $"✓  Auto-fit: scale={sc:F4}, offset=({tx:F2}, {ty:F2})";
        }

        private void ExecuteReset()
        {
            PushUndoSnapshot();
            Transform.Reset();
            SyncTextFromModel();
            PushToUploader();
            _canvasRenderer?.FitToView();
            StatusMessage = "Transform reset to identity.";
        }

        private void ExecuteUndo()
        {
            if (_undoStack.Count == 0) return;

            var snap = _undoStack.Pop();
            _service.Restore(Transform, snap);
            SyncTextFromModel();
            PushToUploader();
            _canvasRenderer?.Redraw();
            StatusMessage = "Undo applied.";
            OnPropertyChanged(nameof(CanUndo));
            CommandManager.InvalidateRequerySuggested();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Helpers
        // ═════════════════════════════════════════════════════════════════════

        private void SyncTextFromModel()
        {
            _offsetXText = Transform.OffsetX.ToString("F3");
            _offsetYText = Transform.OffsetY.ToString("F3");
            _scaleText = Transform.Scale.ToString("F4");
            _rotationText = Transform.RotationDegrees.ToString("F2");

            OnPropertyChanged(nameof(OffsetXText));
            OnPropertyChanged(nameof(OffsetYText));
            OnPropertyChanged(nameof(ScaleText));
            OnPropertyChanged(nameof(RotationText));
        }

        /// <summary>
        /// Forces text boxes to re-sync from the model.
        /// Called by DxfAlignmentDialog after RestoreTransform sets model values.
        /// </summary>
        public void RefreshTextBoxes() => SyncTextFromModel();

        private void PushUndoSnapshot()
        {
            if (_undoStack.Count >= MaxUndoDepth) return;
            _undoStack.Push(_service.Snapshot(Transform));
            OnPropertyChanged(nameof(CanUndo));
            CommandManager.InvalidateRequerySuggested();
        }

        private void PushToUploader()
        {
            if (_uploader == null) return;
            _uploader.AlignmentTransform = GetResultTransform();
            _uploader.Opacity = Opacity;

            // Re-render if the dialog loaded a file OR if the uploader was pre-loaded
            // externally (via DxfTabViewModel.ExecuteUploadDxf before opening the dialog).
            if (!string.IsNullOrEmpty(DxfFilePath) ||
                !string.IsNullOrEmpty(_uploader.LoadedFilePath))
                _uploader.ReApplyTransform();
        }
    }
}
