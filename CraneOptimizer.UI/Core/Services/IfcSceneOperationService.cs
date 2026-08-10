using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Services;
using System.Collections.Generic;

namespace CraneOptimizer.UI.Core.Services
{
    public sealed class IfcSceneOperationService : IIfcSceneOperationService
    {
        private readonly ISceneService _scene;
        private readonly IIfcReaderRepository _ifcReader;
        private readonly IOptimizationViewportHighlightService? _viewportHighlightService;

        public IfcSceneOperationService(
            ISceneService scene,
            IIfcReaderRepository ifcReader,
            IOptimizationViewportHighlightService? viewportHighlightService = null)
        {
            _scene = scene;
            _ifcReader = ifcReader;
            _viewportHighlightService = viewportHighlightService;
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
            _viewportHighlightService?.Clear();
            var modelElements = elements.ToList();
            _scene.SetElements(modelElements);
            return modelElements;
        }

        public List<PrecastElement> MergeIfcModel(IEnumerable<PrecastElement> elements)
        {
            _viewportHighlightService?.Clear();
            var mergedElements = _scene.GetElements();
            mergedElements.AddRange(elements);
            _scene.SetElements(mergedElements);
            return mergedElements;
        }

        public List<PrecastElement> MoveIfcModel(DomainPoint3D delta)
        {
            _viewportHighlightService?.Clear();
            var movedElements = TranslateElements(_scene.GetElements(), delta);
            _scene.ClearSelectedElements();
            _scene.SetElements(movedElements);
            return movedElements;
        }

        public void DeleteIfcModel(bool clearReaderData = true)
        {
            _viewportHighlightService?.Clear();
            _scene.ClearSelectedElements();
            _scene.ClearElements();

            if (clearReaderData)
            {
                ElementVisualBuilder.ClearMeshCache("IFC unloaded");
                _ifcReader.ClearData();
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
