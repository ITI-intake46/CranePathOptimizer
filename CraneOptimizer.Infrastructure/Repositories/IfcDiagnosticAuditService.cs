using CraneOptimizer.Application.Services.Identity;
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
        private readonly ICentralIfcPropertyAliasResolver _aliasResolver;
        private readonly object _auditCacheGate = new();
        private Guid? _lastAuditSessionId;
        private string? _lastAuditFileIdentity;
        private double _lastAuditConcreteDensityKgPerM3 = ConcreteDensityKgPerCubicMeter;
        private IfcAuditResult? _lastAuditResult;

        public IfcDiagnosticAuditService()
            : this(null, new CentralIfcPropertyAliasResolver(new IfcExporterProfileResolver()))
        {
        }

        public IfcDiagnosticAuditService(IIfcModelSessionService sessionService)
            : this(sessionService, new CentralIfcPropertyAliasResolver(new IfcExporterProfileResolver()))
        {
        }

        public IfcDiagnosticAuditService(IIfcModelSessionService? sessionService, ICentralIfcPropertyAliasResolver aliasResolver)
        {
            _sessionService = sessionService;
            _aliasResolver = aliasResolver;
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
                        && Math.Abs(_lastAuditConcreteDensityKgPerM3 - concreteDensityKgPerM3) < 0.000001
                        && _lastAuditResult.Options?.HideGrids == options?.HideGrids)
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

                result.Options = options;
                return result;
            }

            var fallbackResult = RunAuditCore(ifcPath, concreteDensityKgPerM3, progress, cancellationToken);
            fallbackResult.Options = options;
            return fallbackResult;
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

        private void PopulateAuditResult(
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

            var allAuditableProducts = model.Instances
                .OfType<IIfcProduct>()
                .Where(IsAuditableProduct)
                .ToList();

            progress?.Report(40.0);
            result.RawTypes = BuildRawTypeSummary(allAuditableProducts, geometryContext);
            AddGridAxisRawTypeSummary(model, result.RawTypes);
            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(60.0);
            result.Assemblies = BuildAssemblyDecompositionAudit(model, allAuditableProducts, geometryContext, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(80.0);
            ProcessPhysicalElements(allAuditableProducts, model, geometryContext, result, result.Metadata.LengthConversionFactor, result.Metadata.VolumeConversionFactor, result.Metadata.MassConversionFactor, concreteDensityKgPerM3);
            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(100.0);
        }

        private static bool IsAuditableProduct(IIfcProduct product)
        {
            if (product is IIfcGrid)
                return !string.IsNullOrWhiteSpace(product.GlobalId);

            return IsPhysicalProduct(product);
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
                        CountWithShapeInstances = items.Count(p =>
                        {
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

        private static void AddGridAxisRawTypeSummary(IfcStore model, ICollection<RawIfcTypeSummary> summaries)
        {
            var axes = model.Instances.OfType<IIfcGridAxis>().ToList();
            if (axes.Count == 0)
                return;

            summaries.Add(new RawIfcTypeSummary
            {
                RawIfcClass = "IfcGridAxis",
                TotalCount = axes.Count,
                CountWithName = axes.Count(axis => !string.IsNullOrWhiteSpace(axis.AxisTag))
            });
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

        private void ProcessPhysicalElements(
            List<IIfcProduct> physicalProducts,
            IfcStore model,
            Xbim3DModelContext geometryContext,
            IfcAuditResult auditResult,
            double lengthFactor,
            double volumeFactor,
            double? massFactor,
            double concreteDensityKgPerM3)
        {
            var swallowedGlobalIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var assemblyDetail in auditResult.Assemblies.Details)
            {
                foreach (var child in assemblyDetail.ChildDetails)
                {
                    swallowedGlobalIds.Add(child.ChildGlobalId);
                }
            }

            var profileResolver = new IfcExporterProfileResolver();
            var profile = profileResolver.ResolveProfile(auditResult.Metadata.ExporterApplication);

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
                var weightDetail = AuditWeight(product, resolvedCategory, swallowedGlobalIds.Contains(globalId), concreteDensityKgPerM3, profile, massFactor);
                auditResult.Weights.Add(weightDetail);

                // 3. Volume and Material Audit
                var volMat = AuditVolumeMaterial(product, volumeFactor, profile);
                auditResult.VolumesMaterials.Add(volMat);

                // 4. Geometry Ownership Audit
                var geomDetail = AuditGeometryOwnership(product, geometryContext, swallowedGlobalIds, auditResult.Assemblies.Details);
                auditResult.Geometries.Add(geomDetail);
            }

            auditResult.RawNameDiagnostics = BuildRawNameDiagnostics(physicalProducts, model);
            auditResult.AssemblyMarkDiagnostics = BuildAssemblyMarkDiagnostics(
                physicalProducts,
                geometryContext,
                auditResult,
                lengthFactor,
                profile);
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

        private ElementWeightAuditDetail AuditWeight(
            IIfcProduct product,
            string resolvedType,
            bool isSwallowed,
            double concreteDensityKgPerM3,
            IfcExporterProfile profile,
            double? massConversionFactor)
        {
            var detail = new ElementWeightAuditDetail
            {
                GlobalId = product.GlobalId.ToString(),
                ResolvedType = resolvedType,
                IsWeightUsedByProductionCurrently = !isSwallowed && !(product is IIfcElementAssembly)
            };

            var qRes = _aliasResolver.ResolveProperty(product, "Weight", profile);
            if (qRes.Value != null)
            {
                if (double.TryParse(qRes.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedVal))
                {
                    detail.WeightRawValue = parsedVal;
                    detail.WeightPropertyName = qRes.SourcePropertyName;
                    detail.QuantitySetNameOrPropertySetName = qRes.SourcePropertySet;
                    detail.WeightUnitDetected = "Unspecified";

                    var isQty = IsQuantitySet(product, qRes.SourcePropertySet ?? string.Empty);
                    if (isQty)
                    {
                        detail.WeightSource = "IfcQuantityWeight.Instance";
                        detail.WeightUnitDetected = "kg";
                        detail.WeightStandardValueTons = UnitSystem.ToTons(parsedVal, MassUnit.Kilogram);
                    }
                    else
                    {
                        string? unitName = null;
                        var propertySets = product.IsDefinedBy
                            .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet)
                            .Select(r => (IIfcPropertySet)r.RelatingPropertyDefinition);
                        foreach (var pset in propertySets)
                        {
                            if (pset.Name.ToString().Equals(qRes.SourcePropertySet, StringComparison.OrdinalIgnoreCase))
                            {
                                var prop = pset.HasProperties.OfType<IIfcPropertySingleValue>()
                                    .FirstOrDefault(p => p.Name.ToString().Equals(qRes.SourcePropertyName, StringComparison.OrdinalIgnoreCase));
                                unitName = prop?.Unit?.ToString();
                                break;
                            }
                        }
                        if (unitName != null)
                        {
                            detail.WeightUnitDetected = unitName;
                        }

                        detail.WeightSource = IsLikelyRevitWeightProperty(qRes.SourcePropertyName ?? string.Empty, qRes.SourcePropertySet ?? string.Empty)
                            ? "RevitPropertySet.Instance"
                            : "PropertySet.Instance";

                        var unitInfo = $"{qRes.SourcePropertyName} {detail.WeightUnitDetected}".ToLowerInvariant();
                        if (unitInfo.ContainsAny("kg", "kilo"))
                        {
                            detail.WeightStandardValueTons = UnitSystem.ToTons(parsedVal, MassUnit.Kilogram);
                        }
                        else if (unitInfo.ContainsAny("ton", "tonne"))
                        {
                            detail.WeightStandardValueTons = UnitSystem.ToTons(parsedVal, MassUnit.Ton);
                        }
                        else if (unitInfo.ContainsAny("lb", "pound"))
                        {
                            detail.WeightStandardValueTons = parsedVal * 0.00045359237;
                        }
                        else
                        {
                            if (massConversionFactor.HasValue)
                            {
                                detail.WeightStandardValueTons = UnitSystem.ToTons(parsedVal * massConversionFactor.Value, MassUnit.Kilogram);
                            }
                            else
                            {
                                detail.WeightStandardValueTons = parsedVal > 100.0 ? UnitSystem.ToTons(parsedVal, MassUnit.Kilogram) : parsedVal;
                                if (parsedVal > 100.0)
                                {
                                    detail.WeightSource = $"{detail.WeightSource} (Heuristically Scaled)";
                                    detail.Warning = "Weight property value exceeded 100. Assumed kilograms and converted to metric tons.";
                                }
                            }
                        }
                    }
                }
            }

            // Fallback estimation using existing concrete density logic in pipeline
            if (!detail.WeightRawValue.HasValue)
            {
                double? volume = GetDirectVolume(product, profile);
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

        private double? GetDirectVolume(IIfcProduct product, IfcExporterProfile profile)
        {
            var qRes = _aliasResolver.ResolveProperty(product, "Volume", profile);
            if (qRes.Value != null && double.TryParse(qRes.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
            return null;
        }

        private ElementVolumeMaterialAuditDetail AuditVolumeMaterial(IIfcProduct product, double volumeFactor, IfcExporterProfile profile)
        {
            var detail = new ElementVolumeMaterialAuditDetail();
            detail.GlobalId = product.GlobalId.ToString();

            var qRes = _aliasResolver.ResolveProperty(product, "Volume", profile);
            if (qRes.Value != null)
            {
                if (double.TryParse(qRes.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                {
                    detail.VolumeRawValue = parsed;
                    detail.VolumeStandardValueM3 = parsed * volumeFactor;
                    var isQty = IsQuantitySet(product, qRes.SourcePropertySet ?? string.Empty);
                    detail.VolumeSource = isQty
                        ? $"Quantity: {qRes.SourcePropertySet}.{qRes.SourcePropertyName}"
                        : $"Property: {qRes.SourcePropertySet}.{qRes.SourcePropertyName}";
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

            detail.WeightFallbackDependsOnMaterial = true;

            return detail;
        }

        private static bool IsQuantitySet(IIfcProduct product, string psetName)
        {
            return product.IsDefinedBy
                .Any(r => r.RelatingPropertyDefinition is IIfcElementQuantity eq && eq.Name.ToString().Equals(psetName, StringComparison.OrdinalIgnoreCase));
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

        private static string CleanElementName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return "-";

            return rawName.Replace("- Assembly", "")
                         .Replace(" Assembly", "")
                         .Trim();
        }

        private List<RawNameDiagnosticDetail> BuildRawNameDiagnostics(List<IIfcProduct> physicalProducts, IfcStore model)
        {
            var profileResolver = new IfcExporterProfileResolver();
            var exporterProfile = profileResolver.ResolveProfile(model.Header?.FileName?.OriginatingSystem ?? string.Empty);

            var list = new List<RawNameDiagnosticDetail>();
            foreach (var product in physicalProducts)
            {
                var rawIfcName = product?.Name?.ToString()?.Trim();
                var rawIfcLongName = string.Empty;
                var rawObjectType = string.Empty;
                var rawTag = string.Empty;

                if (product is Xbim.Ifc4.ProductExtension.IfcElement element)
                {
                    rawTag = element.Tag?.ToString()?.Trim() ?? string.Empty;
                }

                if (product is Xbim.Ifc4.Kernel.IfcObject obj)
                {
                    rawObjectType = obj.ObjectType?.ToString()?.Trim() ?? string.Empty;
                }

                var longNameProp = product?.GetType().GetProperty("LongName");
                if (longNameProp != null)
                {
                    rawIfcLongName = longNameProp.GetValue(product)?.ToString()?.Trim() ?? string.Empty;
                }

                var aliasResult = _aliasResolver.ResolveCanonicalAssemblyIdentity(product!, rawIfcName ?? string.Empty, exporterProfile);
                var rawAssemblyPos = _aliasResolver.ResolveProperty(product!, "ASSEMBLY_POS", exporterProfile)?.Value ?? string.Empty;
                var rawCastUnitPos = _aliasResolver.ResolveProperty(product!, "CAST_UNIT_POS", exporterProfile)?.Value ?? string.Empty;

                var extractedElementName = !string.IsNullOrWhiteSpace(rawIfcName) ? rawIfcName : string.Empty;
                var elementMark = CleanElementName(aliasResult.AssemblyMark ?? string.Empty);
                var f5NameResolved = !string.IsNullOrWhiteSpace(extractedElementName) ? extractedElementName : "-";

                var diagnosticElement = new PrecastElement
                {
                    ElementID = product?.GlobalId.ToString() ?? string.Empty,
                    ElementName = extractedElementName,
                    ElementMark = elementMark,
                    AssemblyMark = aliasResult.AssemblyMark,
                    AssemblyMarkIsExplicit = aliasResult.IsExplicitAssemblyMark
                };
                var canonicalIdentity = CanonicalMarkResolver.Resolve(diagnosticElement);

                var detail = new RawNameDiagnosticDetail
                {
                    GlobalId = product?.GlobalId.ToString() ?? string.Empty,
                    IfcEntity = product?.ExpressType?.Name ?? string.Empty,
                    RawProductName = rawIfcName ?? string.Empty,
                    RawProductLongName = rawIfcLongName,
                    RawObjectType = rawObjectType,
                    RawTag = rawTag,
                    RawAssemblyPos = rawAssemblyPos,
                    RawCastUnitPos = rawCastUnitPos,
                    ExtractedElementName = extractedElementName,
                    ElementID = product?.GlobalId.ToString() ?? string.Empty,
                    ElementMark = elementMark,
                    AssemblyMark = aliasResult.AssemblyMark ?? string.Empty,
                    ResolvedDisplayMark = canonicalIdentity.DisplayLabel,
                    CanonicalDisplayLabel = canonicalIdentity.DisplayLabel,
                    PersistedDisplayName = extractedElementName,
                    F5IfcNameResolved = f5NameResolved
                };
                list.Add(detail);
            }
            return list;
        }

        private List<AssemblyMarkAuditDetail> BuildAssemblyMarkDiagnostics(
            List<IIfcProduct> physicalProducts,
            Xbim3DModelContext geometryContext,
            IfcAuditResult auditResult,
            double lengthFactor,
            IfcExporterProfile profile)
        {
            var weightsById = auditResult.Weights.ToDictionary(w => w.GlobalId, StringComparer.Ordinal);
            var materialById = auditResult.VolumesMaterials.ToDictionary(v => v.GlobalId, StringComparer.Ordinal);
            var classById = auditResult.Classifications.ToDictionary(c => c.GlobalId, StringComparer.Ordinal);
            var details = new List<AssemblyMarkAuditDetail>(physicalProducts.Count);
            var sourceSystem = profile == IfcExporterProfile.Tekla
                ? SourceSystem.Tekla.ToString()
                : profile == IfcExporterProfile.Revit
                    ? SourceSystem.Revit.ToString()
                    : SourceSystem.IFC.ToString();

            foreach (var product in physicalProducts)
            {
                var globalId = product.GlobalId.ToString();
                var rawName = product.Name?.ToString() ?? string.Empty;
                var rawTag = (product as IIfcElement)?.Tag?.ToString() ?? string.Empty;
                weightsById.TryGetValue(globalId, out var weight);
                materialById.TryGetValue(globalId, out var material);
                classById.TryGetValue(globalId, out var classification);

                var identity = _aliasResolver.ResolveCanonicalAssemblyIdentity(
                    product,
                    rawName,
                    profile);
                var normalizedAssemblyMark = identity.AssemblyMark ?? string.Empty;
                var resolvedDisplayMark = !string.IsNullOrWhiteSpace(normalizedAssemblyMark)
                    ? normalizedAssemblyMark
                    : rawName;
                var canonicalDisplayLabel = !string.IsNullOrWhiteSpace(resolvedDisplayMark)
                    ? resolvedDisplayMark
                    : globalId;

                var warnings = new List<string>(identity.Warnings);
                if (!identity.IsExplicitAssemblyMark)
                {
                    warnings.Add("MissingAssemblyMark: no explicit assembly mark property was resolved.");
                }
                if (weight?.WeightStandardValueTons is not > 0)
                {
                    warnings.Add("MissingTrustedWeight: no positive audit weight was resolved for this element.");
                }

                var cog = TryGetGeometryCenter(product, geometryContext, lengthFactor);
                var groupKey = IsExplicitAssemblyGroupCandidate(identity)
                    ? identity.AssemblyMark?.Trim() ?? string.Empty
                    : string.Empty;

                details.Add(new AssemblyMarkAuditDetail
                {
                    GlobalId = globalId,
                    ElementID = globalId,
                    IfcEntity = product.ExpressType?.Name ?? product.GetType().Name,
                    ElementName = rawName,
                    ElementType = classification?.CandidateResolvedCategory ?? string.Empty,
                    RawName = rawName,
                    RawTag = rawTag,
                    Material = material?.MaterialName ?? string.Empty,
                    ElementWeightKg = weight?.WeightStandardValueTons is > 0 ? weight.WeightStandardValueTons.Value * 1000.0 : null,
                    ElementWeightTons = weight?.WeightStandardValueTons,
                    RawAssemblyPos = ReadExactProperty(product, "ASSEMBLY_POS") ?? string.Empty,
                    RawCastUnitPos = ReadExactProperty(product, "CAST_UNIT_POS") ?? string.Empty,
                    NormalizedAssemblyMark = normalizedAssemblyMark,
                    ResolvedAssemblyMark = normalizedAssemblyMark,
                    ResolvedDisplayMark = resolvedDisplayMark,
                    CanonicalDisplayLabel = canonicalDisplayLabel,
                    AssemblyMarkSource = identity.SourcePropertyName != null
                        ? $"{identity.SourcePropertySet}.{identity.SourcePropertyName}"
                        : "Fallback",
                    AssemblyMarkConfidence = identity.Confidence ?? AssemblyMarkConfidence.None.ToString(),
                    IsExplicitAssemblyMark = identity.IsExplicitAssemblyMark,
                    AssemblyGroupKey = groupKey,
                    AssemblyMemberCount = string.IsNullOrWhiteSpace(groupKey) ? 0 : 1,
                    AssemblyMemberGlobalIds = string.IsNullOrWhiteSpace(groupKey) ? new List<string>() : new List<string> { globalId },
                    AssemblyTotalWeightKg = weight?.WeightStandardValueTons is > 0 ? weight.WeightStandardValueTons.Value * 1000.0 : null,
                    AssemblyTotalWeightTons = weight?.WeightStandardValueTons,
                    MemberCogX = cog.X,
                    MemberCogY = cog.Y,
                    MemberCogZ = cog.Z,
                    AssemblyCogX = cog.X,
                    AssemblyCogY = cog.Y,
                    AssemblyCogZ = cog.Z,
                    ExporterProfile = profile.ToString(),
                    SourceSystem = sourceSystem,
                    ValidationStatus = warnings.Count == 0 ? ValidationStatus.Valid.ToString() : ValidationStatus.Warning.ToString(),
                    ValidationWarnings = warnings,
                    Action = identity.IsExplicitAssemblyMark ? "UseElementAsLiftUnit" : "ReviewFallbackMark"
                });
            }

            foreach (var group in details
                .Where(d => !string.IsNullOrWhiteSpace(d.AssemblyGroupKey))
                .GroupBy(d => d.AssemblyGroupKey, StringComparer.OrdinalIgnoreCase))
            {
                var members = group.ToList();
                var memberIds = members.Select(m => m.GlobalId).OrderBy(id => id, StringComparer.Ordinal).ToList();
                var totalWeightTons = SumPositive(members.Select(m => m.ElementWeightTons));
                var totalWeightKg = totalWeightTons.HasValue ? totalWeightTons.Value * 1000.0 : (double?)null;
                var weightedCog = ComputeWeightedCog(members, totalWeightTons);
                var fallbackCog = ComputeAverageCog(members);
                var groupCog = weightedCog ?? fallbackCog;
                var action = members.Count > 1 ? "GroupForGALiftUnit" : "UseElementAsLiftUnit";

                foreach (var member in members)
                {
                    member.AssemblyMemberCount = members.Count;
                    member.AssemblyMemberGlobalIds = memberIds;
                    member.AssemblyTotalWeightTons = totalWeightTons;
                    member.AssemblyTotalWeightKg = totalWeightKg;
                    member.AssemblyCogX = groupCog?.X;
                    member.AssemblyCogY = groupCog?.Y;
                    member.AssemblyCogZ = groupCog?.Z;
                    member.Action = action;
                }
            }

            return details;
        }

        private static bool IsExplicitAssemblyGroupCandidate(CanonicalAssemblyIdentityResult identity)
        {
            if (!identity.IsExplicitAssemblyMark || string.IsNullOrWhiteSpace(identity.AssemblyMark))
                return false;

            if (!Enum.TryParse<AssemblyMarkConfidence>(identity.Confidence, out var confidence) || confidence != AssemblyMarkConfidence.High)
                return false;

            var mark = identity.AssemblyMark.Trim();
            return !IsPlaceholderAssemblyMark(mark);
        }

        private static bool IsPlaceholderAssemblyMark(string mark)
        {
            if (string.IsNullOrWhiteSpace(mark))
                return true;

            var normalized = mark.Trim().ToUpperInvariant();
            return normalized is "WALL" or "BEAM" or "COLUMN" or "SLAB" or "ASSEMBLY" or "ELEMENT" or "UNKNOWN" or "-";
        }

        private static string? ReadExactProperty(IIfcProduct product, string propertyName)
        {
            var propertySets = product.IsDefinedBy
                .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet)
                .Select(r => (IIfcPropertySet)r.RelatingPropertyDefinition);

            foreach (var pset in propertySets)
            {
                foreach (var prop in pset.HasProperties.OfType<IIfcPropertySingleValue>())
                {
                    if (!prop.Name.ToString().Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var value = prop.NominalValue?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
            }

            return null;
        }

        private static (double? X, double? Y, double? Z) TryGetGeometryCenter(
            IIfcProduct product,
            Xbim3DModelContext geometryContext,
            double lengthFactor)
        {
            try
            {
                var shapes = geometryContext.ShapeInstancesOf(product).ToList();
                if (shapes.Count == 0)
                    return (null, null, null);

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

                if (minX == double.MaxValue)
                    return (null, null, null);

                return (((minX + maxX) / 2.0) * lengthFactor,
                    ((minY + maxY) / 2.0) * lengthFactor,
                    ((minZ + maxZ) / 2.0) * lengthFactor);
            }
            catch
            {
                return (null, null, null);
            }
        }

        private static double? SumPositive(IEnumerable<double?> values)
        {
            double sum = 0;
            bool hasValue = false;
            foreach (var value in values)
            {
                if (value is > 0)
                {
                    sum += value.Value;
                    hasValue = true;
                }
            }
            return hasValue ? sum : null;
        }

        private static (double X, double Y, double Z)? ComputeWeightedCog(
            IReadOnlyList<AssemblyMarkAuditDetail> members,
            double? totalWeightTons)
        {
            if (totalWeightTons is not > 0)
                return null;

            double x = 0, y = 0, z = 0, weightSum = 0;
            foreach (var member in members)
            {
                if (member.ElementWeightTons is not > 0 || member.MemberCogX == null || member.MemberCogY == null || member.MemberCogZ == null)
                    continue;

                x += member.MemberCogX.Value * member.ElementWeightTons.Value;
                y += member.MemberCogY.Value * member.ElementWeightTons.Value;
                z += member.MemberCogZ.Value * member.ElementWeightTons.Value;
                weightSum += member.ElementWeightTons.Value;
            }

            return weightSum > 0 ? (x / weightSum, y / weightSum, z / weightSum) : null;
        }

        private static (double X, double Y, double Z)? ComputeAverageCog(IReadOnlyList<AssemblyMarkAuditDetail> members)
        {
            var valid = members
                .Where(member => member.MemberCogX != null && member.MemberCogY != null && member.MemberCogZ != null)
                .ToList();

            return valid.Count == 0
                ? null
                : (valid.Average(member => member.MemberCogX!.Value),
                    valid.Average(member => member.MemberCogY!.Value),
                    valid.Average(member => member.MemberCogZ!.Value));
        }

        private static void GenerateRawNameCsvReport(IfcAuditResult auditResult, string csvPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("GlobalId,IfcEntity,RawProductName,RawProductLongName,RawObjectType,RawTag,RawAssemblyPos,RawCastUnitPos,ExtractedElementName,ElementID,ElementMark,AssemblyMark,ResolvedDisplayMark,CanonicalDisplayLabel,PersistedDisplayName,F5IfcNameResolved");

            foreach (var detail in auditResult.RawNameDiagnostics)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    Csv(detail.GlobalId),
                    Csv(detail.IfcEntity),
                    Csv(detail.RawProductName),
                    Csv(detail.RawProductLongName),
                    Csv(detail.RawObjectType),
                    Csv(detail.RawTag),
                    Csv(detail.RawAssemblyPos),
                    Csv(detail.RawCastUnitPos),
                    Csv(detail.ExtractedElementName),
                    Csv(detail.ElementID),
                    Csv(detail.ElementMark),
                    Csv(detail.AssemblyMark),
                    Csv(detail.ResolvedDisplayMark),
                    Csv(detail.CanonicalDisplayLabel),
                    Csv(detail.PersistedDisplayName),
                    Csv(detail.F5IfcNameResolved)
                }));
            }
            File.WriteAllText(csvPath, sb.ToString(), Encoding.UTF8);
        }

        private static void GenerateAssemblyMarkCsvReport(IfcAuditResult auditResult, string csvPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("GlobalId,ElementID,ElementName,ElementType,RawName,RawTag,RawAssemblyPos,RawCastUnitPos,NormalizedAssemblyMark,ResolvedDisplayMark,CanonicalDisplayLabel,AssemblyMarkSource,AssemblyMarkConfidence,IsExplicitAssemblyMark,ValidationStatus,ValidationWarnings,IfcEntity,Material,ElementWeightKg,ElementWeightTons,AssemblyGroupKey,AssemblyMemberCount,AssemblyMemberGlobalIds,AssemblyTotalWeightKg,AssemblyTotalWeightTons,MemberCogX,MemberCogY,MemberCogZ,AssemblyCogX,AssemblyCogY,AssemblyCogZ,ExporterProfile,SourceSystem,Action");

            foreach (var detail in auditResult.AssemblyMarkDiagnostics)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    Csv(detail.GlobalId),
                    Csv(detail.ElementID),
                    Csv(detail.ElementName),
                    Csv(detail.ElementType),
                    Csv(detail.RawName),
                    Csv(detail.RawTag),
                    Csv(detail.RawAssemblyPos),
                    Csv(detail.RawCastUnitPos),
                    Csv(detail.NormalizedAssemblyMark),
                    Csv(detail.ResolvedDisplayMark),
                    Csv(detail.CanonicalDisplayLabel),
                    Csv(detail.AssemblyMarkSource),
                    Csv(detail.AssemblyMarkConfidence),
                    detail.IsExplicitAssemblyMark ? "True" : "False",
                    Csv(detail.ValidationStatus),
                    Csv(string.Join(";", detail.ValidationWarnings)),
                    Csv(detail.IfcEntity),
                    Csv(detail.Material),
                    FormatNullable(detail.ElementWeightKg),
                    FormatNullable(detail.ElementWeightTons),
                    Csv(detail.AssemblyGroupKey),
                    detail.AssemblyMemberCount.ToString(CultureInfo.InvariantCulture),
                    Csv(string.Join(";", detail.AssemblyMemberGlobalIds)),
                    FormatNullable(detail.AssemblyTotalWeightKg),
                    FormatNullable(detail.AssemblyTotalWeightTons),
                    FormatNullable(detail.MemberCogX),
                    FormatNullable(detail.MemberCogY),
                    FormatNullable(detail.MemberCogZ),
                    FormatNullable(detail.AssemblyCogX),
                    FormatNullable(detail.AssemblyCogY),
                    FormatNullable(detail.AssemblyCogZ),
                    Csv(detail.ExporterProfile),
                    Csv(detail.SourceSystem),
                    Csv(detail.Action)
                }));
            }

            File.WriteAllText(csvPath, sb.ToString(), Encoding.UTF8);
        }

        private static string FormatNullable(double? value) =>
            value.HasValue ? value.Value.ToString("0.####", CultureInfo.InvariantCulture) : string.Empty;

        private static string Csv(string? value) =>
            $"\"{EscapeCsv(value ?? string.Empty)}\"";
        private string GenerateReportsCore(IfcAuditResult auditResult, string? projectFolderPath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(projectFolderPath))
            {
                throw new ArgumentException("projectFolderPath must not be null or whitespace. Ensure a project is loaded.", nameof(projectFolderPath));
            }
            if (!Directory.Exists(projectFolderPath))
            {
                throw new DirectoryNotFoundException($"The specified project folder path does not exist: {projectFolderPath}");
            }

            string outputDirectory = Path.Combine(projectFolderPath, "Reports", "IFC_Audit");
            Directory.CreateDirectory(outputDirectory);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var jsonPath = Path.Combine(outputDirectory, $"debug_ifc_details_{timestamp}.json");
            var csvPath = Path.Combine(outputDirectory, $"debug_ifc_elements_{timestamp}.csv");
            var assemblyMarkCsvPath = Path.Combine(outputDirectory, $"debug_ifc_check_assembly_mark_{timestamp}.csv");
            var rawNameCsvPath = Path.Combine(outputDirectory, $"debug_ifc_raw_name_check_{timestamp}.csv");

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

            cancellationToken.ThrowIfCancellationRequested();
            GenerateAssemblyMarkCsvReport(auditResult, assemblyMarkCsvPath);

            cancellationToken.ThrowIfCancellationRequested();
            GenerateRawNameCsvReport(auditResult, rawNameCsvPath);

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
