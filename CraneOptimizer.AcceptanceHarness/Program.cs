using System.Security.Cryptography;
using System.Text.Json;
using CraneOptimizer.Infrastructure.Ifc;
using CraneOptimizer.Infrastructure.Repositories;
using Xbim.Ifc4.Interfaces;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: CraneOptimizer.AcceptanceHarness <fixture-dir> <evidence-json>");
    return 2;
}

var fixtureDir = Path.GetFullPath(args[0]);
var evidencePath = Path.GetFullPath(args[1]);
var main = Path.Combine(fixtureDir, "rev05_acceptance.ifc");
var negative = Directory.GetFiles(fixtureDir, "rev05_*unit*.ifc")
    .Where(p => !Path.GetFileName(p).Equals("rev05_acceptance.ifc", StringComparison.OrdinalIgnoreCase))
    .OrderBy(p => p)
    .ToList();

var checks = new List<object>();
var negativeEvidence = new List<object>();
var massTrace = new List<object>();
var passed = true;
void Check(string name, bool ok, object? detail = null)
{
    checks.Add(new { name, passed = ok, detail });
    if (!ok) passed = false;
}

try
{
    Check("native-engine-present",
        File.Exists(Path.Combine(AppContext.BaseDirectory, "Xbim.Geometry.Engine64.dll")),
        AppContext.BaseDirectory);

    using var sessions = new IfcModelSessionService();
    var first = sessions.OpenSession(main);
    var firstSessionId = first.SessionId;
    Check("geometry-created", first.ShapeInstanceCount >= 5, new { first.ShapeInstanceCount });

    var labels = first.GeometryContext.ShapeInstances()
        .Select(x => x.IfcProductLabel)
        .Where(x => x > 0)
        .Distinct()
        .ToHashSet();
    Check("geometry-resolves-product",
        first.Model.Instances.OfType<IIfcProduct>().Any(p => labels.Contains(p.EntityLabel)),
        new { geometryProductLabels = labels.Count });

    var selectedLabel = labels.OrderBy(x => x).FirstOrDefault();
    var selectedProduct = selectedLabel > 0
        ? first.Model.Instances.OfType<IIfcProduct>().FirstOrDefault(p => p.EntityLabel == selectedLabel)
        : null;
    Check("selection-product-roundtrip",
        selectedProduct is not null && !string.IsNullOrWhiteSpace(selectedProduct.GlobalId),
        selectedProduct is null ? null : new { selectedProduct.EntityLabel, selectedProduct.GlobalId, selectedProduct.Name });

    var audit = new IfcDiagnosticAuditService(sessions);
    var norm = new IfcNormalizationDecisionService(new IfcExporterProfileResolver());
    using var reader = new IfcReaderRepository(audit, norm, sessions);

    var mainAudit = await audit.RunAuditAsync(main);
    foreach (var w in mainAudit.Weights.Where(w => w.WeightRawValue.HasValue).OrderBy(w => w.GlobalId))
    {
        massTrace.Add(new
        {
            globalId = w.GlobalId,
            rawMass = w.WeightRawValue,
            detectedUnit = w.WeightUnitDetected,
            normalizedMassTons = w.WeightStandardValueTons,
            trustStatus = w.WeightUnitTrustStatus,
            unitConflict = w.HasUnitConflict,
            gaEligible = w.IsGaEligibleByUnit,
            source = w.WeightSource,
            property = w.WeightPropertyName,
            quantityOrPropertySet = w.QuantitySetNameOrPropertySetName
        });
    }

    Check("mass-trace-has-five-representative-elements",
        massTrace.Count == 5,
        new { count = massTrace.Count });
    Check("mass-trace-all-resolved-and-ga-eligible",
        mainAudit.Weights.Count(w => w.WeightRawValue.HasValue) == 5 &&
        mainAudit.Weights.Where(w => w.WeightRawValue.HasValue)
            .All(w => w.WeightStandardValueTons is > 0 && w.IsGaEligibleByUnit && !string.IsNullOrWhiteSpace(w.WeightUnitDetected) && !string.IsNullOrWhiteSpace(w.WeightUnitTrustStatus)),
        massTrace);

    var elements = reader.ReadElements(main)
        .Where(x => x.WeightTons is > 0)
        .ToDictionary(x => x.ElementID, x => x.WeightTons!.Value);
    var expected = new Dictionary<string, double>
    {
        ["3t3TDZl_D9NOIWB0BSjzJI"] = 8.5,
        ["1xS3BCk291UvhgP2a6eflT"] = 5.0,
        ["0YvctVUKr2A9XkQZxR2W3q"] = 4.0,
        ["3J0f$_Dhv7eA5qURx2A4lH"] = 3.0,
        ["1DA9$Kf2H1WhX8C0x7YV4F"] = 2.0
    };
    Check("mass-normalization",
        expected.All(kv => elements.TryGetValue(kv.Key, out var v) && Math.Abs(v - kv.Value) < 1e-4),
        elements);

    sessions.Clear();
    var reopened = sessions.OpenSession(main);
    Check("close-reopen",
        reopened.SessionId != firstSessionId && reopened.ShapeInstanceCount >= 5,
        new { reopened.ShapeInstanceCount });

    foreach (var path in negative)
    {
        var a = await audit.RunAuditAsync(path);
        var rows = a.Weights
            .Where(w => w.WeightRawValue.HasValue)
            .Select(w => new
            {
                globalId = w.GlobalId,
                rawMass = w.WeightRawValue,
                detectedUnit = w.WeightUnitDetected,
                normalizedMassTons = w.WeightStandardValueTons,
                trustStatus = w.WeightUnitTrustStatus,
                unitConflict = w.HasUnitConflict,
                gaEligible = w.IsGaEligibleByUnit
            })
            .ToList();
        var unresolved = rows.Any(w => !w.gaEligible);
        Check("negative-unit-fails-closed:" + Path.GetFileName(path), unresolved, rows);
        negativeEvidence.Add(new { file = Path.GetFileName(path), passed = unresolved, massTrace = rows });
    }
}
catch (Exception ex)
{
    Check("harness-exception", false, ex.ToString());
}

Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)!);
var output = new
{
    schemaVersion = 2,
    release = "REV05",
    fixtureType = "SOFTWARE ACCEPTANCE FIXTURE",
    passed,
    executedAtUtc = DateTime.UtcNow,
    host = Environment.OSVersion.ToString(),
    fixtureSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(main))).ToLowerInvariant(),
    massTrace,
    negativeEvidence,
    checks
};
await File.WriteAllTextAsync(
    evidencePath,
    JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
return passed ? 0 : 1;
