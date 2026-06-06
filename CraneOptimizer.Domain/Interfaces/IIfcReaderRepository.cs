using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CraneOptimizer.Domain.Interfaces
{
    public interface IIfcReaderRepository : IDisposable
    {
        List<PrecastElement> ReadElements(string ifcPath);
        List<PrecastElement> ReadElements(string ifcPath, IfcReadingOptions? options);
        List<PrecastElement> ReadElements(string path, IProgress<double> progress);
        List<PrecastElement> ReadElements(string path, IProgress<double> progress, IfcReadingOptions? options, CancellationToken cancellationToken = default);
        
        Task InitializeSessionForRenderingAsync(string ifcPath, CancellationToken cancellationToken = default);
        
        double ModelLengthFactor { get; }
        double? ModelGroundElevation { get; }
        
        void ClearData();
    }
}
