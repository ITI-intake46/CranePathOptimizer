using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Reports.Definitions;
using CraneOptimizer.Application.Reports.DTOs;
using CraneOptimizer.Application.Reports.Services;
using CraneOptimizer.Application.Services;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.CreatedProject;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Units;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Models;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Services;
using CraneOptimizer.UI.Features.F12_Reports.Services;
using CraneOptimizer.UI.Features.F12_Reports.ViewModels;
using CraneOptimizer.UI.Features.F16_Filter.Services;
using CraneOptimizer.UI.Features.F2_IfcViewer.DTOs;
using CraneOptimizer.UI.Features.F5_ElementManagement.ViewModels;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Services;
using CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels;
using HelixToolkit.Wpf;
using UiCranePlacementResult = CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs.CranePlacementResult;
using UiCranePositionResult = CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs.CranePositionResult;
using UiElementLiftResult = CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs.ElementLiftResult;

namespace CraneOptimizer.Application.Tests.Reports;

public sealed class ReportBuilderViewModelFlowTests
{
    [Fact]
    public async Task PreviewCommand_UsesCurrentPlacementResult_ToPopulateLiftingReportRequestAndRows()
    {
        var scene = new TestSceneService();
        scene.SetElements([BuildElement()]);
        var liftState = new TestElementLiftStateService();
        var cranePlacement = new CranePlacementViewModel(
            new TestRenderingService(),
            new TestCraneOptimizerDialogService(),
            new TestGaResultsWindowService(),
            new TestGaDiagnosticsWindowService(),
            new TestReportBuilderDialogService(),
            new TestViewportInteractionService(),
            new TestViewportHitTestService(),
            liftState);
        var position = new UiCranePositionResult
        {
            X = 10,
            Y = 20,
            Z = 0,
            ElementResults =
            [
                new UiElementLiftResult
                {
                    ElementId = "E1",
                    ElementMark = "MARK-001",
                    ElementType = "Footing",
                    Weight = 3.30,
                    Status = LiftStatus.Liftable,
                    ReachStatus = ReachStatus.Reachable,
                    ChartStatus = ChartStatus.ListedCapacityFound,
                    Radius = 12.0,
                    BoomLength = 24.0,
                    RequiredRadius = 12.0,
                    RequiredCapacity = 4.00,
                    RawChartCapacity = 10.00,
                    AvailableCapacity = 8.55,
                    SafetyMargin = 4.55,
                    AssignedPointId = "1",
                    AssignedPointIndex = 0,
                    FinalOwnerPointId = "1",
                    IsFeasibleFromPoint = true,
                    IsFinalAssignedToPoint = true,
                    IsFinalAssigned = true
                }
            ]
        };
        cranePlacement.Result = new UiCranePlacementResult
        {
            CraneName = "Crane A",
            TotalElementCount = 1,
            LiftedElementCount = 1,
            CoverageRatio = 100,
            AdditionalWeight = 0.70,
            DeratingRatio = 0.90,
            SafetyFactor = 0.95,
            WireLength = 30.0,
            Positions = [position]
        };
        cranePlacement.SelectedPosition = position;

        var elementManagement = new ElementManagementViewModel(
            scene,
            liftState,
            new TestSceneGridSizingService(),
            new TestElementVisibilityFilterService(),
            new TestUnitSettingsService(),
            new TestProjectCoordinateTransformService(),
            new TestCoordinateReadoutFormatter(),
            new TestProjectMaterialSettingsService(),
            new TestProjectWorkspaceService());
        var reportBuilder = new CapturingReportBuilder();
        var previewDialogs = new CapturingReportPreviewDialogService();
        var diagnosticsReader = new TestGaDiagnosticsReaderService();
        using var traceCapture = new StringWriter();
        var listener = new TextWriterTraceListener(traceCapture);
        Trace.Listeners.Add(listener);

        try
        {
            var viewModel = new ReportBuilderViewModel(
                scene,
                new TestProjectWorkspaceService(),
                cranePlacement,
                elementManagement,
                new GASettings(),
                reportBuilder,
                new TestReportPdfExporter(),
                new TestReportExcelExporter(),
                new TestReportTemplateStore(),
                new TestReportFileDialogService(),
                previewDialogs,
                diagnosticsReader,
                new FakeProjectOutputPathService(),
                new ImportReadinessService());
            viewModel.Initialize(ReportFamily.LiftingResult, ReportTemplateCatalog.BasicLiftingResult, "Optimization Result");

            Assert.True(viewModel.PreviewCommand.CanExecute(null));
            viewModel.PreviewCommand.Execute(null);

            var completed = await Task.WhenAny(previewDialogs.ReportShown.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            Assert.Same(previewDialogs.ReportShown.Task, completed);
        }
        finally
        {
            Trace.Flush();
            Trace.Listeners.Remove(listener);
            listener.Dispose();
            elementManagement.Dispose();
        }

        Assert.NotNull(reportBuilder.LastRequest);
        var request = reportBuilder.LastRequest;
        Assert.Equal(ReportFamily.LiftingResult, request.Family);
        Assert.Equal(ReportTemplateCatalog.BasicLiftingResult, request.TemplateName);
        var liftingResult = Assert.Single(request.LiftingResults);
        Assert.Equal("PlacementResult / ElementLiftResult", liftingResult.SourceType);
        Assert.Equal(0, diagnosticsReader.DetailedDiagnosticsReadCount);

        var report = await previewDialogs.ReportShown.Task;
        var row = Assert.Single(report.Rows);
        Assert.Equal("Liftable", row.Values["LiftStatus"]);
        Assert.Equal(12.0, Assert.IsType<double>(row.Values["BestRadius"]), precision: 3);
        Assert.Equal(24.0, Assert.IsType<double>(row.Values["RequiredBoomLength"]), precision: 3);
        Assert.Equal(4.00, Assert.IsType<double>(row.Values["RequiredCapacity"]), precision: 3);
        Assert.Equal(10.00, Assert.IsType<double>(row.Values["RawChartCapacity"]), precision: 3);
        Assert.Equal(8.55, Assert.IsType<double>(row.Values["AvailableCapacity"]), precision: 3);
        Assert.Equal(46.78, Assert.IsType<double>(row.Values["CapacityUtilizationPercent"]), precision: 2);
        Assert.Equal("Point_1", row.Values["AssignedGaPoint"]);

        var debugLine = traceCapture.ToString();
        Assert.Contains("SelectedTemplateName=Basic Lifting Result", debugLine);
        Assert.Contains("ReportFamily=LiftingResult", debugLine);
        Assert.Contains("UsesGaData=True", debugLine);
        Assert.Contains("SelectedColumnKeys=No,ElementMark,Weight,HookRiggingWeight,RequiredCapacity,RawChartCapacity,AvailableCapacity,AssignedGaPoint,LiftStatus,BestRadius,RequiredBoomLength,CapacityUtilizationPercent,SafetyMargin,FailureReason,ParentAssemblyGlobalId,AssemblyLiftWeight,AssemblyLiftCOG,AssemblyMemberIds,SuggestedAction", debugLine);
        Assert.Contains("CurrentPlacementResultPresent=True", debugLine);
        Assert.Contains("CurrentPlacementResultPositionsCount=1", debugLine);
        Assert.Contains("CurrentPlacementResultElementRowsCount=1", debugLine);
        Assert.Contains("ReportBuildRequestDto.LiftingResultsCount=1", debugLine);
        Assert.Contains("DiagnosticsFallbackRowsCount=0", debugLine);
    }

    private static PrecastElement BuildElement() => new()
    {
        ElementID = "E1",
        ElementMark = "MARK-001",
        ElementName = "FOOTING-A",
        ElementType = "Footing",
        ElementMaterial = "C30",
        ElementWT = 3.30,
        WeightTons = 3.30,
        IncludeInGa = true,
        ElementCOG = new DomainPoint3D(0, 0, 0)
    };

    private sealed class CapturingReportBuilder : IReportBuilder
    {
        private readonly UserFacingReportBuilder _inner = new(
            new ElementReportDataProvider(new ReportableElementResolver(), new ImportReadinessService()),
            new LiftingResultReportDataProvider());

        public ReportBuildRequestDto? LastRequest { get; private set; }

        public Task<ReportDocumentDto> BuildAsync(ReportBuildRequestDto request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return _inner.BuildAsync(request, cancellationToken);
        }
    }

    private sealed class CapturingReportPreviewDialogService : IReportPreviewDialogService
    {
        public TaskCompletionSource<ReportDocumentDto> ReportShown { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void ShowPreview(
            ReportDocumentDto report,
            ReportLayoutFitResult layoutFit,
            int? previewRowLimit,
            Func<Task> exportPdfAsync,
            Func<Task> exportExcelAsync)
        {
            ReportShown.TrySetResult(report);
        }
    }

    private sealed class TestGaDiagnosticsReaderService : IGADiagnosticsReaderService
    {
        public int DetailedDiagnosticsReadCount { get; private set; }

        public IReadOnlyList<GARunHistoryDto> ReadRunHistory(string? projectFolderPath) => [];

        public IReadOnlyList<GAElementDiagnosticDto> ReadDetailedDiagnostics(string detailedCsvPath)
        {
            DetailedDiagnosticsReadCount++;
            return [];
        }

        public IReadOnlyList<GAFailureBreakdownDto> ReadFailureBreakdown(string breakdownCsvPath) => [];
    }

    private sealed class TestReportTemplateStore : IReportTemplateStore
    {
        public Task SaveAsync(ReportTemplateDto template, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<ReportTemplateDto>> LoadAsync(ReportFamily family, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ReportTemplateDto>>([]);
        public Task DeleteAsync(ReportFamily family, string templateName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestReportFileDialogService : IReportFileDialogService
    {
        public string? BrowseLogo() => null;
        public string? SavePdf(string suggestedFileName, string initialDirectory) => null;
        public string? SaveExcel(string suggestedFileName, string initialDirectory) => null;
        public string? PromptForTemplateName(string defaultName, IEnumerable<string> existingNames) => null;
        public void ShowInfo(string message, string title) { }
        public bool Confirm(string message, string title) => true;
    }

    private sealed class TestReportPdfExporter : IReportPdfExporter
    {
        public Task ExportAsync(ReportDocumentDto report, string outputPath, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestReportExcelExporter : IReportExcelExporter
    {
        public Task ExportAsync(ReportDocumentDto report, string outputPath, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestProjectWorkspaceService : IProjectWorkspaceService
    {
        public ProjectSession? CurrentSession => new ProjectSession
        {
            ProjectFolderPath = @"C:\FakeProjectFolderPath",
            ProjectName = "Test Project"
        };
        public Task<ProjectSession> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProjectCardModel>> GetRecentProjectsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProjectCardModel>>([]);
        public Task<ProjectSession?> OpenProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult<ProjectSession?>(null);
        public Task<ProjectSession> OpenProjectFolderAsync(string projectFolderPath, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ToggleFavoriteAsync(Guid projectId, bool isFavorite, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<ProjectCardModel>> SearchProjectsAsync(string searchText, bool favoritesOnly, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProjectCardModel>>([]);
    }

    private sealed class TestSceneService : ISceneService
    {
        private List<PrecastElement> _elements = [];
        private List<PrecastElement> _selectedElements = [];

        public List<PrecastElement> GetElements() => _elements;
        public void SetElements(List<PrecastElement> elements) { _elements = elements; ElementsChanged?.Invoke(elements); }
        public void ClearElements() => SetElements([]);
        public List<PrecastElement> GetSelectedElements() => _selectedElements;
        public void SetSelectedElements(List<PrecastElement> elements) { _selectedElements = elements; ElementsSelectionChanged?.Invoke(elements); }
        public void ClearSelectedElements() => SetSelectedElements([]);
        public event Action<List<PrecastElement>>? ElementsChanged;
        public event Action<List<PrecastElement>>? ElementsSelectionChanged;
        public List<GreenAreaZone> GetZones() => [];
        public void AddZone(GreenAreaZone zone) => ZoneAdded?.Invoke(zone);
        public void RemoveZone(GreenAreaZone zone) => ZoneRemoved?.Invoke(zone);
        public void RemoveSelectedZone() { }
        public void ClearZones() => ZonesCleared?.Invoke();
        public void UpdateZone(GreenAreaZone zone) => ZoneUpdated?.Invoke(zone);
        public GreenAreaZone? SelectedZone => null;
        public void SetSelectedZone(GreenAreaZone? zone) => ZoneSelected?.Invoke(zone);
        public void ClearZoneSelection() => ZoneSelected?.Invoke(null);
        public event Action<GreenAreaZone>? ZoneAdded;
        public event Action<GreenAreaZone>? ZoneRemoved;
        public event Action<GreenAreaZone>? ZoneUpdated;
        public event Action? ZonesCleared;
        public event Action<GreenAreaZone?>? ZoneSelected;
        public List<SceneCrane> GetCranes() => [];
        public void AddCrane(SceneCrane crane) => CraneAdded?.Invoke(crane);
        public void RemoveCrane(SceneCrane crane) => CraneRemoved?.Invoke(crane);
        public void ClearCranes() => CranesCleared?.Invoke();
        public void MoveCrane(SceneCrane crane, double x, double y, double z) => CraneMoved?.Invoke(crane);
        public void RotateCrane(SceneCrane crane, double angle) => CraneRotated?.Invoke(crane);
        public void UpdateCrane(SceneCrane crane) => CraneUpdated?.Invoke(crane);
        public SceneCrane? SelectedCrane => null;
        public void SetSelectedCrane(SceneCrane? crane) => CraneSelected?.Invoke(crane);
        public void ClearCraneSelection() => CraneSelected?.Invoke(null);
        public event Action<SceneCrane>? CraneAdded;
        public event Action<SceneCrane>? CraneRemoved;
        public event Action? CranesCleared;
        public event Action<SceneCrane>? CraneMoved;
        public event Action<SceneCrane>? CraneRotated;
        public event Action<SceneCrane>? CraneUpdated;
        public event Action<SceneCrane?>? CraneSelected;
        public List<SceneDxf> GetDxfEntities() => [];
        public void AddDxfEntity(SceneDxf entity) => DxfEntityAdded?.Invoke(entity);
        public void RemoveDxfEntity(SceneDxf entity) => DxfEntityRemoved?.Invoke(entity);
        public void RemoveSelectedDxfEntity() { }
        public void ClearDxfEntities() => DxfEntitiesCleared?.Invoke();
        public void UpdateDxfEntity(SceneDxf entity) => DxfEntityUpdated?.Invoke(entity);
        public SceneDxf? SelectedDxfEntity => null;
        public void SetSelectedDxfEntity(SceneDxf? entity) => DxfEntitySelected?.Invoke(entity);
        public void ClearDxfSelection() => DxfEntitySelected?.Invoke(null);
        public event Action<SceneDxf>? DxfEntityAdded;
        public event Action<SceneDxf>? DxfEntityRemoved;
        public event Action<SceneDxf>? DxfEntityUpdated;
        public event Action? DxfEntitiesCleared;
        public event Action<SceneDxf?>? DxfEntitySelected;
        public object? GetSelectedObject() => null;
        public void SetSelectedObject(object? obj) => SelectionChanged?.Invoke(obj);
        public event Action<object?>? SelectionChanged;
    }

    private sealed class TestElementLiftStateService : IElementLiftStateService
    {
        private readonly Dictionary<string, ElementLiftStateDto> _states = new(StringComparer.Ordinal);
        public event Action? StateChanged;
        public bool HasCurrentState => _states.Count > 0;
        public ElementLiftStateDto? GetState(string? elementId) => elementId != null && _states.TryGetValue(elementId, out var state) ? state : null;
        public void SetStates(IEnumerable<ElementLiftStateDto> states)
        {
            _states.Clear();
            foreach (var state in states)
                _states[state.ElementId] = state;
            StateChanged?.Invoke();
        }
        public void Clear() { _states.Clear(); StateChanged?.Invoke(); }
    }

    private sealed class TestSceneGridSizingService : ISceneGridSizingService
    {
        public (double widthMeters, double lengthMeters, double centerXMeters, double centerYMeters) CalculateGridSizeMeters(IReadOnlyList<PrecastElement> elements) => (1, 1, 0, 0);
    }

    private sealed class TestElementVisibilityFilterService : IElementVisibilityFilterService
    {
        private IReadOnlyList<PrecastElement> _elements = [];
        public event Action? StateChanged;
        public IReadOnlyList<PrecastElement> AllElements => _elements;
        public IReadOnlyList<PrecastElement> VisibleElements => _elements;
        public IReadOnlyList<string> ElementTypes => [];
        public IReadOnlyList<string> Materials => [];
        public IReadOnlyList<string> Storeys => [];
        public int ShowingCount => _elements.Count;
        public int TotalCount => _elements.Count;
        public double AvailableMinWeightKg => 0;
        public double AvailableMaxWeightKg => 0;
        public double MinWeightKg => 0;
        public double MaxWeightKg => 0;
        public string SearchText => string.Empty;
        public void SetElements(IEnumerable<PrecastElement> elements) { _elements = elements.ToList(); StateChanged?.Invoke(); }
        public void SetSearchText(string text) { }
        public void SetTypeEnabled(string type, bool enabled) { }
        public void SetMaterialEnabled(string material, bool enabled) { }
        public void SetStoreyEnabled(string storey, bool enabled) { }
        public void SetWeightRange(double minWeightKg, double maxWeightKg) { }
        public void SetIncludedElementIds(IEnumerable<string>? elementIds) { }
        public void SetExcludedElementIds(IEnumerable<string>? elementIds) { }
        public void ResetFilters() { }
        public void ShowAll() { }
        public void ShowElements(IEnumerable<PrecastElement> elements) => SetElements(elements);
        public void HideElements(IEnumerable<PrecastElement> elements) { }
        public void IsolateElements(IEnumerable<PrecastElement> elements) => SetElements(elements);
        public void ApplyLoadOptions(IfcLoadOptionsDto options) { }
        public string GetStorey(PrecastElement element) => string.Empty;
        public bool IsTypeEnabled(string type) => true;
        public bool IsMaterialEnabled(string material) => true;
        public bool IsStoreyEnabled(string storey) => true;
        public bool IsElementIncluded(PrecastElement element) => true;
        public bool IsElementHidden(PrecastElement element) => false;
    }

    private sealed class TestUnitSettingsService : IUnitSettingsService
    {
        public UnitSettings Current { get; } = UnitSettings.Default.Clone();
        public event EventHandler<UnitSettings>? SettingsChanged;
        public Task LoadProjectSettingsAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveProjectSettingsAsync(Guid projectId, UnitSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void ApplyRuntimeSettings(UnitSettings settings) => SettingsChanged?.Invoke(this, settings);
    }

    private sealed class TestProjectCoordinateTransformService : IProjectCoordinateTransformService
    {
        public event EventHandler? ReferenceChanged;
        public bool IsEnabled => false;
        public ProjectCoordinateReference CurrentReference { get; } = new();
        public DomainPoint3D LocalToSurvey(DomainPoint3D local) => local;
        public DomainPoint3D SurveyToLocal(DomainPoint3D survey) => survey;
        public string FormatCoordinate(double value) => value.ToString("0.##");
        public string FormatLocalPoint(DomainPoint3D point) => $"{point.X:0.##},{point.Y:0.##},{point.Z:0.##}";
        public string FormatSurveyPoint(DomainPoint3D point) => FormatLocalPoint(point);
        public bool ApplyReference(ProjectCoordinateReference reference, out string? errorMessage) { errorMessage = null; ReferenceChanged?.Invoke(this, EventArgs.Empty); return true; }
        public void ResetReference() => ReferenceChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class TestCoordinateReadoutFormatter : ICoordinateReadoutFormatter
    {
        public string FormatModelPoint(DomainPoint3D point) => $"{point.X:0.##},{point.Y:0.##},{point.Z:0.##}";
        public string FormatSitePoint(DomainPoint3D point) => FormatModelPoint(point);
        public string FormatCombinedPoint(DomainPoint3D point) => FormatModelPoint(point);
        public string BuildCoordinateReadout(DomainPoint3D point) => FormatModelPoint(point);
    }

    private sealed class TestProjectMaterialSettingsService : IProjectMaterialSettingsService
    {
        public Task<double> GetConcreteDensityKgPerM3Async(Guid projectId, CancellationToken ct = default) => Task.FromResult(2500.0);
        public Task SetConcreteDensityKgPerM3Async(Guid projectId, double density, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class TestRenderingService : IRenderingService
    {
        public void Initialize(HelixViewport3D viewport) { }
        public void Render<T>(IEnumerable<T> items, IVisualBuilder<T> builder, string layerName) { }
        public void Clear() { }
        public void ClearLayer(string layerKey) { }
        public void SetGridSize(double widthMeters, double lengthMeters) { }
        public void SetGridSize(double widthMeters, double lengthMeters, double centerXMeters, double centerYMeters) { }
        public HelixViewport3D? GetViewport() => null;
        public void ZoomExtents() { }
    }

    private sealed class TestCraneOptimizerDialogService : ICraneOptimizerDialogService
    {
        public CraneOptimizerDialogOutcome? ShowDialog() => null;
    }

    private sealed class TestGaResultsWindowService : IGAResultsWindowService
    {
        public void Show(UiCranePlacementResult result, Action<UiCranePositionResult?> selectedPointChanged, Action<GAViewMode> viewModeChanged, GAViewMode initialViewMode) { }
    }

    private sealed class TestGaDiagnosticsWindowService : IGADiagnosticsWindowService
    {
        public void Show(GAViewMode currentMode = GAViewMode.FinalAssignment) { }
    }

    private sealed class TestReportBuilderDialogService : IReportBuilderDialogService
    {
        public void ShowReportBuilder(ReportFamily family, string templateName, string itemsIncluded) { }
    }

    private sealed class TestViewportInteractionService : IViewportInteractionService
    {
        public IInteractionMode CurrentMode { get; } = new TestInteractionMode();
        public void PushMode(IInteractionMode mode) { }
        public void PopMode() { }
        public void ResetToDefaultMode(IInteractionMode defaultMode) { }
        public void HandleMouseDown(HelixViewport3D viewport, MouseButtonEventArgs e) { }
        public void HandleMouseMove(HelixViewport3D viewport, MouseEventArgs e) { }
        public void HandleMouseUp(HelixViewport3D viewport, MouseButtonEventArgs e) { }
        public void HandleKeyDown(HelixViewport3D viewport, KeyEventArgs e) { }
    }

    private sealed class TestInteractionMode : IInteractionMode
    {
        public void OnMouseDown(HelixViewport3D viewport, MouseButtonEventArgs e) { }
        public void OnMouseMove(HelixViewport3D viewport, MouseEventArgs e) { }
        public void OnMouseUp(HelixViewport3D viewport, MouseButtonEventArgs e) { }
        public void OnKeyDown(HelixViewport3D viewport, KeyEventArgs e) { }
    }

    private sealed class TestViewportHitTestService : IViewportHitTestService
    {
        public bool TryGetWorldPoint(HelixViewport3D viewport, Point screenPt, out Point3D worldPoint) { worldPoint = new Point3D(); return false; }
        public bool TryGetPlacementPoint(HelixViewport3D viewport, Point screenPt, out Point3D worldPoint) { worldPoint = new Point3D(); return false; }
        public bool TryGetHitCrane(HelixViewport3D viewport, Point screenPt, out SceneCrane crane) { crane = null!; return false; }
        public object? GetHitObject(HelixViewport3D viewport, Point screenPt) => null;
    }

    [Fact]
    public void ElementManagementViewModel_BuildPropertyGroups_PopulatesAssemblySummary()
    {
        var scene = new TestSceneService();
        var liftState = new TestElementLiftStateService();

        var parent = new PrecastElement
        {
            ElementID = "parent-id",
            Role = IfcElementRole.LiftAssembly,
            AssemblyMark = "W1",
            MainPartGlobalId = "child-id",
            MainPartCategory = "WALL",
            HasSecondaryParts = true,
            HasHiddenContributors = true,
            AssemblyLiftWeight = 9.28,
            AssemblyLiftCOG = new DomainPoint3D(89.5, 109.1, 1.9),
            AssemblyMemberIds = new List<string> { "child-id", "child-sec" }
        };

        var child = new PrecastElement
        {
            ElementID = "child-id",
            ParentGlobalId = "parent-id",
            ElementType = "IfcWall",
            ElementWidth = 0.20,
            ElementLength = 4.0
        };

        scene.SetElements(new List<PrecastElement> { parent, child });

        var unitService = new TestUnitSettingsService();
        unitService.Current.DisplayLengthUnit = LengthUnit.Millimeter;

        var vm = new ElementManagementViewModel(
            scene,
            liftState,
            new TestSceneGridSizingService(),
            new TestElementVisibilityFilterService(),
            unitService,
            new TestProjectCoordinateTransformService(),
            new TestCoordinateReadoutFormatter(),
            new TestProjectMaterialSettingsService(),
            new TestProjectWorkspaceService());

        vm.SelectedElement = child;

        var assemblyGroup = vm.ElementProperties.FirstOrDefault(g => g.Name == "Assembly Summary");
        Assert.NotNull(assemblyGroup);

        var assemblyMarkProp = assemblyGroup.Children.FirstOrDefault(p => p.Name == "Assembly");
        Assert.NotNull(assemblyMarkProp);
        Assert.Equal("W1", assemblyMarkProp.Value);

        var mainPartProp = assemblyGroup.Children.FirstOrDefault(p => p.Name == "Main Part");
        Assert.NotNull(mainPartProp);
        Assert.Equal("WALL", mainPartProp.Value);

        var secProp = assemblyGroup.Children.FirstOrDefault(p => p.Name == "Has Secondary Parts");
        Assert.NotNull(secProp);
        Assert.Equal("Yes", secProp.Value);

        var hiddenProp = assemblyGroup.Children.FirstOrDefault(p => p.Name == "Included Hidden Contributors");
        Assert.NotNull(hiddenProp);
        Assert.Equal("Rebar weight included", hiddenProp.Value);

        var weightProp = assemblyGroup.Children.FirstOrDefault(p => p.Name == "Total Lift Weight");
        Assert.NotNull(weightProp);
        Assert.Equal("9.28t", weightProp.Value);

        var profileProp = assemblyGroup.Children.FirstOrDefault(p => p.Name == "Profile");
        Assert.NotNull(profileProp);
        Assert.Equal("200*4000", profileProp.Value);

        vm.Dispose();
    }

    private sealed class FakeProjectOutputPathService : IProjectOutputPathService
    {
        private readonly string _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CraneOptimizerReportsTest");
        private string R(params string[] segs) { var p = System.IO.Path.Combine(new[] { _root }.Concat(segs).ToArray()); System.IO.Directory.CreateDirectory(p); return p; }
        public string GetProjectRoot() => _root;
        public string GetExportsDirectory() => R("Exports");
        public string GetUserPdfExportsDirectory() => R("Exports", "PDF");
        public string GetUserExcelExportsDirectory() => R("Exports", "Excel");
        public string GetUserCsvExportsDirectory() => R("Exports", "CSV");
        public string GetReportsDirectory() => R("Reports");
        public string GetGaReportsDirectory() => R("Reports", "GA");
        public string GetGaRunReportsDirectory(string runId) => R("Reports", "GA", runId);
        public string GetGaHistoryDirectory() => R("Reports", "GA", "History");
        public string GetIfcAuditReportsDirectory() => R("Reports", "IFC");
        public string GetDataSourceAuditDirectory() => R("Reports", "DataSourceAudit");
        public string GetLogsDirectory() => R("Logs");
        public string GetGaLogsDirectory() => R("Logs", "GA");
        public string GetModelsDirectory() => R("Models");
        public string GetIfcModelsDirectory() => R("Models", "IFC");
        public string GetDxfModelsDirectory() => R("Models", "DXF");
    }
}
