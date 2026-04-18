using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Domain.Interfaces
{
    public interface IIfcReaderRepository
    {
        List<PrecastElement> ReadElements(string ifcPath);
    }
}
