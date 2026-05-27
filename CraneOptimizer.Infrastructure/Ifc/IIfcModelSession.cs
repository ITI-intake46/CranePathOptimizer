using Xbim.Ifc;
using Xbim.ModelGeometry.Scene;

namespace CraneOptimizer.Infrastructure.Ifc
{
    public interface IIfcModelSession : IDisposable
    {
        Guid SessionId { get; }
        string IfcPath { get; }
        string FileIdentity { get; }
        IfcStore Model { get; }
        Xbim3DModelContext GeometryContext { get; }
        double ModelLengthFactor { get; }
        double? ModelGroundElevation { get; }
        int CreateContextCount { get; }
        int ShapeInstanceCount { get; }
        TimeSpan OpenDuration { get; }
        TimeSpan CreateContextDuration { get; }
        long MemoryBeforeLoadBytes { get; }
        long MemoryAfterCreateContextBytes { get; }
        bool IsDisposed { get; }
    }
}
