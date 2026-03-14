using CraneOptimizer.Domain.IfcEntities;

namespace CraneOptimizer.Domain.Interfaces
{
    public interface IIfcReader
    {
        List<PrecastElement> ReadElements(string ifcPath);
    }
}
