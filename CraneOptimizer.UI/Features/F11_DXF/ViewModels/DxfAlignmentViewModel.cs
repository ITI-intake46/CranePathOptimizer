using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Features.F11_DXF.Models;
using CraneOptimizer.UI.Features.F11_DXF.Services;
using Microsoft.Win32;
using netDxf;
using netDxf.Units;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Features.F11_DXF.Views;
using System.Windows;


namespace CraneOptimizer.UI.Features.F11_DXF.ViewModels
{
    public sealed class DxfAlignmentViewModel : ViewModelBase
    {
        // ── Injected collaborators ────────────────────────────────────────────

        private readonly DxfUploader? _uploader;
        private readonly DxfAlignmentService _service = new();

        // ── Domain model ──────────────────────────────────────────────────────

        public DxfTransformModel Transform { get; } = new();

        // ── Undo stack ────────────────────────────────────────────────────────

        private readonly Stack<object> _undoStack = new();
        private const int MaxUndoDepth = 20;

        // ── Loaded document ───────────────────────────────────────────────────

        private DxfDocument? _loadedDocument;

        // ── DXF bounds (for anchor-point auto-fill) ───────────────────────────

        private Rect _dxfBounds = Rect.Empty;

        public static IReadOnlyList<string> DxfUnitOptions { get; } = DxfTransformModel.DxfUnitLabels;
        public static IReadOnlyList<string> UnitOptions { get; } = DxfTransformModel.UnitLabels;
        public static IReadOnlyList<string> DxfReferenceAnchorOptions { get; } =
            new[] { "Center", "Top Left", "Top Right", "Bottom Left", "Bottom Right" };

        // ═════════════════════════════════════════════════════════════════════
        //  Constructor
        // ═════════════════════════════════════════════════════════════════════

        public DxfAlignmentViewModel(DxfUploader? uploader = null)
        {
            _uploader = uploader;

            Transform.PropertyChanged += (_, _) => OnPropertyChanged(nameof(TransformSummary));

            LoadDxfCommand   = new RelayCommand(ExecuteLoadDxf);
            ApplyManualCommand = new RelayCommand(ExecuteApplyManual);
            ResetCommand     = new RelayCommand(ExecuteReset);

            SyncTextFromModel();
            InitializeFromUploader();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Commands
        // ═════════════════════════════════════════════════════════════════════

        public ICommand LoadDxfCommand    { get; }
        public ICommand ApplyManualCommand { get; }
        public ICommand ResetCommand      { get; }

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

        private string _scaleText    = "1.0000";
        private string _rotationText = "0.00";

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
        //  Bindable properties — DXF reference point (world units)
        // ═════════════════════════════════════════════════════════════════════

        private string _detectedDxfRefXText = "0";
        private string _detectedDxfRefYText = "0";
        private string _dxfRefXText = "0";
        private string _dxfRefYText = "0";
        private bool _useDetectedDxfReference;
        private string _selectedDxfReferenceAnchor = "Bottom Left";

        public string DetectedDxfRefXText
        {
            get => _detectedDxfRefXText;
            private set
            {
                _detectedDxfRefXText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DetectedDxfRefXDisplayText));
                OnPropertyChanged(nameof(DxfOriginalReferenceSummary));
            }
        }

        public string DetectedDxfRefYText
        {
            get => _detectedDxfRefYText;
            private set
            {
                _detectedDxfRefYText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DetectedDxfRefYDisplayText));
                OnPropertyChanged(nameof(DxfOriginalReferenceSummary));
            }
        }

        public bool UseDetectedDxfReference
        {
            get => _useDetectedDxfReference;
            set
            {
                if (_useDetectedDxfReference == value) return;
                _useDetectedDxfReference = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UseCustomDxfReference));
                OnPropertyChanged(nameof(IsCustomDxfReferenceEnabled));
                OnPropertyChanged(nameof(TransformSummary));
            }
        }

        public bool UseCustomDxfReference
        {
            get => !UseDetectedDxfReference;
            set
            {
                if (value)
                    UseDetectedDxfReference = false;
            }
        }

        public bool IsCustomDxfReferenceEnabled => UseCustomDxfReference;

        public string SelectedDxfReferenceAnchor
        {
            get => _selectedDxfReferenceAnchor;
            set
            {
                if (string.Equals(_selectedDxfReferenceAnchor, value, StringComparison.OrdinalIgnoreCase))
                    return;

                _selectedDxfReferenceAnchor = value ?? "Center";
                OnPropertyChanged();
                OnPropertyChanged(nameof(DxfOriginalGeometryReferenceSummary));
                OnPropertyChanged(nameof(TransformSummary));
            }
        }

        public string DxfRefXText
        {
            get => _dxfRefXText;
            set { _dxfRefXText = value; OnPropertyChanged(); OnPropertyChanged(nameof(TransformSummary)); }
        }

        public string DxfRefYText
        {
            get => _dxfRefYText;
            set { _dxfRefYText = value; OnPropertyChanged(); OnPropertyChanged(nameof(TransformSummary)); }
        }

        public string DxfReferenceUnitLabel => DxfTransformModel.UnitLabels[Transform.ResolvedDxfUnitIndex];
        public string DxfReferenceXLabel => $"X ({DxfReferenceUnitLabel})";
        public string DxfReferenceYLabel => $"Y ({DxfReferenceUnitLabel})";
        public string DetectedDxfRefXDisplayText => $"{DetectedDxfRefXText} {DxfReferenceUnitLabel}";
        public string DetectedDxfRefYDisplayText => $"{DetectedDxfRefYText} {DxfReferenceUnitLabel}";
        public string DxfOriginalReferenceSummary =>
            $"DXF base point: X={DetectedDxfRefXDisplayText}, Y={DetectedDxfRefYDisplayText}";
        public string DxfOriginalGeometryReferenceSummary
        {
            get
            {
                if (_dxfBounds.IsEmpty)
                    return "Original drawing point: unavailable";

                var (x, y) = GetReferenceAnchorPoint(SelectedDxfReferenceAnchor);
                return $"Original {SelectedDxfReferenceAnchor}: X={FormatReferenceValue(x)} {DxfReferenceUnitLabel}, " +
                       $"Y={FormatReferenceValue(y)} {DxfReferenceUnitLabel}";
            }
        }

        public string SelectedDxfUnit
        {
            get => DxfUnitLabelFromIndex(Transform.DxfUnitIndex);
            set
            {
                int oldUnitIndex = Transform.ResolvedDxfUnitIndex;
                int index = DxfUnitIndexFromLabel(value);
                if (Transform.DxfUnitIndex == index) return;

                Transform.DxfUnitIndex = index;
                ConvertReferenceTextsForDxfUnitChange(oldUnitIndex, Transform.ResolvedDxfUnitIndex);
                OnPropertyChanged();
                RaiseDxfReferenceUnitChanged();
                OnPropertyChanged(nameof(TransformSummary));
            }
        }

        private string _globalOffsetXText = "0";
        private string _globalOffsetYText = "0";

        public string GlobalOffsetXText
        {
            get => _globalOffsetXText;
            set { _globalOffsetXText = value; OnPropertyChanged(); OnPropertyChanged(nameof(TransformSummary)); }
        }

        public string GlobalOffsetYText
        {
            get => _globalOffsetYText;
            set { _globalOffsetYText = value; OnPropertyChanged(); OnPropertyChanged(nameof(TransformSummary)); }
        }

        public string SelectedGlobalOffsetUnit
        {
            get => DxfTransformModel.UnitLabels[Transform.GlobalOffsetUnitIndex];
            set
            {
                int oldIndex = Transform.GlobalOffsetUnitIndex;
                if (TryParseDouble(GlobalOffsetXText, out double x))
                    Transform.GlobalOffsetX = UnitToSceneMillimeters(x, oldIndex);
                if (TryParseDouble(GlobalOffsetYText, out double y))
                    Transform.GlobalOffsetY = UnitToSceneMillimeters(y, oldIndex);

                int index = UnitIndexFromLabel(value);
                if (Transform.GlobalOffsetUnitIndex == index) return;

                Transform.GlobalOffsetUnitIndex = index;
                SyncTextFromModel();
                OnPropertyChanged();
                OnPropertyChanged(nameof(TransformSummary));
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Bindable properties — visual / overlay
        // ═════════════════════════════════════════════════════════════════════

        public double Opacity
        {
            get => Transform.Opacity;
            set { Transform.Opacity = value; OnPropertyChanged(); PushToUploader(); }
        }

        public bool KeepText
        {
            get => Transform.ShowText;
            set
            {
                if (Transform.ShowText == value) return;
                Transform.ShowText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TransformSummary));
                PushToUploader();
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Bindable properties — computed / read-only
        // ═════════════════════════════════════════════════════════════════════

        public string TransformSummary
        {
            get
            {
                string global =
                    $"Global({SceneMillimetersToUnit(Transform.GlobalOffsetX, Transform.GlobalOffsetUnitIndex):F2}, " +
                    $"{SceneMillimetersToUnit(Transform.GlobalOffsetY, Transform.GlobalOffsetUnitIndex):F2}) {SelectedGlobalOffsetUnit}";
                string common = $"{global}  DXF={DxfUnitSummary}  Scale={Transform.Scale:F4}  Rot={Transform.RotationDegrees:F1} deg";

                if (UseCustomDxfReference)
                {
                    var (x, y) = GetCustomReferenceTargetMeters();
                    return $"{SelectedDxfReferenceAnchor} -> ({x:F2}, {y:F2}) m  {common}";
                }

                if (UseDetectedDxfReference)
                {
                    double x = SceneMillimetersToUnit(Transform.GlobalOffsetX, 2);
                    double y = SceneMillimetersToUnit(Transform.GlobalOffsetY, 2);
                    return $"DXF original -> ({x:F2}, {y:F2}) m  {common}";
                }

                return $"Offset({SceneMillimetersToUnit(Transform.OffsetX, 2):F2}, {SceneMillimetersToUnit(Transform.OffsetY, 2):F2}) m  {common}";
            }
        }

        public bool CanUndo => _undoStack.Count > 0;

        // ═════════════════════════════════════════════════════════════════════
        //  Public API
        // ═════════════════════════════════════════════════════════════════════

        public Matrix3D GetResultTransform()
        {
            double s   = Transform.EffectiveScale;
            double rad = -Transform.RotationRadians;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);

            return new Matrix3D(
                s * cos, -s * sin, 0, 0,
                s * sin,  s * cos, 0, 0,
                0,        0,       1, 0,
                Transform.OffsetX, Transform.OffsetY, 0, 1);
        }

        /// <summary>Forces text boxes to re-sync from the model (called by code-behind after RestoreTransform).</summary>
        public void RefreshTextBoxes() => SyncTextFromModel();

        public void RefreshReferenceTextBoxes()
        {
            DxfRefXText = Transform.DxfRefX.ToString("F2", CultureInfo.InvariantCulture);
            DxfRefYText = Transform.DxfRefY.ToString("F2", CultureInfo.InvariantCulture);
        }

        public void PushCurrentTransformToUploader()
        {
            ValidationError = "";
            PushToUploader();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Command execute methods
        // ═════════════════════════════════════════════════════════════════════

        private void ExecuteLoadDxf()
        {
            IsDxfLoading  = true;
            ValidationError = "";

            try
            {
                var dlg = new OpenFileDialog
                {
                    Title  = "Open DXF Site Plan",
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

                int textTotal = CountDxfTextEntities(doc);
                int total =
                    doc.Entities.Lines.Count() +
                    doc.Entities.Polylines2D.Count() +
                    doc.Entities.Arcs.Count() +
                    doc.Entities.Circles.Count() +
                    doc.Entities.Ellipses.Count() +
                    textTotal;

                long fileSizeKb = new FileInfo(dlg.FileName).Length / 1024;
                DxfInfo = $"{total:N0} entities  •  {doc.Layers.Count} layers  •  {fileSizeKb:F0} KB";

                DxfInfo = $"{total:N0} entities - {doc.Layers.Count} layers - {fileSizeKb:F0} KB - text {textTotal:N0}";

                // Compute DXF bounds so the locked DXF reference can use the drawing centre.
                ApplyDxfDocumentUnit(doc);
                ComputeDxfBounds(doc);
                ApplyDxfOriginalReferencePoint(doc);
                TryApplyReferencePlacement(out _);

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

            // Preserve existing offset values — user is only setting scale/rotation here
            bool ok = _service.TryApplyManual(
                Transform,
                Transform.OffsetX.ToString("F3"),
                Transform.OffsetY.ToString("F3"),
                ScaleText, RotationText,
                out string error);

            if (!ok) { ValidationError = error; return; }

            if (!TryApplyReferencePlacement(out string placementError))
            {
                ValidationError = placementError;
                return;
            }

            ValidationError = "";
            PushToUploader();
            StatusMessage = $"✓  Transform applied — {TransformSummary}";
        }

        private void ExecuteReset()
        {
            PushUndoSnapshot();
            Transform.Reset();
            if (_loadedDocument != null)
                ApplyDxfDocumentUnit(_loadedDocument);
            else
                SyncTextFromModel();
            PushToUploader();
            StatusMessage = "Transform reset to identity.";
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Reference-point helpers
        // ═════════════════════════════════════════════════════════════════════

        private void ComputeDxfBounds(DxfDocument doc)
        {
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            bool   hasPoints = false;

            void Expand(double x, double y)
            {
                hasPoints = true;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }

            foreach (var l in doc.Entities.Lines)
            {
                Expand(l.StartPoint.X, l.StartPoint.Y);
                Expand(l.EndPoint.X,   l.EndPoint.Y);
            }
            foreach (var p in doc.Entities.Polylines2D)
            {
                foreach (var v in p.Vertexes)
                    Expand(v.Position.X, v.Position.Y);
            }
            foreach (var a in doc.Entities.Arcs)
            {
                Expand(a.Center.X - a.Radius, a.Center.Y - a.Radius);
                Expand(a.Center.X + a.Radius, a.Center.Y + a.Radius);
            }
            foreach (var c in doc.Entities.Circles)
            {
                Expand(c.Center.X - c.Radius, c.Center.Y - c.Radius);
                Expand(c.Center.X + c.Radius, c.Center.Y + c.Radius);
            }
            foreach (var e in doc.Entities.Ellipses)
            {
                for (int i = 0; i < 36; i++)
                {
                    var point = GetEllipsePoint(e, i * 10.0);
                    Expand(point.X, point.Y);
                }
            }

            _dxfBounds = hasPoints
                ? new Rect(minX, minY, maxX - minX, maxY - minY)
                : Rect.Empty;
            OnPropertyChanged(nameof(DxfOriginalGeometryReferenceSummary));
        }

        private void ApplyDxfOriginalReferencePoint(DxfDocument doc)
        {
            var origin = doc.DrawingVariables.InsBase;
            DetectedDxfRefXText = FormatReferenceValue(origin.X);
            DetectedDxfRefYText = FormatReferenceValue(origin.Y);
        }

        private bool TryApplyReferencePlacement(out string error)
        {
            if (!TryParseGlobalOffset(out double globalX, out double globalY, out error))
                return false;

            if (!TryResolveSelectedRefPoint(out double dxfRefX, out double dxfRefY, out error))
                return false;

            Transform.GlobalOffsetX = globalX;
            Transform.GlobalOffsetY = globalY;
            Transform.DxfRefX = dxfRefX;
            Transform.DxfRefY = dxfRefY;

            if (UseDetectedDxfReference)
            {
                ApplyOriginalReferenceOffset(dxfRefX, dxfRefY);
            }
            else if (UseCustomDxfReference)
            {
                ApplyCustomAnchorOffset(dxfRefX, dxfRefY);
            }
            else
            {
                Transform.OffsetX = Transform.GlobalOffsetX;
                Transform.OffsetY = Transform.GlobalOffsetY;
            }

            error = "";
            return true;
        }

        private void ApplyOriginalReferenceOffset(double referenceX, double referenceY)
        {
            var (projectedX, projectedY) = ProjectDxfVector(referenceX, referenceY);
            Transform.OffsetX = Transform.GlobalOffsetX - projectedX;
            Transform.OffsetY = Transform.GlobalOffsetY - projectedY;
        }

        private void ApplyCustomAnchorOffset(double anchorTargetX, double anchorTargetY)
        {
            if (_dxfBounds.IsEmpty)
            {
                Transform.OffsetX = Transform.GlobalOffsetX;
                Transform.OffsetY = Transform.GlobalOffsetY;
                return;
            }

            var (anchorX, anchorY) = GetReferenceAnchorPoint(SelectedDxfReferenceAnchor);
            var (shiftX, shiftY) = ProjectDxfVector(anchorTargetX - anchorX, anchorTargetY - anchorY);

            Transform.OffsetX = Transform.GlobalOffsetX + shiftX;
            Transform.OffsetY = Transform.GlobalOffsetY + shiftY;
        }

        private (double X, double Y) GetCustomReferenceTargetMeters()
        {
            double dxfX = Transform.DxfRefX;
            double dxfY = Transform.DxfRefY;
            double globalX = Transform.GlobalOffsetX;
            double globalY = Transform.GlobalOffsetY;

            if (TryParseDouble(DxfRefXText, out double parsedDxfX))
                dxfX = parsedDxfX;
            if (TryParseDouble(DxfRefYText, out double parsedDxfY))
                dxfY = parsedDxfY;
            if (TryParseGlobalOffset(out double parsedGlobalX, out double parsedGlobalY, out _))
            {
                globalX = parsedGlobalX;
                globalY = parsedGlobalY;
            }

            double targetX = globalX + UnitToSceneMillimeters(dxfX, Transform.ResolvedDxfUnitIndex);
            double targetY = globalY + UnitToSceneMillimeters(dxfY, Transform.ResolvedDxfUnitIndex);
            return (SceneMillimetersToUnit(targetX, 2), SceneMillimetersToUnit(targetY, 2));
        }

        private void ConvertReferenceTextsForDxfUnitChange(int oldUnitIndex, int newUnitIndex)
        {
            if (oldUnitIndex == newUnitIndex) return;

            if (TryParseDouble(DxfRefXText, out double customX))
                DxfRefXText = ConvertReferenceValue(customX, oldUnitIndex, newUnitIndex);
            if (TryParseDouble(DxfRefYText, out double customY))
                DxfRefYText = ConvertReferenceValue(customY, oldUnitIndex, newUnitIndex);
            if (TryParseDouble(DetectedDxfRefXText, out double detectedX))
                DetectedDxfRefXText = ConvertReferenceValue(detectedX, oldUnitIndex, newUnitIndex);
            if (TryParseDouble(DetectedDxfRefYText, out double detectedY))
                DetectedDxfRefYText = ConvertReferenceValue(detectedY, oldUnitIndex, newUnitIndex);
        }

        private static string ConvertReferenceValue(double value, int oldUnitIndex, int newUnitIndex)
        {
            double sceneMillimeters = UnitToSceneMillimeters(value, oldUnitIndex);
            return FormatReferenceValue(SceneMillimetersToUnit(sceneMillimeters, newUnitIndex));
        }

        private static string FormatReferenceValue(double value)
            => value.ToString("0.##", CultureInfo.InvariantCulture);

        private (double X, double Y) GetReferenceAnchorPoint(string? anchor)
        {
            double left = _dxfBounds.Left;
            double right = _dxfBounds.Right;
            double bottom = _dxfBounds.Top;
            double top = _dxfBounds.Bottom;
            double centerX = left + _dxfBounds.Width / 2.0;
            double centerY = bottom + _dxfBounds.Height / 2.0;

            return anchor switch
            {
                "Top Left" => (left, top),
                "Top Right" => (right, top),
                "Bottom Left" => (left, bottom),
                "Bottom Right" => (right, bottom),
                _ => (centerX, centerY)
            };
        }

        private (double X, double Y) ProjectDxfVector(double x, double y)
        {
            double s = Transform.EffectiveScale;
            double rad = -Transform.RotationRadians;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);

            return (
                s * (cos * x + sin * y),
                s * (-sin * x + cos * y));
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Helpers
        // ═════════════════════════════════════════════════════════════════════

        private void SyncTextFromModel()
        {
            _scaleText    = Transform.Scale.ToString("F4");
            _rotationText = Transform.RotationDegrees.ToString("F2");
            _globalOffsetXText = SceneMillimetersToUnit(Transform.GlobalOffsetX, Transform.GlobalOffsetUnitIndex)
                .ToString("F2", CultureInfo.InvariantCulture);
            _globalOffsetYText = SceneMillimetersToUnit(Transform.GlobalOffsetY, Transform.GlobalOffsetUnitIndex)
                .ToString("F2", CultureInfo.InvariantCulture);

            OnPropertyChanged(nameof(ScaleText));
            OnPropertyChanged(nameof(RotationText));
            OnPropertyChanged(nameof(GlobalOffsetXText));
            OnPropertyChanged(nameof(GlobalOffsetYText));
            OnPropertyChanged(nameof(SelectedDxfUnit));
            OnPropertyChanged(nameof(KeepText));
            RaiseDxfReferenceUnitChanged();
            OnPropertyChanged(nameof(SelectedGlobalOffsetUnit));
        }

        private void RaiseDxfReferenceUnitChanged()
        {
            OnPropertyChanged(nameof(DxfReferenceUnitLabel));
            OnPropertyChanged(nameof(DxfReferenceXLabel));
            OnPropertyChanged(nameof(DxfReferenceYLabel));
            OnPropertyChanged(nameof(DetectedDxfRefXDisplayText));
            OnPropertyChanged(nameof(DetectedDxfRefYDisplayText));
            OnPropertyChanged(nameof(DxfOriginalReferenceSummary));
            OnPropertyChanged(nameof(DxfOriginalGeometryReferenceSummary));
        }

        private void InitializeFromUploader()
        {
            if (_uploader == null || string.IsNullOrWhiteSpace(_uploader.LoadedFilePath))
                return;

            DxfFilePath = _uploader.LoadedFilePath;
            DxfFileName = Path.GetFileName(_uploader.LoadedFilePath);
            Transform.ShowText = _uploader.ShowText;

            try
            {
                var doc = _uploader.LoadedDocument ?? DxfDocument.Load(_uploader.LoadedFilePath);
                if (doc == null) return;

                _loadedDocument = doc;
                int textTotal = CountDxfTextEntities(doc);
                int total =
                    doc.Entities.Lines.Count() +
                    doc.Entities.Polylines2D.Count() +
                    doc.Entities.Arcs.Count() +
                    doc.Entities.Circles.Count() +
                    doc.Entities.Ellipses.Count() +
                    textTotal;

                long fileSizeKb = new FileInfo(_uploader.LoadedFilePath).Length / 1024;
                DxfInfo = $"{total:N0} entities - {doc.Layers.Count} layers - {fileSizeKb:F0} KB - text {textTotal:N0}";
                ApplyDxfDocumentUnit(doc);
                ComputeDxfBounds(doc);
                ApplyDxfOriginalReferencePoint(doc);
                TryApplyReferencePlacement(out _);
                StatusMessage = $"Ready: {DxfFileName}";
                PushToUploader();
            }
            catch (Exception ex)
            {
                StatusMessage = $"DXF loaded; reference bounds unavailable: {ex.Message}";
            }
        }

        private void ApplyDxfDocumentUnit(DxfDocument doc)
        {
            Transform.DxfDocumentUnitIndex = UnitIndexFromDrawingUnit(doc.DrawingVariables.InsUnits);
            Transform.GlobalOffsetUnitIndex = 0;
            SyncTextFromModel();
        }

        private static int UnitIndexFromDrawingUnit(DrawingUnits unit) => unit switch
        {
            DrawingUnits.Centimeters => 1,
            DrawingUnits.Meters => 2,
            DrawingUnits.Inches or DrawingUnits.USSurveyInches => 3,
            DrawingUnits.Feet or DrawingUnits.USSurveyFeet => 4,
            _ => 0
        };

        private bool TryParseRefPoint(out double x, out double y, out string error)
        {
            bool okX = TryParseDouble(DxfRefXText, out x);
            bool okY = TryParseDouble(DxfRefYText, out y);

            if (okX && okY)
            {
                error = "";
                return true;
            }

            error = "DXF reference X and Y must be numbers.";
            return false;
        }

        private bool TryParseDetectedRefPoint(out double x, out double y, out string error)
        {
            bool okX = TryParseDouble(DetectedDxfRefXText, out x);
            bool okY = TryParseDouble(DetectedDxfRefYText, out y);

            if (okX && okY)
            {
                error = "";
                return true;
            }

            error = "Detected DXF reference point is unavailable.";
            return false;
        }

        private bool TryResolveSelectedRefPoint(out double x, out double y, out string error)
            => UseDetectedDxfReference
                ? TryParseDetectedRefPoint(out x, out y, out error)
                : TryParseRefPoint(out x, out y, out error);

        private bool TryParseGlobalOffset(out double xSceneMillimeters, out double ySceneMillimeters, out string error)
        {
            bool okX = TryParseDouble(GlobalOffsetXText, out double x);
            bool okY = TryParseDouble(GlobalOffsetYText, out double y);

            if (okX && okY)
            {
                xSceneMillimeters = UnitToSceneMillimeters(x, Transform.GlobalOffsetUnitIndex);
                ySceneMillimeters = UnitToSceneMillimeters(y, Transform.GlobalOffsetUnitIndex);
                error = "";
                return true;
            }

            xSceneMillimeters = 0;
            ySceneMillimeters = 0;
            error = "Global offset X and Y must be numbers.";
            return false;
        }

        private static bool TryParseDouble(string? text, out double value)
            => double.TryParse(
                text?.Trim(),
                NumberStyles.Float | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out value);

        private static int UnitIndexFromLabel(string? label)
        {
            for (int i = 0; i < DxfTransformModel.UnitLabels.Length; i++)
            {
                if (string.Equals(label, DxfTransformModel.UnitLabels[i], StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return 0;
        }

        private string DxfUnitSummary =>
            Transform.DxfUnitIndex == DxfTransformModel.DefaultUnitIndex
                ? $"Default ({DxfTransformModel.UnitLabels[Transform.ResolvedDxfUnitIndex]})"
                : DxfTransformModel.UnitLabels[Transform.ResolvedDxfUnitIndex];

        private static string DxfUnitLabelFromIndex(int unitIndex)
            => unitIndex == DxfTransformModel.DefaultUnitIndex
                ? "Default"
                : DxfTransformModel.UnitLabels[DxfTransformModel.ClampUnitIndex(unitIndex)];

        private static int DxfUnitIndexFromLabel(string? label)
        {
            if (string.Equals(label, "Default", StringComparison.OrdinalIgnoreCase))
                return DxfTransformModel.DefaultUnitIndex;

            return UnitIndexFromLabel(label);
        }

        private static double UnitToSceneMillimeters(double value, int unitIndex)
            => value * DxfTransformModel.UnitScaleToSceneMillimeters(unitIndex);

        private static double SceneMillimetersToUnit(double sceneMillimeters, int unitIndex)
            => sceneMillimeters / DxfTransformModel.UnitScaleToSceneMillimeters(unitIndex);

        private static int CountDxfTextEntities(DxfDocument doc)
        {
            int count = doc.Entities.Texts.Count(t => t.IsVisible && !string.IsNullOrWhiteSpace(t.Value)) +
                        doc.Entities.MTexts.Count(t => t.IsVisible && !string.IsNullOrWhiteSpace(t.Value));

            foreach (var insert in doc.Entities.Inserts)
                count += CountInsertTextEntities(insert, depth: 0);

            foreach (var dimension in doc.Entities.Dimensions)
                count += CountDimensionTextEntities(dimension);

            return count;
        }

        private static int CountDimensionTextEntities(netDxf.Entities.Dimension dimension)
        {
            if (!dimension.IsVisible)
                return 0;

            int count = 0;
            if (dimension.Block != null)
            {
                count += dimension.Block.Entities.OfType<netDxf.Entities.Text>()
                    .Count(t => t.IsVisible && !string.IsNullOrWhiteSpace(t.Value));
                count += dimension.Block.Entities.OfType<netDxf.Entities.MText>()
                    .Count(t => t.IsVisible && !string.IsNullOrWhiteSpace(t.Value));
            }

            if (count > 0)
                return count;

            return string.Equals(dimension.UserText, " ", StringComparison.Ordinal)
                ? 0
                : 1;
        }

        private static int CountInsertTextEntities(netDxf.Entities.Insert insert, int depth)
        {
            if (depth > 8 || !insert.IsVisible)
                return 0;

            int count = 0;
            count += insert.Attributes.Count(a => a.IsVisible && !string.IsNullOrWhiteSpace(a.Value));

            try
            {
                count += insert.Block.Entities.OfType<netDxf.Entities.Text>()
                    .Count(t => t.IsVisible && !string.IsNullOrWhiteSpace(t.Value));
                count += insert.Block.Entities.OfType<netDxf.Entities.MText>()
                    .Count(t => t.IsVisible && !string.IsNullOrWhiteSpace(t.Value));

                foreach (var nestedInsert in insert.Block.Entities.OfType<netDxf.Entities.Insert>())
                    count += CountInsertTextEntities(nestedInsert, depth + 1);
            }
            catch
            {
                // Some malformed block references can fail while counting; the loader still handles top-level geometry.
            }

            return count;
        }

        private static (double X, double Y) GetEllipsePoint(netDxf.Entities.Ellipse ellipse, double angleDegrees)
        {
            double majorRadius = ellipse.MajorAxis / 2.0;
            double minorRadius = ellipse.MinorAxis / 2.0;
            double angle = angleDegrees * Math.PI / 180.0;
            double rotation = ellipse.Rotation * Math.PI / 180.0;
            double localX = majorRadius * Math.Cos(angle);
            double localY = minorRadius * Math.Sin(angle);
            double cos = Math.Cos(rotation);
            double sin = Math.Sin(rotation);

            return (
                ellipse.Center.X + localX * cos - localY * sin,
                ellipse.Center.Y + localX * sin + localY * cos);
        }

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

            if (!string.IsNullOrEmpty(DxfFilePath) &&
                !string.Equals(DxfFilePath, _uploader.LoadedFilePath, StringComparison.OrdinalIgnoreCase))
            {
                _uploader.AlignmentTransform = GetResultTransform();
                _uploader.Opacity = Opacity;
                _uploader.LoadDxf(DxfFilePath, KeepText);
                return;
            }

            if (!string.IsNullOrEmpty(_uploader.LoadedFilePath))
                _uploader.ApplyDisplayState(GetResultTransform(), Opacity, KeepText);
        }
    }
}
