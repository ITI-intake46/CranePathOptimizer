using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.CreatedProject;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Units;
using CraneOptimizer.Infrastructure.Ifc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        private readonly IIfcDiagnosticAuditService _auditService;
        private readonly IIfcNormalizationDecisionService _normalizationService;
        private readonly IIfcModelSessionService _sessionService;
        private readonly IIfcGeoreferencingExtractor _georeferencingExtractor;
        private readonly ICentralIfcPropertyAliasResolver _aliasResolver;

        public IfcReaderRepository(
            IIfcDiagnosticAuditService auditService,
            IIfcNormalizationDecisionService normalizationService,
            IIfcModelSessionService sessionService)
            : this(auditService, normalizationService, sessionService, new IfcGeoreferencingExtractor(), new CentralIfcPropertyAliasResolver(new IfcExporterProfileResolver()))
        {
        }

        public IfcReaderRepository(
            IIfcDiagnosticAuditService auditService,
            IIfcNormalizationDecisionService normalizationService,
            IIfcModelSessionService sessionService,
            IIfcGeoreferencingExtractor georeferencingExtractor)
            : this(auditService, normalizationService, sessionService, georeferencingExtractor, new CentralIfcPropertyAliasResolver(new IfcExporterProfileResolver()))
        {
        }

        public IfcReaderRepository(
            IIfcDiagnosticAuditService auditService,
            IIfcNormalizationDecisionService normalizationService,
            IIfcModelSessionService sessionService,
            IIfcGeoreferencingExtractor georeferencingExtractor,
            ICentralIfcPropertyAliasResolver aliasResolver)
        {
            _auditService = auditService;
            _normalizationService = normalizationService;
            _sessionService = sessionService;
            _georeferencingExtractor = georeferencingExtractor;
            _aliasResolver = aliasResolver;
        }

        public IfcStore? CurrentModel => _sessionService.Current?.Model;
        public Xbim3DModelContext? GeometryContext => _sessionService.Current?.GeometryContext;
        public Guid? CurrentSessionId => _sessionService.Current?.SessionId;
        public string? CurrentFileIdentity => _sessionService.Current?.FileIdentity;
        public double ModelLengthFactor => _sessionService.Current?.ModelLengthFactor ?? 1.0;
        public double? ModelGroundElevation => _sessionService.Current?.ModelGroundElevation;

        public IfcGeoreferencingResult? GetGeoreferencingData()
        {
            if (CurrentModel == null)
            {
                return null;
            }

            return _georeferencingExtractor.Extract(CurrentModel);
        }


        public void ClearData()
        {
            _sessionService.Clear();
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
            return ReadElementsCore(ifcPath, null, null, CancellationToken.None);
        }

        public List<PrecastElement> ReadElements(string ifcPath, IfcReadingOptions? options)
        {
            return ReadElementsCore(ifcPath, null, options, CancellationToken.None);
        }

        public List<PrecastElement> ReadElements(string path, IProgress<double> progress)
        {
            return ReadElementsCore(path, progress, null, CancellationToken.None);
        }

        public List<PrecastElement> ReadElements(
            string path,
            IProgress<double> progress,
            IfcReadingOptions? options,
            CancellationToken cancellationToken = default)
        {
            return ReadElementsCore(path, progress, options, cancellationToken);
        }

        public List<PrecastElement> ReadElements(string path, IProgress<double> progress, CancellationToken cancellationToken)
        {
            return ReadElementsCore(path, progress, null, cancellationToken);
        }

        public Task InitializeSessionForRenderingAsync(string ifcPath, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                _sessionService.OpenSession(ifcPath, cancellationToken);
            }, cancellationToken);
        }

        private List<PrecastElement> ReadElementsCore(
            string ifcPath,
            IProgress<double>? progress,
            IfcReadingOptions? options,
            CancellationToken cancellationToken)
        {
            try
            {
                var concreteDensityKgPerM3 = ResolveConcreteDensity(options);
                var loadMemoryBefore = GetPrivateMemoryBytes();
                var session = _sessionService.OpenSession(ifcPath, cancellationToken);

                // Run synchronous diagnostics audit and normalization decision preview (Task 3 & 4)
                var auditTimer = Stopwatch.StartNew();
                var auditResult = _auditService.RunAuditAsync(ifcPath, options, null, cancellationToken).GetAwaiter().GetResult();
                auditTimer.Stop();
                var previewResult = _normalizationService.GenerateNormalizationPreviewAsync(auditResult, cancellationToken).GetAwaiter().GetResult();

                var extractor = new IfcEngineeringExtractor(
                    session.Model,
                    session.GeometryContext,
                    session.ModelLengthFactor,
                    concreteDensityKgPerM3,
                    _aliasResolver,
                    auditResult.Metadata);

                var extractionTimer = Stopwatch.StartNew();
                var elements = extractor.ExtractNormalized(previewResult, auditResult, progress, cancellationToken);
                extractionTimer.Stop();

                Debug.WriteLine(
                    $"[IfcReaderRepository] Load complete. SessionId={session.SessionId}, " +
                    $"Open={session.OpenDuration.TotalMilliseconds:F0} ms, " +
                    $"CreateContext={session.CreateContextDuration.TotalMilliseconds:F0} ms, " +
                    $"Audit={auditTimer.ElapsedMilliseconds} ms, " +
                    $"Extraction={extractionTimer.ElapsedMilliseconds} ms, " +
                    $"Elements={elements.Count}, ShapeInstances={session.ShapeInstanceCount}, " +
                    $"MemoryBeforeLoad={FormatBytes(loadMemoryBefore)}, " +
                    $"MemoryAfterLoad={FormatBytes(GetPrivateMemoryBytes())}");

                return elements;
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

        private static double ResolveConcreteDensity(IfcReadingOptions? options)
        {
            var density = options?.ConcreteDensityKgPerM3;
            return density.HasValue && density.Value > 0
                ? density.Value
                : ConcreteDensityKgPerCubicMeter;
        }

        // -------------------------------------------------------------------------
        // Extractor
        // -------------------------------------------------------------------------

        private sealed class IfcEngineeringExtractor
        {
            private readonly IfcStore _model;
            private readonly Xbim3DModelContext _geometryContext;
            private readonly double _lengthToMetersFactor;
            private readonly double _volumeToCubicMetersFactor;
            private readonly double _concreteDensityKgPerCubicMeter;
            private readonly ICentralIfcPropertyAliasResolver _aliasResolver;
            private readonly IfcExporterProfile _exporterProfile;
            private readonly IfcAuditMetadata _auditMetadata;

            private readonly Dictionary<int, SourceElementSnapshot> _sourceCache = new();
            private readonly Dictionary<int, AggregateSnapshot> _aggregateCache = new();

            public IfcEngineeringExtractor(
                IfcStore model,
                Xbim3DModelContext geometryContext,
                double lengthToMetersFactor,
                double concreteDensityKgPerCubicMeter,
                ICentralIfcPropertyAliasResolver aliasResolver,
                IfcAuditMetadata auditMetadata)
            {
                _model = model;
                _geometryContext = geometryContext;
                _lengthToMetersFactor = lengthToMetersFactor;
                _volumeToCubicMetersFactor = Math.Pow(lengthToMetersFactor, 3);
                _concreteDensityKgPerCubicMeter = concreteDensityKgPerCubicMeter;
                _aliasResolver = aliasResolver;
                _auditMetadata = auditMetadata;

                var profileResolver = new IfcExporterProfileResolver();
                _exporterProfile = profileResolver.ResolveProfile(model.Header?.FileName?.OriginatingSystem ?? string.Empty);
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

                // Apply the same post-processing used by ExtractNormalized.
                // This protects any legacy caller still using Extract().
                PropagateConcreteMaterials(result);
                RepairWeightsAndGaAfterMaterialPropagation(result);

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

                var resolvedVolume = _aliasResolver.ResolveProperty(product, "Volume", _exporterProfile);
                double? rawVolume = null;
                if (resolvedVolume != null && double.TryParse(resolvedVolume.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedVol))
                {
                    rawVolume = parsedVol;
                }
                var volume = ConvertVolume(rawVolume);
                if (!(volume is > 0) && geometry?.MeshVolumeM3 is > 0)
                {
                    volume = geometry.MeshVolumeM3;
                }

                var material = NormalizeMaterial(GetMaterial(product));
                var weight = GetWeight(product);

                // Fallback: If Weight is 0 or missing, use direct volume and the project concrete density.
                var revitId = GetRevitId(product);
                var productName = product.Name?.ToString()?.Trim();
                var rawName = GetMark(product);
                var cleanedName = CleanElementName(rawName ?? string.Empty);

                if ((weight ?? 0) <= 0
                    && volume.HasValue
                    && volume.Value > 0
                    && IsValidMaterialName(material))
                {
                    weight = EstimateWeightFromMaterial(material, volume.Value);
                }

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
                    cleanedName, productName);
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
                var productName = assembly.Name?.ToString()?.Trim();
                var rawName = GetMark(assembly);
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
                    cleanedName, productName);

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
                return IsValidMaterialName(parentSnapshot.Material)
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

                var meshVolumeM3 = totalMeshVolume > 0
    ? totalMeshVolume * _volumeToCubicMetersFactor
    : (double?)null;

                return new GeometrySnapshot(
                    ConvertLength(minX),
                    ConvertLength(minY),
                    ConvertLength(minZ),
                    ConvertLength(maxX),
                    ConvertLength(maxY),
                    ConvertLength(maxZ),
                    center,
                    meshVolumeM3);
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
                var resolved = _aliasResolver.ResolveProperty(product, "Weight", _exporterProfile);
                if (resolved != null && double.TryParse(resolved.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                {
                    var isQty = product.IsDefinedBy
                        .Where(r => r.RelatingPropertyDefinition is IIfcElementQuantity)
                        .SelectMany(r => ((IIfcElementQuantity)r.RelatingPropertyDefinition).Quantities)
                        .Any(q => q.Name.ToString() == resolved.SourcePropertyName);

                    if (isQty)
                    {
                        return UnitSystem.ToTons(parsed, MassUnit.Kilogram);
                    }
                    else
                    {
                        var propUnit = product.IsDefinedBy
                            .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet)
                            .SelectMany(r => ((IIfcPropertySet)r.RelatingPropertyDefinition).HasProperties.OfType<IIfcPropertySingleValue>())
                            .FirstOrDefault(p => p.Name.ToString() == resolved.SourcePropertyName)?
                            .Unit?.ToString();

                        return ConvertWeightPropertyToTons(parsed, resolved.SourcePropertyName ?? string.Empty, propUnit);
                    }
                }
                return null;
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

            private CanonicalAssemblyIdentityResult ResolveCanonicalAssemblyIdentity(
                IIfcProduct? product, 
                IfcNormalizedElementPreview preview,
                IfcExporterProfile exporterProfile)
            {
                return _aliasResolver.ResolveCanonicalAssemblyIdentity(product, preview.Name, exporterProfile);
            }

            private static void ValidateCanonicalAssembly(PrecastElement pe)
            {
                pe.ValidationWarnings = new List<string>();
                var severities = new List<string>();

                // 1. MissingAssemblyMark
                if (!pe.AssemblyMarkIsExplicit)
                {
                    pe.ValidationWarnings.Add("MissingAssemblyMark: No explicit assembly-level or element-level mark found; falling back to display name.");
                    severities.Add("Warning");
                }

                // 2. MissingTrustedWeight
                var isWeightTrusted = pe.WeightTrustLevel == IfcWeightTrustLevel.ExplicitQuantity ||
                                      pe.WeightTrustLevel == IfcWeightTrustLevel.ExplicitProperty;
                if (!isWeightTrusted)
                {
                    pe.ValidationWarnings.Add("MissingTrustedWeight: Element weight is not from an explicit trusted source.");
                    severities.Add("Warning");
                }

                // 3. EstimatedWeightNotApproved
                if (pe.WeightTrustLevel == IfcWeightTrustLevel.EstimatedFallback_NotAllowedForGA)
                {
                    pe.ValidationWarnings.Add("EstimatedWeightNotApproved: Estimated fallback weight is used and strictly blocked from GA.");
                    severities.Add("Error");
                }

                // 4. MissingAssemblyCOG
                if (pe.AssemblyCenterOfGravity == null || (Math.Abs(pe.AssemblyCenterOfGravity.X) < 0.0001 && Math.Abs(pe.AssemblyCenterOfGravity.Y) < 0.0001 && Math.Abs(pe.AssemblyCenterOfGravity.Z) < 0.0001))
                {
                    pe.ValidationWarnings.Add("MissingAssemblyCOG: Assembly Center of Gravity is missing, zero, or invalid.");
                    severities.Add("Warning");
                }

                // 5. MissingAssemblyVolume
                if (!pe.AssemblyVolumeM3.HasValue || pe.AssemblyVolumeM3.Value <= 0)
                {
                    pe.ValidationWarnings.Add("MissingAssemblyVolume: Assembly volume is missing or zero.");
                    severities.Add("Warning");
                }

                // 6. MissingMaterial
                if (string.IsNullOrWhiteSpace(pe.AssemblyMaterial) || pe.AssemblyMaterial == "-")
                {
                    pe.ValidationWarnings.Add("MissingMaterial: Assembly material is not specified.");
                    severities.Add("Info");
                }

                // 7. SourceMappingLowConfidence
                if (pe.AssemblyMarkConfidence == AssemblyMarkConfidence.Low || pe.AssemblyMarkConfidence == AssemblyMarkConfidence.None)
                {
                    pe.ValidationWarnings.Add($"SourceMappingLowConfidence: Assembly mark has low/no confidence ({pe.AssemblyMarkConfidence}).");
                    severities.Add("Warning");
                }

                // ParentChildDoubleCountRisk (initial warning if parent global id is present)
                if (!string.IsNullOrWhiteSpace(pe.ParentGlobalId) && pe.IncludeInGa)
                {
                    pe.ValidationWarnings.Add($"ParentChildDoubleCountRisk: Child component '{pe.ElementID}' is associated with parent assembly '{pe.ParentGlobalId}'. Excluded dynamically from GA if parent is liftable.");
                    severities.Add("Warning");
                }

                if (severities.Contains("Error"))
                {
                    pe.ValidationStatus = ValidationStatus.Error;
                }
                else if (severities.Contains("Warning"))
                {
                    pe.ValidationStatus = ValidationStatus.Warning;
                }
                else
                {
                    pe.ValidationStatus = ValidationStatus.Valid;
                }
            }

            private string? GetBestMarkCandidate(IIfcProduct product)
            {
                if (product == null) return null;
                var res = _aliasResolver.ResolveCanonicalAssemblyIdentity(product, product.Name?.ToString() ?? string.Empty, _exporterProfile);
                return res.AssemblyMark;
            }

            private string? GetMark(IIfcProduct product) => GetBestMarkCandidate(product);


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



            private double ConvertWeightPropertyToTons(double value, string propertyName, string? unitName)
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

                if (source.Contains("lb", StringComparison.OrdinalIgnoreCase) ||
                    source.Contains("pound", StringComparison.OrdinalIgnoreCase))
                {
                    return value * 0.00045359237; // lb to ton
                }

                // Fallback to project MassConversionFactor
                if (_auditMetadata.MassConversionFactor.HasValue)
                {
                    return UnitSystem.ToTons(value * _auditMetadata.MassConversionFactor.Value, MassUnit.Kilogram);
                }

                // Tekla often writes plain numeric "Weight" properties in kilograms.
                // Smaller values are kept as tons to avoid changing hand-authored data.
                return value > 100d
                    ? UnitSystem.ToTons(value, MassUnit.Kilogram)
                    : value;
            }
            //GetMaterial
            private string? GetMaterial(IIfcProduct product)
            {
                try
                {
                    var resolved = _aliasResolver.ResolveProperty(product, "Material", _exporterProfile);
                    return resolved?.Value;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Material extraction error: {ex.Message}");
                    return null;
                }
            }

            private double ResolveDensityKgPerCubicMeter(string? material)
            {
                var name = string.IsNullOrWhiteSpace(material) ? string.Empty : material.ToUpperInvariant();

                return
                    name.ContainsAny("STEEL", "REBAR", "METAL") ? SteelDensityKgPerCubicMeter :
                    name.ContainsAny("ALUMIN") ? AluminumDensityKgPerCubicMeter :
                    name.ContainsAny("GLASS") ? GlassDensityKgPerCubicMeter :
                    name.ContainsAny("TIMBER", "WOOD") ? TimberDensityKgPerCubicMeter :
                    _concreteDensityKgPerCubicMeter;
            }

            private double? EstimateWeightFromMaterial(string? material, double volumeInCubicMeters)
            {
                if (volumeInCubicMeters <= 0)
                    return null;

                var densityKgPerCubicMeter = ResolveDensityKgPerCubicMeter(material);

                return (volumeInCubicMeters * densityKgPerCubicMeter) / UnitSystem.KilogramsPerTon;
            }
            private double? EstimateVolumeFromWeightAndMaterial(double? weightTons, string? material)
            {
                if (!(weightTons is > 0))
                    return null;

                var densityKgPerCubicMeter = ResolveDensityKgPerCubicMeter(material);

                if (densityKgPerCubicMeter <= 0)
                    return null;

                return (weightTons.Value * UnitSystem.KilogramsPerTon) / densityKgPerCubicMeter;
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
                rawVolume.HasValue ? rawVolume.Value * _auditMetadata.VolumeConversionFactor : null;

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
            private static bool IsValidMaterialName(string? material)
            {
                if (string.IsNullOrWhiteSpace(material))
                    return false;

                var m = material.Trim();

                return !m.Equals("<Unnamed>", StringComparison.OrdinalIgnoreCase)
                    && !m.Equals("Unnamed", StringComparison.OrdinalIgnoreCase)
                    && !m.Equals("N/A", StringComparison.OrdinalIgnoreCase)
                    && !m.Equals("NA", StringComparison.OrdinalIgnoreCase)
                    && !m.Equals("Unspecified", StringComparison.OrdinalIgnoreCase)
                    && !m.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
                    && !m.Equals("Default", StringComparison.OrdinalIgnoreCase)
                    && !m.Equals("<By Category>", StringComparison.OrdinalIgnoreCase)
                    && !m.Equals("By Category", StringComparison.OrdinalIgnoreCase);
            }

            private static string? NormalizeMaterial(string? material)
            {
                return IsValidMaterialName(material) ? material!.Trim() : null;
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
                    .Select(NormalizeMaterial)
                    .Where(IsValidMaterialName)
                    .Select(m => m!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return distinct.Count > 0 ? string.Join(", ", distinct) : null;
            }

            public List<PrecastElement> ExtractNormalized(
                IfcNormalizationPreviewSummary previewSummary,
                IfcAuditResult auditResult,
                IProgress<double>? progress,
                CancellationToken cancellationToken)
            {
                // 1. Get all engineering products to build the _sourceCache
                var physicalProducts = _model.Instances
                    .OfType<IIfcProduct>()
                    .Where(IsEngineeringProduct)
                    .ToList();

                var selectedProducts = physicalProducts
                    .Concat(_model.Instances
                        .OfType<IIfcProduct>()
                        .Where(product => product is IIfcGrid && !string.IsNullOrWhiteSpace(product.GlobalId)))
                    .GroupBy(product => product.EntityLabel)
                    .Select(group => group.First())
                    .ToList();
                var retainedGridGlobalIds = new IfcGridReferenceDeduplicator(
                        _geometryContext,
                        _lengthToMetersFactor)
                    .SelectBottomGridGlobalIds(selectedProducts.OfType<IIfcGrid>());

                if (physicalProducts.Count > 0)
                {
                    CacheSourceProducts(physicalProducts, progress, cancellationToken);
                }

                // Create a fast lookup by GlobalId for IIfcProduct
                var productByGlobalId = selectedProducts
    .Where(p => !string.IsNullOrWhiteSpace(p.GlobalId))
    .GroupBy(p => (string)p.GlobalId, StringComparer.Ordinal)
    .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

                var previewByGlobalId = previewSummary.Elements
    .Where(p => !string.IsNullOrWhiteSpace(p.GlobalId))
    .GroupBy(p => p.GlobalId, StringComparer.Ordinal)
    .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

                var result = new List<PrecastElement>();
                int unresolvedAssemblyMarkCount = 0;

                // 2. Loop through elements from the previewSummary
                foreach (var preview in previewSummary.Elements)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Try to retrieve cached SourceElementSnapshot
                    IIfcProduct? product = null;
                    SourceElementSnapshot? sourceSnapshot = null;

                    if (productByGlobalId.TryGetValue(preview.GlobalId, out product))
                    {
                        _sourceCache.TryGetValue(product.EntityLabel, out sourceSnapshot);
                    }

                    if (product is IIfcGrid
                        && !retainedGridGlobalIds.Contains(preview.GlobalId))
                    {
                        continue;
                    }

                    IIfcProduct? parentProduct = null;
                    SourceElementSnapshot? parentSnapshot = null;
                    IfcNormalizedElementPreview? parentPreview = null;

                    if (!string.IsNullOrWhiteSpace(preview.ParentGlobalId))
                    {
                        previewByGlobalId.TryGetValue(preview.ParentGlobalId, out parentPreview);

                        if (productByGlobalId.TryGetValue(preview.ParentGlobalId, out parentProduct))
                        {
                            _sourceCache.TryGetValue(parentProduct.EntityLabel, out parentSnapshot);
                        }
                    }

                    var isRevitAssemblyChild =
    parentPreview != null
    && parentPreview.RawIfcType.Equals("IfcElementAssembly", StringComparison.OrdinalIgnoreCase)
    && parentPreview.Role == IfcElementRole.GroupingOnly;
                    //var isRevitAssemblyChild =
                    //    previewSummary.DetectedProfile == IfcExporterProfile.Revit
                    //    && parentPreview != null
                    //    && parentPreview.RawIfcType.Equals("IfcElementAssembly", StringComparison.OrdinalIgnoreCase)
                    //    && parentPreview.Role == IfcElementRole.GroupingOnly;

                    // Map all values to a new PrecastElement
                    var pe = new PrecastElement();
                    pe.ElementID = preview.GlobalId;
                    pe.ElementRevitId = sourceSnapshot?.RevitId ?? (product != null ? GetRevitId(product) : null);
                    pe.ElementMark = product != null 
                        ? CleanElementName(GetBestMarkCandidate(product) ?? string.Empty) 
                        : (preview.Name ?? string.Empty);
                    var rawIfcName = product?.Name?.ToString()?.Trim();
                    pe.ElementName = !string.IsNullOrWhiteSpace(rawIfcName)
                        ? rawIfcName
                        : (preview.Name ?? string.Empty);
                    
                    // Traceability to parent assembly
                    pe.ParentAssemblyName = parentProduct != null 
                        ? CleanElementName(GetBestMarkCandidate(parentProduct) ?? parentProduct.Name?.ToString() ?? string.Empty) 
                        : (parentPreview?.Name ?? string.Empty);

                    // Set ElementType = preview.CandidateCategory to fix UI filter labels (Requirement)
                    pe.ElementType = preview.CandidateCategory;

                    // Set standard fields
                    pe.RawIfcType = preview.RawIfcType;
                    pe.ResolvedCategory = preview.CandidateCategory;
                    pe.Role = preview.Role;
                    pe.ParentGlobalId = preview.ParentGlobalId;
                    pe.ParentGlobalIdSource = preview.ParentGlobalIdSource;

                    // Phase 3 Assembly Classification fields
                    pe.MainPartGlobalId = preview.MainPartGlobalId;
                    pe.MainPartName = preview.MainPartName;
                    pe.MainPartCategory = preview.MainPartCategory;
                    pe.MainPartDetectionMethod = preview.MainPartDetectionMethod;
                    pe.ChildCount = preview.ChildCount;
                    pe.VisibleSecondaryPartCount = preview.VisibleSecondaryPartCount;
                    pe.HiddenWeightContributorCount = preview.HiddenWeightContributorCount;
                    pe.AccessoryWeightContributorCount = preview.AccessoryWeightContributorCount;
                    pe.IgnoredReferenceCount = preview.IgnoredReferenceCount;
                    pe.HasSecondaryParts = preview.HasSecondaryParts;
                    pe.HasHiddenContributors = preview.HasHiddenContributors;
                    pe.ChildGlobalIds = new List<string>(preview.ChildGlobalIds);
                    pe.VisibleGeometryOwnerGlobalIds = new List<string>(preview.VisibleGeometryOwnerGlobalIds);
                    pe.HiddenInternalGeometryOwnerGlobalIds = new List<string>(preview.HiddenInternalGeometryOwnerGlobalIds);
                    pe.IncludeInRender = preview.IncludeInRenderPreview;
                    pe.IncludeInElementTree = preview.IncludeInElementTreePreview;
                    pe.IncludeInGa = preview.IncludeInGaPreview;

                    // Populate Canonical Assembly Schema (Phase 2)
                    var canonicalIdentity = ResolveCanonicalAssemblyIdentity(product, preview, previewSummary.DetectedProfile);
                    pe.ExplicitAssemblyMarkFound = canonicalIdentity.IsExplicitAssemblyMark;

                    pe.AssemblyMark = canonicalIdentity.AssemblyMark;
                    pe.ResolvedDisplayMark = canonicalIdentity.AssemblyMark ?? pe.ElementMark ?? pe.ElementName;
                    pe.AssemblyMarkSource = canonicalIdentity.SourcePropertyName != null 
                        ? $"{canonicalIdentity.SourcePropertySet}.{canonicalIdentity.SourcePropertyName}" 
                        : "Fallback";
                    _ = Enum.TryParse<AssemblyMarkConfidence>(canonicalIdentity.Confidence, out var confidence);
                    pe.AssemblyMarkConfidence = confidence;
                    pe.AssemblyMarkIsExplicit = canonicalIdentity.IsExplicitAssemblyMark;
                    pe.SourceSystem = previewSummary.DetectedProfile == IfcExporterProfile.Tekla ? SourceSystem.Tekla :
                                      previewSummary.DetectedProfile == IfcExporterProfile.Revit ? SourceSystem.Revit :
                                      SourceSystem.IFC;
                    pe.ExporterProfile = previewSummary.DetectedProfile;

                    // Run initial validation
                    ValidateCanonicalAssembly(pe);

                    // Assembly Identity Preservation Warnings
                    if ((pe.Role == IfcElementRole.LiftAssembly || pe.Role == IfcElementRole.GroupingOnly) && product != null)
                    {
                        if (!pe.ExplicitAssemblyMarkFound)
                        {
                            unresolvedAssemblyMarkCount++;
                            pe.ExtractionWarnings.Add("No explicit assembly-level mark found; falling back to element/type/product name.");
                        }
                    }

                    // Weight setup
                    pe.WeightTons = preview.WeightStandardValueTons;
                    pe.WeightSource = preview.WeightSource;
                    pe.WeightTrustLevel = preview.WeightTrustLevel;
                    // For ElementWT, set to WeightStandardValueTons to align with UI expectations
                    pe.ElementWT = preview.WeightStandardValueTons;

                    // Decision/Warnings
                    pe.NormalizationDecisionReason = preview.DecisionReason;
                    pe.ExtractionWarnings = new List<string>(preview.Warnings);

                    // Material, with Revit parent assembly inheritance
                    var ownMaterial = NormalizeMaterial(
                        sourceSnapshot?.Material ??
                        (product != null ? GetMaterial(product) : null));

                    var parentMaterial = NormalizeMaterial(parentSnapshot?.Material);

                    if (IsValidMaterialName(ownMaterial))
                    {
                        pe.ElementMaterial = ownMaterial!;
                    }
                    else if (isRevitAssemblyChild && IsValidMaterialName(parentMaterial))
                    {
                        pe.ElementMaterial = parentMaterial!;
                        pe.ExtractionWarnings.Add(
                            $"Material inherited from Revit parent assembly '{parentProduct?.Name}'. ParentGlobalId={preview.ParentGlobalId}");
                    }
                    else
                    {
                        pe.ElementMaterial = string.Empty;
                    }

                    // Geometry / COG / Volume / BoundingBox setup
                    // Geometry / COG / Volume / BoundingBox setup
                    if (preview.Role == IfcElementRole.LiftAssembly)
                    {
                        // Consolidated assembly: we should resolve aggregate geometry/COG/bounds
                        if (product is IIfcElementAssembly assembly)
                        {
                            var aggregate = BuildAggregateSnapshot(assembly, new HashSet<int>());
                            if (aggregate != null)
                            {
                                pe.ElementVolume = aggregate.Volume;
                                pe.VolumeM3 = aggregate.Volume; // standardized unit is m^3

                                // BoundingBox and COG must be standard METERS
                                if (aggregate.Geometry != null)
                                {
                                    pe.BBoxLength = aggregate.Geometry.Length;
                                    pe.BBoxWidth = aggregate.Geometry.Width;
                                    pe.BBoxHeight = aggregate.Geometry.Height;
                                    pe.BoundingBox_M = new DomainPoint3D
                                    {
                                        X = aggregate.Geometry.Length,
                                        Y = aggregate.Geometry.Width,
                                        Z = aggregate.Geometry.Height
                                    };
                                }
                                pe.ElementCOG = aggregate.CenterOfGravity;
                                pe.COG_M = aggregate.CenterOfGravity; // standardized unit is meters
                                pe.PhysicalCOGAvailable = sourceSnapshot?.Geometry != null;
                            }
                        }
                    }
                    else
                    {
                        // Single / standard element
                        if (sourceSnapshot != null)
                        {
                            pe.ElementVolume = preview.VolumeStandardValueM3 ?? sourceSnapshot.Volume;
                            pe.VolumeM3 = preview.VolumeStandardValueM3 ?? sourceSnapshot.Volume;

                            if (sourceSnapshot.Geometry != null)
                            {
                                pe.BBoxLength = sourceSnapshot.Geometry.Length;
                                pe.BBoxWidth = sourceSnapshot.Geometry.Width;
                                pe.BBoxHeight = sourceSnapshot.Geometry.Height;
                                pe.BoundingBox_M = new DomainPoint3D
                                {
                                    X = sourceSnapshot.Geometry.Length,
                                    Y = sourceSnapshot.Geometry.Width,
                                    Z = sourceSnapshot.Geometry.Height
                                };
                                pe.PhysicalCOGAvailable = true;
                            }
                            else
                            {
                                pe.BBoxLength = sourceSnapshot.ElementLength;
                                pe.BBoxWidth = sourceSnapshot.ElementWidth;
                                pe.BBoxHeight = sourceSnapshot.ElementHeight;
                                pe.BoundingBox_M = new DomainPoint3D
                                {
                                    X = sourceSnapshot.ElementLength ?? 0,
                                    Y = sourceSnapshot.ElementWidth ?? 0,
                                    Z = sourceSnapshot.ElementHeight ?? 0
                                };
                                pe.PhysicalCOGAvailable = false;
                            }

                            pe.ElementCOG = sourceSnapshot.CenterOfGravity;
                            pe.COG_M = sourceSnapshot.CenterOfGravity;
                        }
                    }

                    if (!pe.PhysicalCOGAvailable && preview.RawIfcType == "IfcElementAssembly")
                    {
                        pe.ExtractionWarnings.Add("ContainerOnlyAssembly: Physical COG is unavailable because the assembly has no direct geometry shape representation.");
                    }
                    // Last-resort geometry volume fallback from bounding box.
                    // This is approximate, but better than zero for GA pre-screening.
                    if (!(pe.VolumeM3 is > 0)
                        && sourceSnapshot?.Geometry != null
                        && sourceSnapshot.Geometry.Length > 0
                        && sourceSnapshot.Geometry.Width > 0
                        && sourceSnapshot.Geometry.Height > 0)
                    {
                        var bboxVolume = sourceSnapshot.Geometry.Length
                            * sourceSnapshot.Geometry.Width
                            * sourceSnapshot.Geometry.Height;

                        if (bboxVolume > 0)
                        {
                            pe.VolumeM3 = bboxVolume;
                            pe.ElementVolume = bboxVolume;

                            pe.ExtractionWarnings.Add(
                                $"Volume estimated from bounding box because IFC/mesh volume was missing. BBoxVolumeM3={bboxVolume:F3}");
                        }
                    }
                    
                    // Visible geometry owner fallback.
                    // Some normalized Revit/Proxy elements have the logical preview.GlobalId,
                    // but their actual geometry/volume lives on VisibleGeometryOwnerGlobalIds.
                    if (preview.VisibleGeometryOwnerGlobalIds != null && preview.VisibleGeometryOwnerGlobalIds.Count > 0)
                    {
                        var ownerSnapshots = new List<SourceElementSnapshot>();

                        foreach (var ownerId in preview.VisibleGeometryOwnerGlobalIds)
                        {
                            if (string.IsNullOrWhiteSpace(ownerId))
                                continue;

                            if (!productByGlobalId.TryGetValue(ownerId, out var ownerProduct))
                                continue;

                            if (_sourceCache.TryGetValue(ownerProduct.EntityLabel, out var ownerSnapshot))
                                ownerSnapshots.Add(ownerSnapshot);
                        }

                        if (ownerSnapshots.Count > 0)
                        {
                            if (!(pe.VolumeM3 is > 0))
                            {
                                var volumeFromOwners = SumOrNull(ownerSnapshots.Select(s => s.Volume));

                                if (volumeFromOwners is > 0)
                                {
                                    pe.VolumeM3 = volumeFromOwners;
                                    pe.ElementVolume = volumeFromOwners;

                                    pe.ExtractionWarnings.Add(
                                        $"Volume recovered from visible geometry owners. OwnerCount={ownerSnapshots.Count}, VolumeM3={volumeFromOwners:F3}");
                                }
                            }

                            if (!(pe.VolumeM3 is > 0))
                            {
                                var bboxVolumeFromOwners = ownerSnapshots
                                    .Where(s => s.Geometry != null
                                                && s.Geometry.Length > 0
                                                && s.Geometry.Width > 0
                                                && s.Geometry.Height > 0)
                                    .Sum(s => s.Geometry!.Length * s.Geometry.Width * s.Geometry.Height);

                                if (bboxVolumeFromOwners > 0)
                                {
                                    pe.VolumeM3 = bboxVolumeFromOwners;
                                    pe.ElementVolume = bboxVolumeFromOwners;

                                    pe.ExtractionWarnings.Add(
                                        $"Volume estimated from visible geometry owner bounding boxes. OwnerCount={ownerSnapshots.Count}, BBoxVolumeM3={bboxVolumeFromOwners:F3}");
                                }
                            }

                            if (!IsValidMaterialName(pe.ElementMaterial))
                            {
                                var ownerMaterial = JoinDistinctMaterials(ownerSnapshots.Select(s => s.Material));

                                if (IsValidMaterialName(ownerMaterial))
                                {
                                    pe.ElementMaterial = ownerMaterial!;
                                    pe.ExtractionWarnings.Add(
                                        $"Material recovered from visible geometry owners. Material='{pe.ElementMaterial}'");
                                }
                            }

                            if (pe.ElementCOG == null || (pe.ElementCOG.X == 0 && pe.ElementCOG.Y == 0 && pe.ElementCOG.Z == 0))
                            {
                                var firstGeometry = ownerSnapshots.Select(s => s.Geometry).FirstOrDefault(g => g?.HasBounds == true);

                                if (firstGeometry != null)
                                {
                                    pe.ElementCOG = firstGeometry.Center;
                                    pe.COG_M = firstGeometry.Center;
                                }
                            }

                            if ((pe.BBoxLength ?? 0) <= 0 || (pe.BBoxWidth ?? 0) <= 0 || (pe.BBoxHeight ?? 0) <= 0)
                            {
                                var ownerGeometries = ownerSnapshots
                                    .Select(s => s.Geometry)
                                    .Where(g => g?.HasBounds == true)
                                    .Cast<GeometrySnapshot>()
                                    .ToList();

                                var union = GeometrySnapshot.Union(
                                    ownerGeometries,
                                    null,
                                    ownerSnapshots.Select(s => s.CenterOfGravity).ToList(),
                                    ownerSnapshots.Select(s => s.Weight).ToList());

                                if (union != null)
                                {
                                    pe.BBoxLength = union.Length;
                                    pe.BBoxWidth = union.Width;
                                    pe.BBoxHeight = union.Height;
                                    pe.BoundingBox_M = new DomainPoint3D
                                    {
                                        X = union.Length,
                                        Y = union.Width,
                                        Z = union.Height
                                    };
                                }
                            }
                        }
                    }
                    // Revit single-child assembly volume inheritance
                    if (!(pe.VolumeM3 is > 0)
                        && isRevitAssemblyChild
                        && (parentPreview?.ChildGlobalIds?.Count ?? 0) == 1
                        && parentSnapshot?.Volume is > 0)
                    {
                        pe.VolumeM3 = parentSnapshot.Volume;
                        pe.ElementVolume = parentSnapshot.Volume;

                        pe.ExtractionWarnings.Add(
                            $"Volume inherited from Revit single-child parent assembly. ParentGlobalId={preview.ParentGlobalId}, VolumeM3={pe.VolumeM3:F3}");
                    }
                    // Trusted density fallback after material/volume inheritance
                    if (!(pe.WeightTons is > 0)
                        && !(pe.ElementWT is > 0)
                        && pe.VolumeM3 is > 0
                        && IsValidMaterialName(pe.ElementMaterial))
                    {
                        var estimatedWeightTons = EstimateWeightFromMaterial(pe.ElementMaterial, pe.VolumeM3.Value);

                        if (estimatedWeightTons is > 0)
                        {
                            pe.WeightTons = estimatedWeightTons;
                            pe.ElementWT = estimatedWeightTons;
                            pe.WeightSource = isRevitAssemblyChild
                                ? "InheritedParentMaterialDensityFallback"
                                : "MaterialDensityFallback";

                            pe.WeightTrustLevel = IfcWeightTrustLevel.EstimatedFallback_NotAllowedForGA;

                            pe.ExtractionWarnings.Add(
                                $"Weight estimated from volume × material density. Material='{pe.ElementMaterial}', VolumeM3={pe.VolumeM3:F3}, WeightTons={estimatedWeightTons:F3}");
                        }
                    }
                    // Re-enable valid Revit liftable children after inheritance/fallback repair
                    if (pe.Role == IfcElementRole.LiftableElement
    && pe.WeightTons is > 0
    && pe.ElementWT is > 0
    && pe.VolumeM3 is > 0
    && IsValidMaterialName(pe.ElementMaterial))
                    {
                        if (pe.WeightTrustLevel == IfcWeightTrustLevel.EstimatedFallback_NotAllowedForGA)
                        {
                            pe.IncludeInGa = false;
                            if (!pe.NormalizationDecisionReason.Contains("Estimated weight (requires user approval for GA)", StringComparison.OrdinalIgnoreCase))
                            {
                                pe.NormalizationDecisionReason +=
                                    " | Repaired with estimated fallback weight (requires user approval for GA).";
                            }
                        }
                        else
                        {
                            pe.IncludeInGa = true;
                            pe.NormalizationDecisionReason +=
                                " | Re-enabled after material/volume/weight repair.";
                        }
                    }
                    // Make sure standardized units are enforced (everything scaled to meters, tons, m3)
                    // If a NeedsReview parent/child exists, block it from GA
                    if (preview.Role == IfcElementRole.NeedsReview)
                    {
                        pe.IncludeInGa = false;
                    }
                    if (pe.ElementMark?.Contains("Hollow", StringComparison.OrdinalIgnoreCase) == true
    || pe.ElementType?.Contains("Slab", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        Debug.WriteLine(
                            $"[IFC_REPAIR] Id={pe.ElementID}, Role={pe.Role}, Parent={pe.ParentGlobalId}, " +
                            $"IsAssemblyChild={isRevitAssemblyChild}, Material='{pe.ElementMaterial}', " +
                            $"VolumeM3={pe.VolumeM3}, WeightTons={pe.WeightTons}, ElementWT={pe.ElementWT}, " +
                            $"IncludeInGa={pe.IncludeInGa}, Source='{pe.WeightSource}', Reason='{pe.NormalizationDecisionReason}'");
                    }
                    result.Add(pe);
                }

                // Propagate materials for concrete elements of the same family/type
                PropagateConcreteMaterials(result);
                // Recompute weight and GA eligibility after material propagation.
                // This is important because material may be filled AFTER the first weight fallback.
                RepairWeightsAndGaAfterMaterialPropagation(result);

                // Second pass validation for parent-child double counting risk
                var elementsMap = result.ToDictionary(e => e.ElementID, StringComparer.Ordinal);
                foreach (var pe in result)
                {
                    if (!string.IsNullOrWhiteSpace(pe.ParentGlobalId) && elementsMap.TryGetValue(pe.ParentGlobalId, out var parentElement))
                    {
                        if (parentElement.IncludeInGa && pe.IncludeInGa &&
                            (parentElement.Role == IfcElementRole.LiftAssembly || parentElement.Role == IfcElementRole.LiftableElement))
                        {
                            var parentWeightTrusted = parentElement.WeightTrustLevel == IfcWeightTrustLevel.ExplicitQuantity ||
                                                      parentElement.WeightTrustLevel == IfcWeightTrustLevel.ExplicitProperty;
                            if (parentWeightTrusted)
                            {
                                pe.ValidationWarnings.Add($"ParentChildDoubleCountRisk: Both parent assembly '{pe.ParentGlobalId}' and child part '{pe.ElementID}' are marked for GA. Child will be dynamically excluded to prevent double-counting.");
                                pe.ValidationStatus = ValidationStatus.Warning;
                            }
                        }
                    }
                }

                foreach (var pe in result.Where(e =>
             e.ElementMark?.Contains("Hollow", StringComparison.OrdinalIgnoreCase) == true
          || e.ElementType?.Contains("Slab", StringComparison.OrdinalIgnoreCase) == true
          || e.ElementType?.Contains("Proxy", StringComparison.OrdinalIgnoreCase) == true))
                {
                    Debug.WriteLine(
                        $"[IFC_REPAIR_FINAL] Id={pe.ElementID}, Mark='{pe.ElementMark}', Type='{pe.ElementType}', Role={pe.Role}, " +
                        $"Material='{pe.ElementMaterial}', VolumeM3={pe.VolumeM3}, ElementVolume={pe.ElementVolume}, " +
                        $"BBox=({pe.BBoxLength} x {pe.BBoxWidth} x {pe.BBoxHeight}), " +
                        $"WeightTons={pe.WeightTons}, ElementWT={pe.ElementWT}, IncludeInGa={pe.IncludeInGa}, " +
                        $"Source='{pe.WeightSource}', Trust={pe.WeightTrustLevel}");
                }

                // Log a comprehensive diagnostic summary via Debug.WriteLine
                Debug.WriteLine("=== Phase 2A Normalization Element Extraction Diagnostics ===");
                Debug.WriteLine($"Total Normalized Elements Extracted: {result.Count}");
                Debug.WriteLine($"GA-Ready Elements: {result.Count(r => r.IncludeInGa)}");
                Debug.WriteLine($"GA-Blocked (Hiding/Auxiliary/NeedsReview/Excluded): {result.Count(r => !r.IncludeInGa)}");
                Debug.WriteLine($"IncludeInRender Elements: {result.Count(r => r.IncludeInRender)}");
                Debug.WriteLine($"ExcludeInRender Elements: {result.Count(r => !r.IncludeInRender)}");
                Debug.WriteLine($"Explicit Weights (Quantity/Property): {result.Count(r => r.WeightTrustLevel == IfcWeightTrustLevel.ExplicitQuantity || r.WeightTrustLevel == IfcWeightTrustLevel.ExplicitProperty)}");
                Debug.WriteLine($"Zero/Fallback/Missing Weights: {result.Count(r => r.WeightTrustLevel != IfcWeightTrustLevel.ExplicitQuantity && r.WeightTrustLevel != IfcWeightTrustLevel.ExplicitProperty)}");
                if (unresolvedAssemblyMarkCount > 0)
                {
                    Debug.WriteLine($"[IFC_WARN] {unresolvedAssemblyMarkCount} assemblies did not have a resolved explicit assembly-level mark property.");
                }
                Debug.WriteLine("=============================================================");

                progress?.Report(100d);
                return result;
            }

            private static PrecastElement ToPrecastElement(IEngineeringSnapshot snapshot)
            {
                return new PrecastElement
                {
                    ElementID = snapshot.ElementId,
                    ElementRevitId = snapshot.RevitId,
                    ElementMark = snapshot.Mark,
                    ElementName = snapshot.Name,
                    ElementType = snapshot.ElementType,
                    ElementLength = snapshot.ElementLength,
                    ElementWidth = snapshot.ElementWidth,
                    ElementHeight = snapshot.ElementHeight,
                    ElementMaterial = snapshot.Material ?? string.Empty,

                    ElementWT = snapshot.Weight,
                    WeightTons = snapshot.Weight,

                    ElementVolume = snapshot.Volume,
                    VolumeM3 = snapshot.Volume,

                    ElementCOG = snapshot.CenterOfGravity,
                    COG_M = snapshot.CenterOfGravity,

                    BBoxLength = snapshot.Geometry?.Length,
                    BBoxWidth = snapshot.Geometry?.Width,
                    BBoxHeight = snapshot.Geometry?.Height,

                    BoundingBox_M = new DomainPoint3D
                    {
                        X = snapshot.Geometry?.Length ?? 0,
                        Y = snapshot.Geometry?.Width ?? 0,
                        Z = snapshot.Geometry?.Height ?? 0
                    },

                    // Canonical Assembly Schema (Phase 2)
                    AssemblyMark = snapshot.Mark,
                    AssemblyMarkSource = "Snapshot",
                    AssemblyMarkConfidence = AssemblyMarkConfidence.High,
                    AssemblyMarkIsExplicit = true,
                    PhysicalCOGAvailable = snapshot.Geometry != null,
                    SourceSystem = SourceSystem.IFC,
                    ExporterProfile = IfcExporterProfile.Generic,
                    ValidationStatus = ValidationStatus.Valid
                };
            }

            private static void PropagateConcreteMaterials(List<PrecastElement> elements)
            {
                var familyGroups = elements.GroupBy(GetFamilyTypeName, StringComparer.OrdinalIgnoreCase);

                foreach (var group in familyGroups)
                {
                    var representativeMaterial = group
                        .Select(e => e.ElementMaterial)
                        .Where(IsValidMaterialName)
                        .GroupBy(m => m, StringComparer.OrdinalIgnoreCase)
                        .OrderByDescending(g => g.Count())
                        .Select(g => g.Key)
                        .FirstOrDefault();

                    if (string.IsNullOrWhiteSpace(representativeMaterial))
                        continue;

                    foreach (var e in group)
                    {
                        if (!IsValidMaterialName(e.ElementMaterial) && IsConcreteElement(e))
                        {
                            e.ElementMaterial = representativeMaterial;
                        }
                    }
                }
            }
            private void RepairWeightsAndGaAfterMaterialPropagation(List<PrecastElement> elements)
            {
                foreach (var pe in elements)
                {
                    // 1) Synchronize existing weight fields first.
                    if (!(pe.WeightTons is > 0) && pe.ElementWT is > 0)
                        pe.WeightTons = pe.ElementWT;

                    if (!(pe.ElementWT is > 0) && pe.WeightTons is > 0)
                        pe.ElementWT = pe.WeightTons;

                    // 2) Synchronize existing volume fields.
                    if (!(pe.VolumeM3 is > 0) && pe.ElementVolume is > 0)
                        pe.VolumeM3 = pe.ElementVolume;

                    if (!(pe.ElementVolume is > 0) && pe.VolumeM3 is > 0)
                        pe.ElementVolume = pe.VolumeM3;

                    // 3) Safe concrete material fallback for common precast/concrete names.
                    if (!IsValidMaterialName(pe.ElementMaterial) && IsConcreteElement(pe))
                    {
                        var nameSource =
                            $"{pe.ElementMark} {pe.ElementType} {pe.ResolvedCategory} {pe.RawIfcType}"
                                .ToUpperInvariant();

                        if (nameSource.Contains("HOLLOW")
                            || nameSource.Contains("PRECAST")
                            || nameSource.Contains("CONCRETE")
                            || nameSource.Contains("SLAB")
                            || nameSource.Contains("WALL")
                            || nameSource.Contains("COLUMN")
                            || nameSource.Contains("BEAM"))
                        {
                            pe.ElementMaterial = "Precast Concrete";

                            pe.ExtractionWarnings.Add(
                                "Material defaulted to Precast Concrete from concrete-like element naming/category.");
                        }
                    }

                    // 4) Preferred repair for Tekla IFC2x3:
                    // if weight exists but volume is missing, derive volume from weight / density.
                    if (!(pe.VolumeM3 is > 0)
                        && pe.WeightTons is > 0
                        && IsValidMaterialName(pe.ElementMaterial))
                    {
                        var estimatedVolume = EstimateVolumeFromWeightAndMaterial(
                            pe.WeightTons,
                            pe.ElementMaterial);

                        if (estimatedVolume is > 0)
                        {
                            pe.VolumeM3 = estimatedVolume;
                            pe.ElementVolume = estimatedVolume;

                            pe.ExtractionWarnings.Add(
                                $"Volume estimated from weight ÷ material density. " +
                                $"Material='{pe.ElementMaterial}', WeightTons={pe.WeightTons:F3}, VolumeM3={estimatedVolume:F3}");
                        }
                    }

                    // 5) Last-resort fallback:
                    // if no weight-derived volume exists, estimate from bounding box.
                    if (!(pe.VolumeM3 is > 0)
                        && pe.BBoxLength is > 0
                        && pe.BBoxWidth is > 0
                        && pe.BBoxHeight is > 0)
                    {
                        var bboxVolume =
                            pe.BBoxLength.Value *
                            pe.BBoxWidth.Value *
                            pe.BBoxHeight.Value;

                        if (bboxVolume > 0)
                        {
                            pe.VolumeM3 = bboxVolume;
                            pe.ElementVolume = bboxVolume;

                            pe.ExtractionWarnings.Add(
                                $"Volume repaired from PrecastElement bounding box. BBoxVolumeM3={bboxVolume:F3}");
                        }
                    }

                    // 6) If volume exists but weight is missing, derive weight from volume × density.
                    if (!(pe.WeightTons is > 0)
                        && pe.VolumeM3 is > 0
                        && IsValidMaterialName(pe.ElementMaterial))
                    {
                        var estimatedWeightTons =
                            EstimateWeightFromMaterial(pe.ElementMaterial, pe.VolumeM3.Value);

                        if (estimatedWeightTons is > 0)
                        {
                            pe.WeightTons = estimatedWeightTons;
                            pe.ElementWT = estimatedWeightTons;
                            pe.WeightSource = "PostPropagationMaterialDensityFallback";
                            pe.WeightTrustLevel = IfcWeightTrustLevel.EstimatedFallback_NotAllowedForGA;

                            pe.ExtractionWarnings.Add(
                                $"Weight repaired after material/volume repair. " +
                                $"Material='{pe.ElementMaterial}', VolumeM3={pe.VolumeM3:F3}, WeightTons={estimatedWeightTons:F3}");
                        }
                    }

                    // 7) Final field sync for UI + GA.
                    if (pe.WeightTons is > 0)
                        pe.ElementWT = pe.WeightTons;

                    if (pe.VolumeM3 is > 0)
                        pe.ElementVolume = pe.VolumeM3;

                    // 8) Final GA enablement only if real usable data exists.
                    if (pe.Role == IfcElementRole.LiftableElement
                        && pe.WeightTons is > 0
                        && pe.ElementWT is > 0
                        && pe.VolumeM3 is > 0
                        && IsValidMaterialName(pe.ElementMaterial))
                    {
                        if (pe.WeightTrustLevel == IfcWeightTrustLevel.EstimatedFallback_NotAllowedForGA)
                        {
                            pe.IncludeInGa = false;
                            if (!pe.NormalizationDecisionReason.Contains("Estimated weight (requires user approval for GA)", StringComparison.OrdinalIgnoreCase))
                            {
                                pe.NormalizationDecisionReason +=
                                    " | Estimated weight (requires user approval for GA).";
                            }
                        }
                        else
                        {
                            pe.IncludeInGa = true;

                            if (!pe.NormalizationDecisionReason.Contains("Final repaired", StringComparison.OrdinalIgnoreCase))
                            {
                                pe.NormalizationDecisionReason +=
                                    " | Final repaired: material, volume, and weight are valid.";
                            }
                        }
                    }

                    if (pe.Role == IfcElementRole.NeedsReview)
                        pe.IncludeInGa = false;
                }
            }
            private static string GetFamilyTypeName(PrecastElement e)
            {
                var mark = e.ElementMark ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(e.ElementRevitId))
                {
                    var suffix = ":" + e.ElementRevitId;
                    if (mark.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        return mark.Substring(0, mark.Length - suffix.Length);
                    }
                    var suffix2 = "-" + e.ElementRevitId;
                    if (mark.EndsWith(suffix2, StringComparison.OrdinalIgnoreCase))
                    {
                        return mark.Substring(0, mark.Length - suffix2.Length);
                    }
                    if (mark.EndsWith(e.ElementRevitId, StringComparison.OrdinalIgnoreCase))
                    {
                        return mark.Substring(0, mark.Length - e.ElementRevitId.Length).TrimEnd(':', '-', ' ');
                    }
                }
                return mark;
            }

            private static bool IsConcreteElement(PrecastElement e)
            {
                var source = $"{(e.ElementMaterial ?? string.Empty)} {(e.ElementType ?? string.Empty)} {(e.ElementMark ?? string.Empty)}".ToUpperInvariant();
                if (source.ContainsAny("STEEL", "REBAR", "ALUMIN", "GLASS", "TIMBER", "WOOD"))
                    return false;
                return true;
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
            string? Name { get; }
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
            string? Mark,
            string? Name) : IEngineeringSnapshot
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
            string? Mark,
            string? Name) : IEngineeringSnapshot
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
                    parent.Mark,
                    parent.Name);
        }

        private sealed record GeometrySnapshot(
    double MinX, double MinY, double MinZ,
    double MaxX, double MaxY, double MaxZ,
    DomainPoint3D Center,
    double? MeshVolumeM3)
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
                return new GeometrySnapshot(minX, minY, minZ, maxX, maxY, maxZ, center, null);
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
     center,
     null);
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
