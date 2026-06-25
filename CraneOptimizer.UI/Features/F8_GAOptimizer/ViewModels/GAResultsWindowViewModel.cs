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
using System.Collections.ObjectModel;
using System.Linq;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels
{
    public sealed class CranePositionPresentationDto : ViewModelBase
    {
        private readonly CranePositionResult _underlying;
        private readonly IProjectCoordinateTransformService _transformService;
        private readonly Func<GAViewMode> _getViewMode;

        public CranePositionPresentationDto(
            CranePositionResult underlying,
            IProjectCoordinateTransformService transformService,
            Func<GAViewMode>? getViewMode = null)
        {
            _underlying = underlying ?? throw new ArgumentNullException(nameof(underlying));
            _transformService = transformService ?? throw new ArgumentNullException(nameof(transformService));
            _getViewMode = getViewMode ?? (() => GAViewMode.FinalAssignment);
        }

        public CranePositionResult Underlying => _underlying;

        public double X => _underlying.X;
        public double Y => _underlying.Y;
        public double Z => _underlying.Z;
        public int NewLiftableCount => _underlying.NewLiftableCount;

        public string LiftCountDisplayText
        {
            get
            {
                var mode = _getViewMode();
                if (mode == GAViewMode.FinalAssignment)
                {
                    int assignedCount = _underlying.ElementResults.Count(r => r.IsFinalAssignedToPoint);
                    return $"{assignedCount} lifted";
                }
                else
                {
                    int feasibleCount = _underlying.ElementResults.Count(r => r.Status == LiftStatus.Liftable);
                    return $"{feasibleCount} feasible";
                }
            }
        }

        public void RaiseLiftCountChanged()
        {
            OnPropertyChanged(nameof(LiftCountDisplayText));
        }

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

    /// <summary>
    /// Lightweight display row DTO for the assigned-elements DataGrid.
    /// All heavy data stays in the underlying ElementLiftResult;
    /// this only exposes formatted strings and the ElementId for viewport sync.
    /// </summary>
    public sealed class ElementLiftResultRowDto
    {
        private readonly ElementLiftResult _source;

        public ElementLiftResultRowDto(ElementLiftResult source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public ElementLiftResult Source => _source;

        /// <summary>
        /// Best resolved display mark for this element.
        /// Phase 5: prefers canonical AssemblyMark via CanonicalMark; falls back to raw ElementMark.
        /// </summary>
        public string Mark =>
            !string.IsNullOrWhiteSpace(_source.CanonicalMark) ? _source.CanonicalMark :
            !string.IsNullOrWhiteSpace(_source.ElementMark) ? _source.ElementMark : "-";

        /// <summary>Horizontal radius in metres.</summary>
        public string RadiusText => _source.Radius.ToString("F2") + " m";

        /// <summary>Required boom length in metres.</summary>
        public string BoomLengthText => _source.BoomLength.ToString("F2") + " m";

        /// <summary>Element self-weight (element + rigging) in tonnes.</summary>
        public string WeightText
        {
            get
            {
                var wt = _source.Weight ?? _source.RequiredCapacity;
                return wt > 0 ? wt.ToString("F3") + " t" : "-";
            }
        }

        /// <summary>
        /// Raw manufacturer load chart capacity (tonnes), before derating and safety factors.
        /// Shown as "-" when no chart cell was resolved.
        /// </summary>
        public string RawChartCapacityText
        {
            get
            {
                if (_source.RawChartCapacity.HasValue && _source.RawChartCapacity.Value > 0)
                    return _source.RawChartCapacity.Value.ToString("F3") + " t";
                return "-";
            }
        }

        /// <summary>Lift status as a display string.</summary>
        public string StatusText => _source.Status switch
        {
            LiftStatus.Liftable      => "Liftable",
            LiftStatus.Overweight    => "Overweight",
            LiftStatus.OutOfRadius   => "Out of Radius",
            LiftStatus.HeightFail    => "Height Fail",
            LiftStatus.SpecialCase   => "Special Case",
            LiftStatus.NotCharted    => "Not Charted",
            _                        => _source.Status.ToString()
        };

        /// <summary>True when lift is successful, drives row colour in XAML.</summary>
        public bool IsLiftable => _source.Status == LiftStatus.Liftable;

        public string ElementId => _source.ElementId ?? string.Empty;
    }

    public sealed class GAResultsWindowViewModel : ViewModelBase
    {
        private readonly IRenderingService _renderingService;
        private readonly ISceneService _sceneService;
        private readonly Action<CranePositionResult?> _selectedPointChanged;
        private readonly Action<GAViewMode> _viewModeChanged;
        private readonly CranePlacementResult _result = null!;
        private readonly IProjectCoordinateTransformService _transformService;
        private readonly List<CranePositionPresentationDto> _positions = new();
        private CranePositionPresentationDto? _selectedPoint;
        private ElementLiftResultRowDto? _selectedElement;
        private GAViewMode _currentGAViewMode;
        private bool _isLoaded;
        private bool _isClosing;
        private bool _isSelectingElement;

        public GAResultsWindowViewModel(
            CranePlacementResult result,
            IRenderingService renderingService,
            ISceneService sceneService,
            Action<CranePositionResult?> selectedPointChanged,
            Action<GAViewMode> viewModeChanged,
            GAViewMode initialViewMode,
            IProjectCoordinateTransformService transformService)
        {
            _result = result ?? throw new ArgumentNullException(nameof(result));
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
                    _positions.Add(new CranePositionPresentationDto(pos, _transformService, () => CurrentGAViewMode));
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
                OnPropertyChanged(nameof(RightPanelHeader));
                _viewModeChanged?.Invoke(value);

                // Notify all positions to update their count display text
                foreach (var pos in _positions)
                {
                    pos.RaiseLiftCountChanged();
                }

                RebuildAssignedElements();
            }
        }

        public IReadOnlyList<KeyValuePair<GAViewMode, string>> ViewModes { get; } = new List<KeyValuePair<GAViewMode, string>>
        {
            new KeyValuePair<GAViewMode, string>(GAViewMode.FinalAssignment, "Final Assignment"),
            new KeyValuePair<GAViewMode, string>(GAViewMode.SelectedPointFeasibility, "Selected Point Feasibility")
        };

        public string RightPanelHeader => CurrentGAViewMode == GAViewMode.FinalAssignment ? "ASSIGNED ELEMENTS" : "FEASIBLE ELEMENTS";

        public IReadOnlyList<CranePositionPresentationDto> Positions => _positions;
        public double FitnessScore => _result.FitnessScore;
        public double CoverageRatio => _result.CoverageRatio;
        public string TotalLiftedText => $"Total Lifted: {_result.LiftedElementCount} / {_result.TotalElementCount}";

        // ── Crane run metadata (static for the lifetime of this window) ──────
        public string CraneNameDisplay => !string.IsNullOrWhiteSpace(_result.CraneName) ? _result.CraneName : "-";
        public string WireLengthDisplay => _result.WireLength.ToString("F2") + " m";
        public string DeratingRatioDisplay => _result.DeratingRatio.ToString("P0");
        public string CounterWeightDisplay => _result.CounterWeight > 0 ? _result.CounterWeight.ToString("F1") + " t" : "-";

        // ── Assigned elements collection (DataGrid items source) ─────────────
        public ObservableCollection<ElementLiftResultRowDto> AssignedElements { get; } = new();

        /// <summary>
        /// Currently selected element in the DataGrid.
        /// Setting this also drives viewport selection via ISceneService.
        /// </summary>
        public ElementLiftResultRowDto? SelectedElement
        {
            get => _selectedElement;
            set
            {
                if (ReferenceEquals(_selectedElement, value))
                    return;

                _selectedElement = value;
                OnPropertyChanged();

                // Guard: don't re-enter when scene selection drives this property
                if (!_isSelectingElement && value != null && !string.IsNullOrEmpty(value.ElementId))
                {
                    _isSelectingElement = true;
                    try
                    {
                        var elements = _sceneService.GetElements();
                        var match = elements?.FirstOrDefault(e => string.Equals(e.ElementID, value.ElementId, StringComparison.Ordinal));
                        if (match != null)
                            _sceneService.SetSelectedElements(new List<PrecastElement> { match });
                    }
                    finally
                    {
                        _isSelectingElement = false;
                    }
                }
            }
        }

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
                RebuildAssignedElements();

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
        public string CraneBaseModelX => GetActiveLiftResult() is { } active ? active.CraneBaseX.ToString("F3") : "N/A";
        public string CraneBaseModelY => GetActiveLiftResult() is { } active ? active.CraneBaseY.ToString("F3") : "N/A";
        public string CraneBaseModelZ => GetActiveLiftResult() is { } active ? active.CraneBaseZ.ToString("F3") : "N/A";

        public string CraneBaseSiteE => (GetActiveLiftResult() is { } active && IsSiteReferenceEnabled)
            ? _transformService.LocalToSurvey(new DomainPoint3D(active.CraneBaseX, active.CraneBaseY, active.CraneBaseZ)).X.ToString("F3")
            : "N/A";
        public string CraneBaseSiteN => (GetActiveLiftResult() is { } active && IsSiteReferenceEnabled)
            ? _transformService.LocalToSurvey(new DomainPoint3D(active.CraneBaseX, active.CraneBaseY, active.CraneBaseZ)).Y.ToString("F3")
            : "N/A";
        public string CraneBaseSiteH => (GetActiveLiftResult() is { } active && IsSiteReferenceEnabled)
            ? _transformService.LocalToSurvey(new DomainPoint3D(active.CraneBaseX, active.CraneBaseY, active.CraneBaseZ)).Z.ToString("F3")
            : "N/A";

        // Slew Center Point
        public string SlewCenterModelX => GetActiveLiftResult() is { } active ? active.SlewCenterX.ToString("F3") : "N/A";
        public string SlewCenterModelY => GetActiveLiftResult() is { } active ? active.SlewCenterY.ToString("F3") : "N/A";
        public string SlewCenterModelZ => GetActiveLiftResult() is { } active ? active.SlewCenterZ.ToString("F3") : "N/A";

        public string SlewCenterSiteE => (GetActiveLiftResult() is { } active && IsSiteReferenceEnabled)
            ? _transformService.LocalToSurvey(new DomainPoint3D(active.SlewCenterX, active.SlewCenterY, active.SlewCenterZ)).X.ToString("F3")
            : "N/A";
        public string SlewCenterSiteN => (GetActiveLiftResult() is { } active && IsSiteReferenceEnabled)
            ? _transformService.LocalToSurvey(new DomainPoint3D(active.SlewCenterX, active.SlewCenterY, active.SlewCenterZ)).Y.ToString("F3")
            : "N/A";
        public string SlewCenterSiteH => (GetActiveLiftResult() is { } active && IsSiteReferenceEnabled)
            ? _transformService.LocalToSurvey(new DomainPoint3D(active.SlewCenterX, active.SlewCenterY, active.SlewCenterZ)).Z.ToString("F3")
            : "N/A";

        // Boom Hinge Point
        public string BoomHingeModelX => GetActiveLiftResult() is { } active ? active.BoomHingeX.ToString("F3") : "N/A";
        public string BoomHingeModelY => GetActiveLiftResult() is { } active ? active.BoomHingeY.ToString("F3") : "N/A";
        public string BoomHingeModelZ => GetActiveLiftResult() is { } active ? active.BoomHingeZ.ToString("F3") : "N/A";

        public string BoomHingeSiteE => (GetActiveLiftResult() is { } active && IsSiteReferenceEnabled)
            ? _transformService.LocalToSurvey(new DomainPoint3D(active.BoomHingeX, active.BoomHingeY, active.BoomHingeZ)).X.ToString("F3")
            : "N/A";
        public string BoomHingeSiteN => (GetActiveLiftResult() is { } active && IsSiteReferenceEnabled)
            ? _transformService.LocalToSurvey(new DomainPoint3D(active.BoomHingeX, active.BoomHingeY, active.BoomHingeZ)).Y.ToString("F3")
            : "N/A";
        public string BoomHingeSiteH => (GetActiveLiftResult() is { } active && IsSiteReferenceEnabled)
            ? _transformService.LocalToSurvey(new DomainPoint3D(active.BoomHingeX, active.BoomHingeY, active.BoomHingeZ)).Z.ToString("F3")
            : "N/A";

        // Load Point
        public string LoadPointModelX => GetActiveLiftResult() is { } active ? active.LoadPointX.ToString("F3") : "N/A";
        public string LoadPointModelY => GetActiveLiftResult() is { } active ? active.LoadPointY.ToString("F3") : "N/A";
        public string LoadPointModelZ => GetActiveLiftResult() is { } active ? active.LoadPointZ.ToString("F3") : "N/A";

        public string LoadPointSiteE => (GetActiveLiftResult() is { } active && IsSiteReferenceEnabled)
            ? _transformService.LocalToSurvey(new DomainPoint3D(active.LoadPointX, active.LoadPointY, active.LoadPointZ)).X.ToString("F3")
            : "N/A";
        public string LoadPointSiteN => (GetActiveLiftResult() is { } active && IsSiteReferenceEnabled)
            ? _transformService.LocalToSurvey(new DomainPoint3D(active.LoadPointX, active.LoadPointY, active.LoadPointZ)).Y.ToString("F3")
            : "N/A";
        public string LoadPointSiteH => (GetActiveLiftResult() is { } active && IsSiteReferenceEnabled)
            ? _transformService.LocalToSurvey(new DomainPoint3D(active.LoadPointX, active.LoadPointY, active.LoadPointZ)).Z.ToString("F3")
            : "N/A";

        // Scalars (not converted)
        public string RadiusVal => GetActiveLiftResult() is { } active ? active.Radius.ToString("F2") + " m" : "N/A";
        public string BoomLengthVal => GetActiveLiftResult() is { } active ? active.BoomLength.ToString("F2") + " m" : "N/A";
        public string AvailableCapacityVal => GetActiveLiftResult() is { } active ? active.AvailableCapacity.ToString("F2") + " t" : "N/A";
        public string RequiredCapacityVal => GetActiveLiftResult() is { } active ? active.RequiredCapacity.ToString("F2") + " t" : "N/A";
        public string SafetyMarginVal => GetActiveLiftResult() is { } active ? active.SafetyMargin.ToString("F2") + " t" : "N/A";

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
                    _positions.Add(new CranePositionPresentationDto(pos, _transformService, () => CurrentGAViewMode));
                }
            }
            OnPropertyChanged(nameof(Positions));
            RaiseDetailsChanged();
        }

        private void OnSceneSelectionChanged(List<PrecastElement> selectedElements)
        {
            RaiseDetailsChanged();

            // Synchronise DataGrid selection when viewport selection changes
            if (_isSelectingElement)
                return;

            if (selectedElements == null || selectedElements.Count == 0)
                return;

            var firstId = selectedElements[0].ElementID;
            var matchRow = AssignedElements.FirstOrDefault(r =>
                string.Equals(r.ElementId, firstId, StringComparison.Ordinal));

            if (matchRow != null && !ReferenceEquals(matchRow, _selectedElement))
            {
                _isSelectingElement = true;
                try { _selectedElement = matchRow; }
                finally { _isSelectingElement = false; }

                OnPropertyChanged(nameof(SelectedElement));
            }
        }

        public void OnLoaded()
        {
            if (_isLoaded)
                return;

            _isLoaded = true;

            if (SelectedPoint == null && _positions.Any())
                SelectedPoint = _positions[0];
        }

        /// <summary>
        /// Rebuilds the DataGrid row collection whenever the selected crane setup point changes.
        /// </summary>
        private void RebuildAssignedElements()
        {
            AssignedElements.Clear();
            _selectedElement = null;
            OnPropertyChanged(nameof(SelectedElement));

            if (SelectedPoint == null)
                return;

            IEnumerable<ElementLiftResult> filtered;
            if (CurrentGAViewMode == GAViewMode.FinalAssignment)
            {
                filtered = SelectedPoint.Underlying.ElementResults
                    .Where(er => er.IsFinalAssignedToPoint);
            }
            else // SelectedPointFeasibility
            {
                filtered = SelectedPoint.Underlying.ElementResults
                    .Where(er => er.Status == LiftStatus.Liftable);
            }

            foreach (var er in filtered.OrderBy(e => e.ElementMark))
                AssignedElements.Add(new ElementLiftResultRowDto(er));
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
