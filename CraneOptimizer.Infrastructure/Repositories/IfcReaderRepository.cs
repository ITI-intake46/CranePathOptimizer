using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using System.Diagnostics;
using System.IO;
using Xbim.Common.Geometry;
using Xbim.Common.XbimExtensions;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.ModelGeometry.Scene;

namespace CraneOptimizer.Infrastructure.Repositories
{
    public class IfcReaderRepository : IIfcReaderRepository
    {
        private Xbim3DModelContext _geometryContext;

        public List<PrecastElement> ReadElements(string ifcPath)
        {
            var elements = new List<PrecastElement>();

            try
            {
                using var model = IfcStore.Open(ifcPath);

                _geometryContext = new Xbim3DModelContext(model);
                _geometryContext.CreateContext();
                var products = model.Instances
                    .OfType<IIfcProduct>()
                    .Where(p => p is IIfcBuildingElement && !string.IsNullOrWhiteSpace(p.Name?.ToString())).ToList();

                int count = 0;
                int total = products.Count;

                foreach (var p in products)
                {
                    count++;

                    var bbox = GetBoundingBoxSize(p);
                    var cog = GetCOGFromShapeGeometry(p);
                    var element = new PrecastElement
                    {
                        ElementID = p.GlobalId,
                        ElementType = p.ExpressType?.Name,

                        ElementLength = GetPropertyDouble(p, "length"),
                        ElementWidth = GetPropertyDouble(p, "width"),
                        ElementHeight = GetPropertyDouble(p, "height"),

                        ElementVolume = GetQuantity(p, "volume") ??
                                        GetPropertyDouble(p, "volume"),

                        ElementWT = GetQuantity(p, "weight") ??
                                    GetPropertyDouble(p, "weight"),

                        ElementMaterial = GetMaterial(p),

                        BBoxLength = bbox?.L,
                        BBoxWidth = bbox?.W,
                        BBoxHeight = bbox?.H,

                        ElementCOG = cog.HasValue
                                            ? new DomainPoint3D(cog.Value.X, cog.Value.Y, cog.Value.Z)
                                            : new DomainPoint3D(),
                    };

                    elements.Add(element);

                    if (count % 10 == 0)
                        Debug.WriteLine($"Processing {count}/{total} elements...");
                }

                Debug.WriteLine($"Loaded {elements.Count} elements successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading IFC file: {ex.Message}");
                throw;
            }
            finally
            {
                _geometryContext = null;
            }

            return elements;
        }

        private XbimPoint3D? GetCOGFromShapeGeometry(IIfcProduct product)
        {
            try
            {
                var shapeInstances = _geometryContext
                    .ShapeInstancesOf(product)
                    .ToList();

                if (!shapeInstances.Any())
                    return null;

                double totalVolume = 0;
                double weightedX = 0;
                double weightedY = 0;
                double weightedZ = 0;

                foreach (var shapeInstance in shapeInstances)
                {
                    var shapeGeometry = _geometryContext.ShapeGeometry(shapeInstance);
                    if (shapeGeometry == null)
                        continue;

                    var transform = shapeInstance.Transformation;

                    var result = CalculateCentroidFromShapeGeometry(
                        shapeGeometry,
                        transform);

                    if (result.HasValue)
                    {
                        var (centroid, volume) = result.Value;

                        if (volume > 0)
                        {
                            totalVolume += volume;
                            weightedX += centroid.X * volume;
                            weightedY += centroid.Y * volume;
                            weightedZ += centroid.Z * volume;
                        }
                    }
                }

                if (totalVolume > 0)
                {
                    return new XbimPoint3D(
                        weightedX / totalVolume,
                        weightedY / totalVolume,
                        weightedZ / totalVolume);
                }

                return GetCOGFromBoundingBox(product);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"COG error: {ex.Message}");
                return GetCOGFromBoundingBox(product);
            }
        }

        private (XbimPoint3D centroid, double volume)? CalculateCentroidFromShapeGeometry(XbimShapeGeometry shapeGeometry, XbimMatrix3D transform)
        {
            try
            {
                var data = shapeGeometry.ShapeData;

                if (data == null || data.Length == 0)
                    return null;

                var bytes = Convert.FromBase64String(data);
                using var ms = new MemoryStream(bytes);
                using var br = new BinaryReader(ms);

                var mesh = br.ReadShapeTriangulation();

                if (mesh?.Vertices == null || mesh?.Faces == null)
                    return null;

                var vertices = mesh.Vertices.ToList();

                double totalVolume = 0;
                double cx = 0;
                double cy = 0;
                double cz = 0;

                foreach (var face in mesh.Faces)
                {
                    var indices = face.Indices.ToList();

                    for (int i = 0; i < indices.Count - 2; i += 3)
                    {
                        var p0 = transform.Transform(vertices[indices[i]]);
                        var p1 = transform.Transform(vertices[indices[i + 1]]);
                        var p2 = transform.Transform(vertices[indices[i + 2]]);

                        var tetraVol = SignedTetrahedronVolume(p0, p1, p2);

                        cx += (p0.X + p1.X + p2.X) / 4.0 * tetraVol;
                        cy += (p0.Y + p1.Y + p2.Y) / 4.0 * tetraVol;
                        cz += (p0.Z + p1.Z + p2.Z) / 4.0 * tetraVol;

                        totalVolume += tetraVol;
                    }
                }

                var absVolume = Math.Abs(totalVolume);

                if (absVolume > 1e-10)
                {
                    return (
                        new XbimPoint3D(
                            cx / totalVolume,
                            cy / totalVolume,
                            cz / totalVolume),
                        absVolume);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Mesh parse error: {ex.Message}");
            }

            return null;
        }

        private static double SignedTetrahedronVolume(XbimPoint3D a, XbimPoint3D b, XbimPoint3D c)
        {
            return
                (a.X * (b.Y * c.Z - c.Y * b.Z) -
                 a.Y * (b.X * c.Z - c.X * b.Z) +
                 a.Z * (b.X * c.Y - c.X * b.Y)) / 6.0;
        }

        private XbimPoint3D? GetCOGFromBoundingBox(IIfcProduct product)
        {
            try
            {
                var shapes = _geometryContext
                    .ShapeInstancesOf(product)
                    .ToList();

                if (!shapes.Any())
                    return null;

                double minX = double.MaxValue;
                double minY = double.MaxValue;
                double minZ = double.MaxValue;

                double maxX = double.MinValue;
                double maxY = double.MinValue;
                double maxZ = double.MinValue;

                foreach (var shape in shapes)
                {
                    var bbox = shape.BoundingBox;
                    var transform = shape.Transformation;

                    var corners = new[]
                    {
                        new XbimPoint3D(bbox.Min.X, bbox.Min.Y, bbox.Min.Z),
                        new XbimPoint3D(bbox.Max.X, bbox.Min.Y, bbox.Min.Z),
                        new XbimPoint3D(bbox.Min.X, bbox.Max.Y, bbox.Min.Z),
                        new XbimPoint3D(bbox.Max.X, bbox.Max.Y, bbox.Min.Z),

                        new XbimPoint3D(bbox.Min.X, bbox.Min.Y, bbox.Max.Z),
                        new XbimPoint3D(bbox.Max.X, bbox.Min.Y, bbox.Max.Z),
                        new XbimPoint3D(bbox.Min.X, bbox.Max.Y, bbox.Max.Z),
                        new XbimPoint3D(bbox.Max.X, bbox.Max.Y, bbox.Max.Z)
                    };

                    foreach (var c in corners)
                    {
                        var w = transform.Transform(c);

                        minX = Math.Min(minX, w.X);
                        minY = Math.Min(minY, w.Y);
                        minZ = Math.Min(minZ, w.Z);

                        maxX = Math.Max(maxX, w.X);
                        maxY = Math.Max(maxY, w.Y);
                        maxZ = Math.Max(maxZ, w.Z);
                    }
                }

                return new XbimPoint3D(
                    (minX + maxX) / 2,
                    (minY + maxY) / 2,
                    (minZ + maxZ) / 2);
            }
            catch
            {
                return null;
            }
        }

        private double? GetPropertyDouble(IIfcProduct p, string keyword)
        {
            var rels = p.IsDefinedBy
                .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet)
                .Select(r => r.RelatingPropertyDefinition as IIfcPropertySet);

            foreach (var pset in rels)
            {
                foreach (var prop in pset?.HasProperties
                             .OfType<IIfcPropertySingleValue>() ?? [])
                {
                    if (prop.Name.ToString().ToLower().Contains(keyword))
                    {
                        if (double.TryParse(
                            prop.NominalValue?.ToString(),
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out double v))
                            return v;
                    }
                }
            }

            return null;
        }

        private double? GetQuantity(IIfcProduct p, string keyword)
        {
            var rels = p.IsDefinedBy
                .Where(r => r.RelatingPropertyDefinition is IIfcElementQuantity)
                .Select(r => r.RelatingPropertyDefinition as IIfcElementQuantity);

            foreach (var qSet in rels)
            {
                foreach (var q in qSet?.Quantities)
                {
                    if (!q.Name.ToString().ToLower().Contains(keyword))
                        continue;

                    if (q is IIfcQuantityLength qLen) return qLen.LengthValue;
                    if (q is IIfcQuantityArea qArea) return qArea.AreaValue;
                    if (q is IIfcQuantityVolume qVol) return qVol.VolumeValue;
                    if (q is IIfcQuantityWeight qW) return qW.WeightValue;
                }
            }

            return null;
        }

        private string GetMaterial(IIfcProduct p)
        {
            try
            {
                var rel = p.HasAssociations
                    ?.OfType<IIfcRelAssociatesMaterial>()
                    .FirstOrDefault();

                if (rel == null) return null;

                var mat = rel.RelatingMaterial;

                if (mat is IIfcMaterial m)
                    return m.Name;

                if (mat is IIfcMaterialLayer ml)
                    return ml.Material?.Name;

                if (mat is IIfcMaterialLayerSet set)
                    return string.Join(", ",
                        set.MaterialLayers
                           .Select(l => l.Material?.Name)
                           .Where(n => !string.IsNullOrEmpty(n)));

                if (mat is IIfcMaterialList list)
                    return string.Join(", ",
                        list.Materials
                            .Select(x => x.Name)
                            .Where(n => !string.IsNullOrEmpty(n)));
            }
            catch { }

            return null;
        }

        private (double L, double W, double H)? GetBoundingBoxSize(IIfcProduct product)
        {
            var shapes = _geometryContext.ShapeInstancesOf(product).ToList();

            if (!shapes.Any())
                return null;

            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

            foreach (var shape in shapes)
            {
                var bbox = shape.BoundingBox;
                var transform = shape.Transformation;

                var corners = new[]
                {
            new XbimPoint3D(bbox.Min.X, bbox.Min.Y, bbox.Min.Z),
            new XbimPoint3D(bbox.Max.X, bbox.Min.Y, bbox.Min.Z),
            new XbimPoint3D(bbox.Min.X, bbox.Max.Y, bbox.Min.Z),
            new XbimPoint3D(bbox.Max.X, bbox.Max.Y, bbox.Min.Z),
            new XbimPoint3D(bbox.Min.X, bbox.Min.Y, bbox.Max.Z),
            new XbimPoint3D(bbox.Max.X, bbox.Min.Y, bbox.Max.Z),
            new XbimPoint3D(bbox.Min.X, bbox.Max.Y, bbox.Max.Z),
            new XbimPoint3D(bbox.Max.X, bbox.Max.Y, bbox.Max.Z)
        };

                foreach (var c in corners)
                {
                    var w = transform.Transform(c);

                    minX = Math.Min(minX, w.X);
                    minY = Math.Min(minY, w.Y);
                    minZ = Math.Min(minZ, w.Z);

                    maxX = Math.Max(maxX, w.X);
                    maxY = Math.Max(maxY, w.Y);
                    maxZ = Math.Max(maxZ, w.Z);
                }
            }

            return (
                maxX - minX,
                maxY - minY,
                maxZ - minZ
            );
        }
    }
}