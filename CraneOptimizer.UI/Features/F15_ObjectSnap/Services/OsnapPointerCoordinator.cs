using CraneOptimizer.UI.Features.F15_ObjectSnap.Models;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Services
{
    /// <summary>
    /// Capacity-one pointer coordinator. It runs no WPF work and emits completed
    /// hover requests from a worker thread for the owner to validate and display.
    /// </summary>
    public sealed class OsnapPointerCoordinator : IDisposable
    {
        private readonly IOsnapService _service;
        private readonly object _gate = new();
        private readonly SemaphoreSlim _clickGate = new(1, 1);
        private readonly CancellationTokenSource _lifetime = new();
        private readonly TimeSpan _sampleInterval;

        private PendingHover? _pendingHover;
        private CancellationTokenSource? _activeHoverCancellation;
        private Task? _hoverPumpTask;
        private OsnapQueryCompletion? _latestCompletion;
        private long _lastHoverStartTimestamp;
        private long _hoverSubmissionGeneration;
        private bool _isHoverPumpRunning;
        private bool _disposed;
        private bool _resourcesDisposed;
        private int _activeClickOperationCount;

        private long _submittedHoverCount;
        private long _completedHoverCount;
        private long _cancelledHoverCount;
        private long _droppedPendingHoverCount;
        private int _activeHoverCount;
        private int _maximumConcurrentHoverCount;

        public OsnapPointerCoordinator(
            IOsnapService service,
            TimeSpan? sampleInterval = null)
        {
            _service = service ??
                throw new ArgumentNullException(nameof(service));
            TimeSpan requested =
                sampleInterval ?? TimeSpan.FromMilliseconds(20);
            _sampleInterval = requested < TimeSpan.Zero
                ? TimeSpan.Zero
                : requested;
        }

        public event Action<OsnapQueryCompletion>? HoverCompleted;
        public event Action<Exception>? Faulted;

        public OsnapQueryCompletion? LatestCompletion
        {
            get
            {
                lock (_gate)
                    return _latestCompletion;
            }
        }

        public void SubmitHover(OsnapQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(query.Viewport);
            if (query.IsClick)
                throw new ArgumentException(
                    "Hover requests must have IsClick=false.",
                    nameof(query));

            lock (_gate)
            {
                ThrowIfDisposed();
                Interlocked.Increment(ref _submittedHoverCount);
                if (_pendingHover != null)
                    Interlocked.Increment(
                        ref _droppedPendingHoverCount);

                long generation = ++_hoverSubmissionGeneration;
                _pendingHover = new PendingHover(query, generation);
                _activeHoverCancellation?.Cancel();
                if (_isHoverPumpRunning)
                    return;

                _isHoverPumpRunning = true;
                _hoverPumpTask = Task.Run(RunHoverPumpAsync);
            }
        }

        public async Task<OsnapQueryCompletion> ResolveClickAsync(
            OsnapQuery query,
            double hoverCursorTolerancePixels,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(query.Viewport);
            if (!query.IsClick)
                throw new ArgumentException(
                    "Click requests must have IsClick=true.",
                    nameof(query));

            CancellationTokenSource linkedCancellation;
            lock (_gate)
            {
                ThrowIfDisposed();
                linkedCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken,
                        _lifetime.Token);
                _activeClickOperationCount++;
            }

            bool gateAcquired = false;
            try
            {
                await _clickGate.WaitAsync(linkedCancellation.Token)
                    .ConfigureAwait(false);
                gateAcquired = true;
                InvalidateHover(clearAcceptedResult: false);
                OsnapQueryCompletion? latest;
                lock (_gate)
                    latest = _latestCompletion;

                if (latest != null &&
                    CanReuse(
                        latest,
                        query,
                        hoverCursorTolerancePixels))
                {
                    linkedCancellation.Token
                        .ThrowIfCancellationRequested();
                    return new OsnapQueryCompletion(
                        query,
                        latest.SnapResult,
                        0);
                }

                var stopwatch = Stopwatch.StartNew();
                OsnapResult? result = await _service.SnapAsync(
                        query,
                        linkedCancellation.Token)
                    .ConfigureAwait(false);
                stopwatch.Stop();
                linkedCancellation.Token.ThrowIfCancellationRequested();
                var completion = new OsnapQueryCompletion(
                    query,
                    result,
                    stopwatch.Elapsed.TotalMilliseconds);
                lock (_gate)
                {
                    if (!_disposed)
                        _latestCompletion = completion;
                }

                return completion;
            }
            finally
            {
                if (gateAcquired)
                    _clickGate.Release();

                linkedCancellation.Dispose();
                lock (_gate)
                {
                    _activeClickOperationCount--;
                    TryFinalizeDisposalNoLock();
                }
            }
        }

        public void InvalidateHover(bool clearAcceptedResult = true)
        {
            lock (_gate)
            {
                _hoverSubmissionGeneration++;
                _pendingHover = null;
                _activeHoverCancellation?.Cancel();
                if (clearAcceptedResult)
                    _latestCompletion = null;
            }
        }

        public OsnapCoordinatorDiagnostics GetDiagnostics()
        {
            int pending;
            lock (_gate)
                pending = _pendingHover == null ? 0 : 1;

            return new OsnapCoordinatorDiagnostics(
                Interlocked.Read(ref _submittedHoverCount),
                Interlocked.Read(ref _completedHoverCount),
                Interlocked.Read(ref _cancelledHoverCount),
                Interlocked.Read(ref _droppedPendingHoverCount),
                Volatile.Read(ref _activeHoverCount),
                pending,
                Volatile.Read(ref _maximumConcurrentHoverCount));
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;

                _disposed = true;
                _pendingHover = null;
                _activeHoverCancellation?.Cancel();
                _lifetime.Cancel();
                _latestCompletion = null;
                HoverCompleted = null;
                Faulted = null;
                TryFinalizeDisposalNoLock();
            }
        }

        private async Task RunHoverPumpAsync()
        {
            try
            {
                while (true)
                {
                    PendingHover pending;
                    CancellationTokenSource activeCancellation;
                    lock (_gate)
                    {
                        if (_disposed || _pendingHover == null)
                        {
                            _isHoverPumpRunning = false;
                            _hoverPumpTask = null;
                            TryFinalizeDisposalNoLock();
                            return;
                        }

                        pending = _pendingHover.Value;
                        _pendingHover = null;
                        activeCancellation =
                            CancellationTokenSource
                                .CreateLinkedTokenSource(
                                    _lifetime.Token);
                        _activeHoverCancellation =
                            activeCancellation;
                    }

                    int activeCount =
                        Interlocked.Increment(
                            ref _activeHoverCount);
                    UpdateMaximumConcurrent(activeCount);
                    try
                    {
                        await WaitForSampleWindowAsync(
                                activeCancellation.Token)
                            .ConfigureAwait(false);
                        Interlocked.Exchange(
                            ref _lastHoverStartTimestamp,
                            Stopwatch.GetTimestamp());
                        var stopwatch = Stopwatch.StartNew();
                        OsnapResult? result =
                            await _service.SnapAsync(
                                    pending.Query,
                                    activeCancellation.Token)
                                .ConfigureAwait(false);
                        stopwatch.Stop();
                        var completion =
                            new OsnapQueryCompletion(
                                pending.Query,
                                result,
                                stopwatch.Elapsed
                                    .TotalMilliseconds);

                        bool isLatest;
                        lock (_gate)
                        {
                            isLatest =
                                !_disposed &&
                                !activeCancellation
                                    .IsCancellationRequested &&
                                pending.Generation ==
                                _hoverSubmissionGeneration;
                            if (isLatest)
                                _latestCompletion = completion;
                        }

                        if (isLatest)
                        {
                            Interlocked.Increment(
                                ref _completedHoverCount);
                            RaiseHoverCompleted(completion);
                        }
                    }
                    catch (OperationCanceledException)
                        when (activeCancellation
                                  .IsCancellationRequested)
                    {
                        Interlocked.Increment(
                            ref _cancelledHoverCount);
                    }
                    catch (Exception ex)
                    {
                        RaiseFaulted(ex);
                    }
                    finally
                    {
                        Interlocked.Decrement(
                            ref _activeHoverCount);
                        lock (_gate)
                        {
                            if (ReferenceEquals(
                                    _activeHoverCancellation,
                                    activeCancellation))
                            {
                                _activeHoverCancellation = null;
                            }
                        }

                        activeCancellation.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                RaiseFaulted(ex);
                lock (_gate)
                {
                    _isHoverPumpRunning = false;
                    _hoverPumpTask = null;
                    TryFinalizeDisposalNoLock();
                }
            }
        }

        private async Task WaitForSampleWindowAsync(
            CancellationToken cancellationToken)
        {
            long lastTimestamp =
                Interlocked.Read(ref _lastHoverStartTimestamp);
            if (lastTimestamp == 0)
                return;

            TimeSpan elapsed = Stopwatch.GetElapsedTime(
                lastTimestamp,
                Stopwatch.GetTimestamp());
            TimeSpan remaining = _sampleInterval - elapsed;
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        private static bool CanReuse(
            OsnapQueryCompletion latest,
            OsnapQuery click,
            double cursorTolerancePixels)
        {
            OsnapViewportSnapshot previous =
                latest.Query.Viewport;
            OsnapViewportSnapshot current =
                click.Viewport;
            if (previous.CameraVersion != current.CameraVersion ||
                previous.GeometryVersion != current.GeometryVersion ||
                previous.SourceVersions != current.SourceVersions ||
                previous.SettingsVersion != current.SettingsVersion ||
                previous.DrawingSessionVersion !=
                    current.DrawingSessionVersion ||
                previous.ReferencePlane != current.ReferencePlane ||
                previous.EnabledModes != current.EnabledModes ||
                previous.SnapRadiusPx != current.SnapRadiusPx ||
                previous.MaxIntersectionCandidates !=
                    current.MaxIntersectionCandidates ||
                previous.SnapToDxf != current.SnapToDxf ||
                previous.SnapToIfc != current.SnapToIfc)
            {
                return false;
            }

            double tolerance =
                double.IsFinite(cursorTolerancePixels)
                    ? Math.Max(0, cursorTolerancePixels)
                    : 0;
            if (OsnapProjectionMath.PixelDistanceSquared(
                    previous.CursorScreenPoint,
                    current.CursorScreenPoint) >
                tolerance * tolerance)
            {
                return false;
            }

            if (latest.SnapResult == null)
                return true;

            Point? snapScreen = latest.SnapResult.ScreenPoint;
            if (!snapScreen.HasValue)
                return false;

            double radius = current.SnapRadiusPx;
            return double.IsFinite(radius) &&
                   radius > 0 &&
                   OsnapProjectionMath.PixelDistanceSquared(
                       snapScreen.Value,
                       current.CursorScreenPoint) <=
                   radius * radius;
        }

        private void UpdateMaximumConcurrent(int activeCount)
        {
            int observed;
            do
            {
                observed = Volatile.Read(
                    ref _maximumConcurrentHoverCount);
                if (activeCount <= observed)
                    return;
            }
            while (Interlocked.CompareExchange(
                       ref _maximumConcurrentHoverCount,
                       activeCount,
                       observed) != observed);
        }

        private void RaiseHoverCompleted(
            OsnapQueryCompletion completion)
        {
            try
            {
                HoverCompleted?.Invoke(completion);
            }
            catch (Exception ex)
            {
                RaiseFaulted(ex);
            }
        }

        private void RaiseFaulted(Exception exception)
        {
            try
            {
                Faulted?.Invoke(exception);
            }
            catch
            {
                // A diagnostics subscriber must not fault the tracked pump.
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        private void TryFinalizeDisposalNoLock()
        {
            if (!_disposed ||
                _resourcesDisposed ||
                _isHoverPumpRunning ||
                _activeClickOperationCount != 0)
            {
                return;
            }

            _resourcesDisposed = true;
            _lifetime.Dispose();
            _clickGate.Dispose();
        }

        private readonly record struct PendingHover(
            OsnapQuery Query,
            long Generation);
    }
}
