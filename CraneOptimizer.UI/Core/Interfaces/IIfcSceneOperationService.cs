using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface IIfcSceneOperationService
    {
        bool HasIfcModel { get; }

        List<PrecastElement> GetElements();
        List<PrecastElement> TranslateElements(IEnumerable<PrecastElement> elements, DomainPoint3D offset);
        List<PrecastElement> ReplaceIfcModel(IEnumerable<PrecastElement> elements);
        List<PrecastElement> MergeIfcModel(IEnumerable<PrecastElement> elements);
        List<PrecastElement> MoveIfcModel(DomainPoint3D delta);
        void DeleteIfcModel(bool clearReaderData = true);
    }
}
