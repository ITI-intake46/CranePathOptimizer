using System.Diagnostics;
using System.IO;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.ModelGeometry.Scene;

namespace CraneOptimizer.Infrastructure.Ifc
{
    public sealed class IfcModelSession : IIfcModelSession
    {
        private const double DefaultLengthToMetersFactor = 1d;
        private readonly long _memoryBeforeLoadBytes;
        private bool _disposed;

        private IfcModelSession(
            Guid sessionId,
            string ifcPath,
            string fileIdentity,
            IfcStore model,
            Xbim3DModelContext geometryContext,
            double modelLengthFactor,
            double? modelGroundElevation,
            int createContextCount,
            int shapeInstanceCount,
            TimeSpan openDuration,
            TimeSpan createContextDuration,
            long memoryBeforeLoadBytes,
            long memoryAfterCreateContextBytes)
        {
            SessionId = sessionId;
            IfcPath = ifcPath;
            FileIdentity = fileIdentity;
            Model = model;
            GeometryContext = geometryContext;
            ModelLengthFactor = modelLengthFactor;
            ModelGroundElevation = modelGroundElevation;
            CreateContextCount = createContextCount;
            ShapeInstanceCount = shapeInstanceCount;
            OpenDuration = openDuration;
            CreateContextDuration = createContextDuration;
            MemoryBeforeLoadBytes = memoryBeforeLoadBytes;
            MemoryAfterCreateContextBytes = memoryAfterCreateContextBytes;
            _memoryBeforeLoadBytes = memoryBeforeLoadBytes;
        }

        public Guid SessionId { get; }
        public string IfcPath { get; }
        public string FileIdentity { get; }
        public IfcStore Model { get; }
        public Xbim3DModelContext GeometryContext { get; }
        public double ModelLengthFactor { get; }
        public double? ModelGroundElevation { get; }
        public int CreateContextCount { get; }
        public int ShapeInstanceCount { get; }
        public TimeSpan OpenDuration { get; }
        public TimeSpan CreateContextDuration { get; }
        public long MemoryBeforeLoadBytes { get; }
        public long MemoryAfterCreateContextBytes { get; }
        public bool IsDisposed => _disposed;

        public static IfcModelSession Open(string ifcPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(ifcPath))
                throw new ArgumentException("IFC path is required.", nameof(ifcPath));

            var fullPath = Path.GetFullPath(ifcPath);
            var fileIdentity = CreateFileIdentity(fullPath);
            var sessionId = Guid.NewGuid();
            var memoryBefore = GetPrivateMemoryBytes();

            Debug.WriteLine($"[IfcSession] Opening session {sessionId} for '{fullPath}'. MemoryBefore={FormatBytes(memoryBefore)}");

            var openTimer = Stopwatch.StartNew();
            var model = IfcStore.Open(fullPath);
            openTimer.Stop();
            cancellationToken.ThrowIfCancellationRequested();

            Xbim3DModelContext? context = null;
            try
            {
                _ = new Xbim.Geometry.Engine.Interop.XbimGeometryEngine();

                var contextTimer = Stopwatch.StartNew();
                context = new Xbim3DModelContext(model);
                context.CreateContext();
                contextTimer.Stop();
                cancellationToken.ThrowIfCancellationRequested();

                var modelLengthFactor = GetLengthToMetersFactor(model);
                var modelGroundElevation = GetModelGroundElevation(model, modelLengthFactor);
                var shapeInstanceCount = CountShapeInstances(context);
                var memoryAfter = GetPrivateMemoryBytes();

                Debug.WriteLine(
                    $"[IfcSession] Created session {sessionId}. " +
                    $"IfcStore.Open={openTimer.ElapsedMilliseconds} ms, " +
                    $"CreateContext={contextTimer.ElapsedMilliseconds} ms, " +
                    $"CreateContextCount=1, ShapeInstances={shapeInstanceCount}, " +
                    $"MemoryAfterContext={FormatBytes(memoryAfter)}");

                return new IfcModelSession(
                    sessionId,
                    fullPath,
                    fileIdentity,
                    model,
                    context,
                    modelLengthFactor,
                    modelGroundElevation,
                    1,
                    shapeInstanceCount,
                    openTimer.Elapsed,
                    contextTimer.Elapsed,
                    memoryBefore,
                    memoryAfter);
            }
            catch
            {
                try
                {
                    model.Close();
                    model.Dispose();
                }
                catch { }

                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                if (GeometryContext is IDisposable disposableContext)
                {
                    disposableContext.Dispose();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IfcSession] Error disposing geometry context: {ex.Message}");
            }

            try
            {
                Model.Close();
                Model.Dispose();
            }
            catch { }

            var memoryAfterUnload = GetPrivateMemoryBytes();
            Debug.WriteLine(
                $"[IfcSession] Disposed session {SessionId}. " +
                $"MemoryBeforeLoad={FormatBytes(_memoryBeforeLoadBytes)}, " +
                $"MemoryAfterUnload={FormatBytes(memoryAfterUnload)}");
        }

        public static string CreateFileIdentity(string ifcPath)
        {
            var fullPath = Path.GetFullPath(ifcPath);
            var info = new FileInfo(fullPath);
            return string.Join("|",
                fullPath.ToUpperInvariant(),
                info.Exists ? info.Length.ToString() : "missing",
                info.Exists ? info.LastWriteTimeUtc.Ticks.ToString() : "missing");
        }

        private static int CountShapeInstances(Xbim3DModelContext context)
        {
            try
            {
                return context.ShapeInstances().Count();
            }
            catch
            {
                return 0;
            }
        }

        private static double GetLengthToMetersFactor(IfcStore model)
        {
            var project = model.Instances.OfType<IIfcProject>().FirstOrDefault();
            var units = project?.UnitsInContext?.Units;

            if (units == null)
                return DefaultLengthToMetersFactor;

            foreach (var unit in units)
            {
                if (unit is not IIfcSIUnit si || !string.Equals(si.UnitType.ToString(), "LENGTHUNIT", StringComparison.OrdinalIgnoreCase))
                    continue;

                var prefix = si.Prefix?.ToString();
                return prefix switch
                {
                    "MILLI" => 0.001,
                    "CENTI" => 0.01,
                    "DECI" => 0.1,
                    _ => 1.0
                };
            }

            return DefaultLengthToMetersFactor;
        }

        private static double? GetModelGroundElevation(IfcStore model, double lengthFactor)
        {
            try
            {
                var elevations = model.Instances
                    .OfType<IIfcBuildingStorey>()
                    .Select(s => s.Elevation)
                    .Where(e => e.HasValue)
                    .Select(e => e!.Value * lengthFactor)
                    .ToList();

                return elevations.Count == 0 ? null : elevations.Min();
            }
            catch
            {
                return null;
            }
        }

        private static long GetPrivateMemoryBytes()
        {
            try
            {
                return Process.GetCurrentProcess().PrivateMemorySize64;
            }
            catch
            {
                return GC.GetTotalMemory(false);
            }
        }

        private static string FormatBytes(long bytes)
            => $"{bytes / (1024d * 1024d):F1} MB";
    }
}
