using CraneOptimizer.Domain.Schemas;
using System.Threading;
using System.Threading.Tasks;

namespace CraneOptimizer.Domain.Interfaces
{
    public interface IIfcReaderRepository : IDisposable
    {
        List<PrecastElement> ReadElements(string ifcPath);
        List<PrecastElement> ReadElements(string path, IProgress<double> progress);
        
        Task InitializeSessionForRenderingAsync(string ifcPath, CancellationToken cancellationToken = default);
        
        double ModelLengthFactor { get; }
        double? ModelGroundElevation { get; }
        
        void ClearData();
    }
}
