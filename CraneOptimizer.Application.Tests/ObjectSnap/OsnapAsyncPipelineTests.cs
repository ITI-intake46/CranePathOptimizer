using CraneOptimizer.UI.Features.F15_ObjectSnap.Models;
using CraneOptimizer.UI.Features.F15_ObjectSnap.Services;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Xunit.Abstractions;

namespace CraneOptimizer.Application.Tests.ObjectSnap;

public sealed class OsnapAsyncPipelineTests
{
    private static readonly Point3D SegmentStart = new(0, 1, 0);
    private static readonly Point3D SegmentEnd = new(4, 1, 0);
    private static readonly Point3D Target = new(2, 1, 0);
    private readonly ITestOutputHelper _output;

    public OsnapAsyncPipelineTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(SnapGeometryKind.Dxf, false)]
    [InlineData(SnapGeometryKind.Dxf, true)]
    [InlineData(SnapGeometryKind.Ifc, false)]
    [InlineData(SnapGeometryKind.Ifc, true)]
    [InlineData(SnapGeometryKind.CompletedZone, false)]
    [InlineData(SnapGeometryKind.CompletedZone, true)]
    [InlineData(SnapGeometryKind.ActiveZone, false)]
    [InlineData(SnapGeometryKind.ActiveZone, true)]
    public async Task SnapAsync_ReturnsExactWorldAndScreenPoint_ForEveryGeometrySource(
        SnapGeometryKind geometryKind,
        bool useMidpoint)
    {
        OsnapService service = CreateEnabledService();
        OsnapSource expectedSource = await PublishGeometryAsync(
            service,
            geometryKind,
            useMidpoint);
        OsnapModeMask mode = useMidpoint
            ? OsnapModeMask.Midpoint
            : OsnapModeMask.Endpoint;
        Point expectedScreen = Project(Target);
        OsnapQuery query = CreateQuery(
            service,
            requestId: 1,
            isClick: false,
            expectedScreen,
            mode);

        OsnapResult? result = await service.SnapAsync(
            query,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(
            useMidpoint ? OsnapType.Midpoint : OsnapType.Endpoint,
            result.SnapType);
        Assert.Equal(expectedSource, result.Source);
        AssertPoint(Target, result.WorldPoint);
        Assert.True(result.ScreenPoint.HasValue);
        AssertScreenPoint(expectedScreen, result.ScreenPoint.Value);
    }

    [Fact]
    public async Task SnapAsync_IntersectionIsExactAndAllHeavyStagesRunWithoutSynchronizationContext()
    {
        OsnapService service = CreateEnabledService();
        await service.SetIfcSnapGeometryAsync(
            new[]
            {
                new IfcSnapGeometry(
                    "IFC-1",
                    new[] { new Point3D(8, 8, 0) },
                    Array.Empty<(Point3D A, Point3D B)>())
            });
        await service.SetCompletedZoneGeometryAsync(
            new[]
            {
                new OsnapZoneGeometry(
                    new[]
                    {
                        new Point3D(-8, -8, 0),
                        new Point3D(-7, -8, 0)
                    },
                    IsClosed: false)
            });
        await service.SetTransientZoneGeometryAsync(
            drawingSessionVersion: 12,
            new[]
            {
                new Point3D(8, -8, 0),
                new Point3D(9, -8, 0)
            });
        await service.SetDxfSnapPointsAsync(
            Array.Empty<Point3D>(),
            new[]
            {
                (
                    new Point3D(-4, 0, 0),
                    new Point3D(4, 0, 0)),
                (
                    new Point3D(0, -4, 0),
                    new Point3D(0, 4, 0))
            },
            Array.Empty<Point3D>(),
            Array.Empty<Point3D>(),
            Array.Empty<(Point3D A, Point3D B)>());

        Point intersectionScreen = Project(new Point3D(0, 0, 0));
        OsnapQuery query = CreateQuery(
            service,
            requestId: 2,
            isClick: false,
            intersectionScreen,
            OsnapModeMask.Intersection);

        OsnapResult? result = await service.SnapAsync(
            query,
            CancellationToken.None);
        OsnapDiagnosticsSnapshot diagnostics = service.GetDiagnostics();

        Assert.NotNull(result);
        Assert.Equal(OsnapType.Intersection, result.SnapType);
        Assert.Equal(OsnapSource.Dxf, result.Source);
        AssertPoint(new Point3D(0, 0, 0), result.WorldPoint);
        Assert.True(result.ScreenPoint.HasValue);
        AssertScreenPoint(intersectionScreen, result.ScreenPoint.Value);

        Assert.Equal(4, diagnostics.IndexBuildCount);
        Assert.Equal(1, diagnostics.QueryCount);
        Assert.Equal(0, diagnostics.CancelledQueryCount);
        Assert.Equal(0, diagnostics.RejectedStaleQueryCount);
        Assert.NotEqual(0, diagnostics.LastIfcBuildThreadId);
        Assert.NotEqual(0, diagnostics.LastDxfBuildThreadId);
        Assert.NotEqual(0, diagnostics.LastZoneBuildThreadId);
        Assert.NotEqual(0, diagnostics.LastActiveZoneBuildThreadId);
        Assert.NotEqual(0, diagnostics.LastQueryThreadId);
        Assert.Equal(
            diagnostics.LastQueryThreadId,
            diagnostics.LastIntersectionThreadId);
        Assert.False(diagnostics.LastIfcBuildHadSynchronizationContext);
        Assert.False(diagnostics.LastDxfBuildHadSynchronizationContext);
        Assert.False(diagnostics.LastZoneBuildHadSynchronizationContext);
        Assert.False(
            diagnostics.LastActiveZoneBuildHadSynchronizationContext);
        Assert.False(diagnostics.LastQueryHadSynchronizationContext);
        Assert.Equal(0, diagnostics.LastPointCandidateCount);
        Assert.InRange(diagnostics.LastSegmentCandidateCount, 2, 64);
        Assert.True(diagnostics.LastQueryMilliseconds >= 0);
        Assert.True(diagnostics.MeanQueryMilliseconds >= 0);
        Assert.True(diagnostics.P95QueryMilliseconds >= 0);
    }

    [Fact]
    public async Task SnapAsync_RejectsStaleGeometryAndSettingsVersions()
    {
        OsnapService service = CreateEnabledService();
        Point3D oldPoint = new(-2, 0, 0);
        Point3D currentPoint = new(3, 0, 0);
        await PublishDxfEndpointAsync(service, oldPoint);
        OsnapQuery staleGeometryQuery = CreateQuery(
            service,
            requestId: 3,
            isClick: false,
            Project(oldPoint),
            OsnapModeMask.Endpoint);

        await PublishDxfEndpointAsync(service, currentPoint);

        OsnapResult? staleGeometryResult = await service.SnapAsync(
            staleGeometryQuery,
            CancellationToken.None);
        Assert.Null(staleGeometryResult);

        OsnapQuery staleSettingsQuery = CreateQuery(
            service,
            requestId: 4,
            isClick: false,
            Project(currentPoint),
            OsnapModeMask.Endpoint);
        service.SetSnapRadius(40);

        OsnapResult? staleSettingsResult = await service.SnapAsync(
            staleSettingsQuery,
            CancellationToken.None);
        Assert.Null(staleSettingsResult);

        OsnapQuery currentQuery = CreateQuery(
            service,
            requestId: 5,
            isClick: true,
            Project(currentPoint),
            OsnapModeMask.Endpoint);
        OsnapResult? currentResult = await service.SnapAsync(
            currentQuery,
            CancellationToken.None);
        OsnapDiagnosticsSnapshot diagnostics = service.GetDiagnostics();

        Assert.NotNull(currentResult);
        AssertPoint(currentPoint, currentResult.WorldPoint);
        Assert.Equal(2, diagnostics.RejectedStaleQueryCount);
        Assert.Equal(3, diagnostics.QueryCount);
    }

    [Fact]
    public async Task PointerCoordinator_KeepsOnlyLatestHoverAndBoundsConcurrency()
    {
        TaskCompletionSource<bool> firstStarted = NewSignal();
        TaskCompletionSource<bool> firstCancellationObserved = NewSignal();
        TaskCompletionSource<bool> releaseFirstCancellation = NewSignal();
        DelegatingOsnapService service = new(
            async (query, cancellationToken) =>
            {
                if (query.RequestId == 10)
                {
                    firstStarted.TrySetResult(true);
                    try
                    {
                        await Task.Delay(
                            Timeout.InfiniteTimeSpan,
                            cancellationToken);
                    }
                    catch (OperationCanceledException)
                        when (cancellationToken.IsCancellationRequested)
                    {
                        firstCancellationObserved.TrySetResult(true);
                        await releaseFirstCancellation.Task;
                        throw;
                    }
                }

                return ResultAtCursor(query, OsnapSource.Zone);
            });
        using OsnapPointerCoordinator coordinator = new(
            service,
            TimeSpan.Zero);
        TaskCompletionSource<OsnapQueryCompletion> latestCompletion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.HoverCompleted += completion =>
            latestCompletion.TrySetResult(completion);

        coordinator.SubmitHover(CreateQuery(
            service,
            requestId: 10,
            isClick: false,
            new Point(490, 500),
            OsnapModeMask.Endpoint));
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        coordinator.SubmitHover(CreateQuery(
            service,
            requestId: 11,
            isClick: false,
            new Point(500, 500),
            OsnapModeMask.Endpoint));
        await firstCancellationObserved.Task.WaitAsync(
            TimeSpan.FromSeconds(5));
        coordinator.SubmitHover(CreateQuery(
            service,
            requestId: 12,
            isClick: false,
            new Point(510, 500),
            OsnapModeMask.Endpoint));
        releaseFirstCancellation.TrySetResult(true);

        OsnapQueryCompletion completion = await latestCompletion.Task.WaitAsync(
            TimeSpan.FromSeconds(5));
        await WaitUntilAsync(
            () =>
            {
                OsnapCoordinatorDiagnostics snapshot =
                    coordinator.GetDiagnostics();
                return snapshot.ActiveHoverCount == 0 &&
                       snapshot.PendingHoverCount == 0;
            });
        OsnapCoordinatorDiagnostics diagnostics =
            coordinator.GetDiagnostics();

        Assert.Equal(12, completion.Query.RequestId);
        Assert.Equal(
            new long[] { 10, 12 },
            service.StartedRequestIds.ToArray());
        Assert.Equal(3, diagnostics.SubmittedHoverCount);
        Assert.Equal(1, diagnostics.CompletedHoverCount);
        Assert.Equal(1, diagnostics.CancelledHoverCount);
        Assert.Equal(1, diagnostics.DroppedPendingHoverCount);
        Assert.Equal(0, diagnostics.ActiveHoverCount);
        Assert.Equal(0, diagnostics.PendingHoverCount);
        Assert.Equal(1, diagnostics.MaximumConcurrentHoverCount);
    }

    [Fact]
    public async Task PointerCoordinator_ReusesCurrentHoverForClickButNotStaleMetadata()
    {
        DelegatingOsnapService service = new(
            (query, _) => Task.FromResult<OsnapResult?>(
                ResultAtCursor(query, OsnapSource.Ifc)));
        using OsnapPointerCoordinator coordinator = new(
            service,
            TimeSpan.Zero);
        TaskCompletionSource<OsnapQueryCompletion> hoverCompletion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.HoverCompleted += completion =>
            hoverCompletion.TrySetResult(completion);

        OsnapQuery hover = CreateQuery(
            service,
            requestId: 20,
            isClick: false,
            new Point(500, 500),
            OsnapModeMask.Endpoint);
        coordinator.SubmitHover(hover);
        await hoverCompletion.Task.WaitAsync(TimeSpan.FromSeconds(5));

        OsnapQuery matchingClick = CreateQuery(
            service,
            requestId: 21,
            isClick: true,
            new Point(501, 500),
            OsnapModeMask.Endpoint);
        OsnapQueryCompletion reused = await coordinator.ResolveClickAsync(
            matchingClick,
            hoverCursorTolerancePixels: 3,
            CancellationToken.None);

        Assert.Equal(1, service.SnapCallCount);
        Assert.Equal(21, reused.Query.RequestId);
        Assert.NotNull(reused.SnapResult);
        Assert.Equal(OsnapSource.Ifc, reused.SnapResult.Source);
        Assert.Equal(0, reused.ElapsedMilliseconds);

        OsnapViewportSnapshot staleViewport = matchingClick.Viewport with
        {
            SettingsVersion = matchingClick.Viewport.SettingsVersion + 1
        };
        var metadataChangedClick = new OsnapQuery(
            RequestId: 22,
            IsClick: true,
            staleViewport);
        OsnapQueryCompletion recomputed =
            await coordinator.ResolveClickAsync(
                metadataChangedClick,
                hoverCursorTolerancePixels: 3,
                CancellationToken.None);

        Assert.Equal(2, service.SnapCallCount);
        Assert.Equal(22, recomputed.Query.RequestId);
        Assert.NotNull(recomputed.SnapResult);
    }

    [Fact]
    public async Task LargeBuildAndIntersectionQueries_KeepDispatcherHeartbeatResponsive()
    {
        LargeGeometryFixture geometry = CreateLargeGeometryFixture();

        DispatcherResponsivenessResult responsiveness =
            await RunOnDispatcherAsync(
                async () =>
                {
                    OsnapService service = CreateEnabledService();
                    int heartbeatCount = 0;
                    long lastHeartbeatTimestamp = 0;
                    double maximumHeartbeatGapMilliseconds = 0;
                    bool isWorkInFlight = false;
                    var heartbeat = new DispatcherTimer(
                        TimeSpan.FromMilliseconds(5),
                        DispatcherPriority.Send,
                        (_, _) =>
                        {
                            if (!isWorkInFlight)
                                return;

                            long now = Stopwatch.GetTimestamp();
                            if (lastHeartbeatTimestamp != 0)
                            {
                                maximumHeartbeatGapMilliseconds = Math.Max(
                                    maximumHeartbeatGapMilliseconds,
                                    Stopwatch.GetElapsedTime(
                                            lastHeartbeatTimestamp,
                                            now).TotalMilliseconds);
                            }

                            lastHeartbeatTimestamp = now;
                            heartbeatCount++;
                        },
                        Dispatcher.CurrentDispatcher);

                    heartbeat.Start();
                    isWorkInFlight = true;
                    var workload = Stopwatch.StartNew();
                    Task ifcBuild = service.SetIfcSnapGeometryAsync(
                        geometry.Ifc);
                    Task dxfBuild = service.SetDxfSnapPointsAsync(
                        geometry.DxfEndpoints,
                        geometry.DxfSegments,
                        Array.Empty<Point3D>(),
                        Array.Empty<Point3D>(),
                        geometry.DxfSegments);
                    Task zoneBuild = service.SetCompletedZoneGeometryAsync(
                        geometry.CompletedZones);
                    await Task.WhenAll(ifcBuild, dxfBuild, zoneBuild);

                    for (int index = 0; index < 32; index++)
                    {
                        Point cursor = new(
                            500 + (index % 3) - 1,
                            500 + (index % 5) - 2);
                        OsnapQuery query = CreateQuery(
                            service,
                            requestId: 10_000 + index,
                            isClick: false,
                            cursor,
                            OsnapModeMask.Intersection,
                            cameraVersion: 100 + index);
                        await service.SnapAsync(
                            query,
                            CancellationToken.None);
                    }

                    workload.Stop();
                    isWorkInFlight = false;
                    heartbeat.Stop();
                    OsnapDiagnosticsSnapshot diagnostics =
                        service.GetDiagnostics();
                    return new DispatcherResponsivenessResult(
                        heartbeatCount,
                        maximumHeartbeatGapMilliseconds,
                        workload.Elapsed.TotalMilliseconds,
                        diagnostics.P95QueryMilliseconds,
                        diagnostics.IndexBuildCount,
                        diagnostics.QueryCount,
                        diagnostics.LastIfcBuildHadSynchronizationContext,
                        diagnostics.LastDxfBuildHadSynchronizationContext,
                        diagnostics.LastZoneBuildHadSynchronizationContext,
                        diagnostics.LastQueryHadSynchronizationContext);
                });

        _output.WriteLine(
            "Dispatcher workload {0:F2} ms; heartbeats {1}; " +
            "max heartbeat gap {2:F2} ms; query p95 {3:F3} ms.",
            responsiveness.WorkloadMilliseconds,
            responsiveness.HeartbeatCount,
            responsiveness.MaximumHeartbeatGapMilliseconds,
            responsiveness.QueryP95Milliseconds);

        Assert.Equal(3, responsiveness.IndexBuildCount);
        Assert.Equal(32, responsiveness.QueryCount);
        Assert.True(
            responsiveness.WorkloadMilliseconds >= 10,
            "The representative workload finished too quickly to exercise " +
            "the Dispatcher heartbeat.");
        Assert.True(
            responsiveness.HeartbeatCount >= 2,
            "The Dispatcher heartbeat did not run while Object Snap work " +
            "was in flight.");
        Assert.InRange(
            responsiveness.MaximumHeartbeatGapMilliseconds,
            0,
            750);
        Assert.InRange(
            responsiveness.QueryP95Milliseconds,
            0,
            250);
        Assert.False(responsiveness.IfcBuildHadSynchronizationContext);
        Assert.False(responsiveness.DxfBuildHadSynchronizationContext);
        Assert.False(responsiveness.ZoneBuildHadSynchronizationContext);
        Assert.False(responsiveness.QueryHadSynchronizationContext);
    }

    [Fact]
    public async Task RepeatedPointerAndCameraSnapshots_DoNotRebuildGeometryIndex()
    {
        OsnapService service = CreateEnabledService();
        var endpoints = new Point3D[4_097];
        endpoints[0] = new Point3D(0, 0, 0);
        for (int index = 1; index < endpoints.Length; index++)
        {
            endpoints[index] = new Point3D(
                30 + index % 128,
                30 + index / 128,
                0);
        }

        await service.SetDxfSnapPointsAsync(
            endpoints,
            new[]
            {
                (
                    new Point3D(-8, 0, 0),
                    new Point3D(8, 0, 0)),
                (
                    new Point3D(0, -8, 0),
                    new Point3D(0, 8, 0))
            },
            Array.Empty<Point3D>(),
            Array.Empty<Point3D>(),
            Array.Empty<(Point3D A, Point3D B)>());
        OsnapDiagnosticsSnapshot before = service.GetDiagnostics();
        OsnapGeometryVersionState versionsBefore = service.GeometryVersions;
        int cacheCountBefore = service.SnapCacheBuiltCount;

        const int queryCount = 200;
        for (int index = 0; index < queryCount; index++)
        {
            Point cursor = new(
                500 + index % 7 - 3,
                500 + index % 5 - 2);
            OsnapQuery query = CreateQuery(
                service,
                requestId: 20_000 + index,
                isClick: false,
                cursor,
                OsnapModeMask.Endpoint |
                OsnapModeMask.Intersection,
                cameraVersion: 1_000 + index);
            OsnapResult? result = await service.SnapAsync(
                query,
                CancellationToken.None);
            Assert.NotNull(result);
        }

        OsnapDiagnosticsSnapshot after = service.GetDiagnostics();
        OsnapGeometryVersionState versionsAfter = service.GeometryVersions;
        _output.WriteLine(
            "{0} camera/pointer-only queries: mean {1:F3} ms; " +
            "p95 {2:F3} ms; last {3:F3} ms; index builds {4}.",
            queryCount,
            after.MeanQueryMilliseconds,
            after.P95QueryMilliseconds,
            after.LastQueryMilliseconds,
            after.IndexBuildCount);

        Assert.Equal(before.IndexBuildCount, after.IndexBuildCount);
        Assert.Equal(1, after.IndexBuildCount);
        Assert.Equal(cacheCountBefore, service.SnapCacheBuiltCount);
        Assert.Equal(versionsBefore, versionsAfter);
        Assert.Equal(
            before.QueryCount + queryCount,
            after.QueryCount);
        Assert.InRange(after.LastPointCandidateCount, 1, 48);
        Assert.InRange(after.LastSegmentCandidateCount, 2, 64);
        Assert.InRange(after.P95QueryMilliseconds, 0, 250);
    }

    [Fact]
    public async Task PointerCoordinator_BurstKeepsPendingWorkAndAllocationsBounded()
    {
        TaskCompletionSource<bool> firstStarted = NewSignal();
        TaskCompletionSource<bool> firstCancellationObserved = NewSignal();
        TaskCompletionSource<bool> releaseFirstCancellation = NewSignal();
        DelegatingOsnapService service = new(
            async (query, cancellationToken) =>
            {
                if (query.RequestId == 30_000)
                {
                    firstStarted.TrySetResult(true);
                    try
                    {
                        await Task.Delay(
                            Timeout.InfiniteTimeSpan,
                            cancellationToken);
                    }
                    catch (OperationCanceledException)
                        when (cancellationToken.IsCancellationRequested)
                    {
                        firstCancellationObserved.TrySetResult(true);
                        await releaseFirstCancellation.Task;
                        throw;
                    }
                }

                return ResultAtCursor(query, OsnapSource.Dxf);
            });
        using OsnapPointerCoordinator coordinator = new(
            service,
            TimeSpan.Zero);
        TaskCompletionSource<OsnapQueryCompletion> latestCompletion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.HoverCompleted += completion =>
            latestCompletion.TrySetResult(completion);

        coordinator.SubmitHover(CreateQuery(
            service,
            requestId: 30_000,
            isClick: false,
            new Point(500, 500),
            OsnapModeMask.Endpoint));
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        const int burstCount = 1_000;
        var burst = new OsnapQuery[burstCount];
        for (int index = 0; index < burst.Length; index++)
        {
            burst[index] = CreateQuery(
                service,
                requestId: 30_001 + index,
                isClick: false,
                new Point(500 + index % 3, 500),
                OsnapModeMask.Endpoint,
                cameraVersion: 2_000 + index);
        }

        coordinator.SubmitHover(burst[0]);
        await firstCancellationObserved.Task.WaitAsync(
            TimeSpan.FromSeconds(5));
        long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 1; index < burst.Length; index++)
            coordinator.SubmitHover(burst[index]);
        long submissionAllocation =
            GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
        OsnapCoordinatorDiagnostics whileBlocked =
            coordinator.GetDiagnostics();

        Assert.Equal(1, whileBlocked.ActiveHoverCount);
        Assert.Equal(1, whileBlocked.PendingHoverCount);
        Assert.Equal(1, whileBlocked.MaximumConcurrentHoverCount);

        releaseFirstCancellation.TrySetResult(true);
        OsnapQueryCompletion completion = await latestCompletion.Task.WaitAsync(
            TimeSpan.FromSeconds(5));
        await WaitUntilAsync(
            () =>
            {
                OsnapCoordinatorDiagnostics snapshot =
                    coordinator.GetDiagnostics();
                return snapshot.ActiveHoverCount == 0 &&
                       snapshot.PendingHoverCount == 0;
            });
        OsnapCoordinatorDiagnostics completed =
            coordinator.GetDiagnostics();
        long expectedLatestRequestId = 30_000 + burstCount;
        _output.WriteLine(
            "{0} hover submissions allocated {1:N0} bytes on the " +
            "submitting thread; dropped {2}; max concurrent {3}.",
            burstCount,
            submissionAllocation,
            completed.DroppedPendingHoverCount,
            completed.MaximumConcurrentHoverCount);

        Assert.Equal(expectedLatestRequestId, completion.Query.RequestId);
        Assert.Equal(
            new long[] { 30_000, expectedLatestRequestId },
            service.StartedRequestIds.ToArray());
        Assert.Equal(
            burstCount + 1,
            completed.SubmittedHoverCount);
        Assert.Equal(1, completed.CompletedHoverCount);
        Assert.Equal(1, completed.CancelledHoverCount);
        Assert.Equal(burstCount - 1, completed.DroppedPendingHoverCount);
        Assert.Equal(0, completed.ActiveHoverCount);
        Assert.Equal(0, completed.PendingHoverCount);
        Assert.Equal(1, completed.MaximumConcurrentHoverCount);
        Assert.InRange(submissionAllocation, 0, 2 * 1024 * 1024);
    }

    private static OsnapService CreateEnabledService()
    {
        return new OsnapService(
            new OsnapSettings
            {
                MasterEnabled = true,
                SnapEndpoint = true,
                SnapMidpoint = true,
                SnapIntersection = true,
                SnapCenter = false,
                SnapQuadrant = false,
                SnapToDxf = true,
                SnapToIfc = true,
                SnapRadiusPx = 25,
                MaxEdgesForIntersection = 64
            });
    }

    private static async Task<OsnapSource> PublishGeometryAsync(
        OsnapService service,
        SnapGeometryKind geometryKind,
        bool useMidpoint)
    {
        (Point3D A, Point3D B) segment = (SegmentStart, SegmentEnd);
        switch (geometryKind)
        {
            case SnapGeometryKind.Dxf:
                await service.SetDxfSnapPointsAsync(
                    useMidpoint
                        ? Array.Empty<Point3D>()
                        : new[] { Target },
                    Array.Empty<(Point3D A, Point3D B)>(),
                    Array.Empty<Point3D>(),
                    Array.Empty<Point3D>(),
                    useMidpoint
                        ? new[] { segment }
                        : Array.Empty<(Point3D A, Point3D B)>());
                return OsnapSource.Dxf;

            case SnapGeometryKind.Ifc:
                await service.SetIfcSnapGeometryAsync(
                    new[]
                    {
                        new IfcSnapGeometry(
                            "IFC-SNAP",
                            useMidpoint
                                ? new[] { SegmentStart, SegmentEnd }
                                : new[] { Target },
                            useMidpoint
                                ? new[] { segment }
                                : Array.Empty<(Point3D A, Point3D B)>())
                    });
                return OsnapSource.Ifc;

            case SnapGeometryKind.CompletedZone:
                await service.SetCompletedZoneGeometryAsync(
                    new[]
                    {
                        new OsnapZoneGeometry(
                            useMidpoint
                                ? new[] { SegmentStart, SegmentEnd }
                                : new[] { Target, SegmentEnd },
                            IsClosed: false)
                    });
                return OsnapSource.Zone;

            case SnapGeometryKind.ActiveZone:
                await service.SetTransientZoneGeometryAsync(
                    drawingSessionVersion: 42,
                    useMidpoint
                        ? new[] { SegmentStart, SegmentEnd }
                        : new[] { Target, SegmentEnd });
                return OsnapSource.Zone;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(geometryKind),
                    geometryKind,
                    null);
        }
    }

    private static Task PublishDxfEndpointAsync(
        OsnapService service,
        Point3D endpoint)
    {
        return service.SetDxfSnapPointsAsync(
            new[] { endpoint },
            Array.Empty<(Point3D A, Point3D B)>(),
            Array.Empty<Point3D>(),
            Array.Empty<Point3D>(),
            Array.Empty<(Point3D A, Point3D B)>());
    }

    private static OsnapQuery CreateQuery(
        IOsnapService service,
        long requestId,
        bool isClick,
        Point cursor,
        OsnapModeMask enabledModes,
        long cameraVersion = 7)
    {
        OsnapGeometryVersionState geometryVersions =
            service.GeometryVersions;
        var viewport = new OsnapViewportSnapshot(
            OsnapCameraType.Orthographic,
            new Point3D(0, 0, 10),
            new Vector3D(0, 0, -1),
            new Vector3D(0, 1, 0),
            FieldOfViewOrWidth: 20,
            ViewportWidth: 1000,
            ViewportHeight: 1000,
            ScreenOffsetX: 0,
            ScreenOffsetY: 0,
            DpiScaleX: 1,
            DpiScaleY: 1,
            CameraVersion: cameraVersion,
            GeometryVersion: geometryVersions.Aggregate,
            geometryVersions.Sources,
            SettingsVersion: service.SettingsVersion,
            DrawingSessionVersion: 42,
            CursorScreenPoint: cursor,
            new OsnapReferencePlaneSnapshot(
                new Point3D(0, 0, 0),
                new Vector3D(0, 0, 1)),
            EnabledModes: enabledModes,
            SnapRadiusPx: service.Settings.SnapRadiusPx,
            MaxIntersectionCandidates: 64,
            SnapToDxf: true,
            SnapToIfc: true);
        return new OsnapQuery(requestId, isClick, viewport);
    }

    private static Point Project(Point3D world)
    {
        return new Point(
            500 + world.X * 50,
            500 - world.Y * 50);
    }

    private static OsnapResult ResultAtCursor(
        OsnapQuery query,
        OsnapSource source)
    {
        Point cursor = query.Viewport.CursorScreenPoint;
        return new OsnapResult(
            OsnapType.Endpoint,
            new Point3D(
                (cursor.X - 500) / 50,
                (500 - cursor.Y) / 50,
                0),
            cursor,
            source);
    }

    private static void AssertPoint(Point3D expected, Point3D actual)
    {
        Assert.Equal(expected.X, actual.X, precision: 9);
        Assert.Equal(expected.Y, actual.Y, precision: 9);
        Assert.Equal(expected.Z, actual.Z, precision: 9);
    }

    private static void AssertScreenPoint(Point expected, Point actual)
    {
        Assert.Equal(expected.X, actual.X, precision: 9);
        Assert.Equal(expected.Y, actual.Y, precision: 9);
    }

    private static TaskCompletionSource<bool> NewSignal()
    {
        return new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!predicate() && stopwatch.Elapsed < TimeSpan.FromSeconds(5))
            await Task.Delay(10);

        Assert.True(predicate(), "Timed out waiting for Object Snap work.");
    }

    private static LargeGeometryFixture CreateLargeGeometryFixture()
    {
        const int ifcElementCount = 2_500;
        var ifc = new IfcSnapGeometry[ifcElementCount];
        for (int index = 0; index < ifc.Length; index++)
        {
            double x = (index % 50 - 25) * 4.0;
            double y = (index / 50 - 25) * 4.0;
            var vertices = new[]
            {
                new Point3D(x - 0.6, y - 0.6, 0),
                new Point3D(x + 0.6, y - 0.6, 0),
                new Point3D(x + 0.6, y + 0.6, 0),
                new Point3D(x - 0.6, y + 0.6, 0),
                new Point3D(x - 0.6, y - 0.6, 1.2),
                new Point3D(x + 0.6, y - 0.6, 1.2),
                new Point3D(x + 0.6, y + 0.6, 1.2),
                new Point3D(x - 0.6, y + 0.6, 1.2)
            };
            var segments = new[]
            {
                (vertices[0], vertices[1]),
                (vertices[1], vertices[2]),
                (vertices[2], vertices[3]),
                (vertices[3], vertices[0]),
                (vertices[4], vertices[5]),
                (vertices[5], vertices[6]),
                (vertices[6], vertices[7]),
                (vertices[7], vertices[4]),
                (vertices[0], vertices[4]),
                (vertices[1], vertices[5]),
                (vertices[2], vertices[6]),
                (vertices[3], vertices[7])
            };
            ifc[index] = new IfcSnapGeometry(
                $"IFC-LARGE-{index}",
                vertices,
                segments);
        }

        const int dxfEndpointCount = 20_000;
        var dxfEndpoints = new Point3D[dxfEndpointCount];
        for (int index = 0; index < dxfEndpoints.Length; index++)
        {
            dxfEndpoints[index] = new Point3D(
                30 + index % 200,
                30 + index / 200,
                0);
        }

        const int dxfSegmentCount = 12_000;
        var dxfSegments =
            new (Point3D A, Point3D B)[dxfSegmentCount];
        dxfSegments[0] = (
            new Point3D(-8, 0, 0),
            new Point3D(8, 0, 0));
        dxfSegments[1] = (
            new Point3D(0, -8, 0),
            new Point3D(0, 8, 0));
        for (int index = 2; index < dxfSegments.Length; index++)
        {
            double x = 30 + index % 200;
            double y = 30 + index / 200;
            dxfSegments[index] = (
                new Point3D(x, y, 0),
                new Point3D(x + 0.75, y + 0.25, 0));
        }

        const int zoneCount = 600;
        const int zoneVertexCount = 24;
        var zones = new OsnapZoneGeometry[zoneCount];
        for (int zoneIndex = 0;
             zoneIndex < zones.Length;
             zoneIndex++)
        {
            double centerX = 35 + zoneIndex % 30 * 5.0;
            double centerY = 35 + zoneIndex / 30 * 5.0;
            var vertices = new Point3D[zoneVertexCount];
            for (int vertexIndex = 0;
                 vertexIndex < vertices.Length;
                 vertexIndex++)
            {
                double angle =
                    vertexIndex * Math.PI * 2.0 / vertices.Length;
                vertices[vertexIndex] = new Point3D(
                    centerX + Math.Cos(angle) * 1.5,
                    centerY + Math.Sin(angle) * 1.5,
                    0);
            }

            zones[zoneIndex] = new OsnapZoneGeometry(
                vertices,
                IsClosed: true);
        }

        return new LargeGeometryFixture(
            ifc,
            dxfEndpoints,
            dxfSegments,
            zones);
    }

    private static async Task<T> RunOnDispatcherAsync<T>(
        Func<Task<T>> action)
    {
        var completion = new TaskCompletionSource<T>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(
            () =>
            {
                Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(dispatcher));
                T result = default!;
                Exception? exception = null;
                dispatcher.BeginInvoke(
                    DispatcherPriority.Normal,
                    new Action(
                        async () =>
                        {
                            try
                            {
                                result = await action();
                            }
                            catch (Exception ex)
                            {
                                exception = ex;
                            }
                            finally
                            {
                                dispatcher.BeginInvokeShutdown(
                                    DispatcherPriority.Send);
                            }
                        }));
                Dispatcher.Run();

                if (exception is null)
                    completion.TrySetResult(result);
                else
                    completion.TrySetException(exception);
            })
        {
            IsBackground = true,
            Name = "Object Snap Dispatcher Responsiveness Test"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return await completion.Task.WaitAsync(TimeSpan.FromSeconds(60));
    }

    private sealed record LargeGeometryFixture(
        IReadOnlyList<IfcSnapGeometry> Ifc,
        IReadOnlyList<Point3D> DxfEndpoints,
        IReadOnlyList<(Point3D A, Point3D B)> DxfSegments,
        IReadOnlyList<OsnapZoneGeometry> CompletedZones);

    private readonly record struct DispatcherResponsivenessResult(
        int HeartbeatCount,
        double MaximumHeartbeatGapMilliseconds,
        double WorkloadMilliseconds,
        double QueryP95Milliseconds,
        long IndexBuildCount,
        long QueryCount,
        bool IfcBuildHadSynchronizationContext,
        bool DxfBuildHadSynchronizationContext,
        bool ZoneBuildHadSynchronizationContext,
        bool QueryHadSynchronizationContext);

    public enum SnapGeometryKind
    {
        Dxf,
        Ifc,
        CompletedZone,
        ActiveZone
    }

    private sealed class DelegatingOsnapService : IOsnapService
    {
        private readonly Func<
            OsnapQuery,
            CancellationToken,
            Task<OsnapResult?>> _snap;
        private int _snapCallCount;

        public DelegatingOsnapService(
            Func<
                OsnapQuery,
                CancellationToken,
                Task<OsnapResult?>> snap)
        {
            _snap = snap;
        }

        public OsnapSettings Settings { get; } = new()
        {
            MasterEnabled = true,
            SnapRadiusPx = 25
        };

        public event Action<bool>? MasterEnabledChanged
        {
            add { }
            remove { }
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
                Aggregate: 1,
                new OsnapSourceVersionSnapshot(1, 1, 1, 1));

        public long SettingsVersion => 1;
        public OsnapModeMask EnabledModes => OsnapModeMask.Endpoint;
        public bool IsPreparing => false;
        public double SceneScale => 10;
        public int SnapCacheBuiltCount => 0;
        public int SnapPointCount => 0;
        public double SnapBuildDurationMs => 0;
        public double SnapMemoryDeltaMB => 0;
        public int SnapCallCount => Volatile.Read(ref _snapCallCount);

        public ConcurrentQueue<long> StartedRequestIds { get; } = new();

        public void SetMasterEnabled(bool enabled)
        {
            Settings.MasterEnabled = enabled;
        }

        public void SetSnapEnabled(OsnapType type, bool enabled)
        {
            throw new NotSupportedException();
        }

        public void SetSourceEnabled(OsnapSource source, bool enabled)
        {
            throw new NotSupportedException();
        }

        public void SetSnapRadius(double radiusPixels)
        {
            throw new NotSupportedException();
        }

        public void SetGridSpacing(double spacing)
        {
            throw new NotSupportedException();
        }

        public Task SetCompletedZoneGeometryAsync(
            IReadOnlyList<OsnapZoneGeometry> polygons,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SetDxfSnapPointsAsync(
            IReadOnlyList<Point3D> endpoints,
            IReadOnlyList<(Point3D A, Point3D B)> segments,
            IReadOnlyList<Point3D> centers,
            IReadOnlyList<Point3D> quadrants,
            IReadOnlyList<(Point3D A, Point3D B)> midpointSegments,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SetIfcSnapGeometryAsync(
            IReadOnlyList<IfcSnapGeometry> geometry,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SetTransientZoneGeometryAsync(
            long drawingSessionVersion,
            IReadOnlyList<Point3D> vertices,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public void ClearTransientZoneGeometry(long drawingSessionVersion)
        {
        }

        public void CancelPendingQueries()
        {
        }

        public async Task<OsnapResult?> SnapAsync(
            OsnapQuery query,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _snapCallCount);
            StartedRequestIds.Enqueue(query.RequestId);
            return await _snap(query, cancellationToken);
        }

        public OsnapDiagnosticsSnapshot GetDiagnostics()
        {
            throw new NotSupportedException();
        }
    }
}
