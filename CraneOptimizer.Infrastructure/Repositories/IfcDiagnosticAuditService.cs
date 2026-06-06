using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Infrastructure.Ifc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xbim.Common;
using Xbim.Common.Geometry;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.ModelGeometry.Scene;

namespace CraneOptimizer.Infrastructure.Repositories
{
    public sealed class IfcDiagnosticAuditService : IIfcDiagnosticAuditService
    {
        private const double DefaultLengthToMetersFactor = 1.0;
        private const double ConcreteDensityKgPerCubicMeter = 2500.0;
        private const double TonToKgFactor = 1000.0;
        private readonly IIfcModelSessionService? _sessionService;
        private readonly object _auditCacheGate = new();
        private Guid? _lastAuditSessionId;
        private string? _lastAuditFileIdentity;
        private double _lastAuditConcreteDensityKgPerM3 = ConcreteDensityKgPerCubicMeter;
        private IfcAuditResult? _lastAuditResult;

        public IfcDiagnosticAuditService()
        {
        }

        public IfcDiagnosticAuditService(IIfcModelSessionService sessionService)
        {
            _sessionService = sessionService;
        }

        public Task<IfcAuditResult> RunAuditAsync(string ifcPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            return RunAuditAsync(ifcPath, null, progress, cancellationToken);
        }

        public Task<IfcAuditResult> RunAuditAsync(
            string ifcPath,
            IfcReadingOptions? options,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() => RunAudit(ifcPath, options, progress, cancellationToken), cancellationToken);
        }

        public Task<string> GenerateReportsAsync(IfcAuditResult auditResult, string? projectFolderPath = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => GenerateReportsCore(auditResult, projectFolderPath, cancellationToken), cancellationToken);
        }

        private IfcAuditResult RunAudit(
            string ifcPath,
            IfcReadingOptions? options,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            var concreteDensityKgPerM3 = ResolveConcreteDensity(options);
            var session = _sessionService?.GetActiveSession(ifcPath);
            if (session != null)
            {
                lock (_auditCacheGate)
                {
                    if (_lastAuditResult != null
                        && _lastAuditSessionId == session.SessionId
                        && string.Equals(_lastAuditFileIdentity, session.FileIdentity, StringComparison.Ordinal)
                        && Math.Abs(_lastAuditConcreteDensityKgPerM3 - concreteDensityKgPerM3) < 0.000001)
                    {
                        Debug.WriteLine($"[IfcDiagnosticAudit] Reusing cached audit for session {session.SessionId}.");
                        progress?.Report(100.0);
                        return _lastAuditResult;
                    }
                }

                var sessionAuditTimer = Stopwatch.StartNew();
                var result = RunAuditCore(session, concreteDensityKgPerM3, progress, cancellationToken);
                sessionAuditTimer.Stop();

                lock (_auditCacheGate)
                {
                    _lastAuditSessionId = session.SessionId;
                    _lastAuditFileIdentity = session.FileIdentity;
                    _lastAuditConcreteDensityKgPerM3 = concreteDensityKgPerM3;
                    _lastAuditResult = result;
                }

                Debug.WriteLine(
                    $"[IfcDiagnosticAudit] Session audit completed. SessionId={session.SessionId}, " +
                    $"Duration={sessionAuditTimer.ElapsedMilliseconds} ms, ReopenedIfc=False, CreatedContext=False");

                return result;
            }

            return RunAuditCore(ifcPath, concreteDensityKgPerM3, progress, cancellationToken);
        }

        private IfcAuditResult RunAuditCore(
            IIfcModelSession session,
            double concreteDensityKgPerM3,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            var result = new IfcAuditResult();

            try
            {
                progress?.Report(15.0);
                PopulateAuditResult(result, session.Model, session.GeometryContext, session.IfcPath, concreteDensityKgPerM3, progress, cancellationToken);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IfcDiagnosticAudit] Error running diagnostic audit on active session: {ex.Message}");
                result.Metadata.FileName = Path.GetFileName(session.IfcPath);
                result.Metadata.Warnings.Add($"Audit execution failed: {ex.Message}");
            }

            return result;
        }

        private IfcAuditResult RunAuditCore(
            string ifcPath,
            double concreteDensityKgPerM3,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            var result = new IfcAuditResult();
            IfcStore? model = null;

            try
            {
                progress?.Report(5.0);
                var openTimer = Stopwatch.StartNew();
                model = IfcStore.Open(ifcPath);
                openTimer.Stop();
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(25.0);
                Xbim3DModelContext? geometryContext = null;
                var contextTimer = Stopwatch.StartNew();
                try
                {
                    _ = new Xbim.Geometry.Engine.Interop.XbimGeometryEngine();
                    geometryContext = new Xbim3DModelContext(model);
                    geometryContext.CreateContext();
                }
                catch (Exception geomEx)
                {
                    result.Metadata.Warnings.Add($"Native geometry engine initialization bypassed/failed: {geomEx.Message}");
                    Debug.WriteLine($"[IfcDiagnosticAudit] Native geometry engine initialization failed: {geomEx.Message}");
                }
                contextTimer.Stop();
                cancellationToken.ThrowIfCancellationRequested();

                PopulateAuditResult(result, model, geometryContext, ifcPath, concreteDensityKgPerM3, progress, cancellationToken);

                Debug.WriteLine(
                    $"[IfcDiagnosticAudit] Standalone audit completed. Open={openTimer.ElapsedMilliseconds} ms, " +
                    $"CreateContext={contextTimer.ElapsedMilliseconds} ms, ReopenedIfc=True");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IfcDiagnosticAudit] Error running diagnostic audit: {ex.Message}");
                // Ensure we return at least metadata with a major error warning if the file fails to open
                result.Metadata.FileName = Path.GetFileName(ifcPath);
                result.Metadata.Warnings.Add($"Audit execution failed: {ex.Message}");
            }
            finally
            {
                if (model != null)
                {
                    try
                    {
                        model.Close();
                        model.Dispose();
                    }
                    catch { }
                }
            }

            return result;
        }

        private static void PopulateAuditResult(
            IfcAuditResult result,
            IfcStore model,
            Xbim3DModelContext geometryContext,
            string ifcPath,
            double concreteDensityKgPerM3,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            progress?.Report(15.0);
            result.Metadata = ExtractMetadata(model, ifcPath);
            cancellationToken.ThrowIfCancellationRequested();

            var allPhysicalProducts = model.Instances
                .OfType<IIfcProduct>()
                .Where(IsPhysicalProduct)
                .ToList();

            progress?.Report(40.0);
            result.RawTypes = BuildRawTypeSummary(allPhysicalProducts, geometryContext);
            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(60.0);
            result.Assemblies = BuildAssemblyDecompositionAudit(model, allPhysicalProducts, geometryContext, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(80.0);
            ProcessPhysicalElements(allPhysicalProducts, geometryContext, result, result.Metadata.LengthConversionFactor, result.Metadata.VolumeConversionFactor, concreteDensityKgPerM3);
            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(100.0);
        }

        private static bool IsPhysicalProduct(IIfcProduct product)
        {
            if (product is IIfcOpeningElement or IIfcSpace or IIfcVirtualElement)
                return false;

            return product is IIfcElement;
        }

        private static IfcAuditMetadata ExtractMetadata(IfcStore model, string ifcPath)
        {
            var meta = new IfcAuditMetadata
            {
                FileName = Path.GetFileName(ifcPath),
                SchemaVersion = model.SchemaVersion.ToString(),
                ExporterApplication = model.Header?.FileName?.OriginatingSystem ?? "Unknown Exporter"
            };

            // Detect Project Units
            var project = model.Instances.OfType<IIfcProject>().FirstOrDefault();
            var units = project?.UnitsInContext?.Units;

            if (units == null)
            {
                meta.Warnings.Add("No Unit Assignment found in IIfcProject context. Assuming SI standard meters.");
                return meta;
            }

            bool foundLength = false;
            bool foundArea = false;
            bool foundVolume = false;
            bool foundMass = false;

            foreach (var unit in units)
            {
                if (unit is IIfcNamedUnit namedUnit)
                {
                    var unitTypeName = namedUnit.UnitType.ToString();

                    if (unitTypeName.Contains("LENGTH", StringComparison.OrdinalIgnoreCase))
                    {
                        foundLength = true;
                        if (unit is IIfcSIUnit siUnit)
                        {
                            meta.LengthUnit = (siUnit.Prefix?.ToString() ?? string.Empty) + siUnit.Name.ToString();
                            meta.LengthConversionFactor = GetSiUnitFactor(siUnit);
                        }
                        else if (unit is IIfcConversionBasedUnit convUnit)
                        {
                            meta.LengthUnit = convUnit.Name.ToString();
                            meta.LengthConversionFactor = GetConversionUnitFactor(convUnit);
                        }
                    }
                    else if (unitTypeName.Contains("AREA", StringComparison.OrdinalIgnoreCase))
                    {
                        foundArea = true;
                        if (unit is IIfcSIUnit siUnit)
                        {
                            meta.AreaUnit = (siUnit.Prefix?.ToString() ?? string.Empty) + siUnit.Name.ToString();
                            meta.AreaConversionFactor = Math.Pow(GetSiUnitFactor(siUnit), 2);
                        }
                        else if (unit is IIfcConversionBasedUnit convUnit)
                        {
                            meta.AreaUnit = convUnit.Name.ToString();
                            meta.AreaConversionFactor = GetConversionUnitFactor(convUnit);
                        }
                    }
                    else if (unitTypeName.Contains("VOLUME", StringComparison.OrdinalIgnoreCase))
                    {
                        foundVolume = true;
                        if (unit is IIfcSIUnit siUnit)
                        {
                            meta.VolumeUnit = (siUnit.Prefix?.ToString() ?? string.Empty) + siUnit.Name.ToString();
                            meta.VolumeConversionFactor = Math.Pow(GetSiUnitFactor(siUnit), 3);
                        }
                        else if (unit is IIfcConversionBasedUnit convUnit)
                        {
                            meta.VolumeUnit = convUnit.Name.ToString();
                            meta.VolumeConversionFactor = GetConversionUnitFactor(convUnit);
                        }
                    }
                    else if (unitTypeName.Contains("MASS", StringComparison.OrdinalIgnoreCase) ||
                             unitTypeName.Contains("WEIGHT", StringComparison.OrdinalIgnoreCase))
                    {
                        foundMass = true;
                        if (unit is IIfcSIUnit siUnit)
                        {
                            meta.MassUnit = (siUnit.Prefix?.ToString() ?? string.Empty) + siUnit.Name.ToString();
                            meta.MassConversionFactor = GetSiUnitFactor(siUnit);
                        }
                        else if (unit is IIfcConversionBasedUnit convUnit)
                        {
                            meta.MassUnit = convUnit.Name.ToString();
                            meta.MassConversionFactor = GetConversionUnitFactor(convUnit);
                        }
                    }
                }
            }

            if (!foundLength) meta.Warnings.Add("Length unit not declared in project context.");
            if (!foundArea) meta.Warnings.Add("Area unit not declared in project context.");
            if (!foundVolume) meta.Warnings.Add("Volume unit not declared in project context.");
            if (!foundMass) meta.Warnings.Add("Mass/Weight unit not explicitly declared. Exporters often default to Kilograms.");

            return meta;
        }

        private static double GetSiUnitFactor(IIfcSIUnit siUnit)
        {
            var name = siUnit.Name.ToString();
            var prefix = siUnit.Prefix?.ToString() ?? string.Empty;

            // KILOGRAM to kilograms must be 1, not 1000
            if (name.Equals("GRAM", StringComparison.OrdinalIgnoreCase) || 
                name.Equals("KILOGRAM", StringComparison.OrdinalIgnoreCase))
            {
                if (prefix.Contains("KILO", StringComparison.OrdinalIgnoreCase) || 
                    name.Equals("KILOGRAM", StringComparison.OrdinalIgnoreCase))
                {
                    return 1.0;
                }
                if (string.IsNullOrEmpty(prefix))
                {
                    return 0.001; // gram to kg
                }
                if (prefix.Contains("MILLI", StringComparison.OrdinalIgnoreCase))
                {
                    return 0.000001; // milligram to kg
                }
            }

            if (prefix.Contains("MILLI", StringComparison.OrdinalIgnoreCase)) return 0.001;
            if (prefix.Contains("CENTI", StringComparison.OrdinalIgnoreCase)) return 0.01;
            if (prefix.Contains("DECI", StringComparison.OrdinalIgnoreCase)) return 0.1;
            if (prefix.Contains("KILO", StringComparison.OrdinalIgnoreCase)) return 1000.0;
            if (prefix.Contains("MEGA", StringComparison.OrdinalIgnoreCase)) return 1000000.0;

            return 1.0;
        }

        private static double GetConversionUnitFactor(IIfcConversionBasedUnit conversionUnit)
        {
            var name = conversionUnit.Name.ToString();

            if (name.Contains("MILLIMET", StringComparison.OrdinalIgnoreCase)) return 0.001;
            if (name.Contains("CENTIMET", StringComparison.OrdinalIgnoreCase)) return 0.01;
            
            // Check composite foot dimensions before standard foot to avoid partial matching
            if (name.Contains("SQUARE FOOT", StringComparison.OrdinalIgnoreCase) || 
                name.Contains("SQUARE FEET", StringComparison.OrdinalIgnoreCase) || 
                name.Contains("SQ FT", StringComparison.OrdinalIgnoreCase)) 
                return 0.3048 * 0.3048;

            if (name.Contains("CUBIC FOOT", StringComparison.OrdinalIgnoreCase) || 
                name.Contains("CUBIC FEET", StringComparison.OrdinalIgnoreCase) || 
                name.Contains("CU FT", StringComparison.OrdinalIgnoreCase)) 
                return 0.3048 * 0.3048 * 0.3048;

            if (name.Contains("FOOT", StringComparison.OrdinalIgnoreCase) || 
                name.Contains("FEET", StringComparison.OrdinalIgnoreCase)) 
                return 0.3048;

            if (name.Contains("INCH", StringComparison.OrdinalIgnoreCase)) return 0.0254;

            if (name.Contains("TON", StringComparison.OrdinalIgnoreCase) || 
                name.Contains("TONNE", StringComparison.OrdinalIgnoreCase)) 
                return TonToKgFactor;

            var valComp = conversionUnit.ConversionFactor?.ValueComponent?.ToString();
            if (double.TryParse(valComp, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedFactor))
            {
                var baseFactor = 1.0;
                if (conversionUnit.ConversionFactor?.UnitComponent is IIfcSIUnit baseSi)
                {
                    baseFactor = GetSiUnitFactor(baseSi);
                }
                return parsedFactor * baseFactor;
            }

            return 1.0;
        }

        private static List<RawIfcTypeSummary> BuildRawTypeSummary(List<IIfcProduct> physicalProducts, Xbim3DModelContext geometryContext)
        {
            var grouped = physicalProducts
                .GroupBy(p => p.ExpressType?.Name ?? p.GetType().Name)
                .Select(g =>
                {
                    var items = g.ToList();
                    return new RawIfcTypeSummary
                    {
                        RawIfcClass = g.Key,
                        TotalCount = items.Count,
                        CountWithGlobalId = items.Count(p => !string.IsNullOrEmpty(p.GlobalId)),
                        CountWithName = items.Count(p => !string.IsNullOrEmpty(p.Name?.ToString())),
                        CountWithObjectPlacement = items.Count(p => p.ObjectPlacement != null),
                        CountWithShapeInstances = items.Count(p => {
                            try { return geometryContext.ShapeInstancesOf(p).Any(); } catch { return false; }
                        }),
                        CountWithQuantitySets = items.Count(p => p.IsDefinedBy.Any(r => r.RelatingPropertyDefinition is IIfcElementQuantity)),
                        CountWithPropertySets = items.Count(p => p.IsDefinedBy.Any(r => r.RelatingPropertyDefinition is IIfcPropertySet)),
                        CountWithMaterialAssociation = items.Count(p => p.HasAssociations.OfType<IIfcRelAssociatesMaterial>().Any())
                    };
                })
                .OrderByDescending(s => s.TotalCount)
                .ToList();

            return grouped;
        }

        private static AssemblyDecompositionAudit BuildAssemblyDecompositionAudit(
            IfcStore model,
            List<IIfcProduct> physicalProducts,
            Xbim3DModelContext geometryContext,
            CancellationToken cancellationToken)
        {
            var audit = new AssemblyDecompositionAudit();
            var assemblies = model.Instances.OfType<IIfcElementAssembly>().ToList();
            audit.TotalAssemblyCount = assemblies.Count;

            var parentLabels = new HashSet<int>();
            var consumedLabels = new HashSet<int>();

            foreach (var assembly in assemblies)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var detail = new AssemblyDecompositionDetail
                {
                    AssemblyGlobalId = assembly.GlobalId.ToString(),
                    AssemblyName = assembly.Name?.ToString() ?? "-",
                    NestingRelationshipType = "IfcRelAggregates"
                };

                // Find aggregated children
                var childProducts = assembly.IsDecomposedBy?
                    .SelectMany(rel => rel.RelatedObjects)
                    .OfType<IIfcProduct>()
                    .Where(IsPhysicalProduct)
                    .ToList() ?? new List<IIfcProduct>();

                detail.DirectChildCount = childProducts.Count;

                var recursiveChildren = new List<IIfcProduct>();
                string relType = "IfcRelAggregates";
                int maxDepth = GetMaxNestingDepth(assembly, 1, recursiveChildren, ref relType);
                detail.NestingRelationshipType = relType;
                detail.RecursiveChildCount = recursiveChildren.Count;
                detail.NestingDepth = maxDepth - 1;

                if (detail.DirectChildCount > 0)
                {
                    audit.AssembliesWithChildren++;
                    audit.TotalDirectChildCount += detail.DirectChildCount;
                    audit.TotalRecursiveChildCount += detail.RecursiveChildCount;
                }
                else
                {
                    audit.AssembliesWithoutChildren++;
                }

                if (detail.NestingDepth > audit.MaxDecompositionDepth)
                {
                    audit.MaxDecompositionDepth = detail.NestingDepth;
                }

                // Check geometry ownership pattern
                detail.ParentHasGeometry = HasDirectGeometry(assembly, geometryContext);
                detail.ChildrenHaveGeometry = recursiveChildren.Any(c => HasDirectGeometry(c, geometryContext));

                if (detail.ParentHasGeometry && detail.ChildrenHaveGeometry)
                {
                    detail.GeometryOwnershipPattern = "Both";
                }
                else if (detail.ParentHasGeometry)
                {
                    detail.GeometryOwnershipPattern = "ParentOnly";
                }
                else if (detail.ChildrenHaveGeometry)
                {
                    detail.GeometryOwnershipPattern = "ChildrenOnly";
                }
                else
                {
                    detail.GeometryOwnershipPattern = "None";
                }

                // Details for child elements
                foreach (var child in childProducts)
                {
                    var childRawType = child.ExpressType?.Name ?? child.GetType().Name;
                    if (!audit.ChildRawIfcTypes.Contains(childRawType))
                    {
                        audit.ChildRawIfcTypes.Add(childRawType);
                    }

                    if (!detail.ChildTypes.Contains(childRawType))
                    {
                        detail.ChildTypes.Add(childRawType);
                    }

                    // A child has swallowed risk because under current pipeline it gets ignored as an individual liftable item
                    bool swallowed = true; 

                    // Double counting risk exists if the child has geometry but is also contained in a parent assembly that has geometry
                    bool doubleCounting = detail.ParentHasGeometry && HasDirectGeometry(child, geometryContext);

                    detail.ChildDetails.Add(new AssemblyChildDetail
                    {
                        ChildGlobalId = child.GlobalId.ToString(),
                        ChildRawIfcType = childRawType,
                        ChildName = child.Name?.ToString() ?? "-",
                        HasGeometry = HasDirectGeometry(child, geometryContext),
                        DoubleCountingRisk = doubleCounting,
                        SwallowedRisk = swallowed
                    });

                    if (doubleCounting) audit.DoubleCountingRisksCount++;
                    if (swallowed) audit.ChildrenSwallowedCount++;
                }

                audit.Details.Add(detail);
            }

            return audit;
        }

        private static int GetMaxNestingDepth(IIfcObjectDefinition obj, int currentDepth, List<IIfcProduct> collectedProducts, ref string relType)
        {
            var deeperAggs = obj.IsDecomposedBy?
                .SelectMany(rel => rel.RelatedObjects)
                .ToList() ?? new List<IIfcObjectDefinition>();

            var deeperNests = (obj as IIfcElement)?.IsNestedBy?
                .SelectMany(rel => rel.RelatedObjects)
                .ToList() ?? new List<IIfcObjectDefinition>();

            var combinedDecomp = deeperAggs.Concat(deeperNests).ToList();

            if (combinedDecomp.Count == 0)
            {
                return currentDepth;
            }

            if (deeperNests.Count > 0 && deeperAggs.Count > 0)
            {
                relType = "Mixed";
            }
            else if (deeperNests.Count > 0)
            {
                relType = "IfcRelNests";
            }

            var maxSubDepth = currentDepth;

            foreach (var child in combinedDecomp)
            {
                if (child is IIfcProduct prod && IsPhysicalProduct(prod))
                {
                    collectedProducts.Add(prod);
                }

                int depth = GetMaxNestingDepth(child, currentDepth + 1, collectedProducts, ref relType);
                if (depth > maxSubDepth)
                {
                    maxSubDepth = depth;
                }
            }

            return maxSubDepth;
        }

        private static bool HasDirectGeometry(IIfcProduct product, Xbim3DModelContext geometryContext)
        {
            try
            {
                return geometryContext.ShapeInstancesOf(product).Any();
            }
            catch
            {
                return false;
            }
        }

        private static void ProcessPhysicalElements(
            List<IIfcProduct> physicalProducts,
            Xbim3DModelContext geometryContext,
            IfcAuditResult auditResult,
            double lengthFactor,
            double volumeFactor,
            double concreteDensityKgPerM3)
        {
            var swallowedGlobalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var assemblyDetail in auditResult.Assemblies.Details)
            {
                foreach (var child in assemblyDetail.ChildDetails)
                {
                    swallowedGlobalIds.Add(child.ChildGlobalId);
                }
            }

            foreach (var product in physicalProducts)
            {
                var globalId = product.GlobalId.ToString();
                var rawTypeName = product.ExpressType?.Name ?? product.GetType().Name;
                var productName = product.Name?.ToString() ?? string.Empty;

                // 1. Classification Audit
                var resolvedCategory = ResolveCandidateCategory(rawTypeName, productName, product.ObjectType?.ToString());
                var classification = new ElementClassificationAuditDetail
                {
                    GlobalId = globalId,
                    EntityLabel = product.EntityLabel,
                    Name = productName,
                    RawIfcType = rawTypeName,
                    ExpressTypeName = rawTypeName,
                    ObjectType = product.ObjectType?.ToString() ?? string.Empty,
                    PredefinedType = GetPredefinedType(product),
                    CandidateResolvedCategory = resolvedCategory,
                    CandidateReason = GetCategoryReason(rawTypeName, resolvedCategory),
                    CurrentUiShowAsAssembly = swallowedGlobalIds.Contains(globalId) || (product is IIfcElementAssembly)
                };
                auditResult.Classifications.Add(classification);

                // 2. Weight Audit
                var weightDetail = AuditWeight(product, resolvedCategory, swallowedGlobalIds.Contains(globalId), concreteDensityKgPerM3);
                auditResult.Weights.Add(weightDetail);

                // 3. Volume and Material Audit
                var volMat = AuditVolumeMaterial(product, volumeFactor);
                auditResult.VolumesMaterials.Add(volMat);

                // 4. Geometry Ownership Audit
                var geomDetail = AuditGeometryOwnership(product, geometryContext, swallowedGlobalIds, auditResult.Assemblies.Details);
                auditResult.Geometries.Add(geomDetail);
            }
        }

        private static string ResolveCandidateCategory(string rawTypeName, string name, string? objectType)
        {
            var combined = $"{rawTypeName} {name} {objectType ?? string.Empty}".ToUpperInvariant();

            if (rawTypeName.Equals("IfcColumn", StringComparison.OrdinalIgnoreCase) || combined.ContainsAny("COLUMN", "POST", "PILLAR")) return "Column";
            if (rawTypeName.Equals("IfcBeam", StringComparison.OrdinalIgnoreCase) || combined.ContainsAny("BEAM", "GIRDER", "JOIST")) return "Beam";
            if (rawTypeName.Equals("IfcSlab", StringComparison.OrdinalIgnoreCase) || combined.ContainsAny("SLAB", "FLOOR", "PLATE", "DECK")) return "Slab";
            if (rawTypeName.Contains("Wall", StringComparison.OrdinalIgnoreCase)) return "Wall";
            if (rawTypeName.Equals("IfcRoof", StringComparison.OrdinalIgnoreCase) || combined.Contains("ROOF")) return "Roof";
            if (rawTypeName.Equals("IfcStair", StringComparison.OrdinalIgnoreCase) || combined.Contains("STAIR")) return "Stair";
            if (rawTypeName.Equals("IfcRamp", StringComparison.OrdinalIgnoreCase) || combined.Contains("RAMP")) return "Ramp";
            if (rawTypeName.Equals("IfcFooting", StringComparison.OrdinalIgnoreCase) || combined.ContainsAny("FOOTING", "FOUNDATION")) return "Footing";
            if (rawTypeName.Equals("IfcPile", StringComparison.OrdinalIgnoreCase) || combined.Contains("PILE")) return "Pile";
            if (rawTypeName.Equals("IfcPlate", StringComparison.OrdinalIgnoreCase) || combined.Contains("PLATE")) return "Plate";
            if (rawTypeName.Equals("IfcMember", StringComparison.OrdinalIgnoreCase) || combined.Contains("MEMBER")) return "Member";
            if (rawTypeName.Equals("IfcElementAssembly", StringComparison.OrdinalIgnoreCase)) return "Assembly";
            if (rawTypeName.Equals("IfcBuildingElementProxy", StringComparison.OrdinalIgnoreCase) || combined.Contains("PROXY")) return "Proxy";

            return "Unknown";
        }

        private static string GetCategoryReason(string rawTypeName, string resolved)
        {
            if (rawTypeName.Equals($"Ifc{resolved}", StringComparison.OrdinalIgnoreCase))
                return $"Direct schema mapping of class {rawTypeName}.";

            if (rawTypeName.Equals("IfcElementAssembly", StringComparison.OrdinalIgnoreCase) && resolved.Equals("Assembly", StringComparison.OrdinalIgnoreCase))
                return "Direct schema matching of assembly container.";

            return $"Heuristic text analysis matched keywords for target category: {resolved}.";
        }

        private static string GetPredefinedType(IIfcProduct product)
        {
            try
            {
                // Access PredefinedType using reflection since different IFC interfaces hold different PredefinedTypes
                var prop = product.GetType().GetProperty("PredefinedType");
                if (prop != null)
                {
                    var val = prop.GetValue(product);
                    return val?.ToString() ?? string.Empty;
                }
            }
            catch { }
            return string.Empty;
        }

        private static ElementWeightAuditDetail AuditWeight(
            IIfcProduct product,
            string resolvedType,
            bool isSwallowed,
            double concreteDensityKgPerM3)
        {
            var detail = new ElementWeightAuditDetail
            {
                GlobalId = product.GlobalId.ToString(),
                ResolvedType = resolvedType,
                IsWeightUsedByProductionCurrently = !isSwallowed && !(product is IIfcElementAssembly)
            };

            // Search Quantities
            var quantitySets = product.IsDefinedBy
                .Where(r => r.RelatingPropertyDefinition is IIfcElementQuantity)
                .Select(r => (IIfcElementQuantity)r.RelatingPropertyDefinition);

            foreach (var qSet in quantitySets)
            {
                foreach (var quantity in qSet.Quantities.OfType<IIfcQuantityWeight>())
                {
                    if (quantity.Name.ToString().ContainsAny("weight", "mass"))
                    {
                        detail.WeightRawValue = quantity.WeightValue;
                        detail.WeightPropertyName = quantity.Name.ToString();
                        detail.QuantitySetNameOrPropertySetName = qSet.Name.ToString();
                        detail.WeightSource = "IfcQuantityWeight.Instance";
                        detail.WeightUnitDetected = "kg"; // Standard IFC weight quantity is stored in kilograms
                        detail.WeightStandardValueTons = quantity.WeightValue / 1000.0;
                        break;
                    }
                }
                if (detail.WeightRawValue.HasValue) break;
            }

            // Search Properties if not found in quantities.
            // Revit often exports explicit user/shared parameters with names that do not
            // include the simple "weight" token. Keep this lookup explicit-only: volume
            // or density-derived values are still handled later as non-GA fallback.
            if (!detail.WeightRawValue.HasValue)
            {
                var propertySets = product.IsDefinedBy
                    .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet)
                    .Select(r => (IIfcPropertySet)r.RelatingPropertyDefinition);

                foreach (var pSet in propertySets)
                {
                    foreach (var prop in pSet.HasProperties.OfType<IIfcPropertySingleValue>())
                    {
                        var name = prop.Name.ToString();
                        if (IsExplicitWeightPropertyName(name, pSet.Name.ToString()))
                        {
                            if (TryReadNumericPropertyValue(prop, out var parsedVal))
                            {
                                detail.WeightRawValue = parsedVal;
                                detail.WeightPropertyName = name;
                                detail.QuantitySetNameOrPropertySetName = pSet.Name.ToString();
                                detail.WeightSource = IsLikelyRevitWeightProperty(name, pSet.Name.ToString())
                                    ? "RevitPropertySet.Instance"
                                    : "PropertySet.Instance";
                                detail.WeightUnitDetected = prop.Unit?.ToString() ?? "Unspecified";

                                // Convert to Standard Value (Tons) based on heuristics
                                var unitInfo = $"{name} {detail.WeightUnitDetected}".ToLowerInvariant();
                                if (unitInfo.ContainsAny("kg", "kilo"))
                                {
                                    detail.WeightStandardValueTons = parsedVal / 1000.0;
                                }
                                else if (unitInfo.ContainsAny("ton", "tonne"))
                                {
                                    detail.WeightStandardValueTons = parsedVal;
                                }
                                else if (unitInfo.ContainsAny("lb", "pound"))
                                {
                                    detail.WeightStandardValueTons = parsedVal * 0.00045359237;
                                }
                                else
                                {
                                    // Default heuristic used by existing Tekla extractor logic
                                    detail.WeightStandardValueTons = parsedVal > 100.0 ? parsedVal / 1000.0 : parsedVal;
                                    if (parsedVal > 100.0)
                                    {
                                        detail.WeightSource = $"{detail.WeightSource} (Heuristically Scaled)";
                                        detail.Warning = "Weight property value exceeded 100. Assumed kilograms and converted to metric tons.";
                                    }
                                }
                                break;
                            }
                        }
                    }
                    if (detail.WeightRawValue.HasValue) break;
                }
            }

            // Fallback estimation using existing concrete density logic in pipeline
            if (!detail.WeightRawValue.HasValue)
            {
                // Fetch direct volume if available
                double? volume = GetDirectVolume(product);
                if (volume.HasValue && volume.Value > 0)
                {
                    detail.WeightRawValue = volume.Value * concreteDensityKgPerM3;
                    detail.WeightStandardValueTons = (volume.Value * concreteDensityKgPerM3) / 1000.0;
                    detail.WeightSource = "ExistingDensityFallback";
                    detail.WeightStatus = "EstimatedByFallback";
                    detail.Warning = "Current pipeline estimated weight using density fallback. Verify with user because this is an engineering assumption.";
                }
                else
                {
                    detail.WeightStatus = "MissingWeight";
                    detail.WeightSource = "Missing";
                    detail.Warning = "No explicit weight found in IFC. Weight calculation is outside current project scope unless user provides material/density rules.";
                }
            }
            else if (detail.WeightRawValue.Value == 0.0)
            {
                detail.WeightStatus = "ExplicitZeroFromIFC";
                detail.WeightStandardValueTons = 0.0;
                detail.Warning = "The IFC explicitly provides zero weight. This may be valid or an exporter/data issue. The application must not silently replace it without user approval.";
            }
            else if (detail.WeightRawValue.Value < 0.0)
            {
                detail.WeightStatus = "InvalidNegative";
                detail.WeightSource = "InvalidNegative";
                detail.Warning = "Weight raw value in IFC is negative. This is an invalid physical state.";
            }
            else
            {
                detail.WeightStatus = "ExplicitPositive";
                if (detail.WeightStandardValueTons > 50.0)
                {
                    detail.WeightStatus = "SuspiciousExtreme";
                    detail.Warning = "This element has an extremely high calculated weight (> 50 Tons). Verify that units are not scaled incorrectly.";
                }
            }

            return detail;
        }

        private static bool TryReadNumericPropertyValue(IIfcPropertySingleValue property, out double value)
        {
            value = 0;

            var nominal = property.NominalValue;
            if (nominal == null)
                return false;

            object? rawValue = nominal;
            var valueProperty = nominal.GetType().GetProperty("Value");
            if (valueProperty != null)
            {
                rawValue = valueProperty.GetValue(nominal);
            }

            if (rawValue is double doubleValue)
            {
                value = doubleValue;
                return true;
            }

            if (rawValue is float floatValue)
            {
                value = floatValue;
                return true;
            }

            if (rawValue is decimal decimalValue)
            {
                value = (double)decimalValue;
                return true;
            }

            if (rawValue is int intValue)
            {
                value = intValue;
                return true;
            }

            if (rawValue is long longValue)
            {
                value = longValue;
                return true;
            }

            var valueText = Convert.ToString(rawValue, CultureInfo.InvariantCulture);
            if (double.TryParse(valueText, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                return true;

            valueText = Convert.ToString(nominal, CultureInfo.InvariantCulture);
            if (double.TryParse(valueText, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                return true;

            var numericMatch = Regex.Match(valueText ?? string.Empty, @"[-+]?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?");
            return numericMatch.Success &&
                   double.TryParse(numericMatch.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        private static bool IsExplicitWeightPropertyName(string propertyName, string propertySetName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                return false;

            var combined = $"{propertySetName} {propertyName}";
            return combined.ContainsAny(
                "weight",
                "mass",
                "netweight",
                "grossweight",
                "net weight",
                "gross weight",
                "self weight",
                "selfweight",
                "dead load",
                "deadload",
                "shipping weight",
                "shippingweight",
                "lifting weight",
                "liftingweight",
                "assembly weight",
                "assemblyweight",
                "precast weight",
                "precastweight",
                "revit weight",
                "revit mass");
        }

        private static bool IsLikelyRevitWeightProperty(string propertyName, string propertySetName)
        {
            var combined = $"{propertySetName} {propertyName}";
            return combined.ContainsAny(
                "revit",
                "identity data",
                "dimensions",
                "structural",
                "netweight",
                "grossweight",
                "assembly weight",
                "precast weight");
        }

        private static double ResolveConcreteDensity(IfcReadingOptions? options)
        {
            var density = options?.ConcreteDensityKgPerM3;
            return density.HasValue && density.Value > 0
                ? density.Value
                : ConcreteDensityKgPerCubicMeter;
        }

        private static double? GetDirectVolume(IIfcProduct product)
        {
            var quantitySets = product.IsDefinedBy
                .Where(r => r.RelatingPropertyDefinition is IIfcElementQuantity)
                .Select(r => (IIfcElementQuantity)r.RelatingPropertyDefinition);

            foreach (var qSet in quantitySets)
            {
                foreach (var quantity in qSet.Quantities.OfType<IIfcQuantityVolume>())
                {
                    if (quantity.Name.ToString().ContainsAny("volume"))
                    {
                        return quantity.VolumeValue;
                    }
                }
            }

            var propertySets = product.IsDefinedBy
                .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet)
                .Select(r => (IIfcPropertySet)r.RelatingPropertyDefinition);

            foreach (var pSet in propertySets)
            {
                foreach (var prop in pSet.HasProperties.OfType<IIfcPropertySingleValue>())
                {
                    if (prop.Name.ToString().ContainsAny("volume"))
                    {
                        if (double.TryParse(prop.NominalValue?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                        {
                            return parsed;
                        }
                    }
                }
            }

            return null;
        }

        private static ElementVolumeMaterialAuditDetail AuditVolumeMaterial(IIfcProduct product, double volumeFactor)
        {
            var detail = new ElementVolumeMaterialAuditDetail();
            detail.GlobalId = product.GlobalId.ToString();

            // Direct Volume
            var quantitySets = product.IsDefinedBy
                .Where(r => r.RelatingPropertyDefinition is IIfcElementQuantity)
                .Select(r => (IIfcElementQuantity)r.RelatingPropertyDefinition);

            foreach (var qSet in quantitySets)
            {
                foreach (var quantity in qSet.Quantities.OfType<IIfcQuantityVolume>())
                {
                    if (quantity.Name.ToString().ContainsAny("volume"))
                    {
                        detail.VolumeRawValue = quantity.VolumeValue;
                        detail.VolumeStandardValueM3 = quantity.VolumeValue * volumeFactor;
                        detail.VolumeSource = $"Quantity: {qSet.Name}.{quantity.Name}";
                        break;
                    }
                }
                if (detail.VolumeRawValue.HasValue) break;
            }

            if (!detail.VolumeRawValue.HasValue)
            {
                var propertySets = product.IsDefinedBy
                    .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet)
                    .Select(r => (IIfcPropertySet)r.RelatingPropertyDefinition);

                foreach (var pSet in propertySets)
                {
                    foreach (var prop in pSet.HasProperties.OfType<IIfcPropertySingleValue>())
                    {
                        if (prop.Name.ToString().ContainsAny("volume"))
                        {
                            if (double.TryParse(prop.NominalValue?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                            {
                                detail.VolumeRawValue = parsed;
                                detail.VolumeStandardValueM3 = parsed * volumeFactor;
                                detail.VolumeSource = $"Property: {pSet.Name}.{prop.Name}";
                                break;
                            }
                        }
                    }
                    if (detail.VolumeRawValue.HasValue) break;
                }
            }

            // Material
            try
            {
                var relation = product.HasAssociations
                    .OfType<IIfcRelAssociatesMaterial>()
                    .FirstOrDefault();

                if (relation != null)
                {
                    detail.IsMaterialFromInstance = true;
                    detail.MaterialSource = "HasAssociations.RelatingMaterial";

                    var relMat = relation.RelatingMaterial;
                    if (relMat is IIfcMaterial mat)
                    {
                        detail.MaterialName = mat.Name.ToString();
                        detail.MaterialAssociationType = "Simple";
                    }
                    else if (relMat is IIfcMaterialLayer layer)
                    {
                        detail.MaterialName = layer.Material?.Name.ToString() ?? "-";
                        detail.MaterialAssociationType = "LayerSet";
                    }
                    else if (relMat is IIfcMaterialLayerSet layerSet)
                    {
                        var mats = layerSet.MaterialLayers.Select(l => l.Material?.Name.ToString()).Distinct().ToList();
                        detail.MaterialName = string.Join(", ", mats);
                        detail.MaterialAssociationType = "LayerSet";
                    }
                    else if (relMat is IIfcMaterialList list)
                    {
                        var mats = list.Materials.Select(m => m.Name.ToString()).Distinct().ToList();
                        detail.MaterialName = string.Join(", ", mats);
                        detail.MaterialAssociationType = "ConstituentSet";
                    }
                    else
                    {
                        detail.MaterialName = relMat?.GetType().Name ?? "Unknown";
                        detail.MaterialAssociationType = "Unknown";
                    }
                }
            }
            catch (Exception ex)
            {
                detail.MaterialName = $"Error: {ex.Message}";
            }

            // If the element has no explicit weight, its fallback estimation uses density, which depends heavily on material type
            detail.WeightFallbackDependsOnMaterial = true;

            return detail;
        }

        private static ElementGeometryOwnershipAuditDetail AuditGeometryOwnership(
            IIfcProduct product,
            Xbim3DModelContext geometryContext,
            HashSet<string> swallowedGlobalIds,
            List<AssemblyDecompositionDetail> assemblyDetails)
        {
            var detail = new ElementGeometryOwnershipAuditDetail
            {
                LogicalGlobalId = product.GlobalId.ToString()
            };

            var shapes = new List<XbimShapeInstance>();
            try
            {
                shapes = geometryContext.ShapeInstancesOf(product).ToList();
            }
            catch { }

            detail.ShapeCount = shapes.Count;
            detail.HasGeometryDirectly = shapes.Count > 0;

            var isSwallowed = swallowedGlobalIds.Contains(detail.LogicalGlobalId);

            // Find parent assembly details if any
            var parentAssembly = assemblyDetails.FirstOrDefault(a => a.ChildDetails.Any(c => c.ChildGlobalId == detail.LogicalGlobalId));
            if (parentAssembly != null)
            {
                detail.GeometryOwnerCandidateGlobalId = parentAssembly.AssemblyGlobalId;
                detail.HasGeometryOnlyOnParent = parentAssembly.ParentHasGeometry && !detail.HasGeometryDirectly;
            }
            else
            {
                detail.GeometryOwnerCandidateGlobalId = detail.LogicalGlobalId;
            }

            if (product is IIfcElementAssembly assembly)
            {
                var childrenHaveShapes = assemblyDetails.FirstOrDefault(a => a.AssemblyGlobalId == detail.LogicalGlobalId)?.ChildrenHaveGeometry ?? false;
                detail.HasGeometryOnlyOnChildren = childrenHaveShapes && !detail.HasGeometryDirectly;
            }

            detail.HasNoGeometry = !detail.HasGeometryDirectly && !detail.HasGeometryOnlyOnParent && !detail.HasGeometryOnlyOnChildren;

            // Classify RenderRisk
            if (detail.HasNoGeometry)
            {
                detail.RenderRisk = "NoShapeInstances";
            }
            else if (isSwallowed && parentAssembly != null)
            {
                if (detail.HasGeometryDirectly && parentAssembly.ParentHasGeometry)
                {
                    detail.RenderRisk = "SharedGeometry";
                }
                else if (detail.HasGeometryDirectly)
                {
                    detail.RenderRisk = "ChildHasGeometryButParentIsExtracted";
                }
                else if (parentAssembly.ParentHasGeometry)
                {
                    detail.RenderRisk = "ParentHasGeometryButChildWouldBeExtracted";
                }
            }
            else
            {
                detail.RenderRisk = "OK";
            }

            return detail;
        }

        private string GenerateReportsCore(IfcAuditResult auditResult, string? projectFolderPath, CancellationToken cancellationToken)
        {
            string outputDirectory;
            if (!string.IsNullOrEmpty(projectFolderPath) && Directory.Exists(projectFolderPath))
            {
                outputDirectory = Path.Combine(projectFolderPath, "Reports", "IFC_Audit");
            }
            else
            {
                outputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CraneOptimizer", "Reports", "IFC_Audit");
            }

            Directory.CreateDirectory(outputDirectory);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var jsonPath = Path.Combine(outputDirectory, $"ifc_audit_details_{timestamp}.json");
            var csvPath = Path.Combine(outputDirectory, $"ifc_audit_elements_{timestamp}.csv");

            cancellationToken.ThrowIfCancellationRequested();
            // 1. Generate JSON
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            var jsonContent = JsonSerializer.Serialize(auditResult, jsonOptions);
            File.WriteAllText(jsonPath, jsonContent, Encoding.UTF8);

            cancellationToken.ThrowIfCancellationRequested();
            // 2. Generate CSV (Metadata included at top per user request)
            GenerateCsvReport(auditResult, csvPath);

            Debug.WriteLine($"[IfcDiagnosticAudit] JSON and CSV Reports generated successfully at: {outputDirectory}");
            return outputDirectory;
        }

        private static void GenerateCsvReport(IfcAuditResult auditResult, string csvPath)
        {
            var sb = new StringBuilder();

            // Append professional IFC metadata header (Phase 2A request)
            sb.AppendLine("# ==========================================================================");
            sb.AppendLine("# IFC DIAGNOSTIC AUDIT PROFESSIONAL REPORT");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "# File Name: {0}", auditResult.Metadata.FileName));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "# Schema Version: {0}", auditResult.Metadata.SchemaVersion));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "# Exporter Application: {0}", auditResult.Metadata.ExporterApplication));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "# Total Physical Elements Analyzed: {0}", auditResult.Classifications.Count));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "# Length Unit: {0} ({1} m scale factor)", auditResult.Metadata.LengthUnit, auditResult.Metadata.LengthConversionFactor));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "# Area Unit: {0} ({1} m² scale factor)", auditResult.Metadata.AreaUnit, auditResult.Metadata.AreaConversionFactor));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "# Volume Unit: {0} ({1} m³ scale factor)", auditResult.Metadata.VolumeUnit, auditResult.Metadata.VolumeConversionFactor));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "# Mass Unit: {0} ({1} kg scale factor)", auditResult.Metadata.MassUnit, auditResult.Metadata.MassConversionFactor.HasValue ? auditResult.Metadata.MassConversionFactor.Value.ToString(CultureInfo.InvariantCulture) : "Unspecified"));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "# Generated At (UTC): {0}", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")));
            sb.AppendLine("# ==========================================================================");
            sb.AppendLine();

            sb.AppendLine("GlobalId,EntityLabel,Name,RawIfcType,ResolvedCategory,WeightRawValue,WeightStandardValueTons,WeightSource,WeightStatus,VolumeM3,MaterialName,GeometryOwner,RenderRisk");

            foreach (var element in auditResult.Classifications)
            {
                var weight = auditResult.Weights.FirstOrDefault(w => w.GlobalId == element.GlobalId);
                var volMat = auditResult.VolumesMaterials.FirstOrDefault(v => v.GlobalId == element.GlobalId);
                var geom = auditResult.Geometries.FirstOrDefault(g => g.LogicalGlobalId == element.GlobalId);

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "\"{0}\",{1},\"{2}\",\"{3}\",\"{4}\",{5},{6},\"{7}\",\"{8}\",{9},\"{10}\",\"{11}\",\"{12}\"",
                    element.GlobalId,
                    element.EntityLabel,
                    EscapeCsv(element.Name),
                    element.RawIfcType,
                    element.CandidateResolvedCategory,
                    weight?.WeightRawValue?.ToString("F4", CultureInfo.InvariantCulture) ?? "null",
                    weight?.WeightStandardValueTons?.ToString("F4", CultureInfo.InvariantCulture) ?? "null",
                    weight?.WeightSource ?? "Missing",
                    weight?.WeightStatus ?? "MissingWeight",
                    volMat?.VolumeStandardValueM3?.ToString("F4", CultureInfo.InvariantCulture) ?? "null",
                    EscapeCsv(volMat?.MaterialName ?? string.Empty),
                    geom?.GeometryOwnerCandidateGlobalId ?? element.GlobalId,
                    geom?.RenderRisk ?? "OK"
                ));
            }

            File.WriteAllText(csvPath, sb.ToString(), Encoding.UTF8);
        }

        private static string EscapeCsv(string value)
        {
            return value.Replace("\"", "\"\"");
        }

        private static void GenerateMarkdownReport(IfcAuditResult auditResult, string mdPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# IFC Schema & Decomposition Diagnostic Audit Summary");
            sb.AppendLine();
            sb.AppendLine("## 1. Executive Summary");
            sb.AppendLine("This report contains the raw counts, assembly decompositions, weight sources, unit conversion metadata, and render risks detected in the imported IFC file before processing.");
            sb.AppendLine();
            sb.AppendLine($"- **File Name:** `{auditResult.Metadata.FileName}`");
            sb.AppendLine($"- **Schema Version:** `{auditResult.Metadata.SchemaVersion}`");
            sb.AppendLine($"- **Originating Exporter:** `{auditResult.Metadata.ExporterApplication}`");
            sb.AppendLine($"- **Total Physical Products Analyzed:** `{auditResult.Classifications.Count}`");
            sb.AppendLine();

            if (auditResult.Metadata.Warnings.Count > 0)
            {
                sb.AppendLine("> [!WARNING]");
                sb.AppendLine("> **Project Unit Configuration Warnings:**");
                foreach (var warning in auditResult.Metadata.Warnings)
                {
                    sb.AppendLine($"> - {warning}");
                }
                sb.AppendLine();
            }

            sb.AppendLine("## 2. Project Units Context");
            sb.AppendLine("| Physical Metric | Detected Unit | Conversion Factor to Metric Standard |");
            sb.AppendLine("| :--- | :--- | :---: |");
            sb.AppendLine($"| **Length** | {auditResult.Metadata.LengthUnit} | {auditResult.Metadata.LengthConversionFactor} m |");
            sb.AppendLine($"| **Area** | {auditResult.Metadata.AreaUnit} | {auditResult.Metadata.AreaConversionFactor} m² |");
            sb.AppendLine($"| **Volume** | {auditResult.Metadata.VolumeUnit} | {auditResult.Metadata.VolumeConversionFactor} m³ |");
            sb.AppendLine($"| **Mass/Weight** | {auditResult.Metadata.MassUnit} | {(auditResult.Metadata.MassConversionFactor.HasValue ? auditResult.Metadata.MassConversionFactor.Value.ToString() + " kg" : "Unspecified")} |");
            sb.AppendLine();

            sb.AppendLine("## 3. Raw IFC Type Summary");
            sb.AppendLine("| Raw IFC Class | Total Count | Has GlobalId | Has Name | Has Placement | Has Geometry | Has Qset | Has Pset | Has Material |");
            sb.AppendLine("| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |");
            foreach (var t in auditResult.RawTypes)
            {
                sb.AppendLine($"| `{t.RawIfcClass}` | {t.TotalCount} | {t.CountWithGlobalId} | {t.CountWithName} | {t.CountWithObjectPlacement} | {t.CountWithShapeInstances} | {t.CountWithQuantitySets} | {t.CountWithPropertySets} | {t.CountWithMaterialAssociation} |");
            }
            sb.AppendLine();

            sb.AppendLine("## 4. Assembly & Decomposition Audit");
            sb.AppendLine($"- **Total `IfcElementAssembly` nodes:** `{auditResult.Assemblies.TotalAssemblyCount}`");
            sb.AppendLine($"- **Assemblies with children:** `{auditResult.Assemblies.AssembliesWithChildren}`");
            sb.AppendLine($"- **Assemblies without children:** `{auditResult.Assemblies.AssembliesWithoutChildren}`");
            sb.AppendLine($"- **Max nesting depth detected:** `{auditResult.Assemblies.MaxDecompositionDepth}`");
            sb.AppendLine($"- **Double counting risk occurrences:** `{auditResult.Assemblies.DoubleCountingRisksCount}`");
            sb.AppendLine($"- **Swallowed children count:** `{auditResult.Assemblies.ChildrenSwallowedCount}`");
            sb.AppendLine();

            sb.AppendLine("### Assembly Decomposition Details");
            sb.AppendLine("| Assembly GlobalId | Name | Direct Children | Nesting Depth | Parent Geometry? | Children Geometry? | Pattern | Rel Type |");
            sb.AppendLine("| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :--- |");
            foreach (var a in auditResult.Assemblies.Details.Take(25)) // Cap at 25 in MD report for readability
            {
                sb.AppendLine($"| `{a.AssemblyGlobalId}` | {a.AssemblyName} | {a.DirectChildCount} | {a.NestingDepth} | {(a.ParentHasGeometry ? "Yes" : "No")} | {(a.ChildrenHaveGeometry ? "Yes" : "No")} | {a.GeometryOwnershipPattern} | {a.NestingRelationshipType} |");
            }
            if (auditResult.Assemblies.Details.Count > 25)
            {
                sb.AppendLine($"| *... and {auditResult.Assemblies.Details.Count - 25} more assemblies* | | | | | | | |");
            }
            sb.AppendLine();

            sb.AppendLine("## 5. Element Classification Risk Summary");
            sb.AppendLine("| Candidate Category | Counts | Reason for Candidate Category Heuristic | Current UI Category Displayed |");
            sb.AppendLine("| :--- | :---: | :--- | :--- |");
            var catGroups = auditResult.Classifications.GroupBy(c => c.CandidateResolvedCategory);
            foreach (var cg in catGroups)
            {
                var example = cg.First();
                sb.AppendLine($"| **{cg.Key}** | {cg.Count()} | {example.CandidateReason} | {(example.CurrentUiShowAsAssembly ? "`IfcElementAssembly` / Swallowed" : $"`{example.RawIfcType}`")} |");
            }
            sb.AppendLine();

            sb.AppendLine("## 6. Weight & Quantity Source Summary");
            var weightGroups = auditResult.Weights.GroupBy(w => w.WeightSource);
            sb.AppendLine("| Weight Source | Element Counts | Weight Status Breakdown |");
            sb.AppendLine("| :--- | :---: | :--- |");
            foreach (var wg in weightGroups)
            {
                var statusStr = string.Join(", ", wg.GroupBy(x => x.WeightStatus).Select(g => $"{g.Key}: {g.Count()}"));
                sb.AppendLine($"| {wg.Key} | {wg.Count()} | {statusStr} |");
            }
            sb.AppendLine();

            var zeroWeights = auditResult.Weights.Where(w => w.WeightStatus == "ExplicitZeroFromIFC").ToList();
            sb.AppendLine("### Explicit Zero Weight Elements");
            if (zeroWeights.Count == 0)
            {
                sb.AppendLine("*None detected.*");
            }
            else
            {
                sb.AppendLine("| Element ID (GlobalId) | Candidate Type | Raw Value | Status/Warning |");
                sb.AppendLine("| :--- | :--- | :---: | :--- |");
                foreach (var zw in zeroWeights.Take(15))
                {
                    sb.AppendLine($"| `{zw.GlobalId}` | {zw.ResolvedType} | 0.0 | {zw.Warning} |");
                }
                if (zeroWeights.Count > 15) sb.AppendLine($"| *... and {zeroWeights.Count - 15} more zero-weight elements* | | | |");
            }
            sb.AppendLine();

            var missingWeights = auditResult.Weights.Where(w => w.WeightStatus == "MissingWeight").ToList();
            sb.AppendLine("### Missing Weight Elements");
            if (missingWeights.Count == 0)
            {
                sb.AppendLine("*None detected.*");
            }
            else
            {
                sb.AppendLine("| Element ID (GlobalId) | Candidate Type | Status/Warning |");
                sb.AppendLine("| :--- | :--- | :--- |");
                foreach (var mw in missingWeights.Take(15))
                {
                    sb.AppendLine($"| `{mw.GlobalId}` | {mw.ResolvedType} | {mw.Warning} |");
                }
                if (missingWeights.Count > 15) sb.AppendLine($"| *... and {missingWeights.Count - 15} more missing-weight elements* | | |");
            }
            sb.AppendLine();

            var estimatedWeights = auditResult.Weights.Where(w => w.WeightStatus == "EstimatedByFallback").ToList();
            sb.AppendLine("### Estimated / Fallback Weight Elements");
            if (estimatedWeights.Count == 0)
            {
                sb.AppendLine("*None detected.*");
            }
            else
            {
                sb.AppendLine("| Element ID (GlobalId) | Candidate Type | Standardized Value (Tons) | Status/Warning |");
                sb.AppendLine("| :--- | :--- | :---: | :--- |");
                foreach (var ew in estimatedWeights.Take(15))
                {
                    sb.AppendLine($"| `{ew.GlobalId}` | {ew.ResolvedType} | {ew.WeightStandardValueTons:F4} | {ew.Warning} |");
                }
                if (estimatedWeights.Count > 15) sb.AppendLine($"| *... and {estimatedWeights.Count - 15} more estimated elements* | | | |");
            }
            sb.AppendLine();

            sb.AppendLine("## 7. Geometry Ownership & Render Risks");
            sb.AppendLine("| Render Risk | Count | Explanation / Compatibility Impact |");
            sb.AppendLine("| :--- | :---: | :--- |");
            var geomGroups = auditResult.Geometries.GroupBy(g => g.RenderRisk);
            foreach (var gg in geomGroups)
            {
                var explanation = gg.Key switch
                {
                    "OK" => "Safe to render, logical element maps cleanly to shape geometry.",
                    "ChildHasGeometryButParentIsExtracted" => "Child product has shapes, but parent assembly is registered in the scene. Children are swallowed, but their rendering shapes might not link to the parent assembly GlobalId.",
                    "ParentHasGeometryButChildWouldBeExtracted" => "Parent has shape geometry, but children should be unrolled. Individual children might render with zero coordinates or remain invisible.",
                    "NoShapeInstances" => "No 3D visual representation found in the IFC file. Element will be invisible in Helix viewport.",
                    "SharedGeometry" => "Both child and parent have geometry. Unrolling might cause visual overlap and double-counting of volume/mass.",
                    "AmbiguousGeometryOwner" => "Shape mapping resolved through ambiguous reference structures. Selection highlight issues expected.",
                    _ => "Unknown rendering risk."
                };
                sb.AppendLine($"| `{gg.Key}` | {gg.Count()} | {explanation} |");
            }
            sb.AppendLine();

            var suspiciousElements = auditResult.Weights.Where(w => w.WeightStatus == "SuspiciousExtreme" || w.WeightStatus == "InvalidNegative").ToList();
            sb.AppendLine("## 8. Suspicious Elements Summary");
            if (suspiciousElements.Count == 0)
            {
                sb.AppendLine("*None detected.*");
            }
            else
            {
                sb.AppendLine("| Element ID (GlobalId) | Resolved Type | Parameter Checked | Problematic Value | Warning / Diagnostics |");
                sb.AppendLine("| :--- | :--- | :--- | :---: | :--- |");
                foreach (var se in suspiciousElements)
                {
                    sb.AppendLine($"| `{se.GlobalId}` | {se.ResolvedType} | Weight | {se.WeightStandardValueTons:F4} Tons | {se.Warning} |");
                }
            }
            sb.AppendLine();

            sb.AppendLine("## 9. Recommendations for Phase 2 Implementation");
            sb.AppendLine("> [!IMPORTANT]");
            sb.AppendLine("> Based on this diagnostic audit run, we recommend the following structural adjustments for Phase 2:");
            sb.AppendLine("> 1. **Implement Assembly Unrolling:** Leaf structural items inside assemblies must be registered as individual, liftable `PrecastElement` instances. Set `IncludeInLiftCalculation = false` on their grouping parent assembly.");
            sb.AppendLine("> 2. **Decouple Logical IDs from Geometry Owners:** In cases of `ChildHasGeometryButParentIsExtracted`, set `GeometryOwnerGlobalId = ParentAssembly.GlobalId` on child elements so the Helix viewport queries shape representations correctly.");
            sb.AppendLine("> 3. **Implement Explicit User-Driven Weight Estimations:** Avoid silent assumptions for concrete density fallback. Prompt the user inside the UI to verify density, steel grades, and allow/disallow estimations in crane load charts.");
            sb.AppendLine();

            File.WriteAllText(mdPath, sb.ToString(), Encoding.UTF8);
        }
    }
}
