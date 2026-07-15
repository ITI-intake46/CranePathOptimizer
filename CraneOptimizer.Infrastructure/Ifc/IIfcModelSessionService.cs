using CraneOptimizer.Domain.Diagnostics;
using System;

namespace CraneOptimizer.Infrastructure.Ifc
{
    public interface IIfcModelSessionService : IDisposable
    {
        IIfcModelSession? Current { get; }
        IIfcModelSession OpenSession(string ifcPath, IProgress<IfcLoadingProgressUpdate>? progress = null, CancellationToken cancellationToken = default);
        IIfcModelSession? GetActiveSession(string ifcPath);
        void Clear();
    }
}
