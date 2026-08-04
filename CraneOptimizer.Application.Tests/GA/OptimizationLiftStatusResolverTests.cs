using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.CreatedProject;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Core.Services;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Rendering;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Services;
using CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels;
using HelixToolkit.Wpf;

namespace CraneOptimizer.Application.Tests.GA;

public sealed class OptimizationLiftStatusResolverTests
{
    [Fact]
    public void SelectedPosition_UsesDeterministicFeasibility_NotFinalAssignment()
    {
        CranePositionResult position = Position(
            Evaluation("feasible-unassigned", LiftStatus.Liftable, isAssigned: false),
            Evaluation("assigned-but-failed", LiftStatus.Overweight, isAssigned: true));
        OptimizationLiftStatusData data = Resolve(position);

        OptimizationLiftStatusSelection selected = data.CreateSelection(
            showOverall: false,
            position);

        Assert.Equal(new[] { "feasible-unassigned" }, selected.LiftableLiftUnitIds);
        Assert.Empty(selected.NotLiftableLiftUnitIds);
        Assert.Equal(2, data.EligibleLiftUnitIds.Count);
    }

    [Fact]
    public void OverallMode_UsesLiftableUnion_AndEligibleDifference()
    {
        CranePositionResult first = Position(
            Evaluation("A", LiftStatus.Liftable),
            Evaluation("B", LiftStatus.OutOfRadius),
            Evaluation("C", LiftStatus.Overweight));
        CranePositionResult second = Position(
            Evaluation("A", LiftStatus.OutOfRadius),
            Evaluation("B", LiftStatus.Liftable),
            Evaluation("C", LiftStatus.HeightFail));
        OptimizationLiftStatusData data = Resolve(first, second);

        OptimizationLiftStatusSelection overallFromFirst = data.CreateSelection(
            showOverall: true,
            first);
        OptimizationLiftStatusSelection overallFromSecond = data.CreateSelection(
            showOverall: true,
            second);

        Assert.Equal(new[] { "A", "B" }, Sorted(overallFromFirst.LiftableLiftUnitIds));
        Assert.Equal(new[] { "C" }, overallFromFirst.NotLiftableLiftUnitIds);
        Assert.Equal(
            Sorted(overallFromFirst.LiftableLiftUnitIds),
            Sorted(overallFromSecond.LiftableLiftUnitIds));
        Assert.Equal(
            Sorted(overallFromFirst.NotLiftableLiftUnitIds),
            Sorted(overallFromSecond.NotLiftableLiftUnitIds));

        OptimizationLiftStatusSelection selectedSecond = data.CreateSelection(
            showOverall: false,
            second);
        Assert.Equal(new[] { "B" }, selectedSecond.LiftableLiftUnitIds);
        Assert.Empty(selectedSecond.NotLiftableLiftUnitIds);
    }

    [Fact]
    public void AssemblyRepresentative_PropagatesStatusToVisibleMembers()
    {
        CranePositionResult position = Position(
            Evaluation(
                "assembly-representative",
                LiftStatus.Liftable,
                memberIds: new[] { "member-1", "member-2" }));
        OptimizationLiftStatusData data = Resolve(position);

        OptimizationLiftStatusSelection selected = data.CreateSelection(false, position);

        Assert.Equal(
            new[] { "assembly-representative", "member-1", "member-2" },
            Sorted(selected.LiftableVisualElementIds));
    }

    [Fact]
    public void ViewportState_PreservesSelectionPriority_AndRestoresResultColor()
    {
        OptimizationResultViewportState state = new();
        state.Apply(new[] { "green" }, new[] { "red" });

        Assert.Equal(
            ElementVisualizationStatus.Selected,
            state.Resolve("green", isSelected: true, ElementVisualizationStatus.Default));
        Assert.Equal(
            ElementVisualizationStatus.Liftable,
            state.Resolve("green", isSelected: false, ElementVisualizationStatus.Default));
        Assert.Equal(
            ElementVisualizationStatus.NotLiftable,
            state.Resolve("red", isSelected: false, ElementVisualizationStatus.Default));

        state.Clear();

        Assert.Equal(
            ElementVisualizationStatus.Default,
            state.Resolve("green", isSelected: false, ElementVisualizationStatus.Overweight));
    }

    [Fact]
    public void StaleStoredId_DoesNotColorAnotherSceneElement()
    {
        OptimizationResultViewportState state = new();
        state.Apply(new[] { "missing-from-scene" }, Array.Empty<string>());

        ElementVisualizationStatus actual = state.Resolve(
            "present-in-scene",
            isSelected: false,
            ElementVisualizationStatus.OutOfRadius);

        Assert.Equal(ElementVisualizationStatus.Default, actual);
    }

    [Fact]
    public void OlderResultWithNullCollections_ResolvesAsEmpty()
    {
        CranePlacementResult emptyResult = new() { Positions = null! };
        OptimizationLiftStatusData emptyData = OptimizationLiftStatusResolver.Resolve(emptyResult);
        Assert.Empty(emptyData.EligibleLiftUnitIds);

        CranePositionResult position = new() { ElementResults = null! };
        OptimizationLiftStatusData positionData = Resolve(position);
        OptimizationLiftStatusSelection selection = positionData.CreateSelection(false, position);

        Assert.Empty(selection.LiftableLiftUnitIds);
        Assert.Empty(selection.NotLiftableLiftUnitIds);
    }

    [Fact]
    public void SharedHighlightService_PreservesOverallStateAcrossWindowRecreation()
    {
        CranePositionResult position = Position(
            Evaluation("green", LiftStatus.Liftable),
            Evaluation("red", LiftStatus.Overweight));
        CranePlacementResult result = new() { Positions = new List<CranePositionResult> { position } };
        CapturingSceneRenderingManager rendering = new();
        OptimizationViewportHighlightService service = new(rendering);

        service.LoadResult(result);
        service.SetShowOverallLiftStatus(true);
        service.LoadResult(result);

        Assert.True(service.ShowOverallLiftStatus);
        Assert.Equal(OptimizationViewportHighlightMode.Overall, service.ActiveHighlightMode);
        Assert.Equal(new[] { "green" }, rendering.LiftableIds);
        Assert.Equal(new[] { "red" }, rendering.NotLiftableIds);
        Assert.Equal(0, rendering.ClearCount);
    }

    [Fact]
    public void SharedHighlightService_PositionSelectionDoesNotReplaceOverallColors()
    {
        CranePositionResult first = Position(
            Evaluation("A", LiftStatus.Liftable),
            Evaluation("B", LiftStatus.OutOfRadius));
        CranePositionResult second = Position(
            Evaluation("A", LiftStatus.OutOfRadius),
            Evaluation("B", LiftStatus.Liftable));
        CranePlacementResult result = new() { Positions = new List<CranePositionResult> { first, second } };
        CapturingSceneRenderingManager rendering = new();
        OptimizationViewportHighlightService service = new(rendering);
        service.LoadResult(result);
        service.SetShowOverallLiftStatus(true);
        int applyCount = rendering.ApplyCount;

        service.SelectPosition(second);

        Assert.Equal(applyCount, rendering.ApplyCount);
        Assert.Equal(new[] { "A", "B" }, rendering.LiftableIds);
        Assert.Equal(new[] { "B" }, service.SelectedPositionLiftableIds);
        Assert.Equal(OptimizationViewportHighlightMode.Overall, service.ActiveHighlightMode);
    }

    [Fact]
    public void SharedHighlightService_ExplicitClearResetsSessionAndViewport()
    {
        CranePositionResult position = Position(Evaluation("A", LiftStatus.Liftable));
        CapturingSceneRenderingManager rendering = new();
        OptimizationViewportHighlightService service = new(rendering);
        service.LoadResult(new CranePlacementResult
        {
            Positions = new List<CranePositionResult> { position }
        });
        service.SetShowOverallLiftStatus(true);

        service.Clear();

        Assert.False(service.ShowOverallLiftStatus);
        Assert.Equal(OptimizationViewportHighlightMode.None, service.ActiveHighlightMode);
        Assert.Empty(service.EligibleLiftUnitIds);
        Assert.Equal(1, rendering.ClearCount);
    }

    [Fact]
    public void ResultsViewModel_ClosingDoesNotClearSharedHighlightState()
    {
        CranePositionResult position = Position(
            Evaluation("green", LiftStatus.Liftable),
            Evaluation("red", LiftStatus.Overweight));
        CranePlacementResult result = new()
        {
            Positions = new List<CranePositionResult> { position }
        };
        CapturingSceneRenderingManager rendering = new();
        OptimizationViewportHighlightService highlightService = new(rendering);
        GAResultsWindowViewModel viewModel = new(
            result,
            new NoOpRenderingService(),
            new SceneService(),
            _ => { },
            _ => { },
            GAViewMode.FinalAssignment,
            new IdentityCoordinateTransformService(),
            highlightService);
        viewModel.OnLoaded();
        viewModel.ShowOverallLiftStatus = true;

        viewModel.OnClosing();

        Assert.True(highlightService.ShowOverallLiftStatus);
        Assert.Equal(OptimizationViewportHighlightMode.Overall, highlightService.ActiveHighlightMode);
        Assert.Equal(new[] { "green" }, rendering.LiftableIds);
        Assert.Equal(new[] { "red" }, rendering.NotLiftableIds);
        Assert.Equal(0, rendering.ClearCount);
    }

    [Fact]
    public void ResultsWindowContract_IsModelessTopmostAndSingleInstance()
    {
        string serviceSource = File.ReadAllText(RepositoryFile(
            "CraneOptimizer.UI",
            "Features",
            "F8_GAOptimizer",
            "Services",
            "GAResultsWindowService.cs"));
        string windowXaml = File.ReadAllText(RepositoryFile(
            "CraneOptimizer.UI",
            "Features",
            "F8_GAOptimizer",
            "Views",
            "GAResultsWindow.xaml"));

        Assert.Contains("window.Show();", serviceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ShowDialog", serviceSource, StringComparison.Ordinal);
        Assert.Contains("if (_activeWindow != null)", serviceSource, StringComparison.Ordinal);
        Assert.Contains("ActivateWindow(_activeWindow)", serviceSource, StringComparison.Ordinal);
        Assert.Contains("Topmost=\"True\"", windowXaml, StringComparison.Ordinal);
    }

    private static OptimizationLiftStatusData Resolve(params CranePositionResult[] positions)
    {
        return OptimizationLiftStatusResolver.Resolve(
            new CranePlacementResult { Positions = positions.ToList() });
    }

    private static CranePositionResult Position(params ElementLiftResult[] results)
    {
        return new CranePositionResult { ElementResults = results.ToList() };
    }

    private static ElementLiftResult Evaluation(
        string id,
        LiftStatus status,
        bool isAssigned = false,
        IEnumerable<string>? memberIds = null)
    {
        return new ElementLiftResult
        {
            ElementId = id,
            Status = status,
            IsFinalAssignedToPoint = isAssigned,
            IsFinalAssigned = isAssigned,
            IsAssemblyGroup = memberIds != null,
            AssemblyMemberIds = memberIds?.ToList() ?? new List<string>()
        };
    }

    private static string[] Sorted(IEnumerable<string> values)
    {
        return values.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static string RepositoryFile(params string[] segments)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null &&
               !File.Exists(Path.Combine(directory.FullName, "CraneOptimizer.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
    }

    private sealed class CapturingSceneRenderingManager : ISceneRenderingManager
    {
        public event Action<IReadOnlyList<PrecastElement>>? ElementsChanged;
        public event Action? ElementsCleared;

        public bool ShowCraneRadius { get; set; }
        public int ApplyCount { get; private set; }
        public int ClearCount { get; private set; }
        public IReadOnlyList<string> LiftableIds { get; private set; } = Array.Empty<string>();
        public IReadOnlyList<string> NotLiftableIds { get; private set; } = Array.Empty<string>();

        public void Configure(SceneRenderingContext context)
        {
        }

        public void ReRenderElements()
        {
        }

        public void ReRenderCranes()
        {
        }

        public void UpdateElementMaterials(IReadOnlyList<ElementLiftResult>? results = null)
        {
        }

        public void ApplyOptimizationResultHighlight(
            IEnumerable<string> liftableElementIds,
            IEnumerable<string> notLiftableElementIds)
        {
            ApplyCount++;
            LiftableIds = Sorted(liftableElementIds);
            NotLiftableIds = Sorted(notLiftableElementIds);
        }

        public void ClearOptimizationResultHighlight()
        {
            ClearCount++;
            LiftableIds = Array.Empty<string>();
            NotLiftableIds = Array.Empty<string>();
        }

        public void Dispose()
        {
        }
    }

    private sealed class NoOpRenderingService : IRenderingService
    {
        public bool IsGridVisible => true;

        public void Initialize(HelixViewport3D viewport)
        {
        }

        public void Render<T>(IEnumerable<T> items, IVisualBuilder<T> builder, string layerName)
        {
        }

        public void Clear()
        {
        }

        public void ClearLayer(string layerKey)
        {
        }

        public void SetGridVisible(bool isVisible)
        {
        }

        public void SetGridSize(double widthMeters, double lengthMeters)
        {
        }

        public void SetGridSize(
            double widthMeters,
            double lengthMeters,
            double centerXMeters,
            double centerYMeters)
        {
        }

        public HelixViewport3D? GetViewport() => null;

        public void ZoomExtents()
        {
        }
    }

    private sealed class IdentityCoordinateTransformService : IProjectCoordinateTransformService
    {
        public event EventHandler? ReferenceChanged;

        public bool IsEnabled => false;
        public ProjectCoordinateReference CurrentReference { get; } = new();

        public DomainPoint3D LocalToSurvey(DomainPoint3D local) => local;
        public DomainPoint3D SurveyToLocal(DomainPoint3D survey) => survey;
        public string FormatCoordinate(double value) => value.ToString("0.##");
        public string FormatLocalPoint(DomainPoint3D point) =>
            $"{point.X:0.##},{point.Y:0.##},{point.Z:0.##}";
        public string FormatSurveyPoint(DomainPoint3D point) =>
            FormatLocalPoint(point);
        public bool ApplyReference(ProjectCoordinateReference reference, out string? errorMessage)
        {
            errorMessage = null;
            ReferenceChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public void ResetReference()
        {
            ReferenceChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
