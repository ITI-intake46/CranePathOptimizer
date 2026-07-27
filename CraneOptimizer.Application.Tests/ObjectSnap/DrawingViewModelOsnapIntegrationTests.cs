using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.Domain.Services;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Core.Services;
using CraneOptimizer.UI.Features.F15_ObjectSnap.Models;
using CraneOptimizer.UI.Features.F15_ObjectSnap.Services;
using CraneOptimizer.UI.Features.F7_Zones.Interaction;
using CraneOptimizer.UI.Features.F7_Zones.ViewModels;
using HelixToolkit.Wpf;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.Application.Tests.ObjectSnap;

public sealed class DrawingViewModelOsnapIntegrationTests
{
    [Theory]
    [InlineData(DrawingMode.Allowed)]
    [InlineData(DrawingMode.Forbidden)]
    public async Task CanonicalCommitRoute_UsesExactGlobalSnapBeforeClosureShiftAndRaw(
        DrawingMode mode)
    {
        await RunOnStaAsync(
            () =>
            {
                DrawingFixture fixture = CreateFixture(masterEnabled: true);
                StartMode(fixture.ViewModel, mode);
                Assert.Equal(mode, fixture.ViewModel.ActiveDrawingMode);
                Assert.Single(fixture.Interaction.PushedModeTypes);
                Assert.Equal(
                    typeof(DrawingInteractionMode),
                    fixture.Interaction.PushedModeTypes[0]);

                Assert.True(fixture.ViewModel.CommitResolvedPoint(
                    new DomainPoint3D(0, 0, 0),
                    capturedShift: false,
                    acceptedSnap: null));
                Assert.True(fixture.ViewModel.CommitResolvedPoint(
                    new DomainPoint3D(10, 0, 0),
                    capturedShift: false,
                    acceptedSnap: null));
                Assert.True(fixture.ViewModel.CommitResolvedPoint(
                    new DomainPoint3D(10, 10, 0),
                    capturedShift: false,
                    acceptedSnap: null));

                var acceptedWorld = new Point3D(6, 7, 3);
                var accepted = new OsnapResult(
                    OsnapType.Endpoint,
                    acceptedWorld,
                    ScreenPoint: null,
                    OsnapSource.Ifc);
                DomainPoint3D rawNearFirstPoint =
                    new(0.05, 0.05, 0);

                bool committed = fixture.ViewModel.CommitResolvedPoint(
                    rawNearFirstPoint,
                    capturedShift: true,
                    accepted);

                Assert.True(committed);
                Assert.Equal(4, fixture.ViewModel.PointCount);
                AssertPoint(
                    acceptedWorld,
                    fixture.Drawing.GetPoints()[^1]);
            });
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task CanonicalCommitRoute_FallsBackToRaw_WhenNoAcceptedSnapOrMasterDisabled(
        bool masterEnabled,
        bool passAcceptedSnap)
    {
        await RunOnStaAsync(
            () =>
            {
                DrawingFixture fixture = CreateFixture(masterEnabled);
                StartMode(fixture.ViewModel, DrawingMode.Allowed);
                DomainPoint3D raw = new(4, 5, 6);
                OsnapResult? accepted = passAcceptedSnap
                    ? new OsnapResult(
                        OsnapType.Endpoint,
                        new Point3D(40, 50, 60),
                        ScreenPoint: null,
                        OsnapSource.Dxf)
                    : null;

                bool committed = fixture.ViewModel.CommitResolvedPoint(
                    raw,
                    capturedShift: false,
                    accepted);

                Assert.True(committed);
                Assert.Single(fixture.Drawing.GetPoints());
                AssertPoint(raw, fixture.Drawing.GetPoints()[0]);
            });
    }

    [Theory]
    [InlineData(DrawingMode.Allowed)]
    [InlineData(DrawingMode.Forbidden)]
    public async Task ResolvedHover_UsesAcceptedSnapForPreviewWithoutAddingVertex(
        DrawingMode mode)
    {
        await RunOnStaAsync(
            () =>
            {
                DrawingFixture fixture = CreateFixture(masterEnabled: true);
                StartMode(fixture.ViewModel, mode);
                Assert.True(fixture.ViewModel.CommitResolvedPoint(
                    new DomainPoint3D(0, 0, 0),
                    capturedShift: false,
                    acceptedSnap: null));
                int publicationCountBeforeHover =
                    fixture.Osnap.TransientPublications.Count;
                var acceptedWorld = new Point3D(8, 9, 2);
                var accepted = new OsnapResult(
                    OsnapType.Midpoint,
                    acceptedWorld,
                    ScreenPoint: null,
                    OsnapSource.Zone);

                fixture.ViewModel.SetResolvedHoverPoint(
                    new DomainPoint3D(2, 3, 4),
                    capturedShift: true,
                    accepted);

                Assert.Equal(1, fixture.ViewModel.PointCount);
                Assert.Single(fixture.Drawing.GetPoints());
                Assert.Equal(
                    publicationCountBeforeHover,
                    fixture.Osnap.TransientPublications.Count);
                Assert.NotNull(fixture.Rendering.LastPreviewPoints);
                Assert.Equal(
                    2,
                    fixture.Rendering.LastPreviewPoints.Count);
                AssertPoint(
                    acceptedWorld,
                    fixture.Rendering.LastPreviewPoints[^1]);
            });
    }

    [Theory]
    [InlineData(FinishReason.RightClick)]
    [InlineData(FinishReason.CtrlEnter)]
    [InlineData(FinishReason.CKey)]
    [InlineData(FinishReason.DoubleClick)]
    [InlineData(FinishReason.ToolbarFinishButton)]
    public async Task PendingThirdPoint_CommitsBeforeEveryDeferredFinishReason(
        FinishReason reason)
    {
        await RunOnStaAsync(
            () =>
            {
                DrawingFixture fixture = CreateFixture(masterEnabled: true);
                int dialogCallCount = 0;
                DrawingViewModel.DialogShowHook = (_, type) =>
                {
                    dialogCallCount++;
                    Assert.Equal(3, fixture.ViewModel.PointCount);
                    return (true, type, "Deferred Finish Zone", null, null);
                };

                try
                {
                    StartMode(fixture.ViewModel, DrawingMode.Allowed);
                    Assert.True(fixture.ViewModel.CommitResolvedPoint(
                        new DomainPoint3D(0, 0, 0),
                        capturedShift: false,
                        acceptedSnap: null));
                    Assert.True(fixture.ViewModel.CommitResolvedPoint(
                        new DomainPoint3D(10, 0, 0),
                        capturedShift: false,
                        acceptedSnap: null));

                    long pendingSession =
                        fixture.ViewModel.DrawingSessionVersion;
                    Assert.True(
                        fixture.ViewModel.BeginPointResolution(
                            pendingSession));

                    fixture.ViewModel.FinishDrawingFromUserAction(reason);

                    Assert.True(fixture.ViewModel.IsDrawing);
                    Assert.Equal(2, fixture.ViewModel.PointCount);
                    Assert.Equal(0, dialogCallCount);
                    Assert.Empty(fixture.Scene.GetZones());

                    var thirdPoint = new DomainPoint3D(10, 10, 0);
                    Assert.True(fixture.ViewModel.CommitResolvedPoint(
                        thirdPoint,
                        capturedShift: false,
                        acceptedSnap: null));
                    Assert.Equal(3, fixture.ViewModel.PointCount);
                    Assert.Equal(0, dialogCallCount);

                    fixture.ViewModel.CompletePointResolution(
                        pendingSession);

                    Assert.False(fixture.ViewModel.IsDrawing);
                    Assert.Equal(1, dialogCallCount);
                    GreenAreaZone zone = Assert.Single(
                        fixture.Scene.GetZones());
                    Assert.Equal(3, zone.Vertices.Count);
                    AssertPoint(thirdPoint, zone.Vertices[^1]);
                }
                finally
                {
                    DrawingViewModel.DialogShowHook = null;
                }
            });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelOrReplacement_InvalidatesPendingCompletion(
        bool replaceDrawing)
    {
        await RunOnStaAsync(
            () =>
            {
                DrawingFixture fixture = CreateFixture(masterEnabled: true);
                int dialogCallCount = 0;
                DrawingViewModel.DialogShowHook = (_, type) =>
                {
                    dialogCallCount++;
                    return (true, type, "Stale Finish Zone", null, null);
                };

                try
                {
                    StartMode(fixture.ViewModel, DrawingMode.Allowed);
                    Assert.True(fixture.ViewModel.CommitResolvedPoint(
                        new DomainPoint3D(0, 0, 0),
                        capturedShift: false,
                        acceptedSnap: null));
                    Assert.True(fixture.ViewModel.CommitResolvedPoint(
                        new DomainPoint3D(10, 0, 0),
                        capturedShift: false,
                        acceptedSnap: null));
                    Assert.True(fixture.ViewModel.CommitResolvedPoint(
                        new DomainPoint3D(10, 10, 0),
                        capturedShift: false,
                        acceptedSnap: null));

                    long pendingSession =
                        fixture.ViewModel.DrawingSessionVersion;
                    Assert.True(
                        fixture.ViewModel.BeginPointResolution(
                            pendingSession));
                    fixture.ViewModel.Finish();
                    Assert.True(fixture.ViewModel.IsDrawing);
                    Assert.Equal(0, dialogCallCount);

                    if (replaceDrawing)
                    {
                        StartMode(
                            fixture.ViewModel,
                            DrawingMode.Forbidden);
                    }
                    else
                    {
                        fixture.ViewModel.Cancel();
                    }

                    long currentSession =
                        fixture.ViewModel.DrawingSessionVersion;
                    Assert.NotEqual(pendingSession, currentSession);

                    fixture.ViewModel.CompletePointResolution(
                        pendingSession);

                    Assert.Equal(0, dialogCallCount);
                    Assert.Empty(fixture.Scene.GetZones());
                    if (replaceDrawing)
                    {
                        Assert.True(fixture.ViewModel.IsDrawing);
                        Assert.Equal(
                            DrawingMode.Forbidden,
                            fixture.ViewModel.ActiveDrawingMode);
                        Assert.Equal(0, fixture.ViewModel.PointCount);
                    }
                    else
                    {
                        Assert.False(fixture.ViewModel.IsDrawing);
                        Assert.Equal(
                            DrawingMode.None,
                            fixture.ViewModel.ActiveDrawingMode);
                    }
                }
                finally
                {
                    DrawingViewModel.DialogShowHook = null;
                }
            });
    }

    [Fact]
    public async Task FinishCancelAndModeReplacement_ClearVersionedTransientGeometry()
    {
        await RunOnStaAsync(
            () =>
            {
                DrawingFixture fixture = CreateFixture(masterEnabled: true);
                DrawingViewModel.DialogShowHook = (_, type) =>
                    (true, type, "Object Snap Zone", null, null);
                try
                {
                    StartMode(
                        fixture.ViewModel,
                        DrawingMode.Allowed);
                    Assert.Equal(1, fixture.ViewModel.DrawingSessionVersion);
                    Assert.True(fixture.ViewModel.CommitResolvedPoint(
                        new DomainPoint3D(1, 1, 0),
                        capturedShift: false,
                        acceptedSnap: null));
                    Assert.Contains(
                        fixture.Osnap.TransientPublications,
                        publication =>
                            publication.SessionVersion == 1 &&
                            publication.Vertices.Count == 1);

                    StartMode(
                        fixture.ViewModel,
                        DrawingMode.Forbidden);
                    Assert.Equal(2, fixture.ViewModel.DrawingSessionVersion);
                    Assert.Equal(
                        DrawingMode.Forbidden,
                        fixture.ViewModel.ActiveDrawingMode);
                    Assert.Contains(2, fixture.Osnap.ClearSessions);
                    Assert.Contains(
                        fixture.Osnap.TransientPublications,
                        publication =>
                            publication.SessionVersion == 2 &&
                            publication.Vertices.Count == 0);

                    Assert.True(fixture.ViewModel.CommitResolvedPoint(
                        new DomainPoint3D(0, 0, 0),
                        capturedShift: false,
                        acceptedSnap: null));
                    Assert.True(fixture.ViewModel.CommitResolvedPoint(
                        new DomainPoint3D(10, 0, 0),
                        capturedShift: false,
                        acceptedSnap: null));
                    Assert.True(fixture.ViewModel.CommitResolvedPoint(
                        new DomainPoint3D(10, 10, 0),
                        capturedShift: false,
                        acceptedSnap: null));

                    fixture.ViewModel.Finish();

                    Assert.False(fixture.ViewModel.IsDrawing);
                    Assert.Equal(3, fixture.ViewModel.DrawingSessionVersion);
                    Assert.Contains(3, fixture.Osnap.ClearSessions);
                    GreenAreaZone zone = Assert.Single(
                        fixture.Scene.GetZones());
                    Assert.Equal(ZoneType.Forbidden, zone.Type);

                    StartMode(
                        fixture.ViewModel,
                        DrawingMode.Allowed);
                    Assert.Equal(4, fixture.ViewModel.DrawingSessionVersion);
                    Assert.True(fixture.ViewModel.CommitResolvedPoint(
                        new DomainPoint3D(3, 4, 0),
                        capturedShift: false,
                        acceptedSnap: null));

                    fixture.ViewModel.Cancel();

                    Assert.False(fixture.ViewModel.IsDrawing);
                    Assert.Equal(5, fixture.ViewModel.DrawingSessionVersion);
                    Assert.Equal(
                        new long[] { 1, 2, 3, 4, 5 },
                        fixture.Osnap.ClearSessions);
                }
                finally
                {
                    DrawingViewModel.DialogShowHook = null;
                }
            });
    }

    private static DrawingFixture CreateFixture(bool masterEnabled)
    {
        var drawing = new DrawingService();
        var scene = new RecordingSceneService();
        var rendering = new RecordingRenderingService();
        var interaction = new RecordingInteractionService();
        var hitTest = new StubViewportHitTestService();
        var osnap = new RecordingOsnapService(masterEnabled);
        var viewModel = new DrawingViewModel(
            drawing,
            scene,
            rendering,
            interaction,
            hitTest,
            new ZoneGeometryService(),
            unitSettings: null,
            osnap);
        return new DrawingFixture(
            drawing,
            scene,
            rendering,
            interaction,
            osnap,
            viewModel);
    }

    private static void StartMode(
        DrawingViewModel viewModel,
        DrawingMode mode)
    {
        ICommand command = mode switch
        {
            DrawingMode.Allowed => viewModel.StartAllowedCommand,
            DrawingMode.Forbidden => viewModel.StartForbiddenCommand,
            _ => throw new ArgumentOutOfRangeException(
                nameof(mode),
                mode,
                null)
        };
        command.Execute(null);
    }

    private static async Task RunOnStaAsync(Action action)
    {
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(
            () =>
            {
                try
                {
                    action();
                    completion.TrySetResult();
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            })
        {
            IsBackground = true,
            Name = "Drawing ViewModel Object Snap Integration Test"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static void AssertPoint(
        Point3D expected,
        DomainPoint3D actual)
    {
        Assert.Equal(expected.X, actual.X, precision: 9);
        Assert.Equal(expected.Y, actual.Y, precision: 9);
        Assert.Equal(expected.Z, actual.Z, precision: 9);
    }

    private static void AssertPoint(
        DomainPoint3D expected,
        DomainPoint3D actual)
    {
        Assert.Equal(expected.X, actual.X, precision: 9);
        Assert.Equal(expected.Y, actual.Y, precision: 9);
        Assert.Equal(expected.Z, actual.Z, precision: 9);
    }

    private sealed record DrawingFixture(
        DrawingService Drawing,
        RecordingSceneService Scene,
        RecordingRenderingService Rendering,
        RecordingInteractionService Interaction,
        RecordingOsnapService Osnap,
        DrawingViewModel ViewModel);

    private sealed record TransientPublication(
        long SessionVersion,
        IReadOnlyList<Point3D> Vertices);

    private sealed class RecordingOsnapService : IOsnapService
    {
        private Action<bool>? _masterEnabledChanged;

        public RecordingOsnapService(bool masterEnabled)
        {
            Settings.MasterEnabled = masterEnabled;
        }

        public OsnapSettings Settings { get; } = new();

        public event Action<bool>? MasterEnabledChanged
        {
            add => _masterEnabledChanged += value;
            remove => _masterEnabledChanged -= value;
        }

        public event Action<long>? GeometryVersionChanged
        {
            add { }
            remove { }
        }

        public event Action<long>? SettingsVersionChanged
        {
            add { }
            remove { }
        }

        public event Action<bool>? PreparingChanged
        {
            add { }
            remove { }
        }

        public OsnapGeometryVersionState GeometryVersions { get; } =
            new(
                Aggregate: 0,
                new OsnapSourceVersionSnapshot(0, 0, 0, 0));

        public long SettingsVersion => 1;

        public OsnapModeMask EnabledModes =>
            Settings.MasterEnabled
                ? OsnapModeMask.Endpoint |
                  OsnapModeMask.Intersection |
                  OsnapModeMask.Midpoint
                : OsnapModeMask.None;

        public bool IsPreparing => false;
        public double SceneScale => 10;
        public int SnapCacheBuiltCount => 0;
        public int SnapPointCount => 0;
        public double SnapBuildDurationMs => 0;
        public double SnapMemoryDeltaMB => 0;

        public List<TransientPublication> TransientPublications { get; } =
            new();

        public List<long> ClearSessions { get; } = new();

        public void SetMasterEnabled(bool enabled)
        {
            Settings.MasterEnabled = enabled;
            _masterEnabledChanged?.Invoke(enabled);
        }

        public void SetSnapEnabled(OsnapType type, bool enabled)
        {
        }

        public void SetSourceEnabled(OsnapSource source, bool enabled)
        {
        }

        public void SetSnapRadius(double radiusPixels)
        {
            Settings.SnapRadiusPx = radiusPixels;
        }

        public void SetGridSpacing(double spacing)
        {
            Settings.GridSpacing = spacing;
        }

        public Task SetCompletedZoneGeometryAsync(
            IReadOnlyList<OsnapZoneGeometry> polygons,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SetDxfSnapPointsAsync(
            IReadOnlyList<Point3D> endpoints,
            IReadOnlyList<(Point3D A, Point3D B)> segments,
            IReadOnlyList<Point3D> centers,
            IReadOnlyList<Point3D> quadrants,
            IReadOnlyList<(Point3D A, Point3D B)> midpointSegments,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SetIfcSnapGeometryAsync(
            IReadOnlyList<IfcSnapGeometry> geometry,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SetTransientZoneGeometryAsync(
            long drawingSessionVersion,
            IReadOnlyList<Point3D> vertices,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TransientPublications.Add(
                new TransientPublication(
                    drawingSessionVersion,
                    vertices.ToArray()));
            return Task.CompletedTask;
        }

        public void ClearTransientZoneGeometry(long drawingSessionVersion)
        {
            ClearSessions.Add(drawingSessionVersion);
        }

        public void CancelPendingQueries()
        {
        }

        public Task<OsnapResult?> SnapAsync(
            OsnapQuery query,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<OsnapResult?>(null);
        }

        public OsnapDiagnosticsSnapshot GetDiagnostics()
        {
            throw new NotSupportedException();
        }
    }

    private sealed class RecordingRenderingService : IRenderingService
    {
        public IReadOnlyList<DomainPoint3D>? LastPreviewPoints { get; private set; }
        public bool IsGridVisible { get; private set; } = true;

        public void Initialize(HelixViewport3D viewport)
        {
        }

        public void Render<T>(
            IEnumerable<T> items,
            IVisualBuilder<T> builder,
            string layerName)
        {
            if (!string.Equals(
                    layerName,
                    "Preview",
                    StringComparison.Ordinal))
            {
                return;
            }

            T? first = items.FirstOrDefault();
            if (first is IEnumerable<DomainPoint3D> points)
                LastPreviewPoints = points.ToArray();
        }

        public void Clear()
        {
            LastPreviewPoints = null;
        }

        public void ClearLayer(string layerKey)
        {
            if (string.Equals(
                    layerKey,
                    "Preview",
                    StringComparison.Ordinal))
            {
                LastPreviewPoints = null;
            }
        }

        public void SetGridSize(
            double widthMeters,
            double lengthMeters)
        {
        }

        public void SetGridSize(
            double widthMeters,
            double lengthMeters,
            double centerXMeters,
            double centerYMeters)
        {
        }

        public void SetGridVisible(bool isVisible)
        {
            IsGridVisible = isVisible;
        }

        public HelixViewport3D? GetViewport()
        {
            return null;
        }

        public void ZoomExtents()
        {
        }
    }

    private sealed class RecordingInteractionService :
        IViewportInteractionService
    {
        private readonly Stack<IInteractionMode> _modes = new();

        public IInteractionMode CurrentMode =>
            _modes.TryPeek(out IInteractionMode? mode)
                ? mode
                : null!;

        public List<Type> PushedModeTypes { get; } = new();

        public void PushMode(IInteractionMode mode)
        {
            _modes.Push(mode);
            PushedModeTypes.Add(mode.GetType());
        }

        public void PopMode()
        {
            if (_modes.Count > 0)
                _modes.Pop();
        }

        public void ResetToDefaultMode(IInteractionMode defaultMode)
        {
            _modes.Clear();
            _modes.Push(defaultMode);
        }

        public void HandleMouseDown(
            HelixViewport3D viewport,
            MouseButtonEventArgs e)
        {
        }

        public void HandleMouseMove(
            HelixViewport3D viewport,
            MouseEventArgs e)
        {
        }

        public void HandleMouseUp(
            HelixViewport3D viewport,
            MouseButtonEventArgs e)
        {
        }

        public void HandleKeyDown(
            HelixViewport3D viewport,
            KeyEventArgs e)
        {
        }
    }

    private sealed class StubViewportHitTestService :
        IViewportHitTestService
    {
        public bool TryGetWorldPoint(
            HelixViewport3D viewport,
            Point screenPt,
            out Point3D worldPoint)
        {
            worldPoint = default;
            return false;
        }

        public bool TryGetPlacementPoint(
            HelixViewport3D viewport,
            Point screenPt,
            out Point3D worldPoint)
        {
            worldPoint = default;
            return false;
        }

        public bool TryGetHitCrane(
            HelixViewport3D viewport,
            Point screenPt,
            out SceneCrane crane)
        {
            crane = null!;
            return false;
        }

        public object? GetHitObject(
            HelixViewport3D viewport,
            Point screenPt)
        {
            return null;
        }
    }

    private sealed class RecordingSceneService : ISceneService
    {
        private readonly List<PrecastElement> _elements = new();
        private readonly List<GreenAreaZone> _zones = new();
        private readonly List<SceneCrane> _cranes = new();
        private readonly List<SceneDxf> _dxf = new();

        public List<PrecastElement> GetElements() => _elements;

        public void SetElements(List<PrecastElement> elements)
        {
            _elements.Clear();
            _elements.AddRange(elements);
        }

        public void ClearElements() => _elements.Clear();
        public List<PrecastElement> GetSelectedElements() => new();

        public void SetSelectedElements(
            List<PrecastElement> elements)
        {
        }

        public void ClearSelectedElements()
        {
        }

        public event Action<List<PrecastElement>>? ElementsChanged
        {
            add { }
            remove { }
        }

        public event Action<List<PrecastElement>>? ElementsSelectionChanged
        {
            add { }
            remove { }
        }

        public List<GreenAreaZone> GetZones() => _zones;
        public void AddZone(GreenAreaZone zone) => _zones.Add(zone);
        public void RemoveZone(GreenAreaZone zone) => _zones.Remove(zone);

        public void RemoveSelectedZone()
        {
            if (SelectedZone != null)
                _zones.Remove(SelectedZone);
        }

        public void ClearZones() => _zones.Clear();

        public void UpdateZone(GreenAreaZone zone)
        {
        }

        public GreenAreaZone? SelectedZone { get; private set; }

        public void SetSelectedZone(GreenAreaZone? zone)
        {
            SelectedZone = zone;
        }

        public void ClearZoneSelection()
        {
            SelectedZone = null;
        }

        public event Action<GreenAreaZone>? ZoneAdded
        {
            add { }
            remove { }
        }

        public event Action<GreenAreaZone>? ZoneRemoved
        {
            add { }
            remove { }
        }

        public event Action<GreenAreaZone>? ZoneUpdated
        {
            add { }
            remove { }
        }

        public event Action? ZonesCleared
        {
            add { }
            remove { }
        }

        public event Action<GreenAreaZone?>? ZoneSelected
        {
            add { }
            remove { }
        }

        public List<SceneCrane> GetCranes() => _cranes;
        public void AddCrane(SceneCrane crane) => _cranes.Add(crane);
        public void RemoveCrane(SceneCrane crane) => _cranes.Remove(crane);
        public void ClearCranes() => _cranes.Clear();

        public void MoveCrane(
            SceneCrane crane,
            double x,
            double y,
            double z)
        {
        }

        public void RotateCrane(SceneCrane crane, double angle)
        {
        }

        public void UpdateCrane(SceneCrane crane)
        {
        }

        public SceneCrane? SelectedCrane { get; private set; }

        public void SetSelectedCrane(SceneCrane? crane)
        {
            SelectedCrane = crane;
        }

        public void ClearCraneSelection()
        {
            SelectedCrane = null;
        }

        public event Action<SceneCrane>? CraneAdded
        {
            add { }
            remove { }
        }

        public event Action<SceneCrane>? CraneRemoved
        {
            add { }
            remove { }
        }

        public event Action? CranesCleared
        {
            add { }
            remove { }
        }

        public event Action<SceneCrane>? CraneMoved
        {
            add { }
            remove { }
        }

        public event Action<SceneCrane>? CraneRotated
        {
            add { }
            remove { }
        }

        public event Action<SceneCrane>? CraneUpdated
        {
            add { }
            remove { }
        }

        public event Action<SceneCrane?>? CraneSelected
        {
            add { }
            remove { }
        }

        public List<SceneDxf> GetDxfEntities() => _dxf;
        public void AddDxfEntity(SceneDxf entity) => _dxf.Add(entity);
        public void RemoveDxfEntity(SceneDxf entity) => _dxf.Remove(entity);

        public void RemoveSelectedDxfEntity()
        {
            if (SelectedDxfEntity != null)
                _dxf.Remove(SelectedDxfEntity);
        }

        public void ClearDxfEntities() => _dxf.Clear();

        public void UpdateDxfEntity(SceneDxf entity)
        {
        }

        public SceneDxf? SelectedDxfEntity { get; private set; }

        public void SetSelectedDxfEntity(SceneDxf? entity)
        {
            SelectedDxfEntity = entity;
        }

        public void ClearDxfSelection()
        {
            SelectedDxfEntity = null;
        }

        public event Action<SceneDxf>? DxfEntityAdded
        {
            add { }
            remove { }
        }

        public event Action<SceneDxf>? DxfEntityRemoved
        {
            add { }
            remove { }
        }

        public event Action<SceneDxf>? DxfEntityUpdated
        {
            add { }
            remove { }
        }

        public event Action? DxfEntitiesCleared
        {
            add { }
            remove { }
        }

        public event Action<SceneDxf?>? DxfEntitySelected
        {
            add { }
            remove { }
        }

        public object? GetSelectedObject()
        {
            return SelectedZone ??
                   (object?)SelectedCrane ??
                   SelectedDxfEntity;
        }

        public void SetSelectedObject(object? obj)
        {
        }

        public event Action<object?>? SelectionChanged
        {
            add { }
            remove { }
        }
    }
}
