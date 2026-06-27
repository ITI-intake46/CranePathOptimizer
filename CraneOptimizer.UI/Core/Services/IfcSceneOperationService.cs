using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using System.Collections.Generic;

namespace CraneOptimizer.UI.Core.Services
{
    public sealed class IfcSceneOperationService : IIfcSceneOperationService
    {
        private readonly ISceneService _scene;
        private readonly IIfcReaderRepository _ifcReader;

        public IfcSceneOperationService(ISceneService scene, IIfcReaderRepository ifcReader)
        {
            _scene = scene;
            _ifcReader = ifcReader;
        }

        public bool HasIfcModel => _scene.GetElements().Count > 0;

        public List<PrecastElement> GetElements() => _scene.GetElements();

        public List<PrecastElement> TranslateElements(IEnumerable<PrecastElement> elements, DomainPoint3D offset)
        {
            return elements
                .Select(element => TranslateElement(element, offset))
                .ToList();
        }

        public List<PrecastElement> ReplaceIfcModel(IEnumerable<PrecastElement> elements)
        {
            var modelElements = elements.ToList();
            _scene.SetElements(modelElements);
            return modelElements;
        }

        public List<PrecastElement> MergeIfcModel(IEnumerable<PrecastElement> elements)
        {
            var mergedElements = _scene.GetElements();
            mergedElements.AddRange(elements);
            _scene.SetElements(mergedElements);
            return mergedElements;
        }

        public List<PrecastElement> MoveIfcModel(DomainPoint3D delta)
        {
            var movedElements = TranslateElements(_scene.GetElements(), delta);
            _scene.ClearSelectedElements();
            _scene.SetElements(movedElements);
            return movedElements;
        }

        public void DeleteIfcModel(bool clearReaderData = true)
        {
            _scene.ClearSelectedElements();
            _scene.ClearElements();

            if (clearReaderData)
            {
                _ifcReader.ClearData();
                ElementVisualBuilder.ClearMeshCache("IFC unloaded");
            }
        }

        private static PrecastElement TranslateElement(PrecastElement element, DomainPoint3D offset)
        {
            var translated = element.CreateCopy();
            translated.ElementCOG = new DomainPoint3D(
                element.ElementCOG.X + offset.X,
                element.ElementCOG.Y + offset.Y,
                element.ElementCOG.Z + offset.Z);
            translated.COG_M = new DomainPoint3D(
                element.COG_M.X + offset.X,
                element.COG_M.Y + offset.Y,
                element.COG_M.Z + offset.Z);
            return translated;
        }
    }
}
