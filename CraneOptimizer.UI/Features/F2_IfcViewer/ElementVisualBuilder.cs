using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using HelixToolkit.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Xbim.Common.Geometry;
using Xbim.Common.XbimExtensions;
using Xbim.Ifc4.Interfaces;

namespace CraneOptimizer.UI.Features.F2_IfcViewer
{
    public class ElementVisualBuilder : IVisualBuilder<(PrecastElement element, LiftStatus? status, bool isSelected)>
    {
        private readonly IIfcReaderRepository? _ifcReader;
        private readonly DomainPoint3D? _origin;
        private readonly DomainPoint3D? _placementOffset;
        private readonly bool _renderOnlyStatusElements;
 
        public ElementVisualBuilder(
            IIfcReaderRepository? ifcReader = null,
            DomainPoint3D? origin = null,
            DomainPoint3D? placementOffset = null,
            bool renderOnlyStatusElements = false)
        {
            _ifcReader = ifcReader;
            _origin = origin;
            _placementOffset = placementOffset;
            _renderOnlyStatusElements = renderOnlyStatusElements;
        }
 
        public Visual3D Build((PrecastElement element, LiftStatus? status, bool isSelected) item)
        {
            var container = new ModelVisual3D();
            BuildFallbackBox(container, item.element, item.status, item.isSelected);
            return container;
        }
 
        public Visual3D BuildBatch(IEnumerable<(PrecastElement element, LiftStatus? status, bool isSelected)> items)
        {
            var itemsList = items.ToList();
            if (_renderOnlyStatusElements)
                itemsList = itemsList.Where(item => item.status.HasValue).ToList();

            if (_ifcReader is CraneOptimizer.Infrastructure.Repositories.IfcReaderRepository concreteRepo
                && concreteRepo.GeometryContext != null
                && concreteRepo.CurrentModel != null)
            {
                var model = concreteRepo.CurrentModel;
                var context = concreteRepo.GeometryContext;

                double oneMeter = model.ModelFactors.OneMeter;
                if (oneMeter <= 0) oneMeter = 1000.0;

                var elementMap = new Dictionary<string, (PrecastElement el, LiftStatus? st, Color col, bool sel)>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (var item in itemsList)
                {
                    var color = GetColor(item.status);
                    elementMap[item.element.ElementID] = (item.element, item.status, color, item.isSelected);
                }

                var labelToGid = new Dictionary<int, string>();
                foreach (var p in model.Instances.OfType<IIfcProduct>())
                {
                    var gid = p.GlobalId.ToString();
                    if (!string.IsNullOrEmpty(gid) && elementMap.ContainsKey(gid))
                    {
                        labelToGid[p.EntityLabel] = gid;
                        AddChildProductLabels(p, gid, labelToGid);
                    }
                }

                var renderedElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var allInstances = context.ShapeInstances()
                    .Where(si => labelToGid.ContainsKey(si.IfcProductLabel))
                    .ToList();

                var origin = _origin ?? new DomainPoint3D(0, 0, 0);
                var placementOffset = _placementOffset ?? new DomainPoint3D(0, 0, 0);

                var selectionMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(120, 100, 180, 255)));
                var elementVisualMap = new Dictionary<string, ModelVisual3D>();
                var batchVisual = new ModelVisual3D();

                foreach (var si in allInstances)
                {
                    try
                    {
                        var shapeGeom = context.ShapeGeometry(si) as IXbimShapeGeometryData;
                        if (shapeGeom?.ShapeData == null || shapeGeom.ShapeData.Length == 0) continue;

                        var gid = labelToGid[si.IfcProductLabel];
                        var (el, st, col, isSelected) = elementMap[gid];

                        var mesh = CreateMeshFlat(shapeGeom.ShapeData, si.Transformation, oneMeter, origin, placementOffset);
                        if (mesh == null || mesh.Positions.Count == 0) continue;
                        mesh.Freeze();

                        if (!elementVisualMap.TryGetValue(gid, out var ev))
                        {
                            ev = new ModelVisual3D { Content = new Model3DGroup() };
                            ev.SetValue(System.Windows.FrameworkElement.TagProperty, el);
                            elementVisualMap[gid] = ev;
                            batchVisual.Children.Add(ev);
                        }

                        var baseMaterial = new DiffuseMaterial(new SolidColorBrush(col));
                        Material finalMaterial = baseMaterial;
 
                        if (isSelected)
                        {
                            var mg = new MaterialGroup();
                            mg.Children.Add(baseMaterial);
                            mg.Children.Add(selectionMaterial);
                            finalMaterial = mg;
                        }
 
                        var geoModel = new GeometryModel3D
                        {
                            Geometry = mesh,
                            Material = finalMaterial,
                            BackMaterial = finalMaterial
                        };
                        ((Model3DGroup)ev.Content).Children.Add(geoModel);
 
                        renderedElements.Add(gid);
                    }
                    catch { }
                }
                return batchVisual;
            }
            else
            {
                var batchVisual = new ModelVisual3D();
                foreach (var item in itemsList)
                {
                    BuildFallbackBox(batchVisual, item.element, item.status, item.isSelected);
                }
                return batchVisual;
            }
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
         private static System.Windows.Media.Media3D.MeshGeometry3D? CreateMeshFlat(byte[] shapeData, XbimMatrix3D matrix, double oneMeter, DomainPoint3D origin, DomainPoint3D placementOffset)
        {
            var positions = new Point3DCollection();
            var normals = new Vector3DCollection();
            var indices = new Int32Collection();
            using var ms = new MemoryStream(shapeData);
            using var br = new BinaryReader(ms);
            var xbimMesh = br.ReadShapeTriangulation();
            if (xbimMesh == null) return null;
            var tv = xbimMesh.Vertices.Select(v => {
                var wp = matrix.Transform(new XbimPoint3D(v.X, v.Y, v.Z));
                return new Point3D(wp.X / oneMeter * 1000.0 + placementOffset.X - origin.X, wp.Y / oneMeter * 1000.0 + placementOffset.Y - origin.Y, wp.Z / oneMeter * 1000.0 + placementOffset.Z - origin.Z);
            }).ToArray();
            foreach (var face in xbimMesh.Faces) {
                for (int i = 0; i < face.TriangleCount; i++) {
                    var p0 = tv[face.Indices[3 * i]]; var p1 = tv[face.Indices[3 * i + 1]]; var p2 = tv[face.Indices[3 * i + 2]];
                    var n = Vector3D.CrossProduct(p1 - p0, p2 - p0); n.Normalize(); if (double.IsNaN(n.X)) continue;
                    int idx = positions.Count; positions.Add(p0); normals.Add(n); positions.Add(p1); normals.Add(n); positions.Add(p2); normals.Add(n);
                    indices.Add(idx); indices.Add(idx + 1); indices.Add(idx + 2);
                }
            }
            if (positions.Count == 0) return null;
            return new System.Windows.Media.Media3D.MeshGeometry3D { Positions = positions, Normals = normals, TriangleIndices = indices };
        }
 
        private void BuildFallbackBox(ModelVisual3D container, PrecastElement element, LiftStatus? status, bool isSelected)
        {
            var color = GetColor(status);
            var meshBuilder = new MeshBuilder(true, true);
            var x = element.ElementCOG.X - (_origin?.X ?? 0d);
            var y = element.ElementCOG.Y - (_origin?.Y ?? 0d);
            var z = element.ElementCOG.Z - (_origin?.Z ?? 0d);
            meshBuilder.AddBox(new Vector3((float)x, (float)y, (float)z), (float)(element.BBoxLength ?? 1), (float)(element.BBoxWidth ?? 1), (float)(element.BBoxHeight ?? 1));
            var helixMesh = meshBuilder.ToMesh();
            var meshGeom = new System.Windows.Media.Media3D.MeshGeometry3D {
                Positions = new Point3DCollection(helixMesh.Positions.Select(p => new Point3D(p.X, p.Y, p.Z))),
                TriangleIndices = new Int32Collection(helixMesh.TriangleIndices),
                Normals = new Vector3DCollection(helixMesh.Normals.Select(n => new Vector3D(n.X, n.Y, n.Z)))
            };

            var baseMaterial = new DiffuseMaterial(new SolidColorBrush(color));
            Material finalMaterial = baseMaterial;
            if (isSelected) {
                var mg = new MaterialGroup();
                mg.Children.Add(baseMaterial);
                mg.Children.Add(new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(120, 100, 180, 255))));
                finalMaterial = mg;
            }

            var group = new Model3DGroup();
            group.Children.Add(new GeometryModel3D { Geometry = meshGeom, Material = finalMaterial, BackMaterial = finalMaterial });

            var ev = new ModelVisual3D { Content = group };
            ev.SetValue(System.Windows.FrameworkElement.TagProperty, element);
            container.Children.Add(ev);
        }
 
        private Color GetColor(LiftStatus? status)
        {
            if (!status.HasValue) return Color.FromRgb(160, 160, 170);
            return status.Value switch {
                LiftStatus.Liftable => Colors.LightGreen,
                LiftStatus.Overweight => Colors.Gold,
                LiftStatus.OutOfRadius => Colors.IndianRed,
                LiftStatus.HeightFail => Colors.IndianRed,
                LiftStatus.SpecialCase => Colors.Gold,
                _ => Colors.Gray
            };
        }
    }
}


