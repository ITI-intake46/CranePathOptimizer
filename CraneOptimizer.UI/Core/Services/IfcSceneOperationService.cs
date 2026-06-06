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
            return new PrecastElement
            {
                ElementID = element.ElementID,
                ElementRevitId = element.ElementRevitId,
                ElementMark = element.ElementMark,
                ElementType = element.ElementType,
                ElementLength = element.ElementLength,
                ElementWidth = element.ElementWidth,
                ElementHeight = element.ElementHeight,
                ElementMaterial = element.ElementMaterial,
                ElementWT = element.ElementWT,
                ElementVolume = element.ElementVolume,
                ElementCOG = new DomainPoint3D(
                    element.ElementCOG.X + offset.X,
                    element.ElementCOG.Y + offset.Y,
                    element.ElementCOG.Z + offset.Z),
                BBoxLength = element.BBoxLength,
                BBoxWidth = element.BBoxWidth,
                BBoxHeight = element.BBoxHeight,

                // Copy all Phase 2A Normalization properties
                RawIfcType = element.RawIfcType,
                ResolvedCategory = element.ResolvedCategory,
                Role = element.Role,
                ParentGlobalId = element.ParentGlobalId,
                ChildGlobalIds = new List<string>(element.ChildGlobalIds ?? new()),
                VisibleGeometryOwnerGlobalIds = new List<string>(element.VisibleGeometryOwnerGlobalIds ?? new()),
                HiddenInternalGeometryOwnerGlobalIds = new List<string>(element.HiddenInternalGeometryOwnerGlobalIds ?? new()),
                IncludeInRender = element.IncludeInRender,
                IncludeInElementTree = element.IncludeInElementTree,
                IncludeInGa = element.IncludeInGa,
                WeightTons = element.WeightTons,
                WeightSource = element.WeightSource,
                WeightTrustLevel = element.WeightTrustLevel,
                VolumeM3 = element.VolumeM3,
                COG_M = new DomainPoint3D(
                    element.COG_M.X + offset.X,
                    element.COG_M.Y + offset.Y,
                    element.COG_M.Z + offset.Z),
                BoundingBox_M = new DomainPoint3D(
                    element.BoundingBox_M.X,
                    element.BoundingBox_M.Y,
                    element.BoundingBox_M.Z),
                ExtractionWarnings = new List<string>(element.ExtractionWarnings ?? new()),
                NormalizationDecisionReason = element.NormalizationDecisionReason
            };
        }
    }
}
