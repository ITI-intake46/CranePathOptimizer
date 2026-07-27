using CraneOptimizer.UI.Features.F15_ObjectSnap.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Services
{
    public sealed class OsnapService : IOsnapService
    {
        private const int MaxPointCandidatesPerSource = 48;
        private const int QueryLatencySampleCapacity = 256;
        private const double DistanceTieTolerancePx = 0.75;
        private const double DepthTieTolerance = 1e-6;

        private static readonly object CompatibilityDiagnosticsGate = new();
        private readonly object _buildGate = new();
        private readonly object _queryGate = new();
        private readonly object _diagnosticsGate = new();
        private readonly object _geometryEventGate = new();
        private readonly BuildSlot _ifcBuild = new();
        private readonly BuildSlot _dxfBuild = new();
        private readonly BuildSlot _zoneBuild = new();
        private readonly BuildSlot _activeZoneBuild = new();
        private readonly double[] _queryLatencySamples =
            new double[QueryLatencySampleCapacity];

        private IndexSet _indexes = IndexSet.Empty;
        private CancellationTokenSource _queryEpochCts = new();
        private long _settingsVersion = 1;
        private long _activeDrawingSessionVersion;
        private long _activeZoneMutationVersion;
        private int _isMasterEnabled;
        private int _preparingCount;
        private int _queryLatencySampleCount;
        private int _queryLatencySampleCursor;

        private long _indexBuildCount;
        private long _lastNotifiedGeometryVersion;
        private long _queryCount;
        private long _cancelledQueryCount;
        private long _rejectedStaleQueryCount;
        private int _lastIfcBuildThreadId;
        private int _lastDxfBuildThreadId;
        private int _lastZoneBuildThreadId;
        private int _lastActiveZoneBuildThreadId;
        private int _lastQueryThreadId;
        private int _lastIntersectionThreadId;
        private int _lastIfcBuildHadSynchronizationContext;
        private int _lastDxfBuildHadSynchronizationContext;
        private int _lastZoneBuildHadSynchronizationContext;
        private int _lastActiveZoneBuildHadSynchronizationContext;
        private int _lastQueryHadSynchronizationContext;
        private int _lastPointCandidateCount;
        private int _lastSegmentCandidateCount;
        private double _lastQueryMilliseconds;

        public OsnapService(OsnapSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _isMasterEnabled = settings.MasterEnabled ? 1 : 0;
            if (!settings.MasterEnabled)
                _queryEpochCts.Cancel();
        }

        public OsnapSettings Settings { get; }

        public event Action<bool>? MasterEnabledChanged;
        public event Action<long>? GeometryVersionChanged;
        public event Action<long>? SettingsVersionChanged;
        public event Action<bool>? PreparingChanged;

        public OsnapGeometryVersionState GeometryVersions
        {
            get
            {
                IndexSet indexes = Volatile.Read(ref _indexes);
                return new OsnapGeometryVersionState(
                    indexes.AggregateVersion,
                    indexes.SourceVersions);
            }
        }

        public long SettingsVersion => Interlocked.Read(ref _settingsVersion);

        public OsnapModeMask EnabledModes
        {
            get
            {
                if (Volatile.Read(ref _isMasterEnabled) == 0)
                    return OsnapModeMask.None;

                OsnapModeMask result = OsnapModeMask.None;
                if (Settings.SnapEndpoint)
                    result |= OsnapModeMask.Endpoint;
                if (Settings.SnapIntersection)
                    result |= OsnapModeMask.Intersection;
                if (Settings.SnapMidpoint)
                    result |= OsnapModeMask.Midpoint;
                if (Settings.SnapCenter)
                    result |= OsnapModeMask.Center;
                if (Settings.SnapQuadrant)
                    result |= OsnapModeMask.Quadrant;
                return result;
            }
        }

        public bool IsPreparing => Volatile.Read(ref _preparingCount) > 0;

        public double SceneScale
        {
            get
            {
                double extent = Volatile.Read(ref _indexes).Bounds.MaxExtent;
                return extent > 0 && double.IsFinite(extent) ? extent : 10.0;
            }
        }

        public int SnapCacheBuiltCount
        {
            get
            {
                IndexSet indexes = Volatile.Read(ref _indexes);
                int count = 0;
                if (HasGeometry(indexes.Ifc))
                    count++;
                if (HasGeometry(indexes.Dxf))
                    count++;
                if (HasGeometry(indexes.CompletedZones))
                    count++;
                if (HasGeometry(indexes.ActiveZone))
                    count++;
                return count;
            }
        }

        public int SnapPointCount
        {
            get
            {
                IndexSet indexes = Volatile.Read(ref _indexes);
                return indexes.Ifc.PointCount +
                       indexes.Dxf.PointCount +
                       indexes.CompletedZones.PointCount +
                       indexes.ActiveZone.PointCount;
            }
        }

        public double SnapBuildDurationMs { get; private set; }
        public double SnapMemoryDeltaMB { get; private set; }

        // Kept as compatibility diagnostics for the opt-in legacy benchmark.
        public static int SnapRebuildCount { get; private set; }
        public static double SnapRebuildTotalMs { get; private set; }
        public static double SnapRebuildMaxMs { get; private set; }
        public static int LastRebuildVerticesCount { get; private set; }
        public static int LastRebuildSegmentsCount { get; private set; }

        public static void ResetSnapDiagnostics()
        {
            lock (CompatibilityDiagnosticsGate)
            {
                SnapRebuildCount = 0;
                SnapRebuildTotalMs = 0;
                SnapRebuildMaxMs = 0;
                LastRebuildVerticesCount = 0;
                LastRebuildSegmentsCount = 0;
            }
        }

        public void SetMasterEnabled(bool enabled)
        {
            bool wasEnabled =
                Interlocked.Exchange(
                    ref _isMasterEnabled,
                    enabled ? 1 : 0) != 0;
            if (wasEnabled == enabled)
                return;

            Settings.MasterEnabled = enabled;
            IncrementSettingsVersionAndResetQueries(enabled);
            if (!enabled)
            {
                CancelAllBuilds();
                PublishAllEmpty();
            }

            MasterEnabledChanged?.Invoke(enabled);
        }

        public void SetSnapEnabled(OsnapType type, bool enabled)
        {
            bool changed = type switch
            {
                OsnapType.Endpoint => SetIfChanged(
                    Settings.SnapEndpoint,
                    enabled,
                    value => Settings.SnapEndpoint = value),
                OsnapType.Midpoint => SetIfChanged(
                    Settings.SnapMidpoint,
                    enabled,
                    value => Settings.SnapMidpoint = value),
                OsnapType.Intersection => SetIfChanged(
                    Settings.SnapIntersection,
                    enabled,
                    value => Settings.SnapIntersection = value),
                OsnapType.Center => SetIfChanged(
                    Settings.SnapCenter,
                    enabled,
                    value => Settings.SnapCenter = value),
                OsnapType.Quadrant => SetIfChanged(
                    Settings.SnapQuadrant,
                    enabled,
                    value => Settings.SnapQuadrant = value),
                OsnapType.Perpendicular => SetIfChanged(
                    Settings.SnapPerpendicular,
                    false,
                    value => Settings.SnapPerpendicular = value),
                OsnapType.Nearest => SetIfChanged(
                    Settings.SnapNearest,
                    false,
                    value => Settings.SnapNearest = value),
                OsnapType.Grid => SetIfChanged(
                    Settings.SnapGrid,
                    false,
                    value => Settings.SnapGrid = value),
                _ => false
            };

            if (changed)
                IncrementSettingsVersionAndResetQueries(IsMasterEnabled);
        }

        public void SetSourceEnabled(OsnapSource source, bool enabled)
        {
            bool changed = source switch
            {
                OsnapSource.Dxf => SetIfChanged(
                    Settings.SnapToDxf,
                    enabled,
                    value => Settings.SnapToDxf = value),
                OsnapSource.Ifc => SetIfChanged(
                    Settings.SnapToIfc,
                    enabled,
                    value => Settings.SnapToIfc = value),
                _ => false
            };

            if (changed)
                IncrementSettingsVersionAndResetQueries(IsMasterEnabled);
        }

        public void SetSnapRadius(double radiusPixels)
        {
            double safeRadius =
                double.IsFinite(radiusPixels) && radiusPixels > 0
                    ? radiusPixels
                    : Settings.SnapRadiusPx;
            if (Math.Abs(Settings.SnapRadiusPx - safeRadius) < 1e-9)
                return;

            Settings.SnapRadiusPx = safeRadius;
            IncrementSettingsVersionAndResetQueries(IsMasterEnabled);
        }

        public void SetGridSpacing(double spacing)
        {
            if (!double.IsFinite(spacing) ||
                spacing <= 0 ||
                Math.Abs(Settings.GridSpacing - spacing) < 1e-9)
            {
                return;
            }

            Settings.GridSpacing = spacing;
            IncrementSettingsVersionAndResetQueries(IsMasterEnabled);
        }

        public Task SetCompletedZoneGeometryAsync(
            IReadOnlyList<OsnapZoneGeometry> polygons,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(polygons);
            return BuildAndPublishAsync(
                BuildKind.CompletedZones,
                _zoneBuild,
                token => OsnapGeometryIndex.BuildZones(polygons, token),
                static () => true,
                cancellationToken);
        }

        public Task SetDxfSnapPointsAsync(
            IReadOnlyList<Point3D> endpoints,
            IReadOnlyList<(Point3D A, Point3D B)> segments,
            IReadOnlyList<Point3D> centers,
            IReadOnlyList<Point3D> quadrants,
            IReadOnlyList<(Point3D A, Point3D B)> midpointSegments,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(endpoints);
            ArgumentNullException.ThrowIfNull(segments);
            ArgumentNullException.ThrowIfNull(centers);
            ArgumentNullException.ThrowIfNull(quadrants);
            ArgumentNullException.ThrowIfNull(midpointSegments);
            return BuildAndPublishAsync(
                BuildKind.Dxf,
                _dxfBuild,
                token => OsnapGeometryIndex.BuildDxf(
                    endpoints,
                    segments,
                    centers,
                    quadrants,
                    midpointSegments,
                    token),
                static () => true,
                cancellationToken);
        }

        public Task SetIfcSnapGeometryAsync(
            IReadOnlyList<IfcSnapGeometry> geometry,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(geometry);
            return BuildAndPublishAsync(
                BuildKind.Ifc,
                _ifcBuild,
                token => OsnapGeometryIndex.BuildIfc(geometry, token),
                static () => true,
                cancellationToken);
        }

        public Task SetTransientZoneGeometryAsync(
            long drawingSessionVersion,
            IReadOnlyList<Point3D> vertices,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(vertices);
            long mutationVersion;
            lock (_buildGate)
            {
                if (drawingSessionVersion < _activeDrawingSessionVersion)
                    return Task.CompletedTask;

                _activeDrawingSessionVersion = drawingSessionVersion;
                mutationVersion = ++_activeZoneMutationVersion;
            }

            var polygon = new OsnapZoneGeometry(vertices, IsClosed: false);
            IReadOnlyList<OsnapZoneGeometry> polygons =
                new[] { polygon };
            return BuildAndPublishAsync(
                BuildKind.ActiveZone,
                _activeZoneBuild,
                token => OsnapGeometryIndex.BuildZones(polygons, token),
                () => drawingSessionVersion ==
                          Interlocked.Read(
                              ref _activeDrawingSessionVersion) &&
                      mutationVersion ==
                          Interlocked.Read(
                              ref _activeZoneMutationVersion),
                cancellationToken);
        }

        public void ClearTransientZoneGeometry(long drawingSessionVersion)
        {
            IndexSet next;
            lock (_buildGate)
            {
                if (drawingSessionVersion < _activeDrawingSessionVersion)
                    return;

                _activeDrawingSessionVersion = drawingSessionVersion;
                _activeZoneMutationVersion++;
                CancelSlotNoLock(_activeZoneBuild);
                IndexSet current = Volatile.Read(ref _indexes);
                next = current.Replace(
                    BuildKind.ActiveZone,
                    OsnapGeometryIndex.Empty(OsnapSource.Zone));
                Interlocked.Exchange(ref _indexes, next);
            }

            RaiseGeometryVersionChanged(next.AggregateVersion);
        }

        public void CancelPendingQueries()
        {
            ResetQueryEpoch(IsMasterEnabled);
        }

        public void BeginGeometryPreparation() => BeginPreparing();

        public void EndGeometryPreparation() => EndPreparing();

        public async Task<OsnapResult?> SnapAsync(
            OsnapQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(query.Viewport);
            if (!IsMasterEnabled ||
                !query.Viewport.IsValid ||
                query.Viewport.EnabledModes == OsnapModeMask.None)
            {
                return null;
            }

            CancellationTokenSource linked;
            lock (_queryGate)
            {
                linked = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _queryEpochCts.Token);
            }

            using (linked)
            try
            {
                return await Task.Run(
                        () => ResolveSnap(query, linked.Token),
                        linked.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Interlocked.Increment(ref _cancelledQueryCount);
                throw;
            }
        }

        public OsnapDiagnosticsSnapshot GetDiagnostics()
        {
            double mean;
            double p95;
            lock (_diagnosticsGate)
            {
                int count = Math.Min(
                    _queryLatencySampleCount,
                    QueryLatencySampleCapacity);
                if (count == 0)
                {
                    mean = 0;
                    p95 = 0;
                }
                else
                {
                    var samples = new double[count];
                    Array.Copy(_queryLatencySamples, samples, count);
                    double total = 0;
                    for (int index = 0; index < count; index++)
                        total += samples[index];

                    mean = total / count;
                    Array.Sort(samples);
                    int p95Index = Math.Clamp(
                        (int)Math.Ceiling(count * 0.95) - 1,
                        0,
                        count - 1);
                    p95 = samples[p95Index];
                }
            }

            return new OsnapDiagnosticsSnapshot(
                Interlocked.Read(ref _indexBuildCount),
                Interlocked.Read(ref _queryCount),
                Interlocked.Read(ref _cancelledQueryCount),
                Interlocked.Read(ref _rejectedStaleQueryCount),
                Volatile.Read(ref _lastIfcBuildThreadId),
                Volatile.Read(ref _lastDxfBuildThreadId),
                Volatile.Read(ref _lastZoneBuildThreadId),
                Volatile.Read(ref _lastActiveZoneBuildThreadId),
                Volatile.Read(ref _lastQueryThreadId),
                Volatile.Read(ref _lastIntersectionThreadId),
                Volatile.Read(ref _lastIfcBuildHadSynchronizationContext) != 0,
                Volatile.Read(ref _lastDxfBuildHadSynchronizationContext) != 0,
                Volatile.Read(ref _lastZoneBuildHadSynchronizationContext) != 0,
                Volatile.Read(ref _lastActiveZoneBuildHadSynchronizationContext) != 0,
                Volatile.Read(ref _lastQueryHadSynchronizationContext) != 0,
                Volatile.Read(ref _lastPointCandidateCount),
                Volatile.Read(ref _lastSegmentCandidateCount),
                Volatile.Read(ref _lastQueryMilliseconds),
                mean,
                p95);
        }

        private bool IsMasterEnabled =>
            Volatile.Read(ref _isMasterEnabled) != 0;

        private async Task BuildAndPublishAsync(
            BuildKind kind,
            BuildSlot slot,
            Func<CancellationToken, OsnapGeometryIndex> builder,
            Func<bool> canPublish,
            CancellationToken cancellationToken)
        {
            if (!IsMasterEnabled)
                return;

            CancellationTokenSource linked;
            long generation;
            lock (_buildGate)
            {
                if (!IsMasterEnabled || !canPublish())
                    return;

                CancelSlotNoLock(slot);
                linked = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
                slot.Cancellation = linked;
                generation = ++slot.Generation;
            }

            BeginPreparing();
            try
            {
                BuildResult build = await Task.Run(
                        () =>
                        {
                            RecordBuildThread(kind);
                            long memoryBefore =
                                GC.GetTotalMemory(false);
                            var stopwatch = Stopwatch.StartNew();
                            OsnapGeometryIndex index =
                                builder(linked.Token);
                            stopwatch.Stop();
                            long memoryAfter =
                                GC.GetTotalMemory(false);
                            return new BuildResult(
                                index,
                                stopwatch.Elapsed,
                                Math.Max(
                                    0.0,
                                    (memoryAfter - memoryBefore) /
                                    (1024.0 * 1024.0)));
                        },
                        linked.Token)
                    .ConfigureAwait(false);
                linked.Token.ThrowIfCancellationRequested();

                if (!TryPublishCurrent(
                        kind,
                        slot,
                        linked,
                        generation,
                        build.Index,
                        canPublish,
                        out long publishedVersion))
                {
                    return;
                }

                SnapBuildDurationMs =
                    build.Elapsed.TotalMilliseconds;
                SnapMemoryDeltaMB = build.MemoryDeltaMegabytes;
                Interlocked.Increment(ref _indexBuildCount);
                RecordCompatibilityBuildDiagnostics(
                    build.Index,
                    build.Elapsed);
                RaiseGeometryVersionChanged(publishedVersion);
            }
            catch (OperationCanceledException)
                when (linked.IsCancellationRequested ||
                      cancellationToken.IsCancellationRequested)
            {
                // Superseded geometry preparation is expected.
            }
            finally
            {
                lock (_buildGate)
                {
                    if (ReferenceEquals(slot.Cancellation, linked))
                        slot.Cancellation = null;
                }

                linked.Dispose();
                EndPreparing();
            }
        }

        private OsnapResult? ResolveSnap(
            OsnapQuery query,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref _queryCount);
            Volatile.Write(
                ref _lastQueryThreadId,
                Environment.CurrentManagedThreadId);
            Volatile.Write(
                ref _lastQueryHadSynchronizationContext,
                SynchronizationContext.Current == null ? 0 : 1);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                IndexSet indexes = Volatile.Read(ref _indexes);
                if (!IsQueryCurrent(query.Viewport, indexes))
                {
                    Interlocked.Increment(ref _rejectedStaleQueryCount);
                    return null;
                }

                if (!OsnapProjectionMath.TryCreateCursorRay(
                        query.Viewport,
                        out OsnapRay ray))
                {
                    return null;
                }

                var accumulator = new CandidateAccumulator(query.Viewport);
                int pointCandidateCount = 0;
                QueryPoints(
                    indexes.CompletedZones,
                    query.Viewport,
                    ray,
                    accumulator,
                    ref pointCandidateCount,
                    cancellationToken);
                QueryPoints(
                    indexes.ActiveZone,
                    query.Viewport,
                    ray,
                    accumulator,
                    ref pointCandidateCount,
                    cancellationToken);
                if (query.Viewport.SnapToDxf)
                {
                    QueryPoints(
                        indexes.Dxf,
                        query.Viewport,
                        ray,
                        accumulator,
                        ref pointCandidateCount,
                        cancellationToken);
                }

                if (query.Viewport.SnapToIfc)
                {
                    QueryPoints(
                        indexes.Ifc,
                        query.Viewport,
                        ray,
                        accumulator,
                        ref pointCandidateCount,
                        cancellationToken);
                }

                int segmentCandidateCount = 0;
                if ((query.Viewport.EnabledModes &
                     OsnapModeMask.Intersection) != 0)
                {
                    QueryIntersections(
                        indexes,
                        query.Viewport,
                        ray,
                        accumulator,
                        ref segmentCandidateCount,
                        cancellationToken);
                }

                Volatile.Write(
                    ref _lastPointCandidateCount,
                    pointCandidateCount);
                Volatile.Write(
                    ref _lastSegmentCandidateCount,
                    segmentCandidateCount);
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsQueryCurrent(
                        query.Viewport,
                        Volatile.Read(ref _indexes)))
                {
                    Interlocked.Increment(ref _rejectedStaleQueryCount);
                    return null;
                }

                return accumulator.ToResult();
            }
            finally
            {
                stopwatch.Stop();
                RecordQueryLatency(stopwatch.Elapsed.TotalMilliseconds);
            }
        }

        private static void QueryPoints(
            OsnapGeometryIndex index,
            OsnapViewportSnapshot snapshot,
            OsnapRay ray,
            CandidateAccumulator accumulator,
            ref int candidateCount,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<OsnapPointCandidate> candidates =
                index.QueryPoints(
                    snapshot,
                    ray,
                    MaxPointCandidatesPerSource,
                    cancellationToken);
            candidateCount += candidates.Count;
            for (int indexValue = 0;
                 indexValue < candidates.Count;
                 indexValue++)
            {
                if ((indexValue & 31) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                OsnapPointCandidate candidate = candidates[indexValue];
                if (!OsnapProjectionMath.TryProject(
                        snapshot,
                        candidate.World,
                        out Point screen,
                        out double depth))
                {
                    continue;
                }

                accumulator.TryCandidate(
                    candidate.World,
                    screen,
                    depth,
                    candidate.Type,
                    candidate.Source);
            }
        }

        private void QueryIntersections(
            IndexSet indexes,
            OsnapViewportSnapshot snapshot,
            OsnapRay ray,
            CandidateAccumulator accumulator,
            ref int candidateCount,
            CancellationToken cancellationToken)
        {
            int limit = Math.Clamp(
                snapshot.MaxIntersectionCandidates,
                24,
                64);
            var projected = new List<ProjectedSegment>(limit * 2);
            AddProjectedSegments(
                indexes.CompletedZones,
                snapshot,
                ray,
                projected,
                limit,
                cancellationToken);
            AddProjectedSegments(
                indexes.ActiveZone,
                snapshot,
                ray,
                projected,
                limit,
                cancellationToken);
            if (snapshot.SnapToDxf)
            {
                AddProjectedSegments(
                    indexes.Dxf,
                    snapshot,
                    ray,
                    projected,
                    limit,
                    cancellationToken);
            }

            if (snapshot.SnapToIfc)
            {
                AddProjectedSegments(
                    indexes.Ifc,
                    snapshot,
                    ray,
                    projected,
                    limit,
                    cancellationToken);
            }

            projected.Sort(static (first, second) =>
                first.ScreenDistance.CompareTo(second.ScreenDistance));
            if (projected.Count > limit)
                projected.RemoveRange(limit, projected.Count - limit);

            candidateCount = projected.Count;
            Volatile.Write(
                ref _lastIntersectionThreadId,
                Environment.CurrentManagedThreadId);
            int pairCounter = 0;
            for (int firstIndex = 0;
                 firstIndex < projected.Count;
                 firstIndex++)
            {
                for (int secondIndex = firstIndex + 1;
                     secondIndex < projected.Count;
                     secondIndex++)
                {
                    if ((++pairCounter & 31) == 0)
                        cancellationToken.ThrowIfCancellationRequested();

                    ProjectedSegment first = projected[firstIndex];
                    ProjectedSegment second = projected[secondIndex];
                    if (first.Id == second.Id ||
                        ShareEndpoint(first, second) ||
                        !TrySegmentIntersection3D(
                            first,
                            second,
                            out Point3D world))
                    {
                        continue;
                    }

                    if (!OsnapProjectionMath.TryProject(
                            snapshot,
                            world,
                            out Point screen,
                            out double depth))
                    {
                        continue;
                    }

                    accumulator.TryCandidate(
                        world,
                        screen,
                        depth,
                        OsnapType.Intersection,
                        ResolveIntersectionSource(
                            first.Source,
                            second.Source));
                }
            }
        }

        private static void AddProjectedSegments(
            OsnapGeometryIndex index,
            OsnapViewportSnapshot snapshot,
            OsnapRay ray,
            List<ProjectedSegment> sink,
            int limit,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<OsnapSegmentCandidate> candidates =
                index.QuerySegments(
                    snapshot,
                    ray,
                    limit,
                    cancellationToken);
            double searchRadius = Math.Max(
                snapshot.SnapRadiusPx * 2.5,
                snapshot.SnapRadiusPx + 24.0);
            for (int candidateIndex = 0;
                 candidateIndex < candidates.Count;
                 candidateIndex++)
            {
                OsnapSegmentCandidate candidate = candidates[candidateIndex];
                if (!OsnapProjectionMath.TryProject(
                        snapshot,
                        candidate.A,
                        out Point screenA,
                        out _) ||
                    !OsnapProjectionMath.TryProject(
                        snapshot,
                        candidate.B,
                        out Point screenB,
                        out _))
                {
                    continue;
                }

                double distance =
                    OsnapProjectionMath.DistanceToScreenSegment(
                        snapshot.CursorScreenPoint,
                        screenA,
                        screenB);
                if (distance > searchRadius)
                    continue;

                sink.Add(new ProjectedSegment(
                    candidate.Id,
                    candidate.A,
                    candidate.B,
                    screenA,
                    screenB,
                    candidate.Source,
                    distance));
            }
        }

        private bool IsQueryCurrent(
            OsnapViewportSnapshot snapshot,
            IndexSet indexes)
        {
            return IsMasterEnabled &&
                   snapshot.GeometryVersion == indexes.AggregateVersion &&
                   snapshot.SourceVersions == indexes.SourceVersions &&
                   snapshot.SettingsVersion == SettingsVersion;
        }

        private bool TryPublishCurrent(
            BuildKind kind,
            BuildSlot slot,
            CancellationTokenSource cancellation,
            long generation,
            OsnapGeometryIndex index,
            Func<bool> canPublish,
            out long publishedVersion)
        {
            lock (_buildGate)
            {
                if (!ReferenceEquals(slot.Cancellation, cancellation) ||
                    slot.Generation != generation ||
                    !IsMasterEnabled ||
                    !canPublish())
                {
                    publishedVersion = 0;
                    return false;
                }

                IndexSet current = Volatile.Read(ref _indexes);
                IndexSet next = current.Replace(kind, index);
                Interlocked.Exchange(ref _indexes, next);
                publishedVersion = next.AggregateVersion;
                return true;
            }
        }

        private void PublishAllEmpty()
        {
            IndexSet next;
            lock (_buildGate)
            {
                IndexSet current = Volatile.Read(ref _indexes);
                next = current.ClearAll();
                Interlocked.Exchange(ref _indexes, next);
            }

            RaiseGeometryVersionChanged(next.AggregateVersion);
        }

        private void CancelAllBuilds()
        {
            lock (_buildGate)
            {
                CancelSlotNoLock(_ifcBuild);
                CancelSlotNoLock(_dxfBuild);
                CancelSlotNoLock(_zoneBuild);
                CancelSlotNoLock(_activeZoneBuild);
            }
        }

        private static void CancelSlotNoLock(BuildSlot slot)
        {
            slot.Generation++;
            slot.Cancellation?.Cancel();
        }

        private void IncrementSettingsVersionAndResetQueries(bool keepEnabled)
        {
            long version = Interlocked.Increment(ref _settingsVersion);
            ResetQueryEpoch(keepEnabled);
            SettingsVersionChanged?.Invoke(version);
        }

        private void ResetQueryEpoch(bool keepEnabled)
        {
            var replacement = new CancellationTokenSource();
            if (!keepEnabled)
                replacement.Cancel();

            CancellationTokenSource old;
            lock (_queryGate)
            {
                old = _queryEpochCts;
                _queryEpochCts = replacement;
            }

            old.Cancel();
            old.Dispose();
        }

        private void BeginPreparing()
        {
            if (Interlocked.Increment(ref _preparingCount) == 1)
                PreparingChanged?.Invoke(true);
        }

        private void RaiseGeometryVersionChanged(long version)
        {
            lock (_geometryEventGate)
            {
                if (version <= _lastNotifiedGeometryVersion)
                    return;

                _lastNotifiedGeometryVersion = version;
                GeometryVersionChanged?.Invoke(version);
            }
        }

        private void EndPreparing()
        {
            if (Interlocked.Decrement(ref _preparingCount) == 0)
                PreparingChanged?.Invoke(false);
        }

        private void RecordBuildThread(BuildKind kind)
        {
            int threadId = Environment.CurrentManagedThreadId;
            int hasContext = SynchronizationContext.Current == null ? 0 : 1;
            switch (kind)
            {
                case BuildKind.Ifc:
                    Volatile.Write(ref _lastIfcBuildThreadId, threadId);
                    Volatile.Write(
                        ref _lastIfcBuildHadSynchronizationContext,
                        hasContext);
                    break;
                case BuildKind.Dxf:
                    Volatile.Write(ref _lastDxfBuildThreadId, threadId);
                    Volatile.Write(
                        ref _lastDxfBuildHadSynchronizationContext,
                        hasContext);
                    break;
                case BuildKind.CompletedZones:
                    Volatile.Write(ref _lastZoneBuildThreadId, threadId);
                    Volatile.Write(
                        ref _lastZoneBuildHadSynchronizationContext,
                        hasContext);
                    break;
                case BuildKind.ActiveZone:
                    Volatile.Write(
                        ref _lastActiveZoneBuildThreadId,
                        threadId);
                    Volatile.Write(
                        ref _lastActiveZoneBuildHadSynchronizationContext,
                        hasContext);
                    break;
            }
        }

        private void RecordQueryLatency(double milliseconds)
        {
            Volatile.Write(ref _lastQueryMilliseconds, milliseconds);
            lock (_diagnosticsGate)
            {
                _queryLatencySamples[_queryLatencySampleCursor] =
                    milliseconds;
                _queryLatencySampleCursor =
                    (_queryLatencySampleCursor + 1) %
                    QueryLatencySampleCapacity;
                if (_queryLatencySampleCount <
                    QueryLatencySampleCapacity)
                {
                    _queryLatencySampleCount++;
                }
            }
        }

        private static void RecordCompatibilityBuildDiagnostics(
            OsnapGeometryIndex index,
            TimeSpan elapsed)
        {
            lock (CompatibilityDiagnosticsGate)
            {
                SnapRebuildCount++;
                SnapRebuildTotalMs += elapsed.TotalMilliseconds;
                SnapRebuildMaxMs = Math.Max(
                    SnapRebuildMaxMs,
                    elapsed.TotalMilliseconds);
                LastRebuildVerticesCount = index.PointCount;
                LastRebuildSegmentsCount = index.SegmentCount;
            }
        }

        private static bool SetIfChanged(
            bool current,
            bool next,
            Action<bool> setter)
        {
            if (current == next)
                return false;

            setter(next);
            return true;
        }

        private static bool HasGeometry(OsnapGeometryIndex index) =>
            index.PointCount > 0 || index.SegmentCount > 0;

        private static bool ShareEndpoint(
            ProjectedSegment first,
            ProjectedSegment second) =>
            SamePoint(first.A, second.A) ||
            SamePoint(first.A, second.B) ||
            SamePoint(first.B, second.A) ||
            SamePoint(first.B, second.B);

        private static bool SamePoint(Point3D first, Point3D second)
        {
            const double tolerance = 1e-6;
            return Math.Abs(first.X - second.X) <= tolerance &&
                   Math.Abs(first.Y - second.Y) <= tolerance &&
                   Math.Abs(first.Z - second.Z) <= tolerance;
        }

        private static bool TrySegmentIntersection3D(
            ProjectedSegment first,
            ProjectedSegment second,
            out Point3D world)
        {
            world = default;
            Vector3D firstDirection = first.B - first.A;
            Vector3D secondDirection = second.B - second.A;
            Vector3D betweenStarts = first.A - second.A;
            double firstLengthSquared = firstDirection.LengthSquared;
            double secondLengthSquared = secondDirection.LengthSquared;
            if (firstLengthSquared < 1e-12 ||
                secondLengthSquared < 1e-12)
            {
                return false;
            }

            double cross = Vector3D.DotProduct(
                firstDirection,
                secondDirection);
            double firstOffset = Vector3D.DotProduct(
                firstDirection,
                betweenStarts);
            double secondOffset = Vector3D.DotProduct(
                secondDirection,
                betweenStarts);
            double denominator =
                firstLengthSquared * secondLengthSquared -
                cross * cross;
            double lengthProduct =
                firstLengthSquared * secondLengthSquared;
            if (!double.IsFinite(denominator) ||
                denominator <= lengthProduct * 1e-12)
            {
                // Parallel and near-parallel segments do not define a unique
                // intersection. Treating their nearest points as an
                // intersection creates false positives at large scene scales.
                return false;
            }

            double firstParameter =
                (cross * secondOffset -
                 secondLengthSquared * firstOffset) /
                denominator;
            double secondParameter =
                (firstLengthSquared * secondOffset -
                 cross * firstOffset) /
                denominator;
            const double parameterTolerance = 1e-9;
            if (!double.IsFinite(firstParameter) ||
                !double.IsFinite(secondParameter) ||
                firstParameter < -parameterTolerance ||
                firstParameter > 1.0 + parameterTolerance ||
                secondParameter < -parameterTolerance ||
                secondParameter > 1.0 + parameterTolerance)
            {
                return false;
            }

            firstParameter = Math.Clamp(firstParameter, 0.0, 1.0);
            secondParameter = Math.Clamp(secondParameter, 0.0, 1.0);

            Point3D firstPoint =
                first.A + firstDirection * firstParameter;
            Point3D secondPoint =
                second.A + secondDirection * secondParameter;
            double tolerance = Math.Max(
                1e-6,
                Math.Max(
                    Math.Sqrt(firstLengthSquared),
                    Math.Sqrt(secondLengthSquared)) * 1e-9);
            if ((firstPoint - secondPoint).Length > tolerance)
                return false;

            world = new Point3D(
                (firstPoint.X + secondPoint.X) / 2.0,
                (firstPoint.Y + secondPoint.Y) / 2.0,
                (firstPoint.Z + secondPoint.Z) / 2.0);
            return true;
        }

        private static OsnapSource ResolveIntersectionSource(
            OsnapSource first,
            OsnapSource second)
        {
            if (first == second)
                return first;
            if (first == OsnapSource.Ifc || second == OsnapSource.Ifc)
                return OsnapSource.Ifc;
            if (first == OsnapSource.Dxf || second == OsnapSource.Dxf)
                return OsnapSource.Dxf;
            return OsnapSource.Zone;
        }

        private enum BuildKind
        {
            Ifc,
            Dxf,
            CompletedZones,
            ActiveZone
        }

        private sealed class BuildSlot
        {
            public long Generation;
            public CancellationTokenSource? Cancellation;
        }

        private readonly record struct BuildResult(
            OsnapGeometryIndex Index,
            TimeSpan Elapsed,
            double MemoryDeltaMegabytes);

        private sealed record IndexSet(
            OsnapGeometryIndex Ifc,
            OsnapGeometryIndex Dxf,
            OsnapGeometryIndex CompletedZones,
            OsnapGeometryIndex ActiveZone,
            long AggregateVersion,
            OsnapSourceVersionSnapshot SourceVersions,
            OsnapWorldBounds Bounds)
        {
            public static IndexSet Empty { get; } = Create(
                OsnapGeometryIndex.Empty(OsnapSource.Ifc),
                OsnapGeometryIndex.Empty(OsnapSource.Dxf),
                OsnapGeometryIndex.Empty(OsnapSource.Zone),
                OsnapGeometryIndex.Empty(OsnapSource.Zone),
                0,
                new OsnapSourceVersionSnapshot(0, 0, 0, 0));

            public IndexSet Replace(
                BuildKind kind,
                OsnapGeometryIndex index)
            {
                OsnapGeometryIndex ifc =
                    kind == BuildKind.Ifc ? index : Ifc;
                OsnapGeometryIndex dxf =
                    kind == BuildKind.Dxf ? index : Dxf;
                OsnapGeometryIndex zones =
                    kind == BuildKind.CompletedZones
                        ? index
                        : CompletedZones;
                OsnapGeometryIndex active =
                    kind == BuildKind.ActiveZone ? index : ActiveZone;
                OsnapSourceVersionSnapshot versions = kind switch
                {
                    BuildKind.Ifc => SourceVersions with
                    {
                        Ifc = SourceVersions.Ifc + 1
                    },
                    BuildKind.Dxf => SourceVersions with
                    {
                        Dxf = SourceVersions.Dxf + 1
                    },
                    BuildKind.CompletedZones => SourceVersions with
                    {
                        CompletedZones =
                            SourceVersions.CompletedZones + 1
                    },
                    BuildKind.ActiveZone => SourceVersions with
                    {
                        ActiveZone = SourceVersions.ActiveZone + 1
                    },
                    _ => SourceVersions
                };
                return Create(
                    ifc,
                    dxf,
                    zones,
                    active,
                    AggregateVersion + 1,
                    versions);
            }

            public IndexSet ClearAll()
            {
                var versions = new OsnapSourceVersionSnapshot(
                    SourceVersions.Ifc + 1,
                    SourceVersions.Dxf + 1,
                    SourceVersions.CompletedZones + 1,
                    SourceVersions.ActiveZone + 1);
                return Create(
                    OsnapGeometryIndex.Empty(OsnapSource.Ifc),
                    OsnapGeometryIndex.Empty(OsnapSource.Dxf),
                    OsnapGeometryIndex.Empty(OsnapSource.Zone),
                    OsnapGeometryIndex.Empty(OsnapSource.Zone),
                    AggregateVersion + 1,
                    versions);
            }

            private static IndexSet Create(
                OsnapGeometryIndex ifc,
                OsnapGeometryIndex dxf,
                OsnapGeometryIndex completedZones,
                OsnapGeometryIndex activeZone,
                long aggregateVersion,
                OsnapSourceVersionSnapshot sourceVersions)
            {
                OsnapWorldBounds bounds = OsnapWorldBounds.Empty
                    .Include(ifc.Bounds)
                    .Include(dxf.Bounds)
                    .Include(completedZones.Bounds)
                    .Include(activeZone.Bounds);
                return new IndexSet(
                    ifc,
                    dxf,
                    completedZones,
                    activeZone,
                    aggregateVersion,
                    sourceVersions,
                    bounds);
            }
        }

        private readonly record struct ProjectedSegment(
            long Id,
            Point3D A,
            Point3D B,
            Point ScreenA,
            Point ScreenB,
            OsnapSource Source,
            double ScreenDistance);

        private sealed class CandidateAccumulator
        {
            private readonly OsnapViewportSnapshot _snapshot;
            private Point3D? _bestWorld;
            private Point? _bestScreen;
            private double _bestDepth = double.PositiveInfinity;
            private double _bestDistanceSquared = double.PositiveInfinity;
            private OsnapType _bestType;
            private OsnapSource _bestSource;

            public CandidateAccumulator(OsnapViewportSnapshot snapshot)
            {
                _snapshot = snapshot;
            }

            public void TryCandidate(
                Point3D world,
                Point screen,
                double depth,
                OsnapType type,
                OsnapSource source)
            {
                double distanceSquared =
                    OsnapProjectionMath.PixelDistanceSquared(
                        _snapshot.CursorScreenPoint,
                        screen);
                double radiusSquared =
                    _snapshot.SnapRadiusPx *
                    _snapshot.SnapRadiusPx;
                if (distanceSquared > radiusSquared)
                    return;

                if (_bestWorld.HasValue &&
                    !IsBetter(distanceSquared, depth, type, source, world))
                {
                    return;
                }

                _bestWorld = world;
                _bestScreen = screen;
                _bestDepth = depth;
                _bestDistanceSquared = distanceSquared;
                _bestType = type;
                _bestSource = source;
            }

            public OsnapResult? ToResult()
            {
                return _bestWorld.HasValue
                    ? new OsnapResult(
                        _bestType,
                        _bestWorld.Value,
                        _bestScreen,
                        _bestSource)
                    : null;
            }

            private bool IsBetter(
                double distanceSquared,
                double depth,
                OsnapType type,
                OsnapSource source,
                Point3D world)
            {
                double bestDistance = Math.Sqrt(_bestDistanceSquared);
                double distance = Math.Sqrt(distanceSquared);
                if (distance < bestDistance - DistanceTieTolerancePx)
                    return true;
                if (distance > bestDistance + DistanceTieTolerancePx)
                    return false;
                if (depth < _bestDepth - DepthTieTolerance)
                    return true;
                if (depth > _bestDepth + DepthTieTolerance)
                    return false;
                if (type != _bestType)
                    return type < _bestType;
                if (source != _bestSource)
                    return source < _bestSource;
                if (!_bestWorld.HasValue)
                    return true;

                Point3D best = _bestWorld.Value;
                int x = world.X.CompareTo(best.X);
                if (x != 0)
                    return x < 0;
                int y = world.Y.CompareTo(best.Y);
                return y != 0
                    ? y < 0
                    : world.Z.CompareTo(best.Z) < 0;
            }
        }
    }
}
