using System;
using System.Windows.Input;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.CreatedProject;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Application.Services;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;

namespace CraneOptimizer.UI.Features.F18_CoordinateReference.ViewModels
{
    public sealed class CoordinateReferenceViewModel : ViewModelBase, IDisposable
    {
        private readonly IProjectCoordinateTransformService _transformService;
        private readonly IStatusService _statusService;

        private bool _isEnabled;
        private string _name = "Coordinate Reference";
        private string? _coordinateSystemName;
        private string? _epsgCode;
        private double _localReferenceX;
        private double _localReferenceY;
        private double _localReferenceZ;
        private double _surveyEasting;
        private double _surveyNorthing;
        private double _surveyElevation;
        private double _appliedSurveyEasting;
        private double _appliedSurveyNorthing;
        private double _appliedSurveyElevation;
        private double _rotationToNorthDegrees;
        private double _scale = 1.0;
        private double? _latitude;
        private double? _longitude;
        private string? _description;

        private double _previewLocalX = 10.0;
        private double _previewLocalY = 10.0;
        private double _previewLocalZ = 0.0;
        private double _previewSurveyEasting = 500000.0;
        private double _previewSurveyNorthing = 5000000.0;
        private double _previewSurveyElevation = 10.0;

        private string _previewSurveyResult = string.Empty;
        private string _previewLocalResult = string.Empty;
        private string _statusText = "Reference disabled. Defaulting to local coordinates.";
        private bool _isSuccess = true;

        public CoordinateReferenceViewModel(IProjectCoordinateTransformService transformService, IStatusService statusService)
        {
            _transformService = transformService ?? throw new ArgumentNullException(nameof(transformService));
            _statusService = statusService ?? throw new ArgumentNullException(nameof(statusService));

            _transformService.ReferenceChanged += OnReferenceChanged;

            ApplyCommand = new RelayCommand(ApplySettings);
            ResetCommand = new RelayCommand(ResetSettings);

            LoadFromService();
        }

        public ICommand ApplyCommand { get; }
        public ICommand ResetCommand { get; }

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); UpdateStatus(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string? CoordinateSystemName
        {
            get => _coordinateSystemName;
            set { _coordinateSystemName = value; OnPropertyChanged(); }
        }

        public string? EPSGCode
        {
            get => _epsgCode;
            set { _epsgCode = value; OnPropertyChanged(); }
        }

        public double LocalReferenceX
        {
            get => _localReferenceX;
            set { _localReferenceX = value; OnPropertyChanged(); UpdatePreviews(); }
        }

        public double LocalReferenceY
        {
            get => _localReferenceY;
            set { _localReferenceY = value; OnPropertyChanged(); UpdatePreviews(); }
        }

        public double LocalReferenceZ
        {
            get => _localReferenceZ;
            set { _localReferenceZ = value; OnPropertyChanged(); UpdatePreviews(); }
        }

        public double SurveyEasting
        {
            get => _surveyEasting;
            set { _surveyEasting = value; OnPropertyChanged(); UpdatePreviews(); }
        }

        public double SurveyNorthing
        {
            get => _surveyNorthing;
            set { _surveyNorthing = value; OnPropertyChanged(); UpdatePreviews(); }
        }

        public double SurveyElevation
        {
            get => _surveyElevation;
            set { _surveyElevation = value; OnPropertyChanged(); UpdatePreviews(); }
        }

        public double AppliedSurveyEasting
        {
            get => _appliedSurveyEasting;
            private set { _appliedSurveyEasting = value; OnPropertyChanged(); }
        }

        public double AppliedSurveyNorthing
        {
            get => _appliedSurveyNorthing;
            private set { _appliedSurveyNorthing = value; OnPropertyChanged(); }
        }

        public double AppliedSurveyElevation
        {
            get => _appliedSurveyElevation;
            private set { _appliedSurveyElevation = value; OnPropertyChanged(); }
        }

        public double RotationToNorthDegrees
        {
            get => _rotationToNorthDegrees;
            set { _rotationToNorthDegrees = value; OnPropertyChanged(); UpdatePreviews(); }
        }

        public double Scale
        {
            get => _scale;
            set { _scale = value; OnPropertyChanged(); }
        }

        public double? Latitude
        {
            get => _latitude;
            set { _latitude = value; OnPropertyChanged(); }
        }

        public double? Longitude
        {
            get => _longitude;
            set { _longitude = value; OnPropertyChanged(); }
        }

        public string? Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public double PreviewLocalX
        {
            get => _previewLocalX;
            set { _previewLocalX = value; OnPropertyChanged(); UpdatePreviews(); }
        }

        public double PreviewLocalY
        {
            get => _previewLocalY;
            set { _previewLocalY = value; OnPropertyChanged(); UpdatePreviews(); }
        }

        public double PreviewLocalZ
        {
            get => _previewLocalZ;
            set { _previewLocalZ = value; OnPropertyChanged(); UpdatePreviews(); }
        }

        public double PreviewSurveyEasting
        {
            get => _previewSurveyEasting;
            set { _previewSurveyEasting = value; OnPropertyChanged(); UpdatePreviews(); }
        }

        public double PreviewSurveyNorthing
        {
            get => _previewSurveyNorthing;
            set { _previewSurveyNorthing = value; OnPropertyChanged(); UpdatePreviews(); }
        }

        public double PreviewSurveyElevation
        {
            get => _previewSurveyElevation;
            set { _previewSurveyElevation = value; OnPropertyChanged(); UpdatePreviews(); }
        }

        public string PreviewSurveyResult
        {
            get => _previewSurveyResult;
            private set { _previewSurveyResult = value; OnPropertyChanged(); }
        }

        public string PreviewLocalResult
        {
            get => _previewLocalResult;
            private set { _previewLocalResult = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get => _statusText;
            private set { _statusText = value; OnPropertyChanged(); }
        }

        public bool IsSuccess
        {
            get => _isSuccess;
            private set { _isSuccess = value; OnPropertyChanged(); }
        }

        private void OnReferenceChanged(object? sender, EventArgs e)
        {
            LoadFromService();
        }

        private void LoadFromService()
        {
            var r = _transformService.CurrentReference;
            _isEnabled = r.IsEnabled;
            _name = r.Name ?? "Coordinate Reference";
            _coordinateSystemName = r.CoordinateSystemName;
            _epsgCode = r.EPSGCode;
            _localReferenceX = r.LocalReferenceX;
            _localReferenceY = r.LocalReferenceY;
            _localReferenceZ = r.LocalReferenceZ;
            _surveyEasting = r.SurveyEasting;
            _surveyNorthing = r.SurveyNorthing;
            _surveyElevation = r.SurveyElevation;
            _appliedSurveyEasting = r.SurveyEasting;
            _appliedSurveyNorthing = r.SurveyNorthing;
            _appliedSurveyElevation = r.SurveyElevation;
            _rotationToNorthDegrees = r.RotationToNorthDegrees;
            _scale = r.Scale;
            _latitude = r.Latitude;
            _longitude = r.Longitude;
            _description = r.Description;

            OnPropertyChanged(nameof(Latitude));
            OnPropertyChanged(nameof(Longitude));
            OnPropertyChanged(nameof(Description));

            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(CoordinateSystemName));
            OnPropertyChanged(nameof(EPSGCode));
            OnPropertyChanged(nameof(LocalReferenceX));
            OnPropertyChanged(nameof(LocalReferenceY));
            OnPropertyChanged(nameof(LocalReferenceZ));
            OnPropertyChanged(nameof(SurveyEasting));
            OnPropertyChanged(nameof(SurveyNorthing));
            OnPropertyChanged(nameof(SurveyElevation));
            OnPropertyChanged(nameof(AppliedSurveyEasting));
            OnPropertyChanged(nameof(AppliedSurveyNorthing));
            OnPropertyChanged(nameof(AppliedSurveyElevation));
            OnPropertyChanged(nameof(RotationToNorthDegrees));
            OnPropertyChanged(nameof(Scale));

            UpdateStatus();
            UpdatePreviews();
        }

        private void ApplySettings()
        {
            var reference = new ProjectCoordinateReference
            {
                IsEnabled = IsEnabled,
                Name = Name ?? "Coordinate Reference",
                CoordinateSystemName = CoordinateSystemName,
                EPSGCode = EPSGCode,
                LocalReferenceX = LocalReferenceX,
                LocalReferenceY = LocalReferenceY,
                LocalReferenceZ = LocalReferenceZ,
                SurveyEasting = SurveyEasting,
                SurveyNorthing = SurveyNorthing,
                SurveyElevation = SurveyElevation,
                RotationToNorthDegrees = RotationToNorthDegrees,
                Scale = Scale,
                Latitude = Latitude,
                Longitude = Longitude,
                Description = Description,
                UpdatedAt = DateTime.UtcNow
            };

            if (_transformService.ApplyReference(reference, out var error))
            {
                StatusText = "Applied successfully. Model coordinates transformed safely in display layer. Model was not moved.";
                IsSuccess = true;
                _statusService.Set("Coordinate Reference applied successfully.", "Display and reports will now show site coordinates.");
            }
            else
            {
                StatusText = $"Invalid input: {error}";
                IsSuccess = false;
            }
        }

        private void ResetSettings()
        {
            _transformService.ResetReference();
            StatusText = "Reference reset to disabled. Local coordinates used.";
            IsSuccess = true;
            _statusService.Set("Coordinate Reference reset to default (disabled).");
        }

        private void UpdateStatus()
        {
            if (!IsEnabled)
            {
                StatusText = "Reference disabled. Defaulting to local coordinates.";
                IsSuccess = true;
            }
            else if (_transformService.IsEnabled)
            {
                StatusText = "Applied successfully. Model coordinates transformed safely in display layer. Model was not moved.";
                IsSuccess = true;
            }
            else
            {
                StatusText = "Settings modified. Press Apply to save changes.";
                IsSuccess = true;
            }
        }

        private void UpdatePreviews()
        {
            var localPt = new DomainPoint3D(PreviewLocalX, PreviewLocalY, PreviewLocalZ);

            var tempRef = new ProjectCoordinateReference
            {
                IsEnabled = true,
                LocalReferenceX = LocalReferenceX,
                LocalReferenceY = LocalReferenceY,
                LocalReferenceZ = LocalReferenceZ,
                SurveyEasting = SurveyEasting,
                SurveyNorthing = SurveyNorthing,
                SurveyElevation = SurveyElevation,
                RotationToNorthDegrees = RotationToNorthDegrees,
                Scale = Scale
            };

            var tempService = new ProjectCoordinateTransformService();
            tempService.ApplyReference(tempRef, out _);

            var surveyResult = tempService.LocalToSurvey(localPt);
            PreviewSurveyResult = $"E: {surveyResult.X:F3}, N: {surveyResult.Y:F3}, H: {surveyResult.Z:F3}";

            var surveyPt = new DomainPoint3D(PreviewSurveyEasting, PreviewSurveyNorthing, PreviewSurveyElevation);
            var localResult = tempService.SurveyToLocal(surveyPt);
            PreviewLocalResult = $"X: {localResult.X:F3}, Y: {localResult.Y:F3}, Z: {localResult.Z:F3}";
        }

        public void Dispose()
        {
            _transformService.ReferenceChanged -= OnReferenceChanged;
        }
    }
}
