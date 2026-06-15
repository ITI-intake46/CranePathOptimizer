using CraneOptimizer.Domain.CreatedProject;

namespace CraneOptimizer.Domain.Interfaces
{
    public interface IIfcGeoreferencingExtractor
    {
        IfcGeoreferencingResult? Extract(object model);
        IfcGeoreferencingResult? ExtractFromFile(string filePath);
    }
}
