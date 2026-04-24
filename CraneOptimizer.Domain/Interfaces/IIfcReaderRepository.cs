using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Domain.Interfaces
{
    public interface IIfcReaderRepository
    {
        List<PrecastElement> ReadElements(string ifcPath);
        List<PrecastElement> ReadElements(string path, IProgress<double> progress);
    }
}
