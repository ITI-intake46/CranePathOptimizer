using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels
{
    public sealed class CranePositionPresentationDto
    {
        private readonly CranePositionResult _underlying;
        private readonly IProjectCoordinateTransformService _transformService;

        public CranePositionPresentationDto(CranePositionResult underlying, IProjectCoordinateTransformService transformService)
        {
            _underlying = underlying ?? throw new ArgumentNullException(nameof(underlying));
            _transformService = transformService ?? throw new ArgumentNullException(nameof(transformService));
        }

        public CranePositionResult Underlying => _underlying;

        public double X => _underlying.X;
        public double Y => _underlying.Y;
        public double Z => _underlying.Z;
        public int NewLiftableCount => _underlying.NewLiftableCount;

        public string PositionDisplayText
        {
            get
            {
                var localStr = $"Local X: {X:F2}, Local Y: {Y:F2}";
                if (!CraneOptimizer.UI.Core.Configuration.AppFeatureFlags.ShowCoordinateReferenceUi || _transformService == null || !_transformService.IsEnabled)
                {
                    return localStr;
                }

                var sitePt = _transformService.LocalToSurvey(new DomainPoint3D(X, Y, Z));
                var siteStr = $"Easting: {sitePt.X:F2}, Northing: {sitePt.Y:F2}";
                return $"{localStr} | {siteStr}";
            }
        }
    }

    public sealed class GAResultsWindowViewModel : ViewModelBase
    {
        private readonly IRenderingService _renderingService;
        private readonly ISceneService _sceneService;
        private readonly Action<CranePositionResult?> _selectedPointChanged;
        private readonly Action<GAViewMode> _viewModeChanged;
        private readonly CranePlacementResult _result;
        private readonly IProjectCoordinateTransformService _transformService;
        private readonly List<CranePositionPresentationDto> _positions = new();
        private CranePositionPresentationDto? _selectedPoint;
        private GAViewMode _currentGAViewMode;
        private bool _isLoaded;
        private bool _isClosing;

        public GAResultsWindowViewModel(
            CranePlacementResult result,
            IRenderingService renderingService,
            ISceneService sceneService,
            Action<CranePositionResult?> selectedPointChanged,
            Action<GAViewMode> viewModeChanged,
            GAViewMode initialViewMode,
            IProjectCoordinateTransformService transformService)
        {
            _result = result;
            _renderingService = renderingService;
            _sceneService = sceneService;
            _selectedPointChanged = selectedPointChanged;
            _viewModeChanged = viewModeChanged;
            _currentGAViewMode = initialViewMode;
            _transformService = transformService;

            _transformService.ReferenceChanged += OnReferenceChanged;
            _sceneService.ElementsSelectionChanged += OnSceneSelectionChanged;

            if (_result?.Positions != null)
            {
                foreach (var pos in _result.Positions)
                {
                    _positions.Add(new CranePositionPresentationDto(pos, _transformService));
                }
            }
        }

        public GAViewMode CurrentGAViewMode
        {
            get => _currentGAViewMode;
            set
            {
                if (_currentGAViewMode == value)
                    return;

                _currentGAViewMode = value;
                OnPropertyChanged();
                _viewModeChanged?.Invoke(value);
            }
        }

        public IReadOnlyList<KeyValuePair<GAViewMode, string>> ViewModes { get; } = new List<KeyValuePair<GAViewMode, string>>
        {
            new KeyValuePair<GAViewMode, string>(GAViewMode.FinalAssignment, "Final Assignment"),
            new KeyValuePair<GAViewMode, string>(GAViewMode.SelectedPointFeasibility, "Selected Point Feasibility"),
            new KeyValuePair<GAViewMode, string>(GAViewMode.AllFeasibleDebug, "All Feasible Debug")
        };

        public IReadOnlyList<CranePositionPresentationDto> Positions => _positions;
        public double FitnessScore => _result.FitnessScore;
        public double CoverageRatio => _result.CoverageRatio;
        public string TotalLiftedText => $"Total Lifted: {_result.LiftedElementCount} / {_result.TotalElementCount}";

        public CranePositionPresentationDto? SelectedPoint
        {
            get => _selectedPoint;
            set
            {
                if (ReferenceEquals(_selectedPoint, value))
                    return;

                _selectedPoint = value;
                OnPropertyChanged();
                RaiseDetailsChanged();

                if (!_isLoaded || _isClosing)
                    return;

                RenderSelectedPoint();
            }
        }

        // Temporarily hidden by product decision. Keep logic intact for future re-enable.
        public bool IsSiteReferenceEnabled => CraneOptimizer.UI.Core.Configuration.AppFeatureFlags.ShowCoordinateReferenceUi && (_transformService?.IsEnabled ?? false);

        // Candidate Point
        public string CandidateModelX => SelectedPoint != null ? SelectedPoint.X.ToString("F3") : "N/A";
        public string CandidateModelY => SelectedPoint != null ? SelectedPoint.Y.ToString("F3") : "N/A";
        public string CandidateModelZ => SelectedPoint != null ? SelectedPoint.Z.ToString("F3") : "N/A";

        public string CandidateSiteE => (SelectedPoint != null && IsSiteReferenceEnabled)
            ? _transformService.LocalToSurvey(new DomainPoint3D(SelectedPoint.X, SelectedPoint.Y, SelectedPoint.Z)).X.ToString("F3")
            : "N/A";
        public string CandidateSiteN => (SelectedPoint != null && IsSiteReferenceEnabled)
            ? _transformService.LocalToSurvey(new DomainPoint3D(SelectedPoint.X, SelectedPoint.Y, SelectedPoint.Z)).Y.ToString("F3")
            : "N/A";
        public string CandidateSiteH => (SelectedPoint != null && IsSiteReferenceEnabled)
            ? _transformService.LocalToSurvey(new DomainPoint3D(SelectedPoint.X, SelectedPoint.Y, SelectedPoint.Z)).Z.ToString("F3")
            : "N/A";

        private ElementLiftResult? GetActiveLiftResult()
        {
            if (SelectedPoint == null) return null;
            var selectedElements = _sceneService.GetSelectedElements();
            if (selectedElements != null && selectedElements.Any())
            {
                var el = selectedElements.First();
                var match = SelectedPoint.Underlying.ElementResults.FirstOrDefault(r => string.Equals(r.ElementId, el.ElementID, StringComparison.Ordinal));
                if (match != null) return match;
            }
            return SelectedPoint.Underlying.ElementResults.FirstOrDefault();
        }

        public string ActiveElementName => GetActiveLiftResult()?.ElementMark ?? "No Element Selected / First Element";

        // Crane Base Point
        public string CraneBaseModelX => GetActiveLiftResult() != null ? GetActiveLiftResult().CraneBaseX.ToString("F3") : "N/A";
        public string CraneBaseModelY => GetActiveLiftResult() != null ? GetActiveLiftResult().CraneBaseY.ToString("F3") : "N/A";
        public string CraneBaseModelZ => GetActiveLiftResult() != null ? GetActiveLiftResult().CraneBaseZ.ToString("F3") : "N/A";

        public string CraneBaseSiteE => (GetActiveLiftResult() != null && IsSiteReferenceEnabled)
            ? _transformService.LocalToSurvey(new DomainPoint3D(GetActiveLiftResult().CraneBaseX, GetActiveLiftResult().CraneBaseY, GetActiveLiftResult().CraneBaseZ)).X.ToString("F3")
            : "N/A";
        public string CraneBaseSiteN => (GetActiveLiftResult() != null && IsSiteReferenceEnabled)
            ? _transformService.LocalToSurvey(new DomainPoint3D(GetActiveLiftResult().CraneBaseX, GetActiveLiftResult().CraneBaseY, GetActiveLiftResult().CraneBaseZ)).Y.ToString("F3")
            : "N/A";
        public string CraneBaseSiteH => (GetActiveLiftResult() != null && IsSiteReferenceEnabled)
            ? _transformService.LocalToSurvey(new DomainPoint3D(GetActiveLiftResult().CraneBaseX, GetActiveLiftResult().CraneBaseY, GetActiveLiftResult().CraneBaseZ)).Z.ToString("F3")
            : "N/A";

        // Slew Center Point
        public string SlewCenterModelX => GetActiveLiftResult() != null ? GetActiveLiftResult().SlewCenterX.ToString("F3") : "N/A";
        public string SlewCenterModelY => GetActiveLiftResult() != null ? GetActiveLiftResult().SlewCenterY.ToString("F3") : "N/A";
        public string SlewCenterModelZ => GetActiveLiftResult() != null ? GetActiveLiftResult().SlewCenterZ.ToString("F3") : "N/A";

        public string SlewCenterSiteE => (GetActiveLiftResult() != null && IsSiteReferenceEnabled)
            ? _transformService.LocalToSurvey(new DomainPoint3D(GetActiveLiftResult().SlewCenterX, GetActiveLiftResult().SlewCenterY, GetActiveLiftResult().SlewCenterZ)).X.ToString("F3")
            : "N/A";
        public string SlewCenterSiteN => (GetActiveLiftResult() != null && IsSiteReferenceEnabled)
            ? _transformService.LocalToSurvey(new DomainPoint3D(GetActiveLiftResult().SlewCenterX, GetActiveLiftResult().SlewCenterY, GetActiveLiftResult().SlewCenterZ)).Y.ToString("F3")
            : "N/A";
        public string SlewCenterSiteH => (GetActiveLiftResult() != null && IsSiteReferenceEnabled)
            ? _transformService.LocalToSurvey(new DomainPoint3D(GetActiveLiftResult().SlewCenterX, GetActiveLiftResult().SlewCenterY, GetActiveLiftResult().SlewCenterZ)).Z.ToString("F3")
            : "N/A";

        // Boom Hinge Point
        public string BoomHingeModelX => GetActiveLiftResult() != null ? GetActiveLiftResult().BoomHingeX.ToString("F3") : "N/A";
        public string BoomHingeModelY => GetActiveLiftResult() != null ? GetActiveLiftResult().BoomHingeY.ToString("F3") : "N/A";
        public string BoomHingeModelZ => GetActiveLiftResult() != null ? GetActiveLiftResult().BoomHingeZ.ToString("F3") : "N/A";

        public string BoomHingeSiteE => (GetActiveLiftResult() != null && IsSiteReferenceEnabled)
            ? _transformService.LocalToSurvey(new DomainPoint3D(GetActiveLiftResult().BoomHingeX, GetActiveLiftResult().BoomHingeY, GetActiveLiftResult().BoomHingeZ)).X.ToString("F3")
            : "N/A";
        public string BoomHingeSiteN => (GetActiveLiftResult() != null && IsSiteReferenceEnabled)
            ? _transformService.LocalToSurvey(new DomainPoint3D(GetActiveLiftResult().BoomHingeX, GetActiveLiftResult().BoomHingeY, GetActiveLiftResult().BoomHingeZ)).Y.ToString("F3")
            : "N/A";
        public string BoomHingeSiteH => (GetActiveLiftResult() != null && IsSiteReferenceEnabled)
            ? _transformService.LocalToSurvey(new DomainPoint3D(GetActiveLiftResult().BoomHingeX, GetActiveLiftResult().BoomHingeY, GetActiveLiftResult().BoomHingeZ)).Z.ToString("F3")
            : "N/A";

        // Load Point
        public string LoadPointModelX => GetActiveLiftResult() != null ? GetActiveLiftResult().LoadPointX.ToString("F3") : "N/A";
        public string LoadPointModelY => GetActiveLiftResult() != null ? GetActiveLiftResult().LoadPointY.ToString("F3") : "N/A";
        public string LoadPointModelZ => GetActiveLiftResult() != null ? GetActiveLiftResult().LoadPointZ.ToString("F3") : "N/A";

        public string LoadPointSiteE => (GetActiveLiftResult() != null && IsSiteReferenceEnabled)
            ? _transformService.LocalToSurvey(new DomainPoint3D(GetActiveLiftResult().LoadPointX, GetActiveLiftResult().LoadPointY, GetActiveLiftResult().LoadPointZ)).X.ToString("F3")
            : "N/A";
        public string LoadPointSiteN => (GetActiveLiftResult() != null && IsSiteReferenceEnabled)
            ? _transformService.LocalToSurvey(new DomainPoint3D(GetActiveLiftResult().LoadPointX, GetActiveLiftResult().LoadPointY, GetActiveLiftResult().LoadPointZ)).Y.ToString("F3")
            : "N/A";
        public string LoadPointSiteH => (GetActiveLiftResult() != null && IsSiteReferenceEnabled)
            ? _transformService.LocalToSurvey(new DomainPoint3D(GetActiveLiftResult().LoadPointX, GetActiveLiftResult().LoadPointY, GetActiveLiftResult().LoadPointZ)).Z.ToString("F3")
            : "N/A";

        // Scalars (not converted)
        public string RadiusVal => GetActiveLiftResult() != null ? GetActiveLiftResult().Radius.ToString("F2") + " m" : "N/A";
        public string BoomLengthVal => GetActiveLiftResult() != null ? GetActiveLiftResult().BoomLength.ToString("F2") + " m" : "N/A";
        public string AvailableCapacityVal => GetActiveLiftResult() != null ? GetActiveLiftResult().AvailableCapacity.ToString("F2") + " t" : "N/A";
        public string RequiredCapacityVal => GetActiveLiftResult() != null ? GetActiveLiftResult().RequiredCapacity.ToString("F2") + " t" : "N/A";
        public string SafetyMarginVal => GetActiveLiftResult() != null ? GetActiveLiftResult().SafetyMargin.ToString("F2") + " t" : "N/A";

        public void RaiseDetailsChanged()
        {
            OnPropertyChanged(nameof(IsSiteReferenceEnabled));
            OnPropertyChanged(nameof(CandidateModelX));
            OnPropertyChanged(nameof(CandidateModelY));
            OnPropertyChanged(nameof(CandidateModelZ));
            OnPropertyChanged(nameof(CandidateSiteE));
            OnPropertyChanged(nameof(CandidateSiteN));
            OnPropertyChanged(nameof(CandidateSiteH));

            OnPropertyChanged(nameof(ActiveElementName));
            OnPropertyChanged(nameof(CraneBaseModelX));
            OnPropertyChanged(nameof(CraneBaseModelY));
            OnPropertyChanged(nameof(CraneBaseModelZ));
            OnPropertyChanged(nameof(CraneBaseSiteE));
            OnPropertyChanged(nameof(CraneBaseSiteN));
            OnPropertyChanged(nameof(CraneBaseSiteH));

            OnPropertyChanged(nameof(SlewCenterModelX));
            OnPropertyChanged(nameof(SlewCenterModelY));
            OnPropertyChanged(nameof(SlewCenterModelZ));
            OnPropertyChanged(nameof(SlewCenterSiteE));
            OnPropertyChanged(nameof(SlewCenterSiteN));
            OnPropertyChanged(nameof(SlewCenterSiteH));

            OnPropertyChanged(nameof(BoomHingeModelX));
            OnPropertyChanged(nameof(BoomHingeModelY));
            OnPropertyChanged(nameof(BoomHingeModelZ));
            OnPropertyChanged(nameof(BoomHingeSiteE));
            OnPropertyChanged(nameof(BoomHingeSiteN));
            OnPropertyChanged(nameof(BoomHingeSiteH));

            OnPropertyChanged(nameof(LoadPointModelX));
            OnPropertyChanged(nameof(LoadPointModelY));
            OnPropertyChanged(nameof(LoadPointModelZ));
            OnPropertyChanged(nameof(LoadPointSiteE));
            OnPropertyChanged(nameof(LoadPointSiteN));
            OnPropertyChanged(nameof(LoadPointSiteH));

            OnPropertyChanged(nameof(RadiusVal));
            OnPropertyChanged(nameof(BoomLengthVal));
            OnPropertyChanged(nameof(AvailableCapacityVal));
            OnPropertyChanged(nameof(RequiredCapacityVal));
            OnPropertyChanged(nameof(SafetyMarginVal));
        }

        private void OnReferenceChanged(object? sender, EventArgs e)
        {
            _positions.Clear();
            if (_result?.Positions != null)
            {
                foreach (var pos in _result.Positions)
                {
                    _positions.Add(new CranePositionPresentationDto(pos, _transformService));
                }
            }
            OnPropertyChanged(nameof(Positions));
            RaiseDetailsChanged();
        }

        private void OnSceneSelectionChanged(List<PrecastElement> selectedElements)
        {
            RaiseDetailsChanged();
        }

        public void OnLoaded()
        {
            if (_isLoaded)
                return;

            _isLoaded = true;

            if (SelectedPoint == null && _positions.Any())
                SelectedPoint = _positions[0];
        }

        public void OnClosing()
        {
            _isClosing = true;
            _transformService.ReferenceChanged -= OnReferenceChanged;
            _sceneService.ElementsSelectionChanged -= OnSceneSelectionChanged;
            SelectedPointRenderer.Clear(_renderingService);
        }

        private void RenderSelectedPoint()
        {
            SelectedPointRenderer.Clear(_renderingService);
            _selectedPointChanged(SelectedPoint?.Underlying);

            if (SelectedPoint != null)
                SelectedPointRenderer.RenderPlacementPoint(
                    _renderingService,
                    new DomainPoint3D(SelectedPoint.X, SelectedPoint.Y, SelectedPoint.Z));

            // Move the existing crane to the selected GA point
            if (SelectedPoint != null)
            {
                var sceneCrane = _sceneService.GetCranes().FirstOrDefault();
                if (sceneCrane != null)
                {
                    sceneCrane.Position = new DomainPoint3D(SelectedPoint.X, SelectedPoint.Y, SelectedPoint.Z);
                    _sceneService.UpdateCrane(sceneCrane);
                }
            }
        }
    }
}
