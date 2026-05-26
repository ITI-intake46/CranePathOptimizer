using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using HelixToolkit.Geometry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Xbim.Common.Geometry;
using Xbim.Common.XbimExtensions;
using Xbim.Ifc4.Interfaces;
using System.Diagnostics;
using MediaMeshGeometry3D = System.Windows.Media.Media3D.MeshGeometry3D;

namespace CraneOptimizer.UI.Features.F2_IfcViewer
{
    public class ElementVisualBuilder : IVisualBuilder<(PrecastElement element, LiftStatus? status, bool isSelected)>
    {
        private const double MinimumBoxSize = 1d;

        // ── Diagnostics counters (instance-scoped, reset per render cycle) ──
        public int DiagTotalElements { get; private set; }
        public int DiagVisibleElements { get; private set; }
        public int DiagIncludeInRenderSkipped { get; private set; }
        public int DiagShapeInstances { get; private set; }
        public int DiagMeshesCreated { get; private set; }
        public int DiagMeshCacheHits { get; private set; }
        public int DiagMeshCacheMisses { get; private set; }
        public int DiagGeometryModel3DCount { get; private set; }
        public int DiagModelVisual3DCount { get; private set; }
        public int DiagMaterialCreated { get; private set; }
        public int DiagFallbackBoxCount { get; private set; }
        public int DiagRealMeshElements { get; private set; }

        // Phase 2.3B High Quality mesh cache diagnostics counters
        public int DiagHighQualityMeshCacheHits { get; private set; }
        public int DiagHighQualityMeshCacheMisses { get; private set; }
        public int DiagHighQualityMeshesCreated { get; private set; }

        // Phase 2.2A Batching diagnostics counters
        public int DiagBatchedGroups { get; private set; }
        public int DiagBatchedElements { get; private set; }
        public int DiagHitTestBoxes { get; private set; }
        public int DiagRenderGeometryModel3DCount { get; private set; }
        public int DiagHitTestGeometryModel3DCount { get; private set; }
        public int DiagTotalGeometryModel3DCount { get; private set; }

        public void ResetCounters()
        {
            DiagTotalElements = 0;
            DiagVisibleElements = 0;
            DiagIncludeInRenderSkipped = 0;
            DiagShapeInstances = 0;
            DiagMeshesCreated = 0;
            DiagMeshCacheHits = 0;
            DiagMeshCacheMisses = 0;
            DiagGeometryModel3DCount = 0;
            DiagModelVisual3DCount = 0;
            DiagMaterialCreated = 0;
            DiagFallbackBoxCount = 0;
            DiagRealMeshElements = 0;

            DiagHighQualityMeshCacheHits = 0;
            DiagHighQualityMeshCacheMisses = 0;
            DiagHighQualityMeshesCreated = 0;

            DiagBatchedGroups = 0;
            DiagBatchedElements = 0;
            DiagHitTestBoxes = 0;
            DiagRenderGeometryModel3DCount = 0;
            DiagHitTestGeometryModel3DCount = 0;
            DiagTotalGeometryModel3DCount = 0;
        }

        public enum MeshQuality
        {
            Low,
            Medium,
            High
        }

        public static MeshQuality CurrentQuality { get; set; } = MeshQuality.Medium;
        public static bool UseBatchedRealMeshRendering { get; set; } = true;
        public static bool UseSpatialHitTestingInsteadOfTransparentBoxes { get; set; } = true;

        public sealed class SpatialHitTestElement
        {
            public Guid? SessionId { get; set; }
            public DateTime RenderTimestampUtc { get; set; }
            public string ElementId { get; set; } = null!;
            public PrecastElement Element { get; set; } = null!;
            public Point3D Min { get; set; }
            public Point3D Max { get; set; }
            public bool IsVisible { get; set; }
            public List<(MediaMeshGeometry3D Mesh, Matrix3D Transform)> Meshes { get; set; } = new();

            // Phase 2.4.2 diagnostics: AABB derivation source tracking
            public bool UsedMeshDerivedBounds { get; set; }
            public Point3D CogBasedMin { get; set; }
            public Point3D CogBasedMax { get; set; }
        }

        private static readonly object MeshCacheGate = new();
        private static readonly Dictionary<MeshCacheKey, MediaMeshGeometry3D> MeshCache = new();
        private static Guid? MeshCacheSessionId;

        private static readonly object HighQualityMeshCacheGate = new();
        private static readonly Dictionary<HighQualityMeshCacheKey, MediaMeshGeometry3D> HighQualityMeshCache = new();

        public static int HighQualityMeshCacheCount
        {
            get
            {
                lock (HighQualityMeshCacheGate)
                {
                    return HighQualityMeshCache.Count;
                }
            }
        }

        private static readonly object SpatialHitTestGate = new();
        private static readonly List<SpatialHitTestElement> SpatialHitTestElements = new();

        public static int SpatialHitTestElementCount
        {
            get
            {
                lock (SpatialHitTestGate)
                {
                    return SpatialHitTestElements.Count;
                }
            }
        }

        public static void RegisterSpatialHitTestElements(IEnumerable<SpatialHitTestElement> elements)
        {
            lock (SpatialHitTestGate)
            {
                SpatialHitTestElements.Clear();
                SpatialHitTestElements.AddRange(elements);
            }
        }

        public static IReadOnlyList<SpatialHitTestElement> GetSpatialHitTestElements()
        {
            lock (SpatialHitTestGate)
            {
                return SpatialHitTestElements.ToList();
            }
        }

        private readonly IIfcReaderRepository? _ifcReader;
        private readonly DomainPoint3D? _origin;
        private readonly DomainPoint3D? _placementOffset;
        private readonly bool _renderOnlyStatusElements;
        private readonly byte? _statusAlpha;

        public static void ClearMeshCache(string reason = "manual")
        {
            lock (MeshCacheGate)
            {
                var count = MeshCache.Count;
                MeshCache.Clear();
                MeshCacheSessionId = null;
                Debug.WriteLine($"[ElementVisualBuilder] Mesh cache cleared. Reason={reason}, Removed={count}");
            }

            lock (HighQualityMeshCacheGate)
            {
                var count = HighQualityMeshCache.Count;
                HighQualityMeshCache.Clear();
                Debug.WriteLine($"[ElementVisualBuilder] High Quality Mesh cache cleared. Reason={reason}, Removed={count}");
            }

            lock (SpatialHitTestGate)
            {
                var count = SpatialHitTestElements.Count;
                SpatialHitTestElements.Clear();
                Debug.WriteLine($"[ElementVisualBuilder] Spatial Hit Test elements cleared. Reason={reason}, Removed={count}");
            }
        }

        public record RenderCacheStats(Guid? SessionId, int CacheCount);

        public static RenderCacheStats GetCacheStats()
        {
            lock (MeshCacheGate)
            {
                return new RenderCacheStats(MeshCacheSessionId, MeshCache.Count);
            }
        }

        public static void ClearForTesting()
        {
            ClearMeshCache("testing");
        }

        public static MediaMeshGeometry3D? GetOrCreateCachedMeshForTesting(
            Guid sessionId,
            byte[] shapeData,
            double oneMeter,
            MeshQuality quality,
            out bool reused)
        {
            return GetOrCreateCachedMesh(sessionId, shapeData, oneMeter, quality, out reused);
        }

        public static void EnsureCacheSessionForTesting(Guid sessionId)
        {
            EnsureCacheSession(sessionId);
        }

        private static int MeshCacheCount
        {
            get
            {
                lock (MeshCacheGate)
                {
                    return MeshCache.Count;
                }
            }
        }

        private static long GetPrivateMemoryBytes()
        {
            try
            {
                return Process.GetCurrentProcess().PrivateMemorySize64;
            }
            catch
            {
                return GC.GetTotalMemory(false);
            }
        }

        private static string FormatBytes(long bytes) =>
            $"{bytes / (1024d * 1024d):F1} MB";

        public ElementVisualBuilder(
            IIfcReaderRepository? ifcReader = null,
            DomainPoint3D? origin = null,
            DomainPoint3D? placementOffset = null,
            bool renderOnlyStatusElements = false,
            byte? statusAlpha = null)
        {
            _ifcReader = ifcReader;
            _origin = origin;
            _placementOffset = placementOffset;
            _renderOnlyStatusElements = renderOnlyStatusElements;
            _statusAlpha = statusAlpha;
        }

        public Visual3D Build((PrecastElement element, LiftStatus? status, bool isSelected) item)
        {
            DiagTotalElements++;
            var container = new ModelVisual3D();
            if (!item.element.IncludeInRender)
            {
                DiagIncludeInRenderSkipped++;
                return container;
            }
            DiagVisibleElements++;
            BuildFallbackBox(container, item.element, item.status, item.isSelected);
            return container;
        }

        public Visual3D BuildBatch(IEnumerable<(PrecastElement element, LiftStatus? status, bool isSelected)> items)
        {
            var elementMeshes = new Dictionary<string, List<(MediaMeshGeometry3D Mesh, Matrix3D Transform)>>(StringComparer.OrdinalIgnoreCase);
            var allItems = items.ToList();
            DiagTotalElements += allItems.Count;
            var skipped = allItems.Count(i => !i.element.IncludeInRender);
            DiagIncludeInRenderSkipped += skipped;

            var itemsList = allItems.Where(item => item.element.IncludeInRender).ToList();
            DiagVisibleElements += itemsList.Count;
            
            // DEBUG: Verify status distribution
            int withStatus = itemsList.Count(i => i.status.HasValue);
            Debug.WriteLine($"[ElementVisualBuilder] BuildBatch called for {itemsList.Count} items. Items with LiftStatus: {withStatus}");

            if (_renderOnlyStatusElements)
                itemsList = itemsList.Where(item => item.status.HasValue).ToList();

            if (CurrentQuality == MeshQuality.Low)
            {
                var lowVisual = BuildLargeModelBatch(itemsList, out var lowGeometryModels);
                DiagGeometryModel3DCount += lowGeometryModels;
                DiagModelVisual3DCount++;
                DiagRenderGeometryModel3DCount = lowGeometryModels;
                DiagHitTestGeometryModel3DCount = 0;
                DiagTotalGeometryModel3DCount = lowGeometryModels;
                Debug.WriteLine(
                    $"[ElementVisualBuilder] Low quality render complete. Elements={itemsList.Count}, " +
                    $"GeometryModel3D={lowGeometryModels}, ModelVisual3D=1, MemoryAfterRender={FormatBytes(GetPrivateMemoryBytes())}");

                var lowOrigin = _origin ?? new DomainPoint3D(0, 0, 0);
                var lowSessionId = (_ifcReader as CraneOptimizer.Infrastructure.Repositories.IfcReaderRepository)?.CurrentSessionId;
                PopulateSpatialHitTestRegistry(allItems, lowSessionId, lowOrigin, elementMeshes);

                return lowVisual;
            }

            if (_ifcReader is CraneOptimizer.Infrastructure.Repositories.IfcReaderRepository concreteRepo
                && concreteRepo.GeometryContext != null
                && concreteRepo.CurrentModel != null)
            {
                var model = concreteRepo.CurrentModel;
                var context = concreteRepo.GeometryContext;
                var sessionId = concreteRepo.CurrentSessionId ?? Guid.Empty;
                EnsureCacheSession(sessionId);

                double oneMeter = model.ModelFactors.OneMeter;
                if (oneMeter <= 0) oneMeter = 1000.0;

                var origin = _origin ?? new DomainPoint3D(0, 0, 0);
                var placementOffset = _placementOffset ?? new DomainPoint3D(0, 0, 0);

                var selectionMaterial = CreateFrozenDiffuseMaterial(Color.FromArgb(110, 100, 180, 255));

                if (!UseBatchedRealMeshRendering)
                {
                    // ─── OLD PRE-OPTIMIZATION RENDERING PATH ───
                    var meshesCreated = 0;
                    var meshesReused = 0;
                    var geometryModelsCreated = 0;
                    var modelVisualsCreated = 1; // batch root visual
                    DiagRealMeshElements += itemsList.Count;

                    var elementMap = new Dictionary<string, (PrecastElement el, LiftStatus? st, Color col, bool sel)>(StringComparer.OrdinalIgnoreCase);
                    foreach (var item in itemsList)
                    {
                        var color = GetColor(item.status);
                        elementMap[item.element.ElementID] = (item.element, item.status, color, item.isSelected);
                    }

                    var labelToGid = new Dictionary<int, string>();
                    foreach (var p in model.Instances.OfType<IIfcProduct>())
                    {
                        var gid = p.GlobalId.ToString();
                        var revitId = (p as IIfcElement)?.Tag?.ToString();

                        var matchingKey = string.Empty;
                        if (revitId != null && elementMap.ContainsKey(revitId))
                            matchingKey = revitId;
                        else if (!string.IsNullOrEmpty(gid) && elementMap.ContainsKey(gid))
                            matchingKey = gid;

                        if (!string.IsNullOrEmpty(matchingKey))
                        {
                            labelToGid[p.EntityLabel] = matchingKey;
                            AddChildProductLabels(p, matchingKey, labelToGid);
                        }
                    }

                    var allInstances = context.ShapeInstances()
                        .Where(si => labelToGid.ContainsKey(si.IfcProductLabel))
                        .ToList();
                    DiagShapeInstances += allInstances.Count;

                    var batchVisual = new ModelVisual3D();
                    var elementVisualMap = new Dictionary<string, ModelVisual3D>();

                    foreach (var si in allInstances)
                    {
                        try
                        {
                            var shapeGeom = context.ShapeGeometry(si) as IXbimShapeGeometryData;
                            if (shapeGeom?.ShapeData == null || shapeGeom.ShapeData.Length == 0) continue;

                            var gid = labelToGid[si.IfcProductLabel];
                            var (el, _, col, isSelected) = elementMap[gid];

                            var cacheReused = false;
                            var mesh = CurrentQuality == MeshQuality.High
                                ? GetOrCreateHighQualityCachedMesh(sessionId, shapeGeom.ShapeData, si, shapeGeom, oneMeter, origin, placementOffset, out cacheReused)
                                : GetOrCreateCachedMesh(sessionId, shapeGeom.ShapeData, oneMeter, CurrentQuality, out cacheReused);
                            if (mesh == null || mesh.Positions.Count == 0) continue;

                            if (!elementMeshes.TryGetValue(gid, out var meshesList))
                            {
                                meshesList = new List<(MediaMeshGeometry3D, Matrix3D)>();
                                elementMeshes[gid] = meshesList;
                            }
                            var matrix = CurrentQuality == MeshQuality.High
                                ? Matrix3D.Identity
                                : CreateInstanceWpfMatrix(si.Transformation, oneMeter, origin, placementOffset);
                            meshesList.Add((mesh, matrix));

                            if (CurrentQuality == MeshQuality.High)
                            {
                                if (cacheReused)
                                {
                                    DiagHighQualityMeshCacheHits++;
                                    meshesReused++;
                                }
                                else
                                {
                                    DiagHighQualityMeshCacheMisses++;
                                    DiagHighQualityMeshesCreated++;
                                    meshesCreated++;
                                }
                            }
                            else if (cacheReused)
                            {
                                meshesReused++;
                            }
                            else
                            {
                                meshesCreated++;
                            }

                            if (!elementVisualMap.TryGetValue(gid, out var ev))
                            {
                                ev = new ModelVisual3D { Content = new Model3DGroup() };
                                ev.SetValue(System.Windows.FrameworkElement.TagProperty, el);
                                elementVisualMap[gid] = ev;
                                batchVisual.Children.Add(ev);
                                modelVisualsCreated++;
                            }

                            var material = BuildMaterial(col, isSelected, selectionMaterial);
                            DiagMaterialCreated++;

                            var geoModel = new GeometryModel3D
                            {
                                Geometry = mesh,
                                Material = material,
                                BackMaterial = material,
                                Transform = CurrentQuality == MeshQuality.High
                                    ? Transform3D.Identity
                                    : CreateInstanceTransform(si.Transformation, oneMeter, origin, placementOffset)
                            };

                            ((Model3DGroup)ev.Content).Children.Add(geoModel);
                            geometryModelsCreated++;
                        }
                        catch
                        {
                            // keep rendering robust per element
                        }
                    }

                    // Propagate local counters to diagnostics properties
                    DiagMeshesCreated += meshesCreated;
                    DiagMeshCacheHits += meshesReused;
                    DiagMeshCacheMisses += meshesCreated; // miss = had to create
                    DiagGeometryModel3DCount += geometryModelsCreated;
                    DiagModelVisual3DCount += modelVisualsCreated;
                    DiagRenderGeometryModel3DCount = geometryModelsCreated;
                    DiagHitTestGeometryModel3DCount = 0;
                    DiagTotalGeometryModel3DCount = geometryModelsCreated;

                    Debug.WriteLine(
                        $"[ElementVisualBuilder] IFC mesh render complete. SessionId={sessionId}, " +
                        $"Elements={itemsList.Count}, ShapeInstances={allInstances.Count}, MeshesCreated={meshesCreated}, " +
                        $"CachedMeshesReused={meshesReused}, CacheSize={MeshCacheCount}, " +
                        $"GeometryModel3D={geometryModelsCreated}, ModelVisual3D={modelVisualsCreated}, " +
                        $"MemoryAfterRender={FormatBytes(GetPrivateMemoryBytes())}");

                    PopulateSpatialHitTestRegistry(allItems, sessionId, origin, elementMeshes);
                    return batchVisual;
                }
                else
                {
                    // ─── HIGH-PERFORMANCE BATCHED REAL MESH RENDERING PATH ───
                    var meshesCreated = 0;
                    var meshesReused = 0;
                    var geometryModelsCreated = 0;
                    var modelVisualsCreated = 1; // batch root visual
                    DiagRealMeshElements += itemsList.Count;

                    var elementMap = new Dictionary<string, (PrecastElement el, LiftStatus? st, Color col, bool sel)>(StringComparer.OrdinalIgnoreCase);
                    foreach (var item in itemsList)
                    {
                        var color = GetColor(item.status);
                        elementMap[item.element.ElementID] = (item.element, item.status, color, item.isSelected);
                    }

                    var labelToGid = new Dictionary<int, string>();
                    foreach (var p in model.Instances.OfType<IIfcProduct>())
                    {
                        var gid = p.GlobalId.ToString();
                        var revitId = (p as IIfcElement)?.Tag?.ToString();

                        var matchingKey = string.Empty;
                        if (revitId != null && elementMap.ContainsKey(revitId))
                            matchingKey = revitId;
                        else if (!string.IsNullOrEmpty(gid) && elementMap.ContainsKey(gid))
                            matchingKey = gid;

                        if (!string.IsNullOrEmpty(matchingKey))
                        {
                            labelToGid[p.EntityLabel] = matchingKey;
                            AddChildProductLabels(p, matchingKey, labelToGid);
                        }
                    }

                    var allInstances = context.ShapeInstances()
                        .Where(si => labelToGid.ContainsKey(si.IfcProductLabel))
                        .ToList();
                    DiagShapeInstances += allInstances.Count;

                    var batchVisual = new ModelVisual3D();

                    // Separate Selected and Unselected instances
                    var selectedInstances = new List<XbimShapeInstance>();
                    var unselectedInstances = new List<XbimShapeInstance>();

                    foreach (var si in allInstances)
                    {
                        var gid = labelToGid[si.IfcProductLabel];
                        var (_, _, _, isSelected) = elementMap[gid];
                        if (isSelected)
                            selectedInstances.Add(si);
                        else
                            unselectedInstances.Add(si);
                    }

                    // A. Render Selected Element(s) independently
                    var elementVisualMap = new Dictionary<string, ModelVisual3D>();
                    foreach (var si in selectedInstances)
                    {
                        try
                        {
                            var shapeGeom = context.ShapeGeometry(si) as IXbimShapeGeometryData;
                            if (shapeGeom?.ShapeData == null || shapeGeom.ShapeData.Length == 0) continue;

                            var gid = labelToGid[si.IfcProductLabel];
                            var (el, _, col, isSelected) = elementMap[gid];

                            var cacheReused = false;
                            var mesh = CurrentQuality == MeshQuality.High
                                ? GetOrCreateHighQualityCachedMesh(sessionId, shapeGeom.ShapeData, si, shapeGeom, oneMeter, origin, placementOffset, out cacheReused)
                                : GetOrCreateCachedMesh(sessionId, shapeGeom.ShapeData, oneMeter, CurrentQuality, out cacheReused);
                            if (mesh == null || mesh.Positions.Count == 0) continue;

                            if (!elementMeshes.TryGetValue(gid, out var meshesList))
                            {
                                meshesList = new List<(MediaMeshGeometry3D, Matrix3D)>();
                                elementMeshes[gid] = meshesList;
                            }
                            var matrix = CurrentQuality == MeshQuality.High
                                ? Matrix3D.Identity
                                : CreateInstanceWpfMatrix(si.Transformation, oneMeter, origin, placementOffset);
                            meshesList.Add((mesh, matrix));

                            if (CurrentQuality == MeshQuality.High)
                            {
                                if (cacheReused)
                                {
                                    DiagHighQualityMeshCacheHits++;
                                    meshesReused++;
                                }
                                else
                                {
                                    DiagHighQualityMeshCacheMisses++;
                                    DiagHighQualityMeshesCreated++;
                                    meshesCreated++;
                                }
                            }
                            else if (cacheReused)
                            {
                                meshesReused++;
                            }
                            else
                            {
                                meshesCreated++;
                            }

                            if (!elementVisualMap.TryGetValue(gid, out var ev))
                            {
                                ev = new ModelVisual3D { Content = new Model3DGroup() };
                                ev.SetValue(System.Windows.FrameworkElement.TagProperty, el);
                                elementVisualMap[gid] = ev;
                                batchVisual.Children.Add(ev);
                                modelVisualsCreated++;
                            }

                            var material = BuildMaterial(col, isSelected, selectionMaterial);
                            DiagMaterialCreated++;

                            var geoModel = new GeometryModel3D
                            {
                                Geometry = mesh,
                                Material = material,
                                BackMaterial = material,
                                Transform = CurrentQuality == MeshQuality.High
                                    ? Transform3D.Identity
                                    : CreateInstanceTransform(si.Transformation, oneMeter, origin, placementOffset)
                            };

                            ((Model3DGroup)ev.Content).Children.Add(geoModel);
                            geometryModelsCreated++;
                        }
                        catch { }
                    }

                    // B. Group and Concatenate Unselected Elements
                    var batchedGroups = new Dictionary<RenderBatchKey, (Point3DCollection positions, Vector3DCollection normals, Int32Collection indices)>();
                    var unselectedGidsHandled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var si in unselectedInstances)
                    {
                        try
                        {
                            var shapeGeom = context.ShapeGeometry(si) as IXbimShapeGeometryData;
                            if (shapeGeom?.ShapeData == null || shapeGeom.ShapeData.Length == 0) continue;

                            var gid = labelToGid[si.IfcProductLabel];
                            var (el, status, col, _) = elementMap[gid];
                            unselectedGidsHandled.Add(gid);

                            var cacheReused = false;
                            var localMesh = CurrentQuality == MeshQuality.High
                                ? GetOrCreateHighQualityCachedMesh(sessionId, shapeGeom.ShapeData, si, shapeGeom, oneMeter, origin, placementOffset, out cacheReused)
                                : GetOrCreateCachedMesh(sessionId, shapeGeom.ShapeData, oneMeter, CurrentQuality, out cacheReused);
                            if (localMesh == null || localMesh.Positions.Count == 0) continue;

                            if (!elementMeshes.TryGetValue(gid, out var meshesList))
                            {
                                meshesList = new List<(MediaMeshGeometry3D, Matrix3D)>();
                                elementMeshes[gid] = meshesList;
                            }
                            var matrix = CurrentQuality == MeshQuality.High
                                ? Matrix3D.Identity
                                : CreateInstanceWpfMatrix(si.Transformation, oneMeter, origin, placementOffset);
                            meshesList.Add((localMesh, matrix));

                            if (CurrentQuality == MeshQuality.High)
                            {
                                if (cacheReused)
                                {
                                    DiagHighQualityMeshCacheHits++;
                                    meshesReused++;
                                }
                                else
                                {
                                    DiagHighQualityMeshCacheMisses++;
                                    DiagHighQualityMeshesCreated++;
                                    meshesCreated++;
                                }
                            }
                            else if (cacheReused)
                            {
                                meshesReused++;
                            }
                            else
                            {
                                meshesCreated++;
                            }

                            var key = new RenderBatchKey(col, status, NormalizeBatchText(el.ElementMaterial), NormalizeBatchText(el.ElementType));
                            if (!batchedGroups.TryGetValue(key, out var geomData))
                            {
                                geomData = (new Point3DCollection(), new Vector3DCollection(), new Int32Collection());
                                batchedGroups[key] = geomData;
                            }

                            if (CurrentQuality == MeshQuality.High)
                            {
                                ConcatenateMesh(geomData.positions, geomData.normals, geomData.indices, localMesh, Matrix3D.Identity);
                            }
                            else
                            {
                                var wpfMatrix = CreateInstanceWpfMatrix(si.Transformation, oneMeter, origin, placementOffset);
                                ConcatenateMesh(geomData.positions, geomData.normals, geomData.indices, localMesh, wpfMatrix);
                            }
                        }
                        catch { }
                    }

                    // C. Render Consolidated Batched Mesh groups
                    var batchedGroupsCount = 0;
                    foreach (var kvp in batchedGroups)
                    {
                        var key = kvp.Key;
                        var (positions, normals, indices) = kvp.Value;
                        if (positions.Count == 0) continue;

                        var batchedMesh = new MediaMeshGeometry3D
                        {
                            Positions = positions,
                            Normals = normals,
                            TriangleIndices = indices
                        };
                        batchedMesh.Freeze();

                        var material = BuildMaterial(key.Color, false, selectionMaterial);
                        if (material.CanFreeze)
                            material.Freeze();
                        DiagMaterialCreated++;

                        var geoModel = new GeometryModel3D
                        {
                            Geometry = batchedMesh,
                            Material = material,
                            BackMaterial = material
                        };
                        geoModel.Freeze();

                        var groupVisual = new ModelVisual3D { Content = geoModel };
                        batchVisual.Children.Add(groupVisual);
                        modelVisualsCreated++;
                        geometryModelsCreated++;
                        batchedGroupsCount++;
                    }

                    // D. Render Lightweight Transparent Hit-Testing Boxes for selection
                    var hitBoxesCount = 0;
                    if (!UseSpatialHitTestingInsteadOfTransparentBoxes)
                    {
                        foreach (var item in itemsList)
                        {
                            try
                            {
                                var isSelected = item.isSelected;
                                if (!isSelected)
                                {
                                    var hitBox = CreateTransparentHitBox(item.element, origin, placementOffset);
                                    batchVisual.Children.Add(hitBox);
                                    modelVisualsCreated++;
                                    hitBoxesCount++;
                                }
                            }
                            catch { }
                        }
                    }

                    // E. Set Diagnostics counters
                    DiagMeshesCreated += meshesCreated;
                    DiagMeshCacheHits += meshesReused;
                    DiagMeshCacheMisses += meshesCreated;

                    DiagBatchedGroups = batchedGroupsCount;
                    DiagBatchedElements = unselectedGidsHandled.Count;
                    DiagHitTestBoxes = hitBoxesCount;
                    DiagRenderGeometryModel3DCount = geometryModelsCreated;
                    DiagHitTestGeometryModel3DCount = hitBoxesCount;
                    DiagGeometryModel3DCount = DiagRenderGeometryModel3DCount + DiagHitTestGeometryModel3DCount;
                    DiagModelVisual3DCount += modelVisualsCreated;
                    DiagTotalGeometryModel3DCount = DiagGeometryModel3DCount;

                    Debug.WriteLine(
                        $"[ElementVisualBuilder] IFC batched mesh render complete. SessionId={sessionId}, " +
                        $"Elements={itemsList.Count}, ShapeInstances={allInstances.Count}, BatchedGroups={batchedGroupsCount}, " +
                        $"RenderGeometryModels={geometryModelsCreated}, HitBoxes={hitBoxesCount}, MeshesCreated={meshesCreated}, " +
                        $"CachedMeshesReused={meshesReused}, CacheSize={MeshCacheCount}, " +
                        $"TotalModelVisuals={modelVisualsCreated}, MemoryAfterRender={FormatBytes(GetPrivateMemoryBytes())}");

                    PopulateSpatialHitTestRegistry(allItems, sessionId, origin, elementMeshes);
                    return batchVisual;
                }
            }

            // Fallback path: no xBIM context, use bounding boxes
            var fallbackBatch = new ModelVisual3D();
            foreach (var item in itemsList)
                BuildFallbackBox(fallbackBatch, item.element, item.status, item.isSelected);

            DiagRenderGeometryModel3DCount = DiagGeometryModel3DCount;
            DiagHitTestGeometryModel3DCount = 0;
            DiagTotalGeometryModel3DCount = DiagGeometryModel3DCount;

            var fallbackOrigin = _origin ?? new DomainPoint3D(0, 0, 0);
            var fallbackSessionId = (_ifcReader as CraneOptimizer.Infrastructure.Repositories.IfcReaderRepository)?.CurrentSessionId;
            PopulateSpatialHitTestRegistry(allItems, fallbackSessionId, fallbackOrigin, null);

            return fallbackBatch;
        }

        private Visual3D BuildLargeModelBatch(
            IReadOnlyList<(PrecastElement element, LiftStatus? status, bool isSelected)> items,
            out int geometryModelsCreated)
        {
            var group = new Model3DGroup();
            geometryModelsCreated = 0;

            foreach (var renderGroup in items.GroupBy(GetBatchKey))
            {
                var mesh = BuildIndexedBoxMesh(renderGroup.Select(item => item.element));
                if (mesh == null || mesh.Positions.Count == 0)
                    continue;

                mesh.Freeze();
                var material = BuildMaterial(renderGroup.Key.Color, renderGroup.Key.IsSelected, CreateFrozenDiffuseMaterial(Color.FromArgb(110, 100, 180, 255)));
                if (material.CanFreeze)
                    material.Freeze();

                group.Children.Add(new GeometryModel3D
                {
                    Geometry = mesh,
                    Material = material,
                    BackMaterial = material
                });
                geometryModelsCreated++;
            }

            if (group.CanFreeze)
                group.Freeze();

            return new ModelVisual3D { Content = group };
        }

        private BatchKey GetBatchKey((PrecastElement element, LiftStatus? status, bool isSelected) item)
        {
            var element = item.element;
            return new BatchKey(
                GetColor(item.status),
                item.status,
                NormalizeBatchText(element.ElementMaterial),
                NormalizeBatchText(element.ElementType),
                item.isSelected);
        }

        private MediaMeshGeometry3D? BuildIndexedBoxMesh(IEnumerable<PrecastElement> elements)
        {
            var origin = _origin ?? new DomainPoint3D(0, 0, 0);
            var positions = new Point3DCollection();
            var indices = new Int32Collection();

            foreach (var element in elements)
            {
                var length = GetBoxDimension(element.BBoxLength ?? element.ElementLength);
                var width = GetBoxDimension(element.BBoxWidth ?? element.ElementWidth);
                var height = GetBoxDimension(element.BBoxHeight ?? element.ElementHeight);
                var center = new Point3D(
                    element.ElementCOG.X - origin.X,
                    element.ElementCOG.Y - origin.Y,
                    element.ElementCOG.Z - origin.Z);

                AddIndexedBox(positions, indices, center, length, width, height);
            }

            if (positions.Count == 0)
                return null;

            var mesh = new MediaMeshGeometry3D
            {
                Positions = positions,
                TriangleIndices = indices
            };

            mesh.Normals = CalculateIndexedNormals(positions, indices);
            return mesh;
        }

        private static void AddIndexedBox(Point3DCollection positions, Int32Collection indices, Point3D center, double length, double width, double height)
        {
            var hx = length / 2d;
            var hy = width / 2d;
            var hz = height / 2d;
            var start = positions.Count;

            positions.Add(new Point3D(center.X - hx, center.Y - hy, center.Z - hz));
            positions.Add(new Point3D(center.X + hx, center.Y - hy, center.Z - hz));
            positions.Add(new Point3D(center.X + hx, center.Y + hy, center.Z - hz));
            positions.Add(new Point3D(center.X - hx, center.Y + hy, center.Z - hz));
            positions.Add(new Point3D(center.X - hx, center.Y - hy, center.Z + hz));
            positions.Add(new Point3D(center.X + hx, center.Y - hy, center.Z + hz));
            positions.Add(new Point3D(center.X + hx, center.Y + hy, center.Z + hz));
            positions.Add(new Point3D(center.X - hx, center.Y + hy, center.Z + hz));

            AddQuad(indices, start, 0, 3, 2, 1);
            AddQuad(indices, start, 4, 5, 6, 7);
            AddQuad(indices, start, 0, 1, 5, 4);
            AddQuad(indices, start, 1, 2, 6, 5);
            AddQuad(indices, start, 2, 3, 7, 6);
            AddQuad(indices, start, 3, 0, 4, 7);
        }

        private static void AddQuad(Int32Collection indices, int start, int a, int b, int c, int d)
        {
            indices.Add(start + a);
            indices.Add(start + b);
            indices.Add(start + c);
            indices.Add(start + a);
            indices.Add(start + c);
            indices.Add(start + d);
        }

        private static Vector3DCollection CalculateIndexedNormals(Point3DCollection positions, Int32Collection indices)
        {
            var normals = new Vector3D[positions.Count];

            for (var i = 0; i + 2 < indices.Count; i += 3)
            {
                var i0 = indices[i];
                var i1 = indices[i + 1];
                var i2 = indices[i + 2];
                var n = Vector3D.CrossProduct(positions[i1] - positions[i0], positions[i2] - positions[i0]);
                if (n.LengthSquared < 1e-18)
                    continue;

                normals[i0] += n;
                normals[i1] += n;
                normals[i2] += n;
            }

            var result = new Vector3DCollection(positions.Count);
            foreach (var normal in normals)
            {
                var n = normal;
                if (n.LengthSquared < 1e-18)
                    n = new Vector3D(0, 0, 1);
                else
                    n.Normalize();

                result.Add(n);
            }

            return result;
        }

        private static double GetBoxDimension(double? value)
        {
            if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value.Value <= 0)
                return MinimumBoxSize;

            return Math.Max(MinimumBoxSize, value.Value);
        }

        private static string NormalizeBatchText(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

        private readonly record struct BatchKey(Color Color, LiftStatus? Status, string Material, string Type, bool IsSelected);

        private static Material BuildMaterial(Color baseColor, bool isSelected, Material selectionOverlay)
        {
            var mg = new MaterialGroup();
            mg.Children.Add(CreateFrozenDiffuseMaterial(baseColor));

            if (CurrentQuality != MeshQuality.Low)
            {
                // Add slight specular for close-up shape readability in mesh modes.
                var specPower = CurrentQuality == MeshQuality.High ? 90.0 : 35.0;
                var specularBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));
                specularBrush.Freeze();
                mg.Children.Add(new SpecularMaterial(specularBrush, specPower));
            }

            if (isSelected)
                mg.Children.Add(selectionOverlay);

            return mg;
        }

        private static DiffuseMaterial CreateFrozenDiffuseMaterial(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            var material = new DiffuseMaterial(brush);
            material.Freeze();
            return material;
        }

        private static void AddChildProductLabels(IIfcProduct product, string owningGlobalId, Dictionary<int, string> labelToGid)
        {
            var visited = new HashSet<int>();
            AddChildProductLabels(product, owningGlobalId, labelToGid, visited);
        }

        private static void AddChildProductLabels(IIfcProduct product, string owningGlobalId, Dictionary<int, string> labelToGid, HashSet<int> visited)
        {
            if (!visited.Add(product.EntityLabel)) return;
            var children = product.IsDecomposedBy?.SelectMany(rel => rel.RelatedObjects).OfType<IIfcProduct>() ?? Enumerable.Empty<IIfcProduct>();
            foreach (var child in children)
            {
                if (!labelToGid.ContainsKey(child.EntityLabel)) labelToGid[child.EntityLabel] = owningGlobalId;
                AddChildProductLabels(child, owningGlobalId, labelToGid, visited);
            }
        }

        private static MediaMeshGeometry3D? GetOrCreateCachedMesh(
            Guid sessionId,
            byte[] shapeData,
            double oneMeter,
            MeshQuality quality,
            out bool reused)
        {
            reused = false;
            var cacheKey = new MeshCacheKey(sessionId, GetShapeDataHash(shapeData), oneMeter, quality);
            lock (MeshCacheGate)
            {
                if (MeshCache.TryGetValue(cacheKey, out var cachedMesh))
                {
                    reused = true;
                    return cachedMesh;
                }
            }

            var mesh = CreateIndexedLocalMesh(shapeData, oneMeter, quality);
            if (mesh == null)
                return null;

            mesh.Freeze();
            lock (MeshCacheGate)
            {
                MeshCache[cacheKey] = mesh;
            }
            return mesh;
        }

        private static MediaMeshGeometry3D? GetOrCreateHighQualityCachedMesh(
            Guid sessionId,
            byte[] shapeData,
            XbimShapeInstance si,
            IXbimShapeGeometryData shapeGeom,
            double oneMeter,
            DomainPoint3D origin,
            DomainPoint3D placementOffset,
            out bool reused)
        {
            reused = false;

            int shapeGeomLabel = si != null ? si.ShapeGeometryLabel : 0;
            int geomHash = shapeGeom != null ? shapeGeom.GeometryHash : 0;
            int ifcProductLabel = si != null ? si.IfcProductLabel : 0;
            int shapeDataHash = 0;

            if (shapeGeomLabel == 0 && geomHash == 0)
            {
                shapeDataHash = GetShapeDataHash(shapeData);
            }

            var matrix = si != null ? si.Transformation : XbimMatrix3D.Identity;
            int transformHash = GetTransformHash(matrix);

            var key = new HighQualityMeshCacheKey(
                sessionId,
                MeshQuality.High,
                shapeGeomLabel,
                geomHash,
                ifcProductLabel,
                shapeDataHash,
                transformHash,
                oneMeter,
                origin.X, origin.Y, origin.Z,
                placementOffset.X, placementOffset.Y, placementOffset.Z,
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.OffsetX, matrix.OffsetY, matrix.OffsetZ, matrix.M44
            );

            lock (HighQualityMeshCacheGate)
            {
                if (HighQualityMeshCache.TryGetValue(key, out var cachedMesh))
                {
                    reused = true;
                    return cachedMesh;
                }
            }

            var mesh = CreateHighQualityDetailedMesh(shapeData, matrix, oneMeter, origin, placementOffset);
            if (mesh == null) return null;

            mesh.Freeze();

            lock (HighQualityMeshCacheGate)
            {
                HighQualityMeshCache[key] = mesh;
            }

            return mesh;
        }

        private static void EnsureCacheSession(Guid sessionId)
        {
            bool sessionChanged = false;
            lock (MeshCacheGate)
            {
                if (MeshCacheSessionId != sessionId)
                {
                    var removed = MeshCache.Count;
                    MeshCache.Clear();
                    MeshCacheSessionId = sessionId;
                    sessionChanged = true;
                    if (removed > 0)
                        Debug.WriteLine($"[ElementVisualBuilder] Mesh cache reset for session {sessionId}. Removed={removed}");
                }
            }

            if (sessionChanged)
            {
                lock (HighQualityMeshCacheGate)
                {
                    var removed = HighQualityMeshCache.Count;
                    HighQualityMeshCache.Clear();
                    if (removed > 0)
                        Debug.WriteLine($"[ElementVisualBuilder] High Quality Mesh cache reset for session {sessionId}. Removed={removed}");
                }

                lock (SpatialHitTestGate)
                {
                    var removed = SpatialHitTestElements.Count;
                    SpatialHitTestElements.Clear();
                    if (removed > 0)
                        Debug.WriteLine($"[ElementVisualBuilder] Spatial Hit Test elements reset for session {sessionId}. Removed={removed}");
                }
            }
        }

        private static int GetShapeDataHash(byte[] shapeData)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + shapeData.Length;

                foreach (var value in shapeData)
                    hash = (hash * 31) + value;

                return hash;
            }
        }

        private readonly record struct MeshCacheKey(
            Guid SessionId,
            int ShapeDataHash,
            double OneMeter,
            MeshQuality Quality);

        private static int GetTransformHash(XbimMatrix3D matrix)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + matrix.M11.GetHashCode();
                hash = (hash * 31) + matrix.M12.GetHashCode();
                hash = (hash * 31) + matrix.M13.GetHashCode();
                hash = (hash * 31) + matrix.M14.GetHashCode();
                hash = (hash * 31) + matrix.M21.GetHashCode();
                hash = (hash * 31) + matrix.M22.GetHashCode();
                hash = (hash * 31) + matrix.M23.GetHashCode();
                hash = (hash * 31) + matrix.M24.GetHashCode();
                hash = (hash * 31) + matrix.M31.GetHashCode();
                hash = (hash * 31) + matrix.M32.GetHashCode();
                hash = (hash * 31) + matrix.M33.GetHashCode();
                hash = (hash * 31) + matrix.M34.GetHashCode();
                hash = (hash * 31) + matrix.OffsetX.GetHashCode();
                hash = (hash * 31) + matrix.OffsetY.GetHashCode();
                hash = (hash * 31) + matrix.OffsetZ.GetHashCode();
                hash = (hash * 31) + matrix.M44.GetHashCode();
                return hash;
            }
        }

        private readonly record struct HighQualityMeshCacheKey(
            Guid SessionId,
            MeshQuality Quality,
            int ShapeGeometryLabel,
            int GeometryHash,
            int IfcProductLabel,
            int ShapeDataHash,
            int TransformHash,
            double OneMeter,
            double OriginX, double OriginY, double OriginZ,
            double OffsetX, double OffsetY, double OffsetZ,
            double M11, double M12, double M13, double M14,
            double M21, double M22, double M23, double M24,
            double M31, double M32, double M33, double M34,
            double TransX, double TransY, double TransZ, double M44
        );

        private static Transform3D CreateInstanceTransform(XbimMatrix3D matrix, double oneMeter, DomainPoint3D origin, DomainPoint3D placementOffset)
        {
            var scale = 1.0 / oneMeter;
            var wpfMatrix = new Matrix3D(
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.OffsetX * scale - origin.X + placementOffset.X,
                matrix.OffsetY * scale - origin.Y + placementOffset.Y,
                matrix.OffsetZ * scale - origin.Z + placementOffset.Z,
                matrix.M44);

            var transform = new MatrixTransform3D(wpfMatrix);
            transform.Freeze();
            return transform;
        }

        // High quality intentionally preserves the pre-optimization rendering path:
        // full transformed IFC triangulation with no decimation or mesh cache reuse.
        private static MediaMeshGeometry3D? CreateHighQualityDetailedMesh(
            byte[] shapeData,
            XbimMatrix3D matrix,
            double oneMeter,
            DomainPoint3D origin,
            DomainPoint3D placementOffset)
        {
            using var ms = new MemoryStream(shapeData);
            using var br = new BinaryReader(ms);
            var xbimMesh = br.ReadShapeTriangulation();
            if (xbimMesh == null) return null;

            var transformedVertices = xbimMesh.Vertices.Select(v =>
            {
                var worldPoint = matrix.Transform(new XbimPoint3D(v.X, v.Y, v.Z));
                return new Point3D(
                    worldPoint.X / oneMeter - origin.X + placementOffset.X,
                    worldPoint.Y / oneMeter - origin.Y + placementOffset.Y,
                    worldPoint.Z / oneMeter - origin.Z + placementOffset.Z);
            }).ToArray();

            var positions = new Point3DCollection();
            var normals = new Vector3DCollection();
            var indices = new Int32Collection();

            foreach (var face in xbimMesh.Faces)
            {
                for (int i = 0; i < face.TriangleCount; i++)
                {
                    var sourceIndex = 3 * i;
                    if (sourceIndex + 2 >= face.Indices.Count)
                        continue;

                    var p0 = transformedVertices[face.Indices[sourceIndex]];
                    var p1 = transformedVertices[face.Indices[sourceIndex + 1]];
                    var p2 = transformedVertices[face.Indices[sourceIndex + 2]];

                    var normal = Vector3D.CrossProduct(p1 - p0, p2 - p0);
                    if (normal.LengthSquared < 1e-18) continue;
                    normal.Normalize();

                    int index = positions.Count;
                    positions.Add(p0); normals.Add(normal);
                    positions.Add(p1); normals.Add(normal);
                    positions.Add(p2); normals.Add(normal);

                    indices.Add(index);
                    indices.Add(index + 1);
                    indices.Add(index + 2);
                }
            }

            return positions.Count == 0
                ? null
                : new MediaMeshGeometry3D
                {
                    Positions = positions,
                    Normals = normals,
                    TriangleIndices = indices
                };
        }

        // Indexed local geometry keeps shared vertices and applies IFC instance transforms separately.
        private static MediaMeshGeometry3D? CreateIndexedLocalMesh(byte[] shapeData, double oneMeter, MeshQuality quality)
        {
            using var ms = new MemoryStream(shapeData);
            using var br = new BinaryReader(ms);
            var xbimMesh = br.ReadShapeTriangulation();
            if (xbimMesh == null) return null;

            var scale = 1.0 / oneMeter;
            var positions = new Point3DCollection(xbimMesh.Vertices.Select(v => new Point3D(v.X * scale, v.Y * scale, v.Z * scale)));
            var triIndices = new Int32Collection();
            var normalAccumulator = new Vector3D[positions.Count];

            foreach (var face in xbimMesh.Faces)
            {
                for (int i = 0; i < face.TriangleCount; i++)
                {
                    int i0 = face.Indices[3 * i];
                    int i1 = face.Indices[3 * i + 1];
                    int i2 = face.Indices[3 * i + 2];

                    if (i0 < 0 || i0 >= positions.Count || i1 < 0 || i1 >= positions.Count || i2 < 0 || i2 >= positions.Count)
                        continue;

                    var p0 = positions[i0];
                    var p1 = positions[i1];
                    var p2 = positions[i2];

                    var n = Vector3D.CrossProduct(p1 - p0, p2 - p0);
                    if (n.LengthSquared < 1e-18) continue;

                    normalAccumulator[i0] += n;
                    normalAccumulator[i1] += n;
                    normalAccumulator[i2] += n;

                    triIndices.Add(i0);
                    triIndices.Add(i1);
                    triIndices.Add(i2);
                }
            }

            if (triIndices.Count == 0)
                return null;

            var normalsShared = new Vector3DCollection(positions.Count);
            for (int i = 0; i < positions.Count; i++)
            {
                var n = normalAccumulator[i];
                if (n.LengthSquared < 1e-18) n = new Vector3D(0, 0, 1);
                else n.Normalize();
                normalsShared.Add(n);
            }

            return new MediaMeshGeometry3D
            {
                Positions = positions,
                Normals = normalsShared,
                TriangleIndices = triIndices
            };
        }

        private void BuildFallbackBox(ModelVisual3D container, PrecastElement element, LiftStatus? status, bool isSelected)
        {
            DiagFallbackBoxCount++;
            DiagGeometryModel3DCount++;
            DiagModelVisual3DCount++;
            DiagMaterialCreated++;

            var color = GetColor(status);
            var meshBuilder = new MeshBuilder(true, true);
            var x = element.ElementCOG.X - (_origin?.X ?? 0d);
            var y = element.ElementCOG.Y - (_origin?.Y ?? 0d);
            var z = element.ElementCOG.Z - (_origin?.Z ?? 0d);
            meshBuilder.AddBox(new Vector3((float)x, (float)y, (float)z), (float)(element.BBoxLength ?? 1), (float)(element.BBoxWidth ?? 1), (float)(element.BBoxHeight ?? 1));
            var helixMesh = meshBuilder.ToMesh();
            var meshGeom = new System.Windows.Media.Media3D.MeshGeometry3D
            {
                Positions = new Point3DCollection(helixMesh.Positions.Select(p => new Point3D(p.X, p.Y, p.Z))),
                TriangleIndices = new Int32Collection(helixMesh.TriangleIndices),
                Normals = helixMesh.Normals == null
                    ? new Vector3DCollection()
                    : new Vector3DCollection(helixMesh.Normals.Select(n => new Vector3D(n.X, n.Y, n.Z)))
            };

            var finalMaterial = BuildMaterial(color, isSelected, new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(120, 100, 180, 255))));

            var group = new Model3DGroup();
            group.Children.Add(new GeometryModel3D { Geometry = meshGeom, Material = finalMaterial, BackMaterial = finalMaterial });

            var ev = new ModelVisual3D { Content = group };
            ev.SetValue(System.Windows.FrameworkElement.TagProperty, element);
            container.Children.Add(ev);
        }

        private Color GetColor(LiftStatus? status)
        {
            if (!status.HasValue) return Color.FromRgb(160, 160, 175); // Neutral Blueprint Gray

            var color = status.Value switch
            {
                // Premium High-Contrast Palette
                LiftStatus.Liftable    => Color.FromRgb(46, 204, 113),  // Emerald Green
                LiftStatus.Overweight  => Color.FromRgb(241, 196, 15),  // Vivid Sun Yellow
                LiftStatus.OutOfRadius => Color.FromRgb(231, 76, 60),   // Alizarin Red
                LiftStatus.HeightFail  => Color.FromRgb(230, 126, 34),  // Carrot Orange
                LiftStatus.SpecialCase => Color.FromRgb(155, 89, 182),  // Amethyst Purple
                _                      => Color.FromRgb(149, 165, 166)  // Asbestos Gray
            };

            return _statusAlpha.HasValue
                ? Color.FromArgb(_statusAlpha.Value, color.R, color.G, color.B)
                : color;
        }

        // --- Helper methods for Real Mesh Batching (Phase 2.2A) ---
        private readonly record struct RenderBatchKey(Color Color, LiftStatus? Status, string Material, string Type);

        private static Matrix3D CreateInstanceWpfMatrix(XbimMatrix3D matrix, double oneMeter, DomainPoint3D origin, DomainPoint3D placementOffset)
        {
            var scale = 1.0 / oneMeter;
            return new Matrix3D(
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.OffsetX * scale - origin.X + placementOffset.X,
                matrix.OffsetY * scale - origin.Y + placementOffset.Y,
                matrix.OffsetZ * scale - origin.Z + placementOffset.Z,
                matrix.M44);
        }

        private static void ConcatenateMesh(
            Point3DCollection targetPos,
            Vector3DCollection targetNorms,
            Int32Collection targetIndices,
            MediaMeshGeometry3D source,
            Matrix3D transform)
        {
            int indexOffset = targetPos.Count;

            if (transform.IsIdentity)
            {
                foreach (var pos in source.Positions)
                {
                    targetPos.Add(pos);
                }
            }
            else
            {
                foreach (var pos in source.Positions)
                {
                    targetPos.Add(transform.Transform(pos));
                }
            }

            if (source.Normals != null && source.Normals.Count > 0)
            {
                if (transform.IsIdentity)
                {
                    foreach (var norm in source.Normals)
                    {
                        targetNorms.Add(norm);
                    }
                }
                else
                {
                    foreach (var norm in source.Normals)
                    {
                        var transNorm = transform.Transform(norm);
                        transNorm.Normalize();
                        targetNorms.Add(transNorm);
                    }
                }
            }

            foreach (int index in source.TriangleIndices)
            {
                targetIndices.Add(indexOffset + index);
            }
        }

        private ModelVisual3D CreateTransparentHitBox(PrecastElement element, DomainPoint3D origin, DomainPoint3D placementOffset)
        {
            var color = Color.FromArgb(1, 0, 0, 0); // Transparent, but hit-testable
            var meshBuilder = new MeshBuilder(true, true);
            var x = element.ElementCOG.X - origin.X;
            var y = element.ElementCOG.Y - origin.Y;
            var z = element.ElementCOG.Z - origin.Z;
            meshBuilder.AddBox(
                new Vector3((float)x, (float)y, (float)z),
                (float)(element.BBoxLength ?? 1),
                (float)(element.BBoxWidth ?? 1),
                (float)(element.BBoxHeight ?? 1));
                
            var helixMesh = meshBuilder.ToMesh();
            var meshGeom = new System.Windows.Media.Media3D.MeshGeometry3D
            {
                Positions = new Point3DCollection(helixMesh.Positions.Select(p => new Point3D(p.X, p.Y, p.Z))),
                TriangleIndices = new Int32Collection(helixMesh.TriangleIndices),
                Normals = helixMesh.Normals == null
                    ? new Vector3DCollection()
                    : new Vector3DCollection(helixMesh.Normals.Select(n => new Vector3D(n.X, n.Y, n.Z)))
            };
            meshGeom.Freeze();

            var transparentBrush = new SolidColorBrush(color);
            transparentBrush.Freeze();
            var material = new DiffuseMaterial(transparentBrush);
            material.Freeze();

            var group = new Model3DGroup();
            var geoModel = new GeometryModel3D
            {
                Geometry = meshGeom,
                Material = material,
                BackMaterial = material
            };
            geoModel.Freeze();
            group.Children.Add(geoModel);
            group.Freeze();

            var ev = new ModelVisual3D { Content = group };
            ev.SetValue(System.Windows.FrameworkElement.TagProperty, element);
            return ev;
        }

        private static void PopulateSpatialHitTestRegistry(
            List<(PrecastElement element, LiftStatus? status, bool isSelected)> allItems,
            Guid? sessionId,
            DomainPoint3D origin,
            Dictionary<string, List<(MediaMeshGeometry3D Mesh, Matrix3D Transform)>>? elementMeshes)
        {
            var spatialElements = new List<SpatialHitTestElement>();
            var renderTimestamp = DateTime.UtcNow;
            int meshDerivedCount = 0;
            int cogFallbackCount = 0;

            foreach (var item in allItems)
            {
                var el = item.element;

                // Always compute the COG-based AABB for diagnostics comparison
                var cogX = el.ElementCOG.X - origin.X;
                var cogY = el.ElementCOG.Y - origin.Y;
                var cogZ = el.ElementCOG.Z - origin.Z;
                var dx = el.BBoxLength ?? 1.0;
                var dy = el.BBoxWidth ?? 1.0;
                var dz = el.BBoxHeight ?? 1.0;
                var cogMin = new Point3D(cogX - dx / 2, cogY - dy / 2, cogZ - dz / 2);
                var cogMax = new Point3D(cogX + dx / 2, cogY + dy / 2, cogZ + dz / 2);

                Point3D min;
                Point3D max;
                bool usedMeshDerived = false;

                // Try to compute AABB from actual rendered mesh vertex data
                if (elementMeshes != null
                    && elementMeshes.TryGetValue(el.ElementID, out var meshes)
                    && meshes.Count > 0)
                {
                    double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
                    double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
                    int totalVerts = 0;

                    foreach (var (mesh, transform) in meshes)
                    {
                        var positions = mesh.Positions;
                        if (positions == null || positions.Count == 0) continue;

                        if (transform.IsIdentity)
                        {
                            foreach (var pos in positions)
                            {
                                if (pos.X < minX) minX = pos.X;
                                if (pos.Y < minY) minY = pos.Y;
                                if (pos.Z < minZ) minZ = pos.Z;
                                if (pos.X > maxX) maxX = pos.X;
                                if (pos.Y > maxY) maxY = pos.Y;
                                if (pos.Z > maxZ) maxZ = pos.Z;
                                totalVerts++;
                            }
                        }
                        else
                        {
                            foreach (var pos in positions)
                            {
                                var worldPos = transform.Transform(pos);
                                if (worldPos.X < minX) minX = worldPos.X;
                                if (worldPos.Y < minY) minY = worldPos.Y;
                                if (worldPos.Z < minZ) minZ = worldPos.Z;
                                if (worldPos.X > maxX) maxX = worldPos.X;
                                if (worldPos.Y > maxY) maxY = worldPos.Y;
                                if (worldPos.Z > maxZ) maxZ = worldPos.Z;
                                totalVerts++;
                            }
                        }
                    }

                    if (totalVerts > 0)
                    {
                        min = new Point3D(minX, minY, minZ);
                        max = new Point3D(maxX, maxY, maxZ);
                        usedMeshDerived = true;
                        meshDerivedCount++;
                    }
                    else
                    {
                        min = cogMin;
                        max = cogMax;
                        cogFallbackCount++;
                    }
                }
                else
                {
                    // Fallback to domain-level COG + BBox
                    min = cogMin;
                    max = cogMax;
                    cogFallbackCount++;
                }

                // Add AABB diagnostic logging (ElementID, COG AABB, mesh AABB, delta, source)
                var cogCenter = new Point3D((cogMin.X + cogMax.X) / 2.0, (cogMin.Y + cogMax.Y) / 2.0, (cogMin.Z + cogMax.Z) / 2.0);
                var meshCenter = new Point3D((min.X + max.X) / 2.0, (min.Y + max.Y) / 2.0, (min.Z + max.Z) / 2.0);
                var delta = usedMeshDerived ? (meshCenter - cogCenter).Length : 0.0;

                if (usedMeshDerived && delta > 0.05)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[AABB Diagnostic] ElementID={el.ElementID}, Source=Mesh, " +
                        $"COG AABB=[{cogMin} to {cogMax}], Mesh AABB=[{min} to {max}], Delta={delta:F4}");
                }

                var hitElement = new SpatialHitTestElement
                {
                    SessionId = sessionId,
                    RenderTimestampUtc = renderTimestamp,
                    ElementId = el.ElementID,
                    Element = el,
                    Min = min,
                    Max = max,
                    IsVisible = el.IncludeInRender,
                    UsedMeshDerivedBounds = usedMeshDerived,
                    CogBasedMin = cogMin,
                    CogBasedMax = cogMax
                };

                if (elementMeshes != null && elementMeshes.TryGetValue(el.ElementID, out var meshesRef))
                {
                    hitElement.Meshes.AddRange(meshesRef);
                }

                spatialElements.Add(hitElement);
            }

            Debug.WriteLine(
                $"[ElementVisualBuilder] SpatialHitTest registry populated: Total={spatialElements.Count}, " +
                $"MeshDerived={meshDerivedCount}, CogFallback={cogFallbackCount}");

            RegisterSpatialHitTestElements(spatialElements);
        }
    }
}
