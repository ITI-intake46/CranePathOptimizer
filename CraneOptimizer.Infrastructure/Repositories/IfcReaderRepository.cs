using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Units;
using System.Diagnostics;
using System.Globalization;
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
        private const double DefaultLengthToMetersFactor = 1d;
        private const double ConcreteDensityKgPerCubicMeter = 2500d;
        private const double SteelDensityKgPerCubicMeter = 7850d;
        private const double AluminumDensityKgPerCubicMeter = 2700d;
        private const double GlassDensityKgPerCubicMeter = 2500d;
        private const double TimberDensityKgPerCubicMeter = 600d;

        public IfcStore? CurrentModel { get; private set; }
        public Xbim3DModelContext? GeometryContext { get; private set; }
        public double ModelLengthFactor { get; private set; } = 1.0;
        public double? ModelGroundElevation { get; private set; }

        public void ClearData()
        {
            if (CurrentModel != null)
            {
                try { CurrentModel.Close(); CurrentModel.Dispose(); } catch { }
                CurrentModel = null;
            }
            GeometryContext = null;
            ModelGroundElevation = null;
        }

        public void Dispose()
        {
            ClearData();
        }

        public IEnumerable<XbimShapeInstance> GetElementShapes(string globalId)
        {
            if (CurrentModel == null || GeometryContext == null) return Enumerable.Empty<XbimShapeInstance>();

            var product = CurrentModel.Instances.FirstOrDefault<IIfcProduct>(p => p.GlobalId == globalId);
            if (product == null) return Enumerable.Empty<XbimShapeInstance>();

            try
            {
                return GeometryContext.ShapeInstancesOf(product);
            }
            catch
            {
                return Enumerable.Empty<XbimShapeInstance>();
            }
        }

        public List<PrecastElement> ReadElements(string ifcPath)
        {
            return ReadElementsCore(ifcPath, null, CancellationToken.None);
        }

        public List<PrecastElement> ReadElements(string path, IProgress<double> progress)
        {
            return ReadElementsCore(path, progress, CancellationToken.None);
        }

        public List<PrecastElement> ReadElements(string path, IProgress<double> progress, CancellationToken cancellationToken)
        {
            return ReadElementsCore(path, progress, cancellationToken);
        }

        private List<PrecastElement> ReadElementsCore(
            string ifcPath,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            try
            {
                ClearData();
                CurrentModel = IfcStore.Open(ifcPath);

                _ = new Xbim.Geometry.Engine.Interop.XbimGeometryEngine();
                GeometryContext = new Xbim3DModelContext(CurrentModel);
                GeometryContext.CreateContext();

                ModelLengthFactor = GetLengthToMetersFactor(CurrentModel);
                ModelGroundElevation = GetModelGroundElevation(CurrentModel, ModelLengthFactor);

                var extractor = new IfcEngineeringExtractor(
                    CurrentModel,
                    GeometryContext,
                    ModelLengthFactor);

                return extractor.Extract(progress, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading IFC file: {ex.Message}");
                throw;
            }
        }

        private static double GetLengthToMetersFactor(IfcStore model)
        {
            var project = model.Instances.OfType<IIfcProject>().FirstOrDefault();
            var units = project?.UnitsInContext?.Units;

            if (units == null)
                return DefaultLengthToMetersFactor;

            foreach (var unit in units)
            {
                if (unit is IIfcSIUnit siUnit &&
                    siUnit.UnitType.ToString().Contains("LENGTH", StringComparison.OrdinalIgnoreCase))
                {
                    return GetSiUnitToMetersFactor(siUnit);
                }

                if (unit is IIfcConversionBasedUnit conversionUnit &&
                    conversionUnit.UnitType.ToString().Contains("LENGTH", StringComparison.OrdinalIgnoreCase))
                {
                    return GetConversionUnitToMetersFactor(conversionUnit);
                }
            }

            return DefaultLengthToMetersFactor;
        }

        private static double GetSiUnitToMetersFactor(IIfcSIUnit siUnit)
        {
            var name = siUnit.Name.ToString();
            var prefix = siUnit.Prefix?.ToString() ?? string.Empty;

            if (!name.Contains("METRE", StringComparison.OrdinalIgnoreCase) &&
                !name.Contains("METER", StringComparison.OrdinalIgnoreCase))
            {
                return DefaultLengthToMetersFactor;
            }

            if (prefix.Contains("MILLI", StringComparison.OrdinalIgnoreCase))
                return 0.001d;

            if (prefix.Contains("CENTI", StringComparison.OrdinalIgnoreCase))
                return 0.01d;

            if (prefix.Contains("DECI", StringComparison.OrdinalIgnoreCase))
                return 0.1d;

            return DefaultLengthToMetersFactor;
        }

        private static double GetConversionUnitToMetersFactor(IIfcConversionBasedUnit conversionUnit)
        {
            var unitName = conversionUnit.Name.ToString();

            if (unitName.Contains("MILLIMET", StringComparison.OrdinalIgnoreCase))
                return 0.001d;

            if (unitName.Contains("CENTIMET", StringComparison.OrdinalIgnoreCase))
                return 0.01d;

            // Explicit imperial handling — common in Revit-exported IFC files
            if (unitName.Contains("FOOT", StringComparison.OrdinalIgnoreCase) ||
                unitName.Contains("FEET", StringComparison.OrdinalIgnoreCase))
                return 0.3048d;

            if (unitName.Contains("INCH", StringComparison.OrdinalIgnoreCase))
                return 0.0254d;

            if (unitName.Contains("MET", StringComparison.OrdinalIgnoreCase))
                return DefaultLengthToMetersFactor;

            var valueComponent = conversionUnit.ConversionFactor?.ValueComponent?.ToString();
            if (!double.TryParse(valueComponent, NumberStyles.Any, CultureInfo.InvariantCulture, out var factorToBase))
                return DefaultLengthToMetersFactor;

            var baseFactor = DefaultLengthToMetersFactor;
            if (conversionUnit.ConversionFactor?.UnitComponent is IIfcSIUnit baseSiUnit)
                baseFactor = GetSiUnitToMetersFactor(baseSiUnit);

            return factorToBase * baseFactor;
        }

        private static double? GetModelGroundElevation(IfcStore model, double lengthToMetersFactor)
        {
            var elevations = model.Instances
                .OfType<IIfcBuildingStorey>()
                .Select(storey => storey.Elevation.HasValue
                    ? storey.Elevation.Value * lengthToMetersFactor
                    : (double?)null)
                .Where(value => value.HasValue && IsFinite(value.Value))
                .Select(value => value!.Value)
                .Distinct()
                .OrderBy(value => value)
                .ToList();

            if (elevations.Count == 0)
                return null;

            const double zeroToleranceMeters = 0.05;
            if (elevations.Any(value => Math.Abs(value) <= zeroToleranceMeters))
                return 0d;

            var nonNegative = elevations.FirstOrDefault(value => value > 0d);
            if (nonNegative > 0d)
                return nonNegative;

            return elevations
                .OrderBy(value => Math.Abs(value))
                .First();
        }

        private static bool IsFinite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);

        // -------------------------------------------------------------------------
        // Extractor
        // -------------------------------------------------------------------------

        private sealed class IfcEngineeringExtractor
        {
            private readonly IfcStore _model;
            private readonly Xbim3DModelContext _geometryContext;
            private readonly double _lengthToMetersFactor;
            private readonly double _volumeToCubicMetersFactor;

            private readonly Dictionary<int, SourceElementSnapshot> _sourceCache = new();
            private readonly Dictionary<int, AggregateSnapshot> _aggregateCache = new();

            public IfcEngineeringExtractor(
                IfcStore model,
                Xbim3DModelContext geometryContext,
                double lengthToMetersFactor)
            {
                _model = model;
                _geometryContext = geometryContext;
                _lengthToMetersFactor = lengthToMetersFactor;
                _volumeToCubicMetersFactor = Math.Pow(lengthToMetersFactor, 3);
            }

            public List<PrecastElement> Extract(IProgress<double>? progress, CancellationToken cancellationToken)
            {
                var physicalProducts = _model.Instances
                    .OfType<IIfcProduct>()
                    .Where(IsEngineeringProduct)
                    .ToList();

                if (physicalProducts.Count == 0)
                    return [];

                CacheSourceProducts(physicalProducts, progress, cancellationToken);

                var rootAssemblies = physicalProducts
                    .OfType<IIfcElementAssembly>()
                    .Where(a => !HasAssemblyParent(a))
                    .ToList();

                var consumedByAssemblies = new HashSet<int>();
                var result = new List<PrecastElement>(physicalProducts.Count);

                foreach (var assembly in rootAssemblies)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var aggregate = BuildAggregateSnapshot(assembly, consumedByAssemblies);
                    if (aggregate != null)
                        result.Add(ToPrecastElement(aggregate));
                }

                foreach (var product in physicalProducts)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (product is IIfcElementAssembly)
                        continue;

                    if (consumedByAssemblies.Contains(product.EntityLabel))
                        continue;

                    if (!_sourceCache.TryGetValue(product.EntityLabel, out var snapshot))
                        continue;

                    if (!snapshot.HasEngineeringData)
                        continue;

                    result.Add(ToPrecastElement(snapshot));
                }

                progress?.Report(100d);

                return result;
            }

            private void CacheSourceProducts(
                List<IIfcProduct> products,
                IProgress<double>? progress,
                CancellationToken cancellationToken)
            {
                for (int i = 0; i < products.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var product = products[i];
                    _sourceCache[product.EntityLabel] = BuildSourceSnapshot(product);

                    progress?.Report(((double)(i + 1) / products.Count) * 70d);
                }
            }

            private SourceElementSnapshot BuildSourceSnapshot(IIfcProduct product)
            {
                var geometry = GetGeometrySnapshot(product);

                var bboxLength = geometry?.Length;
                var bboxWidth = geometry?.Width;
                var bboxHeight = geometry?.Height;

                var elementLength = ConvertLength(
                    GetQuantity(product, "length") ??
                    GetPropertyDouble(product, "length")) ?? bboxLength;

                var elementWidth = ConvertLength(
                    GetQuantity(product, "width") ??
                    GetPropertyDouble(product, "width")) ?? bboxWidth;

                var elementHeight = ConvertLength(
                    GetQuantity(product, "height") ??
                    GetPropertyDouble(product, "height")) ?? bboxHeight;

                var volume = ConvertVolume(
                    GetQuantity(product, "netvolume") ??
                    GetQuantity(product, "volume") ??
                    GetPropertyDouble(product, "netvolume") ??
                    GetPropertyDouble(product, "volume"));
                var material = GetMaterial(product);
                var weight = GetWeight(product);
                
                // Fallback: If Weight is 0 or missing, use Volume * 2500
                if ((weight ?? 0) <= 0 && volume.HasValue && volume.Value > 0)
                {
                    weight = (volume.Value * ConcreteDensityKgPerCubicMeter) / UnitSystem.KilogramsPerTon;
                }

                var revitId = GetRevitId(product);
                var rawName = GetMark(product) ?? product.Name?.ToString();
                var cleanedName = CleanElementName(rawName ?? string.Empty);

                if (!weight.HasValue && volume.HasValue)
                    weight = EstimateWeightFromMaterial(material, volume.Value);

                return new SourceElementSnapshot(
                    product.EntityLabel,
                    revitId ?? product.GlobalId.ToString(),
                    product.ExpressType?.Name ?? product.GetType().Name,
                    material,
                    weight,
                    volume,
                    geometry,
                    elementLength,
                    elementWidth,
                    elementHeight,
                    revitId,
                    cleanedName);
            }

            private AggregateSnapshot? BuildAggregateSnapshot(
                IIfcElementAssembly assembly,
                HashSet<int> consumedByAssemblies)
            {
                if (_aggregateCache.TryGetValue(assembly.EntityLabel, out var cached))
                    return cached;

                if (!_sourceCache.TryGetValue(assembly.EntityLabel, out var parentSnapshot))
                    return null;

                var childProducts = GetAssemblyChildren(assembly);
                var childSnapshots = CollectChildSnapshots(childProducts, consumedByAssemblies);

                // No usable children — fall back to the assembly's own data if available
                if (childSnapshots.Count == 0)
                    return BuildFallbackAggregate(assembly, parentSnapshot);

                var totalWeight = SumOrNull(childSnapshots.Select(c => c.Weight)) ?? parentSnapshot.Weight;
                var totalVolume = SumOrNull(childSnapshots.Select(c => c.Volume)) ?? parentSnapshot.Volume;

                var geometry = ResolveAggregateGeometry(parentSnapshot, childSnapshots, totalWeight);
                var material = ResolveAggregateMaterial(parentSnapshot, childSnapshots);

                var revitId = GetRevitId(assembly);
                var rawName = GetMark(assembly) ?? assembly.Name?.ToString();
                var cleanedName = CleanElementName(rawName ?? string.Empty);

                var aggregate = new AggregateSnapshot(
                    assembly.EntityLabel,
                    revitId ?? assembly.GlobalId.ToString(),
                    assembly.ExpressType?.Name ?? assembly.GetType().Name,
                    material,
                    totalWeight,
                    totalVolume,
                    geometry,
                    geometry?.Length,
                    geometry?.Width,
                    geometry?.Height,
                    revitId,
                    cleanedName);

                _aggregateCache[assembly.EntityLabel] = aggregate;
                return aggregate;
            }

            // Collect direct child snapshots, marking them as consumed.
            private List<IEngineeringSnapshot> CollectChildSnapshots(
                IReadOnlyList<IIfcProduct> childProducts,
                HashSet<int> consumedByAssemblies)
            {
                var snapshots = new List<IEngineeringSnapshot>();

                foreach (var child in childProducts)
                {
                    consumedByAssemblies.Add(child.EntityLabel);

                    if (child is IIfcElementAssembly childAssembly)
                    {
                        var childAggregate = BuildAggregateSnapshot(childAssembly, consumedByAssemblies);
                        if (childAggregate != null)
                            snapshots.Add(childAggregate);
                    }
                    else if (_sourceCache.TryGetValue(child.EntityLabel, out var childSnapshot) &&
                             childSnapshot.HasEngineeringData)
                    {
                        snapshots.Add(childSnapshot);
                    }
                }

                return snapshots;
            }

            private AggregateSnapshot? BuildFallbackAggregate(
                IIfcElementAssembly assembly,
                SourceElementSnapshot parentSnapshot)
            {
                if (!parentSnapshot.HasEngineeringData)
                    return null;

                var fallback = AggregateSnapshot.FromParentOnly(parentSnapshot);
                _aggregateCache[assembly.EntityLabel] = fallback;
                return fallback;
            }

            private static GeometrySnapshot? ResolveAggregateGeometry(
                SourceElementSnapshot parentSnapshot,
                IReadOnlyList<IEngineeringSnapshot> childSnapshots,
                double? totalWeight)
            {
                if (parentSnapshot.Geometry?.HasBounds == true)
                    return parentSnapshot.Geometry;

                var boundedGeometries = childSnapshots
                    .Select(c => c.Geometry)
                    .Where(g => g?.HasBounds == true)
                    .Cast<GeometrySnapshot>()
                    .ToList();

                if (boundedGeometries.Count > 0)
                {
                    return GeometrySnapshot.Union(
                        boundedGeometries,
                        totalWeight,
                        childSnapshots.Select(c => c.CenterOfGravity).ToList(),
                        childSnapshots.Select(c => c.Weight).ToList());
                }

                return GeometrySnapshot.FromCenterPoints(
                    childSnapshots.Select(c => c.CenterOfGravity).ToList(),
                    totalWeight,
                    childSnapshots.Select(c => c.Weight).ToList());
            }

            private static string? ResolveAggregateMaterial(
                SourceElementSnapshot parentSnapshot,
                IReadOnlyList<IEngineeringSnapshot> childSnapshots)
            {
                return !string.IsNullOrWhiteSpace(parentSnapshot.Material)
                    ? parentSnapshot.Material
                    : JoinDistinctMaterials(childSnapshots.Select(c => c.Material));
            }

            private List<IIfcProduct> GetAssemblyChildren(IIfcElementAssembly assembly)
            {
                return assembly.IsDecomposedBy?
                    .SelectMany(rel => rel.RelatedObjects)
                    .OfType<IIfcProduct>()
                    .Where(IsEngineeringProduct)
                    .ToList() ?? [];
            }

            private static bool HasAssemblyParent(IIfcElementAssembly assembly)
            {
                return assembly.Decomposes?
                    .Any(rel => rel.RelatingObject is IIfcElementAssembly) == true;
            }

            // -------------------------------------------------------------------------
            // Geometry
            // -------------------------------------------------------------------------

            private GeometrySnapshot? GetGeometrySnapshot(IIfcProduct product)
            {
                var shapeInstances = _geometryContext.ShapeInstancesOf(product).ToList();
                if (shapeInstances.Count == 0)
                    return null;

                double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

                double totalMeshVolume = 0d;
                double weightedX = 0d, weightedY = 0d, weightedZ = 0d;

                foreach (var shapeInstance in shapeInstances)
                {
                    UpdateBounds(shapeInstance, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);

                    var shapeGeometry = _geometryContext.ShapeGeometry(shapeInstance);
                    if (shapeGeometry == null)
                        continue;

                    var centroidResult = CalculateCentroidFromShapeGeometry(shapeGeometry, shapeInstance.Transformation);
                    if (!centroidResult.HasValue || centroidResult.Value.volume <= 0)
                        continue;

                    var (centroid, volume) = centroidResult.Value;
                    totalMeshVolume += volume;
                    weightedX += centroid.X * volume;
                    weightedY += centroid.Y * volume;
                    weightedZ += centroid.Z * volume;
                }

                // Guard: no valid bounds were ever written
                if (minX == double.MaxValue)
                    return null;

                var center = totalMeshVolume > 0
                    ? new DomainPoint3D(
                        ConvertLength(weightedX / totalMeshVolume),
                        ConvertLength(weightedY / totalMeshVolume),
                        ConvertLength(weightedZ / totalMeshVolume))
                    : new DomainPoint3D(
                        ConvertLength((minX + maxX) / 2d),
                        ConvertLength((minY + maxY) / 2d),
                        ConvertLength((minZ + maxZ) / 2d));

                return new GeometrySnapshot(
                    ConvertLength(minX),
                    ConvertLength(minY),
                    ConvertLength(minZ),
                    ConvertLength(maxX),
                    ConvertLength(maxY),
                    ConvertLength(maxZ),
                    center);
            }

            private static void UpdateBounds(
                XbimShapeInstance shapeInstance,
                ref double minX, ref double minY, ref double minZ,
                ref double maxX, ref double maxY, ref double maxZ)
            {
                var bbox = shapeInstance.BoundingBox;
                var transform = shapeInstance.Transformation;

                ReadOnlySpan<XbimPoint3D> corners =
                [
                    new(bbox.Min.X, bbox.Min.Y, bbox.Min.Z),
                    new(bbox.Max.X, bbox.Min.Y, bbox.Min.Z),
                    new(bbox.Min.X, bbox.Max.Y, bbox.Min.Z),
                    new(bbox.Max.X, bbox.Max.Y, bbox.Min.Z),
                    new(bbox.Min.X, bbox.Min.Y, bbox.Max.Z),
                    new(bbox.Max.X, bbox.Min.Y, bbox.Max.Z),
                    new(bbox.Min.X, bbox.Max.Y, bbox.Max.Z),
                    new(bbox.Max.X, bbox.Max.Y, bbox.Max.Z),
                ];

                foreach (var corner in corners)
                {
                    var world = transform.Transform(corner);
                    minX = Math.Min(minX, world.X);
                    minY = Math.Min(minY, world.Y);
                    minZ = Math.Min(minZ, world.Z);
                    maxX = Math.Max(maxX, world.X);
                    maxY = Math.Max(maxY, world.Y);
                    maxZ = Math.Max(maxZ, world.Z);
                }
            }

            private static (XbimPoint3D centroid, double volume)? CalculateCentroidFromShapeGeometry(
                XbimShapeGeometry shapeGeometry,
                XbimMatrix3D transform)
            {
                try
                {
                    var data = shapeGeometry.ShapeData;
                    if (string.IsNullOrEmpty(data))
                        return null;

                    byte[] bytes;
                    try
                    {
                        bytes = Convert.FromBase64String(data);
                    }
                    catch (FormatException ex)
                    {
                        Debug.WriteLine($"Invalid Base64 shape data: {ex.Message}");
                        return null;
                    }

                    using var stream = new MemoryStream(bytes);
                    using var reader = new BinaryReader(stream);

                    var mesh = reader.ReadShapeTriangulation();
                    if (mesh?.Vertices == null || mesh.Faces == null)
                        return null;

                    var vertices = mesh.Vertices.ToList();

                    double totalVolume = 0d;
                    double centroidX = 0d, centroidY = 0d, centroidZ = 0d;

                    foreach (var face in mesh.Faces)
                    {
                        var indices = face.Indices.ToList();
                        for (int i = 0; i + 2 < indices.Count; i += 3)
                        {
                            var p0 = transform.Transform(vertices[indices[i]]);
                            var p1 = transform.Transform(vertices[indices[i + 1]]);
                            var p2 = transform.Transform(vertices[indices[i + 2]]);

                            var tetrahedronVolume = SignedTetrahedronVolume(p0, p1, p2);

                            centroidX += (p0.X + p1.X + p2.X) / 4d * tetrahedronVolume;
                            centroidY += (p0.Y + p1.Y + p2.Y) / 4d * tetrahedronVolume;
                            centroidZ += (p0.Z + p1.Z + p2.Z) / 4d * tetrahedronVolume;
                            totalVolume += tetrahedronVolume;
                        }
                    }

                    var absoluteVolume = Math.Abs(totalVolume);
                    if (absoluteVolume <= 1e-10)
                        return null;

                    return (
                        new XbimPoint3D(
                            centroidX / totalVolume,
                            centroidY / totalVolume,
                            centroidZ / totalVolume),
                        absoluteVolume);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Mesh centroid extraction error: {ex.Message}");
                    return null;
                }
            }

            private static double SignedTetrahedronVolume(XbimPoint3D a, XbimPoint3D b, XbimPoint3D c)
            {
                return (a.X * (b.Y * c.Z - c.Y * b.Z)
                      - a.Y * (b.X * c.Z - c.X * b.Z)
                      + a.Z * (b.X * c.Y - c.X * b.Y)) / 6d;
            }

            // -------------------------------------------------------------------------
            // Property / Quantity readers
            // -------------------------------------------------------------------------

            /// <summary>
            /// Reads weight/mass from IFC quantity sets or property sets.
            /// </summary>
            /// <remarks>
            /// IFC 4 standard stores IIfcQuantityWeight values in kilograms.
            /// Runtime element weights are canonical metric tons, matching crane
            /// load charts and GA capacity checks.
            ///
            /// LIMITATION: If a non-standard IFC file encodes mass in a
            /// different unit (e.g. lbs) via IfcPropertySingleValue, the
            /// value will be misinterpreted. Such files are rare but exist.
            /// </remarks>
            private double? GetWeight(IIfcProduct product)
            {
                return GetQuantityWeightTons(product, "weight", "mass")
                    ?? GetPropertyWeightTons(product, "weight", "mass");
            }

            private static string? GetRevitId(IIfcProduct product)
            {
                var tag = (product as IIfcElement)?.Tag?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(tag))
                    return tag;

                var propertySets = product.IsDefinedBy
                    .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet)
                    .Select(r => (IIfcPropertySet)r.RelatingPropertyDefinition);

                foreach (var pset in propertySets)
                {
                    if (!pset.Name.ToString().Contains("Revit_Identity", StringComparison.OrdinalIgnoreCase) &&
                        !pset.Name.ToString().Equals("Pset_Revit_Identity Data", StringComparison.OrdinalIgnoreCase))
                        continue;

                    foreach (var prop in pset.HasProperties.OfType<IIfcPropertySingleValue>())
                    {
                        var pname = prop.Name.ToString();
                        if (pname.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
                            pname.Equals("ElementId", StringComparison.OrdinalIgnoreCase))
                        {
                            var val = prop.NominalValue?.ToString()?.Trim();
                            if (!string.IsNullOrWhiteSpace(val))
                                return val;
                        }
                    }
                }

                return null;
            }

            private static string? GetMark(IIfcProduct product)
            {
                var propertySets = product.IsDefinedBy
                    .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet)
                    .Select(r => (IIfcPropertySet)r.RelatingPropertyDefinition)
                    .ToList();

                var psetNames = new[] { "Pset_Revit_Identity Data", "Pset_ConcreteElementGeneral" };

                foreach (var targetName in psetNames)
                {
                    foreach (var pset in propertySets)
                    {
                        if (!pset.Name.ToString().Contains(targetName, StringComparison.OrdinalIgnoreCase))
                            continue;

                        foreach (var prop in pset.HasProperties.OfType<IIfcPropertySingleValue>())
                        {
                            if (!prop.Name.ToString().Equals("Mark", StringComparison.OrdinalIgnoreCase))
                                continue;

                            var val = prop.NominalValue?.ToString()?.Trim();
                            if (!string.IsNullOrWhiteSpace(val))
                                return val;
                        }
                    }
                }

                var productName = product.Name?.ToString()?.Trim();
                return string.IsNullOrWhiteSpace(productName) ? null : productName;
            }

            private static string CleanElementName(string rawName)
            {
                if (string.IsNullOrWhiteSpace(rawName)) return "-";
                
                return rawName.Replace("- Assembly", "")
                             .Replace(" Assembly", "")
                             .Replace(" : ", " - ")
                             .Trim();
            }

            private double? GetQuantity(IIfcProduct product, params string[] keywords)
            {
                var quantitySets = product.IsDefinedBy
                    .Where(r => r.RelatingPropertyDefinition is IIfcElementQuantity)
                    .Select(r => (IIfcElementQuantity)r.RelatingPropertyDefinition);

                foreach (var quantitySet in quantitySets)
                {
                    foreach (var quantity in quantitySet.Quantities)
                    {
                        if (!ContainsAny(quantity.Name.ToString(), keywords))
                            continue;

                        if (quantity is IIfcQuantityLength qLength) return qLength.LengthValue;
                        if (quantity is IIfcQuantityArea qArea) return qArea.AreaValue;
                        if (quantity is IIfcQuantityVolume qVolume) return qVolume.VolumeValue;
                        if (quantity is IIfcQuantityWeight qWeight) return qWeight.WeightValue;
                    }
                }

                return null;
            }

            private double? GetQuantityWeightTons(IIfcProduct product, params string[] keywords)
            {
                var quantitySets = product.IsDefinedBy
                    .Where(r => r.RelatingPropertyDefinition is IIfcElementQuantity)
                    .Select(r => (IIfcElementQuantity)r.RelatingPropertyDefinition);

                foreach (var quantitySet in quantitySets)
                {
                    foreach (var quantity in quantitySet.Quantities.OfType<IIfcQuantityWeight>())
                    {
                        if (!ContainsAny(quantity.Name.ToString(), keywords))
                            continue;

                        return UnitSystem.ToTons(quantity.WeightValue, MassUnit.Kilogram);
                    }
                }

                return null;
            }

            /// <summary>
            /// Searches property sets for a numeric property matching the keywords.
            /// </summary>
            /// <remarks>
            /// Returns the raw numeric value without any unit conversion.
            /// This works for dimensionless properties and standard IFC files
            /// where property values follow the project's declared unit context.
            /// If a property stores a value in a non-standard unit (e.g. a
            /// "weight" property in tons instead of kg), the caller will
            /// receive an unconverted raw number.
            /// </remarks>
            private double? GetPropertyDouble(IIfcProduct product, params string[] keywords)
            {
                var propertySets = product.IsDefinedBy
                    .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet)
                    .Select(r => (IIfcPropertySet)r.RelatingPropertyDefinition);

                foreach (var propertySet in propertySets)
                {
                    foreach (var property in propertySet.HasProperties.OfType<IIfcPropertySingleValue>())
                    {
                        if (!ContainsAny(property.Name.ToString(), keywords))
                            continue;

                        if (double.TryParse(
                            property.NominalValue?.ToString(),
                            NumberStyles.Any,
                            CultureInfo.InvariantCulture,
                            out var parsed))
                        {
                            return parsed;
                        }
                    }
                }

                return null;
            }

            private double? GetPropertyWeightTons(IIfcProduct product, params string[] keywords)
            {
                var propertySets = product.IsDefinedBy
                    .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet)
                    .Select(r => (IIfcPropertySet)r.RelatingPropertyDefinition);

                foreach (var propertySet in propertySets)
                {
                    foreach (var property in propertySet.HasProperties.OfType<IIfcPropertySingleValue>())
                    {
                        var propertyName = property.Name.ToString();
                        if (!ContainsAny(propertyName, keywords))
                            continue;

                        if (!double.TryParse(
                            property.NominalValue?.ToString(),
                            NumberStyles.Any,
                            CultureInfo.InvariantCulture,
                            out var parsed))
                        {
                            continue;
                        }

                        return ConvertWeightPropertyToTons(parsed, propertyName, property.Unit?.ToString());
                    }
                }

                return null;
            }

            private static double ConvertWeightPropertyToTons(double value, string propertyName, string? unitName)
            {
                var source = $"{propertyName} {unitName}".Trim();

                if (source.Contains("kg", StringComparison.OrdinalIgnoreCase) ||
                    source.Contains("kilo", StringComparison.OrdinalIgnoreCase))
                {
                    return UnitSystem.ToTons(value, MassUnit.Kilogram);
                }

                if (source.Contains("ton", StringComparison.OrdinalIgnoreCase) ||
                    source.Contains("tonne", StringComparison.OrdinalIgnoreCase))
                {
                    return value;
                }

                // Tekla often writes plain numeric "Weight" properties in kilograms.
                // Smaller values are kept as tons to avoid changing hand-authored data.
                return value > 100d
                    ? UnitSystem.ToTons(value, MassUnit.Kilogram)
                    : value;
            }

            private string? GetMaterial(IIfcProduct product)
            {
                try
                {
                    var relation = product.HasAssociations
                        ?.OfType<IIfcRelAssociatesMaterial>()
                        .FirstOrDefault();

                    if (relation == null)
                        return null;

                    return relation.RelatingMaterial switch
                    {
                        IIfcMaterial singleMaterial => singleMaterial.Name.ToString(),
                        IIfcMaterialLayer layer => layer.Material?.Name.ToString(),
                        IIfcMaterialLayerSet layerSet => JoinDistinctMaterials(
                            layerSet.MaterialLayers.Select(l => l.Material?.Name.ToString())),
                        IIfcMaterialList materialList => JoinDistinctMaterials(
                            materialList.Materials.Select(m => m.Name.ToString())),
                        _ => null
                    };
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Material extraction error: {ex.Message}");
                    return null;
                }
            }

            private static double? EstimateWeightFromMaterial(string? material, double volumeInCubicMeters)
            {
                var name = string.IsNullOrWhiteSpace(material) ? string.Empty : material.ToUpperInvariant();

                double densityKgPerCubicMeter =
                    name.ContainsAny("STEEL", "REBAR", "METAL") ? SteelDensityKgPerCubicMeter :
                    name.ContainsAny("ALUMIN") ? AluminumDensityKgPerCubicMeter :
                    name.ContainsAny("GLASS") ? GlassDensityKgPerCubicMeter :
                    name.ContainsAny("TIMBER", "WOOD") ? TimberDensityKgPerCubicMeter :
                    ConcreteDensityKgPerCubicMeter;

                return (volumeInCubicMeters * densityKgPerCubicMeter) / UnitSystem.KilogramsPerTon;
            }

            // -------------------------------------------------------------------------
            // Filters
            // -------------------------------------------------------------------------

            private static bool IsEngineeringProduct(IIfcProduct product)
            {
                // Exclude non-physical / spatial elements
                if (product is IIfcOpeningElement or IIfcSpace or IIfcVirtualElement)
                    return false;

                return product is IIfcElement &&
                       !string.IsNullOrWhiteSpace(product.GlobalId) &&
                       !string.IsNullOrWhiteSpace(product.Name?.ToString());
            }

            // -------------------------------------------------------------------------
            // Conversion helpers
            // -------------------------------------------------------------------------

            private double ConvertLength(double rawLength) => rawLength * _lengthToMetersFactor;

            private double? ConvertLength(double? rawLength) =>
                rawLength.HasValue ? ConvertLength(rawLength.Value) : null;

            private double? ConvertVolume(double? rawVolume) =>
                rawVolume.HasValue ? rawVolume.Value * _volumeToCubicMetersFactor : null;

            // -------------------------------------------------------------------------
            // Static helpers
            // -------------------------------------------------------------------------

            private static bool ContainsAny(string source, params string[] keywords)
            {
                foreach (var keyword in keywords)
                {
                    if (source.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }

            private static double? SumOrNull(IEnumerable<double?> values)
            {
                double total = 0d;
                bool hasValue = false;

                foreach (var value in values)
                {
                    if (!value.HasValue)
                        continue;

                    total += value.Value;
                    hasValue = true;
                }

                return hasValue ? total : null;
            }

            private static string? JoinDistinctMaterials(IEnumerable<string?> materials)
            {
                var distinct = materials
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Select(m => m!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return distinct.Count > 0 ? string.Join(", ", distinct) : null;
            }

            private static PrecastElement ToPrecastElement(IEngineeringSnapshot snapshot)
            {
                return new PrecastElement
                {
                    ElementID = snapshot.ElementId,
                    ElementRevitId = snapshot.RevitId,
                    ElementMark = snapshot.Mark,
                    ElementType = snapshot.ElementType,
                    ElementLength = snapshot.ElementLength,
                    ElementWidth = snapshot.ElementWidth,
                    ElementHeight = snapshot.ElementHeight,
                    ElementMaterial = snapshot.Material ?? string.Empty,
                    ElementWT = snapshot.Weight,
                    ElementVolume = snapshot.Volume,
                    ElementCOG = snapshot.CenterOfGravity,
                    BBoxLength = snapshot.Geometry?.Length,
                    BBoxWidth = snapshot.Geometry?.Width,
                    BBoxHeight = snapshot.Geometry?.Height
                };
            }
        }

        // -------------------------------------------------------------------------
        // Snapshot types
        // -------------------------------------------------------------------------

        private interface IEngineeringSnapshot
        {
            string ElementId { get; }
            string? RevitId { get; }
            string? Mark { get; }
            string ElementType { get; }
            string? Material { get; }
            double? Weight { get; }
            double? Volume { get; }
            double? ElementLength { get; }
            double? ElementWidth { get; }
            double? ElementHeight { get; }
            GeometrySnapshot? Geometry { get; }
            DomainPoint3D CenterOfGravity { get; }
        }

        private sealed record SourceElementSnapshot(
            int EntityLabel,
            string ElementId,
            string ElementType,
            string? Material,
            double? Weight,
            double? Volume,
            GeometrySnapshot? Geometry,
            double? ElementLength,
            double? ElementWidth,
            double? ElementHeight,
            string? RevitId,
            string? Mark) : IEngineeringSnapshot
        {
            public bool HasEngineeringData =>
                Geometry?.HasBounds == true ||
                Weight.HasValue ||
                Volume.HasValue;

            public DomainPoint3D CenterOfGravity =>
                Geometry?.Center ?? new DomainPoint3D();
        }

        private sealed record AggregateSnapshot(
            int EntityLabel,
            string ElementId,
            string ElementType,
            string? Material,
            double? Weight,
            double? Volume,
            GeometrySnapshot? Geometry,
            double? ElementLength,
            double? ElementWidth,
            double? ElementHeight,
            string? RevitId,
            string? Mark) : IEngineeringSnapshot
        {
            public DomainPoint3D CenterOfGravity =>
                Geometry?.Center ?? new DomainPoint3D();

            public static AggregateSnapshot FromParentOnly(SourceElementSnapshot parent) =>
                new(parent.EntityLabel,
                    parent.ElementId,
                    parent.ElementType,
                    parent.Material,
                    parent.Weight,
                    parent.Volume,
                    parent.Geometry,
                    parent.ElementLength,
                    parent.ElementWidth,
                    parent.ElementHeight,
                    parent.RevitId,
                    parent.Mark);
        }

        private sealed record GeometrySnapshot(
            double MinX, double MinY, double MinZ,
            double MaxX, double MaxY, double MaxZ,
            DomainPoint3D Center)
        {
            public double Length => MaxX - MinX;
            public double Width => MaxY - MinY;
            public double Height => MaxZ - MinZ;

            public bool HasBounds =>
                IsFinite(MinX) && IsFinite(MinY) && IsFinite(MinZ) &&
                IsFinite(MaxX) && IsFinite(MaxY) && IsFinite(MaxZ);

            public static GeometrySnapshot? Union(
                IReadOnlyList<GeometrySnapshot> geometries,
                double? totalWeight,
                IReadOnlyList<DomainPoint3D> centers,
                IReadOnlyList<double?> weights)
            {
                if (geometries.Count == 0)
                    return null;

                double minX = geometries.Min(g => g.MinX);
                double minY = geometries.Min(g => g.MinY);
                double minZ = geometries.Min(g => g.MinZ);
                double maxX = geometries.Max(g => g.MaxX);
                double maxY = geometries.Max(g => g.MaxY);
                double maxZ = geometries.Max(g => g.MaxZ);

                var center = ComputeCenter(minX, minY, minZ, maxX, maxY, maxZ, totalWeight, centers, weights);
                return new GeometrySnapshot(minX, minY, minZ, maxX, maxY, maxZ, center);
            }

            public static GeometrySnapshot? FromCenterPoints(
                IReadOnlyList<DomainPoint3D> centers,
                double? totalWeight,
                IReadOnlyList<double?> weights)
            {
                if (centers.Count == 0)
                    return null;

                var center = ComputeWeightedCenter(centers, totalWeight, weights)
                    ?? new DomainPoint3D(
                        centers.Average(c => c.X),
                        centers.Average(c => c.Y),
                        centers.Average(c => c.Z));

                return new GeometrySnapshot(
                    center.X, center.Y, center.Z,
                    center.X, center.Y, center.Z,
                    center);
            }

            private static DomainPoint3D ComputeCenter(
                double minX, double minY, double minZ,
                double maxX, double maxY, double maxZ,
                double? totalWeight,
                IReadOnlyList<DomainPoint3D> centers,
                IReadOnlyList<double?> weights)
            {
                return ComputeWeightedCenter(centers, totalWeight, weights) ??
                    new DomainPoint3D(
                        (minX + maxX) / 2d,
                        (minY + maxY) / 2d,
                        (minZ + maxZ) / 2d);
            }

            private static DomainPoint3D? ComputeWeightedCenter(
                IReadOnlyList<DomainPoint3D> centers,
                double? totalWeight,
                IReadOnlyList<double?> weights)
            {
                if (!totalWeight.HasValue || totalWeight.Value <= 0 || centers.Count != weights.Count)
                    return null;

                double weightedX = 0d, weightedY = 0d, weightedZ = 0d, accumulated = 0d;

                for (int i = 0; i < centers.Count; i++)
                {
                    var weight = weights[i];
                    if (!weight.HasValue || weight.Value <= 0)
                        continue;

                    weightedX += centers[i].X * weight.Value;
                    weightedY += centers[i].Y * weight.Value;
                    weightedZ += centers[i].Z * weight.Value;
                    accumulated += weight.Value;
                }

                return accumulated <= 0
                    ? null
                    : new DomainPoint3D(
                        weightedX / accumulated,
                        weightedY / accumulated,
                        weightedZ / accumulated);
            }

            private static bool IsFinite(double value) =>
                !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    // -------------------------------------------------------------------------
    // String extension helper (avoids repeating ContainsAny in static contexts)
    // -------------------------------------------------------------------------
    internal static class StringExtensions
    {
        internal static bool ContainsAny(this string source, params string[] keywords)
        {
            foreach (var keyword in keywords)
            {
                if (source.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
