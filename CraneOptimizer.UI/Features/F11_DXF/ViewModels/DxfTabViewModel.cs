using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Features.F11_DXF.Models;
using CraneOptimizer.UI.Features.F11_DXF.Views;
using HelixToolkit.Wpf;
using Microsoft.Win32;
using System.IO;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F11_DXF.ViewModels
{
    public class DxfTabViewModel : ViewModelBase
    {
        private DxfUploader? _uploader;
        private string? _loadedFilePath;
        private string _loadedFileName = string.Empty;
        private string _statusMessage = "No DXF loaded";
        private bool _hasDxfLoaded;
        private Matrix3D? _pendingTransform;
        private double[] _savedTransformArray = NormalizeTransformArray(null);
        private bool _hasSavedTransformForLoadedFile;
        private DxfAlignmentDialog? _openDialog;

        public DxfTabViewModel()
        {
            UploadDxfCommand = new RelayCommand(ExecuteUploadDxf);
            AlignCommand = new RelayCommand(ExecuteAlign, () => HasDxfLoaded);

            AddAnotherDxfCommand = new RelayCommand(() => { });
            DeleteCommand = new RelayCommand(() => { });
            SelectCommand = new RelayCommand(() => { });
            SelectAllCommand = new RelayCommand(() => { });
        }

        public ICommand UploadDxfCommand { get; }
        public ICommand AlignCommand { get; }
        public ICommand AddAnotherDxfCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand SelectCommand { get; }
        public ICommand SelectAllCommand { get; }

        public event Action<DxfUploader>? DxfLoaded;
        public event Action? DxfStateChanged;

        public DxfUploader? CurrentUploader => _uploader;
        public string? LoadedFilePath => _loadedFilePath;
        public double[] SavedTransformArray => (double[])_savedTransformArray.Clone();

        public string LoadedFileName
        {
            get => _loadedFileName;
            private set { _loadedFileName = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set { _statusMessage = value; OnPropertyChanged(); }
        }

        public bool HasDxfLoaded
        {
            get => _hasDxfLoaded;
            private set { _hasDxfLoaded = value; OnPropertyChanged(); }
        }

        public Matrix3D? PendingTransform
        {
            get => _pendingTransform;
            private set { _pendingTransform = value; OnPropertyChanged(); }
        }

        public void Initialize(HelixViewport3D viewport)
        {
            if (_uploader != null)
                return;

            _uploader = new DxfUploader(viewport);

            if (!string.IsNullOrWhiteSpace(_loadedFilePath))
                LoadCurrentDxfIntoUploader(raiseStateChanged: false);
        }

        public void LoadDxfFromProject(string filePath, double[]? transformArray)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            _loadedFilePath = filePath;
            LoadedFileName = Path.GetFileName(filePath);
            _savedTransformArray = NormalizeTransformArray(transformArray);
            _hasSavedTransformForLoadedFile = transformArray != null && transformArray.Length >= 4;
            PendingTransform = MatrixFromTransformArray(_savedTransformArray);

            LoadCurrentDxfIntoUploader(raiseStateChanged: false);
        }

        private void ExecuteUploadDxf()
        {
            var picker = new OpenFileDialog
            {
                Title = "Select DXF File",
                Filter = "DXF Files (*.dxf)|*.dxf|All Files (*.*)|*.*"
            };

            if (picker.ShowDialog() != true)
                return;

            var isDifferentFile = !string.Equals(_loadedFilePath, picker.FileName, StringComparison.OrdinalIgnoreCase);
            if (isDifferentFile)
            {
                _savedTransformArray = NormalizeTransformArray(null);
                _hasSavedTransformForLoadedFile = false;
            }

            _loadedFilePath = picker.FileName;
            LoadedFileName = Path.GetFileName(picker.FileName);
            StatusMessage = $"Loading: {LoadedFileName}...";

            if (!LoadCurrentDxfIntoUploader(raiseStateChanged: true))
                return;

            ExecuteAlign();
        }

        private void ExecuteAlign()
        {
            if (_openDialog != null)
            {
                _openDialog.Activate();
                return;
            }

            if (_uploader == null)
                return;

            try
            {
                var dlg = new DxfAlignmentDialog(_uploader);
                dlg.Owner = System.Windows.Application.Current.MainWindow;

                if (_hasSavedTransformForLoadedFile)
                    dlg.RestoreTransform(_savedTransformArray);

                dlg.Closed += (_, _) =>
                {
                    _savedTransformArray = dlg.GetTransformArray();
                    _hasSavedTransformForLoadedFile = true;
                    PendingTransform = dlg.GetResultTransform();
                    _openDialog = null;
                    StatusMessage = $"Aligned: {LoadedFileName}";
                    DxfStateChanged?.Invoke();
                };

                _openDialog = dlg;
                dlg.Show();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Alignment error: {ex.Message}";
            }
        }

        private bool LoadCurrentDxfIntoUploader(bool raiseStateChanged)
        {
            if (string.IsNullOrWhiteSpace(_loadedFilePath))
                return false;

            if (_uploader != null)
            {
                var transform = MatrixFromTransformArray(_savedTransformArray);
                var showText = GetShowText(_savedTransformArray);
                bool success;

                try
                {
                    _uploader.AlignmentTransform = transform;
                    success = _uploader.LoadDxf(_loadedFilePath, showText);
                    _uploader.ApplyDisplayState(transform, _uploader.Opacity, showText);
                }
                catch (Exception ex)
                {
                    StatusMessage = $"DXF load failed: {ex.Message}";
                    return false;
                }

                if (!success)
                {
                    StatusMessage = $"Could not load DXF: {_uploader.LastLoadDiagnostic}";
                    System.Windows.MessageBox.Show(
                        $"Could not load DXF file.\n\n{_uploader.LastLoadDiagnostic}\n\n" +
                        "Ensure the file is AutoCAD 2010+ DXF format.",
                        "DXF Load Failed",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    return false;
                }
            }

            HasDxfLoaded = true;
            StatusMessage = $"Loaded: {LoadedFileName}";

            if (_uploader != null)
                DxfLoaded?.Invoke(_uploader);

            if (raiseStateChanged)
                DxfStateChanged?.Invoke();

            return true;
        }

        private static double[] NormalizeTransformArray(double[]? transformArray)
        {
            var normalized = new[] { 0, 0, 1.0, 0, 0, 0, 0, 0, 0, 0, (double)DxfTransformModel.DefaultUnitIndex, 0, 1 };
            if (transformArray == null)
                return normalized;

            for (var i = 0; i < Math.Min(normalized.Length, transformArray.Length); i++)
                normalized[i] = transformArray[i];

            return normalized;
        }

        private static bool GetShowText(double[] transformArray)
            => transformArray.Length < 13 || transformArray[12] >= 0.5;

        private static Matrix3D MatrixFromTransformArray(double[] transformArray)
        {
            var model = DxfTransformModel.FromArray(transformArray);
            var scale = model.EffectiveScale;
            var radians = -model.RotationRadians;
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);

            return new Matrix3D(
                scale * cos, -scale * sin, 0, 0,
                scale * sin, scale * cos, 0, 0,
                0, 0, 1, 0,
                model.OffsetX, model.OffsetY, 0, 1);
        }
    }
}
