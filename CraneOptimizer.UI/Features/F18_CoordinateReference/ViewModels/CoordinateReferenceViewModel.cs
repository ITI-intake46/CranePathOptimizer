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
        private readonly IIfcReaderRepository _ifcReader;

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
        private string _source = "ManualSinglePoint";
        private CoordinateReferenceMode _selectedMode;

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

        public CoordinateReferenceViewModel(IProjectCoordinateTransformService transformService, IStatusService statusService, IIfcReaderRepository ifcReader)
        {
            _transformService = transformService ?? throw new ArgumentNullException(nameof(transformService));
            _statusService = statusService ?? throw new ArgumentNullException(nameof(statusService));
            _ifcReader = ifcReader ?? throw new ArgumentNullException(nameof(ifcReader));

            _transformService.ReferenceChanged += OnReferenceChanged;

            ApplyCommand = new RelayCommand(ApplySettings);
            ResetCommand = new RelayCommand(ResetSettings);

            LoadFromService();
        }

        public ICommand ApplyCommand { get; }
        public ICommand ResetCommand { get; }

        public CoordinateReferenceMode SelectedMode
        {
            get => _selectedMode;
            set
            {
                if (_selectedMode != value)
                {
                    _selectedMode = value;
                    OnPropertyChanged();
                    OnModeChanged();
                    OnPropertyChanged(nameof(AvailableModes));
                    OnPropertyChanged(nameof(IsComboBoxVisible));
                    OnPropertyChanged(nameof(IsManualMode));
                }
            }
        }

        public sealed class ModeItem
        {
            public CoordinateReferenceMode Value { get; set; }
            public string DisplayName { get; set; }
        }

        public List<ModeItem> AvailableModes
        {
            get
            {
                return new List<ModeItem>
                {
                    new ModeItem { Value = CoordinateReferenceMode.LocalOnly, DisplayName = "Local Only" },
                    new ModeItem { Value = CoordinateReferenceMode.IfcOrigin, DisplayName = "IFC Origin Mapping" }
                };
            }
        }

        public bool IsComboBoxVisible => !IsManualMode;

        public System.Windows.Visibility IfcGeoreferencingInactiveMessageVisibility =>
            (!IsEnabled && Source == "IfcMapConversion") ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public string Source
        {
            get => _source;
            set
            {
                _source = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SourceDescription));
                OnPropertyChanged(nameof(IfcGeoreferencingInactiveMessageVisibility));
            }
        }

        public string SourceDescription
        {
            get
            {
                if (!IsEnabled || SelectedMode == CoordinateReferenceMode.LocalOnly)
                {
                    if (Source == "IfcMapConversion")
                    {
                        return "Local Only (IFC Georeferencing Inactive)";
                    }
                    return "Local Only";
                }
                if (Source == "IfcMapConversion")
                {
                    return "IFC Origin Mapping";
                }
                if (Source == "IfcSiteLatLon")
                {
                    return "IfcSiteLatLon (Informational Only)";
                }
                if (Source == "Manual" || Source == "ManualSinglePoint")
                {
                    return "Manual / ManualSinglePoint";
                }
                return Source ?? "Local Only";
            }
        }

        public bool IsIfcOriginMappingEnabled
        {
            get
            {
                var current = _transformService.CurrentReference;
                if (current != null && current.Source == "IfcMapConversion")
                {
                    return true;
                }

                try
                {
                    var result = _ifcReader.GetGeoreferencingData();
                    if (result != null && result.HasMapConversion)
                    {
                        return true;
                    }
                }
                catch
                {
                    // Ignore
                }

                return false;
            }
        }

        public System.Windows.Visibility IfcGeoreferencingNotFoundMessageVisibility =>
            IsIfcOriginMappingEnabled ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

        public bool IsManualMode => SelectedMode == CoordinateReferenceMode.Manual;

        private bool CheckIfcGeoreferencingExists(out ProjectCoordinateReference? ifcRef)
        {
            ifcRef = null;

            var current = _transformService.CurrentReference;
            if (current != null && current.Source == "IfcMapConversion")
            {
                ifcRef = current;
                return true;
            }

            try
            {
                var result = _ifcReader.GetGeoreferencingData();
                if (result != null && result.HasMapConversion)
                {
                    double rotationToNorthDegrees = 0.0;
                    if (result.XAxisAbscissa.HasValue && result.XAxisOrdinate.HasValue)
                    {
                        double abscissa = result.XAxisAbscissa.Value;
                        double ordinate = result.XAxisOrdinate.Value;
                        if (abscissa != 0.0 || ordinate != 0.0)
                        {
                            double angleRad = Math.Atan2(ordinate, abscissa);
                            rotationToNorthDegrees = angleRad * 180.0 / Math.PI;
                        }
                    }

                    ifcRef = new ProjectCoordinateReference
                    {
                        IsEnabled = false,
                        Name = string.IsNullOrWhiteSpace(result.CrsName) ? "IFC Georeferenced" : result.CrsName,
                        CoordinateSystemName = string.IsNullOrWhiteSpace(result.CrsName) ? "Projected CRS" : result.CrsName,
                        EPSGCode = result.EpsgCode,
                        LocalReferenceX = 0.0,
                        LocalReferenceY = 0.0,
                        LocalReferenceZ = 0.0,
                        SurveyEasting = result.Eastings,
                        SurveyNorthing = result.Northings,
                        SurveyElevation = result.OrthogonalHeight,
                        RotationToNorthDegrees = rotationToNorthDegrees,
                        Scale = result.Scale ?? 1.0,
                        Source = "IfcMapConversion",
                        Latitude = result.SiteLatitude,
                        Longitude = result.SiteLongitude,
                        Description = string.IsNullOrWhiteSpace(result.CrsDescription) ? null : result.CrsDescription
                    };
                    return true;
                }
            }
            catch
            {
                // Ignore
            }

            return false;
        }

        private void OnModeChanged()
        {
            if (SelectedMode == CoordinateReferenceMode.LocalOnly)
            {
                IsEnabled = false;
                UpdateStatus();
                OnPropertyChanged(nameof(SourceDescription));
                OnPropertyChanged(nameof(IsManualMode));
            }
            else if (SelectedMode == CoordinateReferenceMode.IfcOrigin)
            {
                if (CheckIfcGeoreferencingExists(out var ifcRef) && ifcRef != null)
                {
                    IsEnabled = true;
                    LocalReferenceX = 0.0;
                    LocalReferenceY = 0.0;
                    LocalReferenceZ = 0.0;
                    SurveyEasting = ifcRef.SurveyEasting;
                    SurveyNorthing = ifcRef.SurveyNorthing;
                    SurveyElevation = ifcRef.SurveyElevation;
                    RotationToNorthDegrees = ifcRef.RotationToNorthDegrees;
                    Scale = ifcRef.Scale;
                    CoordinateSystemName = ifcRef.CoordinateSystemName;
                    EPSGCode = ifcRef.EPSGCode;
                    Latitude = ifcRef.Latitude;
                    Longitude = ifcRef.Longitude;
                    Description = ifcRef.Description;
                    Source = "IfcMapConversion";
                }
                else
                {
                    SelectedMode = CoordinateReferenceMode.LocalOnly;
                }
                UpdateStatus();
                OnPropertyChanged(nameof(SourceDescription));
                OnPropertyChanged(nameof(IsManualMode));
            }
            else if (SelectedMode == CoordinateReferenceMode.Manual)
            {
                IsEnabled = true;
                if (Source != "Manual" && Source != "ManualSinglePoint")
                {
                    Source = "ManualSinglePoint";
                }
                UpdateStatus();
                OnPropertyChanged(nameof(SourceDescription));
                OnPropertyChanged(nameof(IsManualMode));
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set 
            { 
                _isEnabled = value; 
                OnPropertyChanged(); 
                UpdateStatus(); 
                OnPropertyChanged(nameof(IfcGeoreferencingInactiveMessageVisibility));
                OnPropertyChanged(nameof(SourceDescription));
            }
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
            _source = r.Source ?? "ManualSinglePoint";

            if (!_isEnabled)
            {
                _selectedMode = CoordinateReferenceMode.LocalOnly;
            }
            else if (_source == "IfcMapConversion")
            {
                _selectedMode = CoordinateReferenceMode.IfcOrigin;
            }
            else
            {
                _selectedMode = CoordinateReferenceMode.Manual;
            }

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
            OnPropertyChanged(nameof(SelectedMode));
            OnPropertyChanged(nameof(AvailableModes));
            OnPropertyChanged(nameof(IsComboBoxVisible));
            OnPropertyChanged(nameof(IsIfcOriginMappingEnabled));
            OnPropertyChanged(nameof(IfcGeoreferencingNotFoundMessageVisibility));
            OnPropertyChanged(nameof(IfcGeoreferencingInactiveMessageVisibility));
            OnPropertyChanged(nameof(SourceDescription));
            OnPropertyChanged(nameof(IsManualMode));

            UpdateStatus();
            UpdatePreviews();
        }

        private void ApplySettings()
        {
            string finalSource = "ManualSinglePoint";
            if (SelectedMode == CoordinateReferenceMode.LocalOnly)
            {
                finalSource = "LocalOnly";
            }
            else if (SelectedMode == CoordinateReferenceMode.IfcOrigin)
            {
                finalSource = "IfcMapConversion";
            }
            else if (SelectedMode == CoordinateReferenceMode.Manual)
            {
                finalSource = Source ?? "ManualSinglePoint";
            }

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
                Source = finalSource,
                UpdatedAt = DateTime.UtcNow
            };

            if (_transformService.ApplyReference(reference, out var error))
            {
                Source = finalSource;
                StatusText = "Applied successfully. Model coordinates transformed safely in display layer. Model was not moved.";
                IsSuccess = true;
                _statusService.Set("Coordinate Reference applied successfully.", "Display and reports will now show site coordinates.");
                OnPropertyChanged(nameof(SourceDescription));
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
            _selectedMode = CoordinateReferenceMode.LocalOnly;
            Source = "LocalOnly";
            OnPropertyChanged(nameof(SelectedMode));
            OnPropertyChanged(nameof(SourceDescription));
            StatusText = "Reference reset to disabled. Local coordinates used.";
            IsSuccess = true;
            _statusService.Set("Coordinate Reference reset to default (disabled).");
        }

        private void UpdateStatus()
        {
            if (SelectedMode == CoordinateReferenceMode.LocalOnly || !IsEnabled)
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
