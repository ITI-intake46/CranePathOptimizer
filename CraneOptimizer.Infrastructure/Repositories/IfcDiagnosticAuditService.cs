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
        private const int LargeModelSummaryThreshold = 3000;
        private const int LargeModelDetailedSampleLimit = 1000;
        private const int LargeModelMarkDiagnosticSampleLimit = 250;
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
            catch (OperationCanceledException)
            {
                throw;
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
                Debug.WriteLine($"[IFC-PERF] Standalone Audit Open IFC model duration: {openTimer.ElapsedMilliseconds} ms");
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
                Debug.WriteLine($"[IFC-PERF] Standalone Audit Geometry context creation duration: {contextTimer.ElapsedMilliseconds} ms");
                cancellationToken.ThrowIfCancellationRequested();

                PopulateAuditResult(result, model, geometryContext, ifcPath, concreteDensityKgPerM3, progress, cancellationToken);

                Debug.WriteLine(
                    $"[IfcDiagnosticAudit] Standalone audit completed. Open={openTimer.ElapsedMilliseconds} ms, " +
                    $"CreateContext={contextTimer.ElapsedMilliseconds} ms, ReopenedIfc=True");
            }
            catch (OperationCanceledException)
            {
                throw;
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
            Xbim3DModelContext? geometryContext,
            string ifcPath,
            double concreteDensityKgPerM3,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            var totalTimerStart = DateTime.UtcNow;
            var totalTimer = Stopwatch.StartNew();
            try
            {
                progress?.Report(15.0);
                result.Metadata = ExtractMetadata(model, ifcPath);
                cancellationToken.ThrowIfCancellationRequested();

                var enumTimer = Stopwatch.StartNew();
                var allProducts = model.Instances.OfType<IIfcProduct>().ToList();
                enumTimer.Stop();
                Debug.WriteLine($"[IFC-PERF] Standalone Audit Product enumeration: count = {allProducts.Count}, duration = {enumTimer.ElapsedMilliseconds} ms");

                var filterTimer = Stopwatch.StartNew();
                var allAuditableProducts = allProducts.Where(IsAuditableProduct).ToList();
                filterTimer.Stop();
                Debug.WriteLine($"[IFC-PERF] Standalone Audit IIfcElement filtering: count = {allAuditableProducts.Count}, duration = {filterTimer.ElapsedMilliseconds} ms");

                var largeModelSummaryMode = allAuditableProducts.Count > LargeModelSummaryThreshold;
                if (largeModelSummaryMode)
                {
                    result.Metadata.Warnings.Add("[IFC-AUDIT] Large model summary mode enabled; expensive per-product diagnostic loops are bounded. Import data is unchanged.");
                    IfcImportProfiler.Record(
                        "RunningDiagnosticsAudit",
                        "Audit_LargeModelSummaryMode",
                        "Enabled",
                        DateTime.UtcNow,
                        DateTime.UtcNow,
                        0,
                        "PhysicalProductsCount",
                        allAuditableProducts.Count,
                        true);
                }

                ILookup<int, XbimShapeInstance>? shapeInstancesLookup = null;
                long shapeLookupBuildMs = 0;
                int shapeLookupCount = 0;
                int shapeLookupProductKeys = 0;
                var counters = new GeometryQueryCounters();

                var startLookup = DateTime.UtcNow;
                var lookupTimer = Stopwatch.StartNew();
                RecordAuditStageStarted("Audit_BuildShapeLookup", startLookup);
                if (geometryContext != null)
                {
                    try
                    {
                        shapeInstancesLookup = geometryContext.ShapeInstances().ToLookup(si => si.IfcProductLabel);
                        shapeLookupCount = shapeInstancesLookup.Sum(g => g.Count());
                        shapeLookupProductKeys = shapeInstancesLookup.Count;
                    }
                    catch (Exception ex)
                    {
                        result.Metadata.Warnings.Add($"Failed to pre-cache shape instances from geometry context: {ex.Message}");
                    }
                }
                lookupTimer.Stop();
                var endLookup = DateTime.UtcNow;
                shapeLookupBuildMs = lookupTimer.ElapsedMilliseconds;

                IfcImportProfiler.Record(
                    "RunningDiagnosticsAudit",
                    "Audit_BuildShapeLookup",
                    "Building shape lookup...",
                    startLookup,
                    endLookup,
                    shapeLookupBuildMs,
                    "ShapeLookupCount",
                    shapeLookupCount,
                    true);

                var productTimer = Stopwatch.StartNew();

                progress?.Report(40.0);
                var startRawType = DateTime.UtcNow;
                var rawTypeTimer = Stopwatch.StartNew();
                RecordAuditStageStarted("Audit_BuildRawTypeSummary", startRawType, "PhysicalProductsCount", allAuditableProducts.Count);
                result.RawTypes = BuildRawTypeSummary(
                    allAuditableProducts,
                    geometryContext,
                    shapeInstancesLookup,
                    result,
                    counters);

                AddGridAxisRawTypeSummary(model, result.RawTypes);
                rawTypeTimer.Stop();
                var endRawType = DateTime.UtcNow;

                IfcImportProfiler.Record(
                    "RunningDiagnosticsAudit",
                    "Audit_BuildRawTypeSummary",
                    "Building raw type summary...",
                    startRawType,
                    endRawType,
                    rawTypeTimer.ElapsedMilliseconds,
                    "RawTypesCount",
                    result.RawTypes.Count,
                    true);

                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(60.0);
                var startDecomp = DateTime.UtcNow;
                var decompTimer = Stopwatch.StartNew();
                RecordAuditStageStarted("Audit_BuildAssemblyDecomposition", startDecomp, "PhysicalProductsCount", allAuditableProducts.Count);
                result.Assemblies = BuildAssemblyDecompositionAudit(
                    model,
                    allAuditableProducts,
                    geometryContext,
                    shapeInstancesLookup,
                    result,
                    counters,
                    cancellationToken);
                decompTimer.Stop();
                var endDecomp = DateTime.UtcNow;

                IfcImportProfiler.Record(
                    "RunningDiagnosticsAudit",
                    "Audit_BuildAssemblyDecomposition",
                    "Building assembly decomposition...",
                    startDecomp,
                    endDecomp,
                    decompTimer.ElapsedMilliseconds,
                    "AssemblyCount",
                    result.Assemblies?.Details?.Count ?? 0,
                    true);

                cancellationToken.ThrowIfCancellationRequested();

                ProcessPhysicalElements(
                    allAuditableProducts,
                    model,
                    geometryContext,
                    result,
                    result.Metadata.LengthConversionFactor,
                    result.Metadata.VolumeConversionFactor,
                    result.Metadata.MassConversionFactor,
                    concreteDensityKgPerM3,
                    largeModelSummaryMode,
                    shapeInstancesLookup,
                    counters,
                    progress,
                    cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(90.0);
                var startRelations = DateTime.UtcNow;
                var relationsTimer = Stopwatch.StartNew();
                RecordAuditStageStarted("Audit_BuildAssemblyRelations", startRelations, "AssemblyCount", result.Assemblies?.Details?.Count ?? 0);
                result.AssemblyRelations = BuildAssemblyRelations(
                    model,
                    result,
                    geometryContext,
                    shapeInstancesLookup,
                    counters);
                relationsTimer.Stop();
                var endRelations = DateTime.UtcNow;

                IfcImportProfiler.Record(
                    "RunningDiagnosticsAudit",
                    "Audit_BuildAssemblyRelations",
                    largeModelSummaryMode
                        ? "Building assembly relations fully because normalization/import depends on relationship rollups..."
                        : "Building assembly relations...",
                    startRelations,
                    endRelations,
                    relationsTimer.ElapsedMilliseconds,
                    "AssemblyRelationsCount",
                    result.AssemblyRelations?.Count ?? 0,
                    true);
                productTimer.Stop();
                totalTimer.Stop();

                result.Metadata.Warnings.Add($"[PERF] ShapeLookupBuildMs: {shapeLookupBuildMs}");
                result.Metadata.Warnings.Add($"[PERF] ShapeLookupCount: {shapeLookupCount}");
                result.Metadata.Warnings.Add($"[PERF] ShapeLookupProductKeys: {shapeLookupProductKeys}");
                result.Metadata.Warnings.Add($"[PERF] ProductShapeLookupMs: {productTimer.ElapsedMilliseconds}");
                result.Metadata.Warnings.Add($"[PERF] DirectFallbackShapeQueriesCount: {counters.DirectFallbackShapeQueriesCount}");
                result.Metadata.Warnings.Add($"[PERF] GeometryQueryFailuresCount: {counters.GeometryQueryFailuresCount}");
                result.Metadata.Warnings.Add($"[PERF] AuditTotalMs: {totalTimer.ElapsedMilliseconds}");

                IfcImportProfiler.Record(
                    "RunningDiagnosticsAudit",
                    "Audit_TryGetGeometryCenter_Total",
                    "Cumulative duration for TryGetGeometryCenter",
                    totalTimerStart,
                    DateTime.UtcNow,
                    counters.TryGetGeometryCenterTotalMs,
                    "Count",
                    0,
                    true);

                IfcImportProfiler.Record(
                    "RunningDiagnosticsAudit",
                    "Audit_HasDirectGeometry_Total",
                    "Cumulative duration for HasDirectGeometry",
                    totalTimerStart,
                    DateTime.UtcNow,
                    counters.HasDirectGeometryTotalMs,
                    "Count",
                    0,
                    true);

                IfcImportProfiler.Record(
                    "RunningDiagnosticsAudit",
                    "Audit_Completed",
                    "Diagnostics audit completed",
                    totalTimerStart,
                    DateTime.UtcNow,
                    totalTimer.ElapsedMilliseconds,
                    "WarningsCount",
                    result.Metadata.Warnings.Count,
                    true);

                progress?.Report(100.0);
            }
            catch (Exception ex)
            {
                totalTimer.Stop();
                IfcImportProfiler.Record(
                    "RunningDiagnosticsAudit",
                    "Audit_Failed",
                    "Diagnostics audit failed",
                    totalTimerStart,
                    DateTime.UtcNow,
                    totalTimer.ElapsedMilliseconds,
                    "Count",
                    0,
                    false,
                    ex.GetType().Name,
                    ex.Message);
                throw;
            }
        }

        private static void RecordAuditStageStarted(
            string subStage,
            DateTime startedUtc,
            string countName = "Count",
            long countValue = 0)
        {
            IfcImportProfiler.Record(
                "RunningDiagnosticsAudit",
                subStage,
                "Started",
                startedUtc,
                startedUtc,
                0,
                countName,
                countValue,
                false);
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

        private static string NormaliseGuid(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid)) return string.Empty;
            guid = guid.Trim().Trim('\'', '"');
            if (guid.Length == 22)
            {
                try
                {
                    var converterType = typeof(XbimPoint3D).Assembly.GetType("Xbim.Common.GuidConverter");
                    if (converterType != null)
                    {
                        var toGuidMethod = converterType.GetMethod("ToGuid", new[] { typeof(string) });
                        if (toGuidMethod != null)
                        {
                            var res = toGuidMethod.Invoke(null, new object[] { guid });
                            if (res is Guid g)
                            {
                                return g.ToString("D");
                            }
                        }
                    }
                }
                catch
                {
                    return guid.ToLowerInvariant();
                }
            }
            if (Guid.TryParse(guid, out var parsed))
            {
                return parsed.ToString("D");
            }
            return guid.ToLowerInvariant();
        }

        private static string? GetPropertyString(IIfcProduct product, params string[] keywords)
        {
            var propertySets = product.IsDefinedBy
                .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet)
                .Select(r => (IIfcPropertySet)r.RelatingPropertyDefinition);

            foreach (var propertySet in propertySets)
            {
                foreach (var property in propertySet.HasProperties.OfType<IIfcPropertySingleValue>())
                {
                    var name = property.Name.ToString();
                    if (keywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    {
                        var val = property.NominalValue?.Value?.ToString();
                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            return val;
                        }
                    }
                }
            }

            return null;
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

        private static List<RawIfcTypeSummary> BuildRawTypeSummary(
            List<IIfcProduct> physicalProducts,
            Xbim3DModelContext? geometryContext,
            ILookup<int, XbimShapeInstance>? shapeInstancesLookup,
            IfcAuditResult result,
            GeometryQueryCounters counters)
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
                            return GetProductShapeInstances(
                                p,
                                geometryContext,
                                shapeInstancesLookup,
                                result,
                                counters).Any();
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
            Xbim3DModelContext? geometryContext,
            ILookup<int, XbimShapeInstance>? shapeInstancesLookup,
            IfcAuditResult result,
            GeometryQueryCounters counters,
            CancellationToken cancellationToken)
        {
            var audit = new AssemblyDecompositionAudit();
            var assemblies = model.Instances.OfType<IIfcElementAssembly>().ToList();
            audit.TotalAssemblyCount = assemblies.Count;

            var detailsMap = new Dictionary<string, AssemblyDecompositionDetail>();
            var assemblyMap = new Dictionary<string, IIfcElementAssembly>();

            foreach (var assembly in assemblies)
            {
                var normalisedParent = NormaliseGuid(assembly.GlobalId.ToString());
                assemblyMap[normalisedParent] = assembly;

                var detail = new AssemblyDecompositionDetail
                {
                    AssemblyGlobalId = assembly.GlobalId.ToString(),
                    AssemblyName = assembly.Name?.ToString() ?? "-",
                    NestingRelationshipType = "IfcRelAggregates"
                };
                detailsMap[normalisedParent] = detail;
                audit.Details.Add(detail);
            }

            var childToSourceMap = new Dictionary<string, (string ParentGuid, string Source)>();

            foreach (var assembly in assemblies)
            {
                var normalisedParent = NormaliseGuid(assembly.GlobalId.ToString());

                // Find aggregated children
                var aggChildren = assembly.IsDecomposedBy?
                    .SelectMany(rel => rel.RelatedObjects)
                    .OfType<IIfcProduct>()
                    .Where(IsPhysicalProduct)
                    .ToList() ?? new List<IIfcProduct>();

                foreach (var child in aggChildren)
                {
                    var childGuid = child.GlobalId.ToString();
                    var normChild = NormaliseGuid(childGuid);
                    if (!childToSourceMap.ContainsKey(normChild))
                    {
                        childToSourceMap[normChild] = (normalisedParent, "IfcDecomposition");
                    }
                }

                // Find nested children
                var nestChildren = (assembly as IIfcElement)?.IsNestedBy?
                    .SelectMany(rel => rel.RelatedObjects)
                    .OfType<IIfcProduct>()
                    .Where(IsPhysicalProduct)
                    .ToList() ?? new List<IIfcProduct>();

                foreach (var child in nestChildren)
                {
                    var childGuid = child.GlobalId.ToString();
                    var normChild = NormaliseGuid(childGuid);
                    if (!childToSourceMap.ContainsKey(normChild))
                    {
                        childToSourceMap[normChild] = (normalisedParent, "IfcRelNests");
                    }
                }
            }

            var parentGuidKeywords = new[] { "Assembly GUID", "Parent Assembly GUID", "Cast Unit GUID", "AssemblyGlobalId", "Parent GUID", "AssemblyGUID", "CastUnitGUID", "ParentAssemblyGUID" };

            foreach (var product in physicalProducts)
            {
                if (product is IIfcElementAssembly) continue;

                var childGuid = product.GlobalId.ToString();
                var normChild = NormaliseGuid(childGuid);

                // If not already parented, check Tekla custom parent property
                if (!childToSourceMap.ContainsKey(normChild))
                {
                    var parentGuidVal = GetPropertyString(product, parentGuidKeywords);
                    if (!string.IsNullOrWhiteSpace(parentGuidVal))
                    {
                        var normParent = NormaliseGuid(parentGuidVal);
                        if (detailsMap.ContainsKey(normParent))
                        {
                            childToSourceMap[normChild] = (normParent, "TeklaCastUnit");
                        }
                    }
                }
            }

            foreach (var assembly in assemblies)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var normalisedParent = NormaliseGuid(assembly.GlobalId.ToString());
                var detail = detailsMap[normalisedParent];

                // Gather all children for this assembly from the map
                var childProducts = physicalProducts
                    .Where(p => !(p is IIfcElementAssembly) && childToSourceMap.TryGetValue(NormaliseGuid(p.GlobalId.ToString()), out var mapping) && mapping.ParentGuid == normalisedParent)
                    .ToList();

                detail.DirectChildCount = childProducts.Count;

                var relSources = childProducts
                    .Select(p => childToSourceMap[NormaliseGuid(p.GlobalId.ToString())].Source)
                    .Distinct()
                    .ToList();

                if (relSources.Count == 1)
                {
                    detail.NestingRelationshipType = relSources[0] == "IfcDecomposition" ? "IfcRelAggregates" : relSources[0];
                }
                else if (relSources.Count > 1)
                {
                    detail.NestingRelationshipType = "Mixed";
                }
                else
                {
                    detail.NestingRelationshipType = "None";
                }

                var recursiveChildren = new List<IIfcProduct>();
                string relType = "IfcRelAggregates";
                int maxDepth = GetMaxNestingDepth(assembly, 1, recursiveChildren, ref relType);

                // Ensure all our resolved child products are in recursiveChildren
                foreach (var child in childProducts)
                {
                    if (!recursiveChildren.Any(rc => rc.GlobalId == child.GlobalId))
                    {
                        recursiveChildren.Add(child);
                    }
                }

                detail.RecursiveChildCount = recursiveChildren.Count;
                detail.NestingDepth = Math.Max(detail.NestingDepth, maxDepth - 1);

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
                detail.ParentHasGeometry = HasDirectGeometry(assembly, geometryContext, shapeInstancesLookup, result, counters);
                detail.ChildrenHaveGeometry = recursiveChildren.Any(c => HasDirectGeometry(c, geometryContext, shapeInstancesLookup, result, counters));

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

                    bool swallowed = true;
                    bool doubleCounting = detail.ParentHasGeometry && HasDirectGeometry(child, geometryContext, shapeInstancesLookup, result, counters);

                    var relationshipSource = childToSourceMap[NormaliseGuid(child.GlobalId.ToString())].Source;

                    detail.ChildDetails.Add(new AssemblyChildDetail
                    {
                        ChildGlobalId = child.GlobalId.ToString(),
                        ChildRawIfcType = childRawType,
                        ChildName = child.Name?.ToString() ?? "-",
                        HasGeometry = HasDirectGeometry(child, geometryContext, shapeInstancesLookup, result, counters),
                        DoubleCountingRisk = doubleCounting,
                        SwallowedRisk = swallowed,
                        RelationshipSource = relationshipSource
                    });

                    if (doubleCounting) audit.DoubleCountingRisksCount++;
                    if (swallowed) audit.ChildrenSwallowedCount++;
                }
            }

            return audit;
        }

        private static List<AssemblyRelationshipInfo> BuildAssemblyRelations(
            IfcStore model,
            IfcAuditResult auditResult,
            Xbim3DModelContext? geometryContext,
            ILookup<int, XbimShapeInstance>? shapeInstancesLookup,
            GeometryQueryCounters counters)
        {
            var relations = new List<AssemblyRelationshipInfo>();

            var classificationMap = auditResult.Classifications.ToDictionary(c => c.GlobalId, c => c);
            var weightMap = auditResult.Weights.ToDictionary(w => w.GlobalId, w => w);
            var mainPartKeywords = new[] { "IsMainPart", "MainPart", "Main Part", "IsMain", "CastUnitMainPart" };
            var productMap = model.Instances.OfType<IIfcProduct>().ToDictionary(p => p.GlobalId.ToString(), p => p);

            foreach (var assemblyDetail in auditResult.Assemblies.Details)
            {
                var rel = new AssemblyRelationshipInfo
                {
                    ParentGlobalId = assemblyDetail.AssemblyGlobalId,
                    ParentName = assemblyDetail.AssemblyName,
                    ParentGlobalIdSource = assemblyDetail.NestingRelationshipType
                };

                if (classificationMap.TryGetValue(assemblyDetail.AssemblyGlobalId, out var parentClassInfo))
                {
                    var markDiag = auditResult.AssemblyMarkDiagnostics.FirstOrDefault(m => m.GlobalId == assemblyDetail.AssemblyGlobalId);
                    rel.AssemblyMark = markDiag?.ResolvedAssemblyMark ?? parentClassInfo.Name ?? "-";
                }

                var candidateMainParts = new List<AssemblyGraphNode>();
                var explicitMainParts = new List<AssemblyGraphNode>();

                foreach (var childDetail in assemblyDetail.ChildDetails)
                {
                    var childNode = new AssemblyGraphNode
                    {
                        ChildGlobalId = childDetail.ChildGlobalId,
                        ChildName = childDetail.ChildName,
                        ChildIfcClass = childDetail.ChildRawIfcType
                    };

                    if (classificationMap.TryGetValue(childDetail.ChildGlobalId, out var classInfo))
                    {
                        childNode.ChildCategory = classInfo.CandidateResolvedCategory;
                    }
                    if (weightMap.TryGetValue(childDetail.ChildGlobalId, out var weightInfo))
                    {
                        childNode.ChildWeightTons = weightInfo.WeightStandardValueTons;
                    }

                    var ifcClass = childNode.ChildIfcClass.ToUpperInvariant();
                    var name = childNode.ChildName.ToUpperInvariant();
                    var category = childNode.ChildCategory.ToUpperInvariant();

                    if (ifcClass == "IFCGRID" || ifcClass == "IFCANNOTATION" || ifcClass == "IFCOPENINGELEMENT" || ifcClass == "IFCVIRTUALELEMENT" || ifcClass == "IFCSPACE" ||
                        name.Contains("GRID") || name.Contains("OPENING") || name.Contains("VIRTUAL") || name.Contains("ANNOTATION") || name.Contains("SPACE") ||
                        category.Contains("GRID") || category.Contains("OPENING") || category.Contains("VIRTUAL") || category.Contains("ANNOTATION") || category.Contains("SPACE"))
                    {
                        childNode.ChildClassification = AssemblyChildClassification.IgnoredReference;
                        childNode.Reason = "IFC class or name hint matched IgnoredReference";
                        rel.IgnoredReferenceCount++;
                    }
                    else if (ifcClass == "IFCREINFORCINGBAR" || ifcClass == "IFCREINFORCINGMESH" || ifcClass == "IFCREINFORCINGELEMENT" || ifcClass == "IFCTENDON" || ifcClass == "IFCTENDONANCHOR" ||
                             name.Contains("REBAR") || name.Contains("REINFORCEMENT") || name.Contains("MESH") || name.Contains("TENDON") || name.Contains("STRAND") || name.Contains("REINFORCING") ||
                             category.Contains("REBAR") || category.Contains("REINFORCEMENT") || category.Contains("MESH") || category.Contains("TENDON") || category.Contains("STRAND") || category.Contains("REINFORCING"))
                    {
                        childNode.ChildClassification = AssemblyChildClassification.HiddenWeightContributor;
                        childNode.Reason = "IFC class or name hint matched HiddenWeightContributor";
                        rel.HiddenWeightContributorCount++;
                    }
                    else if (ifcClass == "IFCMECHANICALFASTENER" || ifcClass == "IFCDISCRETEACCESSORY" ||
                             name.Contains("PLATE") || name.Contains("INSERT") || name.Contains("ANCHOR") || name.Contains("BOLT") || name.Contains("WELD") || name.Contains("CONNECTION") || name.Contains("GROUT") || name.Contains("PAINT") || name.Contains("FASTENER") || name.Contains("ACCESSORY") ||
                             category.Contains("PLATE") || category.Contains("INSERT") || category.Contains("ANCHOR") || category.Contains("BOLT") || category.Contains("WELD") || category.Contains("CONNECTION") || category.Contains("GROUT") || category.Contains("PAINT") || category.Contains("FASTENER") || category.Contains("ACCESSORY"))
                    {
                        childNode.ChildClassification = AssemblyChildClassification.AccessoryWeightContributor;
                        childNode.Reason = "IFC class or name hint matched AccessoryWeightContributor";
                        rel.AccessoryWeightContributorCount++;
                    }
                    else
                    {
                        childNode.ChildClassification = AssemblyChildClassification.VisibleSecondaryPart;
                        childNode.Reason = "Structural element (candidate for MainPart)";
                        rel.VisibleSecondaryPartCount++;
                        candidateMainParts.Add(childNode);
                    }

                    if (productMap.TryGetValue(childDetail.ChildGlobalId, out var childProduct))
                    {
                        var explicitProp = GetPropertyString(childProduct, mainPartKeywords);
                        if (!string.IsNullOrWhiteSpace(explicitProp) &&
                            (explicitProp.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                             explicitProp.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                             explicitProp.Equals("yes", StringComparison.OrdinalIgnoreCase)))
                        {
                            explicitMainParts.Add(childNode);
                        }
                    }

                    rel.Children.Add(childNode);
                }

                rel.ChildCount = rel.Children.Count;

                if (explicitMainParts.Count == 1)
                {
                    var mainNode = explicitMainParts[0];
                    mainNode.ChildClassification = AssemblyChildClassification.MainPart;
                    mainNode.Reason = "Explicitly defined as MainPart in IFC properties";

                    rel.MainPartGlobalId = mainNode.ChildGlobalId;
                    rel.MainPartName = mainNode.ChildName;
                    rel.MainPartCategory = mainNode.ChildCategory;
                    rel.MainPartDetectionMethod = AssemblyMainPartDecision.ExplicitMainPart;

                    rel.VisibleSecondaryPartCount = Math.Max(0, rel.VisibleSecondaryPartCount - 1);
                }
                else if (explicitMainParts.Count > 1)
                {
                    rel.Warnings.Add("MultipleMainPartsDetected");
                    foreach (var mainNode in explicitMainParts)
                    {
                        mainNode.ChildClassification = AssemblyChildClassification.MainPart;
                        mainNode.Reason = "Conflict: Multiple explicit MainParts defined";
                    }

                    var mainNodeSelect = explicitMainParts[0];
                    rel.MainPartGlobalId = mainNodeSelect.ChildGlobalId;
                    rel.MainPartName = mainNodeSelect.ChildName;
                    rel.MainPartCategory = mainNodeSelect.ChildCategory;
                    rel.MainPartDetectionMethod = AssemblyMainPartDecision.ExplicitMainPart;

                    rel.VisibleSecondaryPartCount = Math.Max(0, rel.VisibleSecondaryPartCount - explicitMainParts.Count);
                }
                else
                {
                    if (candidateMainParts.Count == 1)
                    {
                        var mainNode = candidateMainParts[0];
                        mainNode.ChildClassification = AssemblyChildClassification.MainPart;
                        mainNode.Reason = "Inferred as MainPart (single structural candidate)";

                        rel.MainPartGlobalId = mainNode.ChildGlobalId;
                        rel.MainPartName = mainNode.ChildName;
                        rel.MainPartCategory = mainNode.ChildCategory;
                        rel.MainPartDetectionMethod = AssemblyMainPartDecision.InferredHighConfidence;

                        rel.VisibleSecondaryPartCount = 0;
                    }
                    else if (candidateMainParts.Count > 1)
                    {
                        var sortedCandidates = candidateMainParts
                            .OrderByDescending(c => c.ChildWeightTons ?? 0)
                            .ToList();

                        var heaviest = sortedCandidates[0];
                        var runnerUp = sortedCandidates[1];

                        double heaviestW = heaviest.ChildWeightTons ?? 0;
                        double runnerUpW = runnerUp.ChildWeightTons ?? 0;

                        bool clearlyHeaviest = heaviestW >= 1.5 * runnerUpW && heaviestW > 0;
                        bool aligns = CategoryAligns(heaviest.ChildCategory, rel.ParentName) || CategoryAligns(heaviest.ChildCategory, rel.AssemblyMark);
                        bool competingSimilar = Math.Abs(heaviestW - runnerUpW) < 0.01 && heaviest.ChildCategory.Equals(runnerUp.ChildCategory, StringComparison.OrdinalIgnoreCase);

                        if (clearlyHeaviest && aligns && !competingSimilar)
                        {
                            heaviest.ChildClassification = AssemblyChildClassification.MainPart;
                            heaviest.Reason = $"Inferred as MainPart (clearly heaviest: {heaviestW:F3}t vs runner-up: {runnerUpW:F3}t, category aligns)";

                            rel.MainPartGlobalId = heaviest.ChildGlobalId;
                            rel.MainPartName = heaviest.ChildName;
                            rel.MainPartCategory = heaviest.ChildCategory;
                            rel.MainPartDetectionMethod = AssemblyMainPartDecision.InferredHighConfidence;

                            rel.VisibleSecondaryPartCount = Math.Max(0, rel.VisibleSecondaryPartCount - 1);
                        }
                        else
                        {
                            rel.MainPartDetectionMethod = AssemblyMainPartDecision.InferredLowConfidence;
                            rel.Warnings.Add("NoMainPartInferred");

                            heaviest.ChildClassification = AssemblyChildClassification.MainPart;
                            heaviest.Reason = $"Inferred as MainPart with low confidence (heaviest: {heaviestW:F3}t, runner-up: {runnerUpW:F3}t)";

                            rel.MainPartGlobalId = heaviest.ChildGlobalId;
                            rel.MainPartName = heaviest.ChildName;
                            rel.MainPartCategory = heaviest.ChildCategory;

                            rel.VisibleSecondaryPartCount = Math.Max(0, rel.VisibleSecondaryPartCount - 1);
                        }
                    }
                    else
                    {
                        rel.MainPartDetectionMethod = AssemblyMainPartDecision.NotDetectable;
                    }
                }

                if (rel.MainPartGlobalId != null && rel.Children.Any(c => c.ChildClassification == AssemblyChildClassification.VisibleSecondaryPart))
                {
                    var mainNode = rel.Children.First(c => c.ChildGlobalId == rel.MainPartGlobalId);
                    bool hasDifferentCategory = rel.Children.Any(c => c.ChildClassification == AssemblyChildClassification.VisibleSecondaryPart && !c.ChildCategory.Equals(mainNode.ChildCategory, StringComparison.OrdinalIgnoreCase));
                    if (hasDifferentCategory)
                    {
                        rel.Warnings.Add("MixedCategoryButValidAssembly");
                    }
                }

                // Phase 4 Rollup calculation
                double totalWeight = 0.0;
                string weightSource = "";
                string weightWarning = "";

                double parentWeight = 0.0;
                bool hasParentExplicitWeight = false;

                if (weightMap.TryGetValue(assemblyDetail.AssemblyGlobalId, out var parentWeightInfo))
                {
                    parentWeight = parentWeightInfo.WeightStandardValueTons ?? 0.0;
                    hasParentExplicitWeight = parentWeightInfo.WeightStatus == "ExplicitPositive" ||
                                              parentWeightInfo.WeightSource.Contains("Explicit");
                }

                // Sum of eligible children: MainPart, VisibleSecondaryPart, HiddenWeightContributor, AccessoryWeightContributor
                var eligibleContributors = rel.Children
                    .Where(c => c.ChildClassification != AssemblyChildClassification.IgnoredReference)
                    .ToList();

                double childWeightSum = eligibleContributors.Sum(c => c.ChildWeightTons ?? 0.0);

                if (hasParentExplicitWeight && parentWeight > 0.0)
                {
                    totalWeight = parentWeight;
                    weightSource = "ParentExplicitWeight";

                    if (childWeightSum > 0.0)
                    {
                        double diff = Math.Abs(parentWeight - childWeightSum);
                        double max1Pct = Math.Max(0.1, parentWeight * 0.01);
                        double max5Pct = Math.Max(0.5, parentWeight * 0.05);

                        if (diff > max5Pct)
                        {
                            weightWarning = $"SevereWeightMismatch: Parent explicit weight ({parentWeight:F4}t) differs from child sum ({childWeightSum:F4}t) by {diff:F4}t (>5%).";
                            rel.Warnings.Add("SevereWeightMismatch");
                        }
                        else if (diff > max1Pct)
                        {
                            weightWarning = $"WeightMismatchWarning: Parent explicit weight ({parentWeight:F4}t) differs from child sum ({childWeightSum:F4}t) by {diff:F4}t (>1%).";
                            rel.Warnings.Add("WeightMismatchWarning");
                        }
                    }
                }
                else
                {
                    totalWeight = childWeightSum;
                    weightSource = "ChildSumRollup";
                }

                rel.ParentExplicitWeight = hasParentExplicitWeight && parentWeight > 0.0 ? parentWeight : (double?)null;
                rel.ChildWeightSum = childWeightSum > 0.0 ? childWeightSum : (double?)null;
                rel.AssemblyLiftWeight = totalWeight > 0.0 ? totalWeight : (double?)null;
                rel.WeightSourceUsed = weightSource;
                rel.WeightMismatchWarning = weightWarning;

                // COG Rollup Calculation (using physical child COGs)
                double weightedSumX = 0.0;
                double weightedSumY = 0.0;
                double weightedSumZ = 0.0;
                double totalCogWeight = 0.0;
                string cogSource = "";
                string cogWarning = "";

                var mainNodeObj = rel.Children.FirstOrDefault(c => string.Equals(c.ChildGlobalId, rel.MainPartGlobalId, StringComparison.Ordinal));
                (double X, double Y, double Z)? mainPartCog = null;

                if (mainNodeObj != null && productMap.TryGetValue(mainNodeObj.ChildGlobalId, out var mainProduct))
                {
                    var center = TryGetGeometryCenter(
                        mainProduct,
                        geometryContext,
                        shapeInstancesLookup,
                        auditResult,
                        auditResult.Metadata.LengthConversionFactor,
                        counters);
                    if (center.X.HasValue && center.Y.HasValue && center.Z.HasValue)
                    {
                        mainPartCog = (center.X.Value, center.Y.Value, center.Z.Value);
                    }
                }

                foreach (var childNode in eligibleContributors)
                {
                    double w = childNode.ChildWeightTons ?? 0.0;
                    if (w <= 0.0) continue;

                    if (productMap.TryGetValue(childNode.ChildGlobalId, out var childProduct))
                    {
                        var center = TryGetGeometryCenter(
                            childProduct,
                            geometryContext,
                            shapeInstancesLookup,
                            auditResult,
                            auditResult.Metadata.LengthConversionFactor,
                            counters);
                        if (center.X.HasValue && center.Y.HasValue && center.Z.HasValue)
                        {
                            weightedSumX += center.X.Value * w;
                            weightedSumY += center.Y.Value * w;
                            weightedSumZ += center.Z.Value * w;
                            totalCogWeight += w;
                        }
                        else
                        {
                            // Missing COG fallback rules
                            bool isHiddenOrAccessory = childNode.ChildClassification == AssemblyChildClassification.HiddenWeightContributor ||
                                                       childNode.ChildClassification == AssemblyChildClassification.AccessoryWeightContributor;
                            double pct = w / totalWeight;

                            if (isHiddenOrAccessory && pct <= 0.02 && mainPartCog != null)
                            {
                                weightedSumX += mainPartCog.Value.X * w;
                                weightedSumY += mainPartCog.Value.Y * w;
                                weightedSumZ += mainPartCog.Value.Z * w;
                                totalCogWeight += w;
                                cogWarning += $"HiddenAccessoryMissingCogFallback: Child '{childNode.ChildGlobalId}' lacks physical COG but is minor ({pct:P2}). Fell back to main part COG. ";
                            }
                            else
                            {
                                cogWarning += $"UnsafeCogRollup: Child '{childNode.ChildGlobalId}' has missing COG (weight={w:F4}t, {pct:P2}) and exceeds 2% threshold. ";
                                rel.Warnings.Add("UnsafeCogRollup");
                            }
                        }
                    }
                }

                if (totalCogWeight > 0.0)
                {
                    rel.AssemblyLiftCOG_X = weightedSumX / totalCogWeight;
                    rel.AssemblyLiftCOG_Y = weightedSumY / totalCogWeight;
                    rel.AssemblyLiftCOG_Z = weightedSumZ / totalCogWeight;
                    cogSource = "ChildWeightedSum";
                }
                else if (mainPartCog != null)
                {
                    rel.AssemblyLiftCOG_X = mainPartCog.Value.X;
                    rel.AssemblyLiftCOG_Y = mainPartCog.Value.Y;
                    rel.AssemblyLiftCOG_Z = mainPartCog.Value.Z;
                    cogSource = "MainPartFallback";
                    cogWarning += "NoPhysicalChildrenWithCog: Fallback to main part COG.";
                }
                else
                {
                    cogSource = "None";
                    cogWarning += "NoCogAvailable: Assembly lacks any coordinate references.";
                    rel.Warnings.Add("MissingAssemblyCOG");
                }

                rel.COGSource = cogSource;
                rel.COGWarnings = cogWarning.Trim();

                relations.Add(rel);
            }

            return relations;
        }

        private static bool CategoryAligns(string childCategory, string parentName)
        {
            if (string.IsNullOrWhiteSpace(parentName) || string.IsNullOrWhiteSpace(childCategory)) return false;
            parentName = parentName.ToUpperInvariant();
            childCategory = childCategory.ToUpperInvariant();
            if (childCategory.Contains("WALL") && (parentName.Contains("WALL") || parentName.Contains("W-"))) return true;
            if (childCategory.Contains("BEAM") && (parentName.Contains("BEAM") || parentName.Contains("B-"))) return true;
            if (childCategory.Contains("COLUMN") && (parentName.Contains("COLUMN") || parentName.Contains("C-"))) return true;
            if (childCategory.Contains("SLAB") && (parentName.Contains("SLAB") || parentName.Contains("S-"))) return true;
            if (childCategory.Contains("FOOTING") && (parentName.Contains("FOOTING") || parentName.Contains("F-"))) return true;
            return false;
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

        private sealed class GeometryQueryCounters
        {
            public int DirectFallbackShapeQueriesCount;
            public int GeometryQueryFailuresCount;
            public long TryGetGeometryCenterTotalMs;
            public long HasDirectGeometryTotalMs;
        }
        // Phase 4D: per-audit caches for production-critical ProcessPhysicalElements.
        // These caches are scoped to one audit call only; no static/session-crossing state.
        private sealed class AuditProcessingContext
        {
            public readonly Dictionary<(int EntityLabel, string LogicalField, IfcExporterProfile Profile), SourcePropertyResolutionResult> PropertyResolutionCache = new();
            public readonly Dictionary<(int EntityLabel, string QuantitySetName), bool> QuantitySetCache = new();
            public readonly Dictionary<(int EntityLabel, string PropertySetName, string PropertyName), string?> PropertyUnitCache = new();
            public readonly Dictionary<string, AssemblyDecompositionDetail> ParentAssemblyByChildGlobalId = new(StringComparer.Ordinal);
            public readonly Dictionary<string, AssemblyDecompositionDetail> AssemblyByGlobalId = new(StringComparer.Ordinal);

            public long ClassificationMs;
            public long WeightMs;
            public long VolumeMaterialMs;
            public long GeometryOwnershipMs;

            public AuditProcessingContext(AssemblyDecompositionAudit assemblies)
            {
                foreach (var assemblyDetail in assemblies.Details)
                {
                    if (!string.IsNullOrWhiteSpace(assemblyDetail.AssemblyGlobalId))
                    {
                        AssemblyByGlobalId[assemblyDetail.AssemblyGlobalId] = assemblyDetail;
                    }

                    foreach (var child in assemblyDetail.ChildDetails)
                    {
                        if (!string.IsNullOrWhiteSpace(child.ChildGlobalId) && !ParentAssemblyByChildGlobalId.ContainsKey(child.ChildGlobalId))
                        {
                            ParentAssemblyByChildGlobalId[child.ChildGlobalId] = assemblyDetail;
                        }
                    }
                }
            }
        }

        private static bool IsReinforcementDiagnosticType(IIfcProduct product)
        {
            var rawType = product.ExpressType?.Name ?? product.GetType().Name;
            var name = product.Name?.ToString() ?? string.Empty;
            var objectType = product.ObjectType?.ToString() ?? string.Empty;
            var combined = $"{rawType} {name} {objectType}".ToUpperInvariant();

            return rawType.Equals("IfcReinforcingBar", StringComparison.OrdinalIgnoreCase)
                || rawType.Equals("IfcReinforcingMesh", StringComparison.OrdinalIgnoreCase)
                || rawType.Equals("IfcReinforcingElement", StringComparison.OrdinalIgnoreCase)
                || rawType.Equals("IfcTendon", StringComparison.OrdinalIgnoreCase)
                || rawType.Equals("IfcTendonAnchor", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("REBAR")
                || combined.Contains("REINFORCEMENT")
                || combined.Contains("REINFORCING")
                || combined.Contains("MESH")
                || combined.Contains("TENDON")
                || combined.Contains("STRAND");
        }

        private static List<IIfcProduct> SelectLargeModelDiagnosticProducts(
            List<IIfcProduct> physicalProducts,
            bool largeModelSummaryMode,
            int sampleLimit)
        {
            if (!largeModelSummaryMode)
                return physicalProducts;

            return physicalProducts
                .Where(product => !IsReinforcementDiagnosticType(product))
                .Take(Math.Max(1, sampleLimit))
                .ToList();
        }

        private static void AddLargeModelTypeSummaryWarning(
            IfcAuditResult auditResult,
            string diagnosticName,
            IReadOnlyList<IIfcProduct> allProducts,
            IReadOnlyList<IIfcProduct> detailedProducts)
        {
            var skippedCount = Math.Max(0, allProducts.Count - detailedProducts.Count);
            var topTypes = allProducts
                .GroupBy(product => product.ExpressType?.Name ?? product.GetType().Name)
                .OrderByDescending(group => group.Count())
                .Take(10)
                .Select(group => $"{group.Key}:{group.Count()}");

            auditResult.Metadata.Warnings.Add(
                $"[IFC-AUDIT] {diagnosticName} summary mode: DetailedSamples={detailedProducts.Count}; SkippedOrAggregated={skippedCount}; TopTypes={string.Join(" | ", topTypes)}");
        }
        private static List<XbimShapeInstance> GetProductShapeInstances(
            IIfcProduct product,
            Xbim3DModelContext? geometryContext,
            ILookup<int, XbimShapeInstance>? shapeInstancesLookup,
            IfcAuditResult result,
            GeometryQueryCounters counters)
        {
            if (geometryContext == null)
            {
                return new List<XbimShapeInstance>();
            }

            try
            {
                if (shapeInstancesLookup != null)
                {
                    if (shapeInstancesLookup.Contains(product.EntityLabel))
                    {
                        return shapeInstancesLookup[product.EntityLabel].ToList();
                    }
                    return new List<XbimShapeInstance>();
                }

                counters.DirectFallbackShapeQueriesCount++;
                return geometryContext.ShapeInstancesOf(product).ToList();
            }
            catch (Exception ex)
            {
                counters.GeometryQueryFailuresCount++;

                var parentAgg = product.Decomposes?.FirstOrDefault()?.RelatingObject as IIfcProduct;
                var parentInfo = parentAgg != null
                    ? $"Parent Assembly: ID={parentAgg.GlobalId}, Label={parentAgg.EntityLabel}, Type={parentAgg.ExpressType?.Name}, Name={parentAgg.Name}"
                    : "Parent Assembly: None";

                var errorMsg = $"[GEOMETRY_ERROR] ShapeInstancesOf failed for Product:\n" +
                               $"  - GlobalId: {product.GlobalId}\n" +
                               $"  - EntityLabel: {product.EntityLabel}\n" +
                               $"  - IFC Type: {product.ExpressType?.Name ?? product.GetType().Name}\n" +
                               $"  - Name: {product.Name?.ToString() ?? "NULL"}\n" +
                               $"  - HasRepresentation: {product.Representation != null}\n" +
                               $"  - {parentInfo}\n" +
                               $"  - Exception: {ex.GetType().Name} - {ex.Message}";

                if (ex.InnerException != null)
                {
                    errorMsg += $"\n  - InnerException: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}";
                }

                Debug.WriteLine(errorMsg);
                Console.Error.WriteLine(errorMsg);

                result.Metadata.Warnings.Add($"Geometry query failure on Product {product.GlobalId} ({product.ExpressType?.Name ?? product.GetType().Name}): {ex.Message}");

                return new List<XbimShapeInstance>();
            }
        }

        private static bool HasDirectGeometry(
            IIfcProduct product,
            Xbim3DModelContext? geometryContext,
            ILookup<int, XbimShapeInstance>? shapeInstancesLookup,
            IfcAuditResult result,
            GeometryQueryCounters counters)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                return GetProductShapeInstances(
                    product,
                    geometryContext,
                    shapeInstancesLookup,
                    result,
                    counters).Any();
            }
            finally
            {
                counters.HasDirectGeometryTotalMs += sw.ElapsedMilliseconds;
            }
        }

        private void ProcessPhysicalElements(
            List<IIfcProduct> physicalProducts,
            IfcStore model,
            Xbim3DModelContext? geometryContext,
            IfcAuditResult auditResult,
            double lengthFactor,
            double volumeFactor,
            double? massFactor,
            double concreteDensityKgPerM3,
            bool largeModelSummaryMode,
            ILookup<int, XbimShapeInstance>? shapeInstancesLookup,
            GeometryQueryCounters counters,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
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
            var auditContext = new AuditProcessingContext(auditResult.Assemblies);

            if (largeModelSummaryMode)
            {
                auditResult.Metadata.Warnings.Add(
                    "[IFC-AUDIT] Large model summary mode keeps full classification/weight/volume/geometry audit because normalization/import depends on these collections. Only report-only name/mark diagnostics are bounded.");
                IfcImportProfiler.Record(
                    "RunningDiagnosticsAudit",
                    "Audit_ProcessPhysicalElements_SummaryMode",
                    "Full production-critical audit retained; import data unchanged",
                    DateTime.UtcNow,
                    DateTime.UtcNow,
                    0,
                    "PhysicalProductsCount",
                    physicalProducts.Count,
                    true,
                    currentIndex: 0,
                    totalCount: physicalProducts.Count);
            }

            var startLoop = DateTime.UtcNow;
            var loopTimer = Stopwatch.StartNew();
            IfcImportProfiler.Record(
                "RunningDiagnosticsAudit",
                "Audit_ProcessPhysicalElements",
                "Started",
                startLoop,
                startLoop,
                0,
                "PhysicalProductsCount",
                physicalProducts.Count,
                false,
                currentIndex: 0,
                totalCount: physicalProducts.Count);

            int throttleInterval = Math.Max(1, physicalProducts.Count / 100); // 1% steps
            DateTime lastHeartbeatUtc = startLoop;
            int lastHeartbeatPercentage = -1;
            double startPct = 65.0;
            double endPct = 85.0;
            var sectionTimer = new Stopwatch();

            for (int i = 0; i < physicalProducts.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var product = physicalProducts[i];
                var globalId = product.GlobalId.ToString();
                var rawTypeName = product.ExpressType?.Name ?? product.GetType().Name;
                var productName = product.Name?.ToString() ?? string.Empty;
                var objectType = product.ObjectType?.ToString() ?? string.Empty;
                var isSwallowed = swallowedGlobalIds.Contains(globalId);

                try
                {
                    // 1. Classification Audit - production-critical for normalization/import.
                    sectionTimer.Restart();
                    var resolvedCategory = ResolveCandidateCategory(rawTypeName, productName, objectType);
                    var classification = new ElementClassificationAuditDetail
                    {
                        GlobalId = globalId,
                        EntityLabel = product.EntityLabel,
                        Name = productName,
                        RawIfcType = rawTypeName,
                        ExpressTypeName = rawTypeName,
                        ObjectType = objectType,
                        PredefinedType = GetPredefinedType(product),
                        CandidateResolvedCategory = resolvedCategory,
                        CandidateReason = GetCategoryReason(rawTypeName, resolvedCategory),
                        CurrentUiShowAsAssembly = isSwallowed || (product is IIfcElementAssembly)
                    };
                    auditResult.Classifications.Add(classification);
                    sectionTimer.Stop();
                    auditContext.ClassificationMs += sectionTimer.ElapsedMilliseconds;

                    // 2. Weight Audit - production-critical for normalization/import.
                    sectionTimer.Restart();
                    var weightDetail = AuditWeight(auditContext, product, resolvedCategory, isSwallowed, concreteDensityKgPerM3, profile, massFactor);
                    auditResult.Weights.Add(weightDetail);
                    sectionTimer.Stop();
                    auditContext.WeightMs += sectionTimer.ElapsedMilliseconds;

                    // 3. Volume and Material Audit - production-critical for normalization/import.
                    sectionTimer.Restart();
                    var volMat = AuditVolumeMaterial(auditContext, product, volumeFactor, profile);
                    auditResult.VolumesMaterials.Add(volMat);
                    sectionTimer.Stop();
                    auditContext.VolumeMaterialMs += sectionTimer.ElapsedMilliseconds;

                    // 4. Geometry Ownership Audit - production-critical for visible geometry owner mapping.
                    sectionTimer.Restart();
                    auditContext.ParentAssemblyByChildGlobalId.TryGetValue(globalId, out var parentAssemblyDetail);
                    auditContext.AssemblyByGlobalId.TryGetValue(globalId, out var ownAssemblyDetail);
                    var geomDetail = AuditGeometryOwnership(
                        product,
                        geometryContext,
                        shapeInstancesLookup,
                        auditResult,
                        counters,
                        swallowedGlobalIds,
                        parentAssemblyDetail,
                        ownAssemblyDetail);
                    auditResult.Geometries.Add(geomDetail);
                    sectionTimer.Stop();
                    auditContext.GeometryOwnershipMs += sectionTimer.ElapsedMilliseconds;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[IFC-PERF-FAIL] Audit loop failure at index {i}/{physicalProducts.Count}. GlobalId={globalId}, Label={product.EntityLabel}, ExpressType={rawTypeName}: {ex.Message}");
                    IfcImportProfiler.Record(
                        "RunningDiagnosticsAudit",
                        "Audit_ProcessPhysicalElements",
                        "Failed",
                        startLoop,
                        DateTime.UtcNow,
                        loopTimer.ElapsedMilliseconds,
                        "PhysicalProductsCount",
                        physicalProducts.Count,
                        false,
                        ex.GetType().Name,
                        ex.Message,
                        i + 1,
                        physicalProducts.Count,
                        globalId,
                        product.EntityLabel,
                        rawTypeName);
                    throw;
                }

                DateTime heartbeatUtc = DateTime.UtcNow;
                int heartbeatPercentage = (int)Math.Floor(((i + 1d) / Math.Max(1, physicalProducts.Count)) * 100d);
                if (heartbeatPercentage > lastHeartbeatPercentage ||
                    heartbeatUtc - lastHeartbeatUtc >= TimeSpan.FromSeconds(10))
                {
                    IfcImportProfiler.Record(
                        "RunningDiagnosticsAudit",
                        "Audit_ProcessPhysicalElements",
                        "Heartbeat",
                        startLoop,
                        heartbeatUtc,
                        loopTimer.ElapsedMilliseconds,
                        "PhysicalProductsCount",
                        physicalProducts.Count,
                        false,
                        currentIndex: i + 1,
                        totalCount: physicalProducts.Count,
                        productGlobalId: globalId,
                        entityLabel: product.EntityLabel,
                        expressType: rawTypeName);
                    lastHeartbeatUtc = heartbeatUtc;
                    lastHeartbeatPercentage = heartbeatPercentage;
                }

                if (i % throttleInterval == 0 || i == physicalProducts.Count - 1)
                {
                    double currentPercentage = startPct + ((double)(i + 1) / Math.Max(1, physicalProducts.Count)) * (endPct - startPct);
                    progress?.Report(currentPercentage);
                }
            }
            loopTimer.Stop();
            var endLoop = DateTime.UtcNow;

            IfcImportProfiler.Record(
                "RunningDiagnosticsAudit",
                "Audit_ProcessPhysicalElements",
                "Completed",
                startLoop,
                endLoop,
                loopTimer.ElapsedMilliseconds,
                "PhysicalProductsCount",
                physicalProducts.Count,
                true,
                currentIndex: physicalProducts.Count,
                totalCount: physicalProducts.Count);

            RecordProcessPhysicalElementsSubStage("Audit_ProcessPhysicalElements_Classification", auditContext.ClassificationMs, physicalProducts.Count, endLoop);
            RecordProcessPhysicalElementsSubStage("Audit_ProcessPhysicalElements_Weight", auditContext.WeightMs, physicalProducts.Count, endLoop);
            RecordProcessPhysicalElementsSubStage("Audit_ProcessPhysicalElements_VolumeMaterial", auditContext.VolumeMaterialMs, physicalProducts.Count, endLoop);
            RecordProcessPhysicalElementsSubStage("Audit_ProcessPhysicalElements_GeometryOwnership", auditContext.GeometryOwnershipMs, physicalProducts.Count, endLoop);
            IfcImportProfiler.Record(
                "RunningDiagnosticsAudit",
                "Audit_ProcessPhysicalElements_PropertyResolutionCache",
                "Completed",
                endLoop,
                endLoop,
                0,
                "CacheEntries",
                auditContext.PropertyResolutionCache.Count,
                true,
                currentIndex: physicalProducts.Count,
                totalCount: physicalProducts.Count);

            cancellationToken.ThrowIfCancellationRequested();

            var startMark = DateTime.UtcNow;
            var markTimer = Stopwatch.StartNew();
            RecordAuditStageStarted("Audit_BuildAssemblyMarkDiagnostics", startMark, "PhysicalProductsCount", physicalProducts.Count);
            auditResult.RawNameDiagnostics = BuildRawNameDiagnostics(
                physicalProducts,
                model,
                largeModelSummaryMode,
                startMark,
                markTimer,
                cancellationToken);
            auditResult.AssemblyMarkDiagnostics = BuildAssemblyMarkDiagnostics(
                physicalProducts,
                largeModelSummaryMode,
                geometryContext,
                shapeInstancesLookup,
                auditResult,
                lengthFactor,
                profile,
                counters,
                startMark,
                markTimer,
                cancellationToken);
            markTimer.Stop();
            var endMark = DateTime.UtcNow;

            IfcImportProfiler.Record(
                "RunningDiagnosticsAudit",
                "Audit_BuildAssemblyMarkDiagnostics",
                largeModelSummaryMode ? "Building bounded assembly mark diagnostics summary..." : "Building assembly mark diagnostics...",
                startMark,
                endMark,
                markTimer.ElapsedMilliseconds,
                largeModelSummaryMode ? "AssemblyMarkDiagnosticsSampleCount" : "AssemblyMarkDiagnosticsCount",
                auditResult.AssemblyMarkDiagnostics.Count,
                true,
                currentIndex: auditResult.AssemblyMarkDiagnostics.Count,
                totalCount: largeModelSummaryMode ? physicalProducts.Count : auditResult.AssemblyMarkDiagnostics.Count);
        }

        private static void RecordProcessPhysicalElementsSubStage(string subStage, long elapsedMs, int productCount, DateTime completedUtc)
        {
            IfcImportProfiler.Record(
                "RunningDiagnosticsAudit",
                subStage,
                "Completed",
                completedUtc - TimeSpan.FromMilliseconds(elapsedMs),
                completedUtc,
                elapsedMs,
                "PhysicalProductsCount",
                productCount,
                true,
                currentIndex: productCount,
                totalCount: productCount);
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
            AuditProcessingContext context,
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

            var qRes = ResolveCachedProperty(context, product, "Weight", profile);
            if (qRes.Value != null)
            {
                if (double.TryParse(qRes.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedVal))
                {
                    detail.WeightRawValue = parsedVal;
                    detail.WeightPropertyName = qRes.SourcePropertyName;
                    detail.QuantitySetNameOrPropertySetName = qRes.SourcePropertySet;
                    detail.WeightUnitDetected = "Unspecified";

                    var isQty = IsQuantitySet(context, product, qRes.SourcePropertySet ?? string.Empty);
                    if (isQty)
                    {
                        detail.WeightSource = "IfcQuantityWeight.Instance";
                        detail.WeightUnitDetected = "kg";
                        detail.WeightStandardValueTons = UnitSystem.ToTons(parsedVal, MassUnit.Kilogram);
                    }
                    else
                    {
                        var unitName = GetPropertyUnit(context, product, qRes.SourcePropertySet ?? string.Empty, qRes.SourcePropertyName ?? string.Empty);
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
                double? volume = GetDirectVolume(context, product, profile);
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

        private double? GetDirectVolume(AuditProcessingContext context, IIfcProduct product, IfcExporterProfile profile)
        {
            var qRes = ResolveCachedProperty(context, product, "Volume", profile);
            if (qRes.Value != null && double.TryParse(qRes.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
            return null;
        }
        private ElementVolumeMaterialAuditDetail AuditVolumeMaterial(AuditProcessingContext context, IIfcProduct product, double volumeFactor, IfcExporterProfile profile)
        {
            var detail = new ElementVolumeMaterialAuditDetail();
            detail.GlobalId = product.GlobalId.ToString();

            var qRes = ResolveCachedProperty(context, product, "Volume", profile);
            if (qRes.Value != null)
            {
                if (double.TryParse(qRes.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                {
                    detail.VolumeRawValue = parsed;
                    detail.VolumeStandardValueM3 = parsed * volumeFactor;
                    var isQty = IsQuantitySet(context, product, qRes.SourcePropertySet ?? string.Empty);
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

        private SourcePropertyResolutionResult ResolveCachedProperty(
            AuditProcessingContext context,
            IIfcProduct product,
            string logicalField,
            IfcExporterProfile profile)
        {
            var safeField = logicalField ?? string.Empty;
            var key = (product.EntityLabel, safeField, profile);
            if (context.PropertyResolutionCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var resolved = _aliasResolver.ResolveProperty(product, safeField, profile);
            context.PropertyResolutionCache[key] = resolved;
            return resolved;
        }

        private static bool IsQuantitySet(AuditProcessingContext context, IIfcProduct product, string psetName)
        {
            if (string.IsNullOrWhiteSpace(psetName))
                return false;

            var key = (product.EntityLabel, psetName);
            if (context.QuantitySetCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var result = product.IsDefinedBy
                .Any(r => r.RelatingPropertyDefinition is IIfcElementQuantity eq && eq.Name.ToString().Equals(psetName, StringComparison.OrdinalIgnoreCase));
            context.QuantitySetCache[key] = result;
            return result;
        }

        private static string? GetPropertyUnit(
            AuditProcessingContext context,
            IIfcProduct product,
            string propertySetName,
            string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertySetName) || string.IsNullOrWhiteSpace(propertyName))
                return null;

            var key = (product.EntityLabel, propertySetName, propertyName);
            if (context.PropertyUnitCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            string? unitName = null;
            var propertySets = product.IsDefinedBy
                .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet)
                .Select(r => (IIfcPropertySet)r.RelatingPropertyDefinition);

            foreach (var pset in propertySets)
            {
                if (!pset.Name.ToString().Equals(propertySetName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var prop = pset.HasProperties.OfType<IIfcPropertySingleValue>()
                    .FirstOrDefault(p => p.Name.ToString().Equals(propertyName, StringComparison.OrdinalIgnoreCase));
                unitName = prop?.Unit?.ToString();
                break;
            }

            context.PropertyUnitCache[key] = unitName;
            return unitName;
        }
        private static ElementGeometryOwnershipAuditDetail AuditGeometryOwnership(
            IIfcProduct product,
            Xbim3DModelContext? geometryContext,
            ILookup<int, XbimShapeInstance>? shapeInstancesLookup,
            IfcAuditResult result,
            GeometryQueryCounters counters,
            HashSet<string> swallowedGlobalIds,
            AssemblyDecompositionDetail? parentAssembly,
            AssemblyDecompositionDetail? ownAssemblyDetail)
        {
            var detail = new ElementGeometryOwnershipAuditDetail
            {
                LogicalGlobalId = product.GlobalId.ToString()
            };

            var shapes = GetProductShapeInstances(
                product,
                geometryContext,
                shapeInstancesLookup,
                result,
                counters);

            detail.ShapeCount = shapes.Count;
            detail.HasGeometryDirectly = shapes.Count > 0;

            var isSwallowed = swallowedGlobalIds.Contains(detail.LogicalGlobalId);

            if (parentAssembly != null)
            {
                detail.GeometryOwnerCandidateGlobalId = parentAssembly.AssemblyGlobalId;
                detail.HasGeometryOnlyOnParent = parentAssembly.ParentHasGeometry && !detail.HasGeometryDirectly;
            }
            else
            {
                detail.GeometryOwnerCandidateGlobalId = detail.LogicalGlobalId;
            }

            if (product is IIfcElementAssembly)
            {
                var childrenHaveShapes = ownAssemblyDetail?.ChildrenHaveGeometry ?? false;
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

        private List<RawNameDiagnosticDetail> BuildRawNameDiagnostics(
            List<IIfcProduct> physicalProducts,
            IfcStore model,
            bool largeModelSummaryMode,
            DateTime stageStartedUtc,
            Stopwatch stageTimer,
            CancellationToken cancellationToken)
        {
            var profileResolver = new IfcExporterProfileResolver();
            var exporterProfile = profileResolver.ResolveProfile(model.Header?.FileName?.OriginatingSystem ?? string.Empty);
            var diagnosticProducts = SelectLargeModelDiagnosticProducts(physicalProducts, largeModelSummaryMode, LargeModelMarkDiagnosticSampleLimit);

            var list = new List<RawNameDiagnosticDetail>(diagnosticProducts.Count);
            DateTime lastHeartbeatUtc = stageStartedUtc;
            int lastHeartbeatPercentage = -1;
            for (int i = 0; i < diagnosticProducts.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IIfcProduct product = diagnosticProducts[i];
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
                DateTime heartbeatUtc = DateTime.UtcNow;
                int heartbeatPercentage = (int)Math.Floor(((i + 1d) / Math.Max(1, diagnosticProducts.Count)) * 100d);
                if (heartbeatPercentage > lastHeartbeatPercentage ||
                    heartbeatUtc - lastHeartbeatUtc >= TimeSpan.FromSeconds(10))
                {
                    IfcImportProfiler.Record(
                        "RunningDiagnosticsAudit",
                        "Audit_BuildAssemblyMarkDiagnostics",
                        largeModelSummaryMode ? "HeartbeatRawNameSummarySample" : "Heartbeat",
                        stageStartedUtc,
                        heartbeatUtc,
                        stageTimer.ElapsedMilliseconds,
                        largeModelSummaryMode ? "RawNameDiagnosticsSampleCount" : "RawNameDiagnosticsCount",
                        diagnosticProducts.Count,
                        false,
                        currentIndex: i + 1,
                        totalCount: largeModelSummaryMode ? physicalProducts.Count : diagnosticProducts.Count,
                        productGlobalId: product!.GlobalId.ToString(),
                        entityLabel: product.EntityLabel,
                        expressType: product.ExpressType?.Name ?? product.GetType().Name);
                    lastHeartbeatUtc = heartbeatUtc;
                    lastHeartbeatPercentage = heartbeatPercentage;
                }
            }
            return list;
        }
        private List<AssemblyMarkAuditDetail> BuildAssemblyMarkDiagnostics(
            List<IIfcProduct> physicalProducts,
            bool largeModelSummaryMode,
            Xbim3DModelContext? geometryContext,
            ILookup<int, XbimShapeInstance>? shapeInstancesLookup,
            IfcAuditResult auditResult,
            double lengthFactor,
            IfcExporterProfile profile,
            GeometryQueryCounters counters,
            DateTime stageStartedUtc,
            Stopwatch stageTimer,
            CancellationToken cancellationToken)
        {
            var diagnosticProducts = SelectLargeModelDiagnosticProducts(physicalProducts, largeModelSummaryMode, LargeModelMarkDiagnosticSampleLimit);

            if (largeModelSummaryMode)
            {
                AddLargeModelTypeSummaryWarning(auditResult, "Audit_BuildAssemblyMarkDiagnostics", physicalProducts, diagnosticProducts);
                IfcImportProfiler.Record(
                    "RunningDiagnosticsAudit",
                    "Audit_BuildAssemblyMarkDiagnostics_SummaryMode",
                    "Detailed assembly mark diagnostics are bounded for large model",
                    DateTime.UtcNow,
                    DateTime.UtcNow,
                    0,
                    "DetailedSampleCount",
                    diagnosticProducts.Count,
                    true,
                    currentIndex: diagnosticProducts.Count,
                    totalCount: physicalProducts.Count);
            }

            var weightsById = auditResult.Weights.ToDictionary(w => w.GlobalId, StringComparer.Ordinal);
            var materialById = auditResult.VolumesMaterials.ToDictionary(v => v.GlobalId, StringComparer.Ordinal);
            var classById = auditResult.Classifications.ToDictionary(c => c.GlobalId, StringComparer.Ordinal);
            var details = new List<AssemblyMarkAuditDetail>(diagnosticProducts.Count);
            var sourceSystem = profile == IfcExporterProfile.Tekla
                ? SourceSystem.Tekla.ToString()
                : profile == IfcExporterProfile.Revit
                    ? SourceSystem.Revit.ToString()
                    : SourceSystem.IFC.ToString();

            DateTime lastHeartbeatUtc = stageStartedUtc;
            int lastHeartbeatPercentage = -1;
            for (int i = 0; i < diagnosticProducts.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IIfcProduct product = diagnosticProducts[i];
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

                (double? X, double? Y, double? Z) cog = largeModelSummaryMode
                    ? (null, null, null)
                    : TryGetGeometryCenter(
                        product,
                        geometryContext,
                        shapeInstancesLookup,
                        auditResult,
                        lengthFactor,
                        counters);
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
                DateTime heartbeatUtc = DateTime.UtcNow;
                int heartbeatPercentage = (int)Math.Floor(((i + 1d) / Math.Max(1, diagnosticProducts.Count)) * 100d);
                if (heartbeatPercentage > lastHeartbeatPercentage ||
                    heartbeatUtc - lastHeartbeatUtc >= TimeSpan.FromSeconds(10))
                {
                    IfcImportProfiler.Record(
                        "RunningDiagnosticsAudit",
                        "Audit_BuildAssemblyMarkDiagnostics",
                        largeModelSummaryMode ? "HeartbeatAssemblyMarkSummarySample" : "Heartbeat",
                        stageStartedUtc,
                        heartbeatUtc,
                        stageTimer.ElapsedMilliseconds,
                        largeModelSummaryMode ? "AssemblyMarkDiagnosticsSampleCount" : "PhysicalProductsCount",
                        diagnosticProducts.Count,
                        false,
                        currentIndex: i + 1,
                        totalCount: largeModelSummaryMode ? physicalProducts.Count : diagnosticProducts.Count,
                        productGlobalId: globalId,
                        entityLabel: product.EntityLabel,
                        expressType: product.ExpressType?.Name ?? product.GetType().Name);
                    lastHeartbeatUtc = heartbeatUtc;
                    lastHeartbeatPercentage = heartbeatPercentage;
                }
            }

            foreach (var group in details
                .Where(d => !string.IsNullOrWhiteSpace(d.AssemblyGroupKey))
                .GroupBy(d => d.AssemblyGroupKey, StringComparer.OrdinalIgnoreCase))
            {
                var members = group.ToList();
                var memberIds = members.Select(m => m.GlobalId).OrderBy(id => id, StringComparer.Ordinal).ToList();
                var totalWeightTons = SumPositive(members.Select(m => m.ElementWeightTons));
                var totalWeightKg = totalWeightTons.HasValue ? totalWeightTons.Value * 1000.0 : (double?)null;
                var weightedCog = largeModelSummaryMode ? null : ComputeWeightedCog(members, totalWeightTons);
                var fallbackCog = largeModelSummaryMode ? null : ComputeAverageCog(members);
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
            Xbim3DModelContext? geometryContext,
            ILookup<int, XbimShapeInstance>? shapeInstancesLookup,
            IfcAuditResult result,
            double lengthFactor,
            GeometryQueryCounters counters)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var shapes = GetProductShapeInstances(
                    product,
                    geometryContext,
                    shapeInstancesLookup,
                    result,
                    counters);
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
            finally
            {
                counters.TryGetGeometryCenterTotalMs += sw.ElapsedMilliseconds;
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

        private static void GenerateAssemblyGraphCsvReport(IfcAuditResult auditResult, string csvPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("ParentGlobalId,ParentGlobalIdSource,AssemblyMark,MainPartGlobalId,MainPartName,MainPartCategory,MainPartDetectionMethod,ChildCount,VisibleSecondaryPartCount,HiddenWeightContributorCount,AccessoryWeightContributorCount,IgnoredReferenceCount,HasSecondaryParts,HasHiddenContributors,Warnings");

            foreach (var rel in auditResult.AssemblyRelations)
            {
                var warningsStr = rel.Warnings != null && rel.Warnings.Count > 0 ? string.Join("|", rel.Warnings) : string.Empty;
                sb.AppendLine(string.Join(",", new[]
                {
                    Csv(rel.ParentGlobalId),
                    Csv(rel.ParentGlobalIdSource),
                    Csv(rel.AssemblyMark),
                    Csv(rel.MainPartGlobalId),
                    Csv(rel.MainPartName),
                    Csv(rel.MainPartCategory),
                    Csv(rel.MainPartDetectionMethod.ToString()),
                    rel.ChildCount.ToString(CultureInfo.InvariantCulture),
                    rel.VisibleSecondaryPartCount.ToString(CultureInfo.InvariantCulture),
                    rel.HiddenWeightContributorCount.ToString(CultureInfo.InvariantCulture),
                    rel.AccessoryWeightContributorCount.ToString(CultureInfo.InvariantCulture),
                    rel.IgnoredReferenceCount.ToString(CultureInfo.InvariantCulture),
                    rel.HasSecondaryParts.ToString().ToLowerInvariant(),
                    rel.HasHiddenContributors.ToString().ToLowerInvariant(),
                    Csv(warningsStr)
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
            var assemblyGraphCsvPath = Path.Combine(outputDirectory, "debug_ifc_assembly_graph_classification_audit.csv");
            var assemblyRollupCsvPath = Path.Combine(outputDirectory, "debug_ifc_assembly_lift_unit_rollup_audit.csv");

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

            cancellationToken.ThrowIfCancellationRequested();
            GenerateAssemblyGraphCsvReport(auditResult, assemblyGraphCsvPath);

            cancellationToken.ThrowIfCancellationRequested();
            GenerateAssemblyLiftUnitRollupCsvReport(auditResult, assemblyRollupCsvPath);

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

        private static void GenerateAssemblyLiftUnitRollupCsvReport(IfcAuditResult auditResult, string csvPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("LiftUnitId,ParentAssemblyGlobalId,AssemblyMark,MainPartGlobalId,AssemblyMemberIds,ChildCount,VisibleSecondaryPartCount,HiddenWeightContributorCount,AccessoryWeightContributorCount,IgnoredReferenceCount,ParentExplicitWeight,ChildWeightSum,AssemblyLiftWeight,WeightSourceUsed,WeightMismatchWarning,AssemblyLiftCOG_X,AssemblyLiftCOG_Y,AssemblyLiftCOG_Z,COGSource,COGWarnings,HasSecondaryParts,HasHiddenContributors");

            foreach (var rel in auditResult.AssemblyRelations)
            {
                var liftUnitId = rel.MainPartGlobalId ?? rel.ParentGlobalId;
                var memberIds = string.Join(";", rel.Children.Select(c => c.ChildGlobalId));

                sb.AppendLine(string.Join(",", new[]
                {
                    Csv(liftUnitId),
                    Csv(rel.ParentGlobalId),
                    Csv(rel.AssemblyMark),
                    Csv(rel.MainPartGlobalId ?? string.Empty),
                    Csv(memberIds),
                    rel.ChildCount.ToString(CultureInfo.InvariantCulture),
                    rel.VisibleSecondaryPartCount.ToString(CultureInfo.InvariantCulture),
                    rel.HiddenWeightContributorCount.ToString(CultureInfo.InvariantCulture),
                    rel.AccessoryWeightContributorCount.ToString(CultureInfo.InvariantCulture),
                    rel.IgnoredReferenceCount.ToString(CultureInfo.InvariantCulture),
                    rel.ParentExplicitWeight?.ToString("F4", CultureInfo.InvariantCulture) ?? string.Empty,
                    rel.ChildWeightSum?.ToString("F4", CultureInfo.InvariantCulture) ?? string.Empty,
                    rel.AssemblyLiftWeight?.ToString("F4", CultureInfo.InvariantCulture) ?? string.Empty,
                    Csv(rel.WeightSourceUsed),
                    Csv(rel.WeightMismatchWarning),
                    rel.AssemblyLiftCOG_X?.ToString("F4", CultureInfo.InvariantCulture) ?? string.Empty,
                    rel.AssemblyLiftCOG_Y?.ToString("F4", CultureInfo.InvariantCulture) ?? string.Empty,
                    rel.AssemblyLiftCOG_Z?.ToString("F4", CultureInfo.InvariantCulture) ?? string.Empty,
                    Csv(rel.COGSource),
                    Csv(rel.COGWarnings),
                    rel.HasSecondaryParts.ToString(CultureInfo.InvariantCulture),
                    rel.HasHiddenContributors.ToString(CultureInfo.InvariantCulture)
                }));
            }

            File.WriteAllText(csvPath, sb.ToString(), Encoding.UTF8);
        }
    }
}
