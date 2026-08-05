using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CraneOptimizer.Application.Reports.Services;
using CraneOptimizer.Application.Services;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;

public static class CanonicalLiftUnitSnapshotBuilder
{
    public const string LiftUnitDefinitionVersion = "precast-ga-lift-unit-v1";

    public static CurrentCanonicalModelSnapshotDto BuildCurrentSnapshot(
        IEnumerable<PrecastElement> sourceElements,
        string projectGuid = "",
        string? modelRevision = null,
        string liftMode = "CurrentCanonical",
        IEnumerable<PrecastElement>? sharedFieldSourceElements = null)
    {
        var allElements = sourceElements
            .Where(element => element != null)
            .ToList();
        var units = BuildCanonicalLiftUnits(allElements, sharedFieldSourceElements).ToList();
        var payloadHash = ComputePayloadHash(units);

        return new CurrentCanonicalModelSnapshotDto
        {
            CanonicalSnapshotId = BuildCanonicalSnapshotId(projectGuid, payloadHash),
            ProjectGuid = projectGuid,
            ModelFingerprint = payloadHash,
            ModelRevision = modelRevision,
            LiftMode = liftMode,
            LiftUnitDefinitionVersion = LiftUnitDefinitionVersion,
            CanonicalPayloadHash = payloadHash,
            CanonicalLiftUnits = units,
            ImportAnomalies = BuildImportAnomalies(allElements, units).ToList()
        };
    }

    public static IReadOnlyList<CanonicalLiftUnitSnapshotDto> BuildCanonicalLiftUnits(
        IEnumerable<PrecastElement> sourceElements,
        IEnumerable<PrecastElement>? sharedFieldSourceElements = null)
    {
        var elements = sourceElements
            .Where(element => element != null && !string.IsNullOrWhiteSpace(element.ElementID))
            .GroupBy(element => element.ElementID, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
        var sharedFieldSources = (sharedFieldSourceElements ?? Array.Empty<PrecastElement>())
            .Where(element => element != null && !string.IsNullOrWhiteSpace(element.ElementID))
            .GroupBy(element => element.ElementID, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var byId = elements.ToDictionary(element => element.ElementID, StringComparer.Ordinal);
        var selected = new List<PrecastElement>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var element in elements)
        {
            if (!IsCanonicalLiftUnitCandidate(element, byId))
                continue;

            var key = element.ElementID;
            if (seen.Add(key))
                selected.Add(element);
        }

        return selected
            .OrderBy(element => element.ElementID, StringComparer.Ordinal)
            .Select(element => MergeSharedFields(element, sharedFieldSources))
            .Select(element => ToCanonicalLiftUnit(element, elements, sharedFieldSources.Values))
            .ToList();
    }

    public static string ComputePayloadHash(IEnumerable<CanonicalLiftUnitSnapshotDto> units)
    {
        var payload = string.Join("\n", units
            .OrderBy(unit => unit.LiftUnitId, StringComparer.Ordinal)
            .Select(unit => unit.SharedDataHash));
        return HashText(payload);
    }

    public static string ComputeSharedDataHash(CanonicalLiftUnitSnapshotDto unit)
    {
        var physicalCog = GetPhysicalCogPoint(unit);
        var parts = new[]
        {
            unit.LiftUnitId,
            unit.ElementMark,
            unit.ElementName,
            unit.ElementType,
            unit.MaterialType,
            unit.MaterialGrade ?? string.Empty,
            Number(unit.LengthM),
            Number(unit.WidthM),
            Number(unit.HeightM),
            Number(unit.VolumeM3),
            Number(unit.LiftWeightTon),
            Number(physicalCog?.X),
            Number(physicalCog?.Y),
            Number(physicalCog?.Z),
            unit.ParentAssemblyGlobalId ?? string.Empty,
            unit.MainPartGlobalId ?? string.Empty,
            string.Join(";", unit.AssemblyMemberIds.OrderBy(id => id, StringComparer.Ordinal))
        };

        return HashText(string.Join("|", parts));
    }

    public static void ValidateSnapshotParity(
        IReadOnlyList<CanonicalLiftUnitSnapshotDto> expected,
        IReadOnlyList<CanonicalLiftUnitSnapshotDto> actual,
        string actualLabel)
    {
        var expectedById = expected.ToDictionary(unit => unit.LiftUnitId, StringComparer.Ordinal);
        var actualById = actual.ToDictionary(unit => unit.LiftUnitId, StringComparer.Ordinal);

        if (expectedById.Count != actualById.Count)
            throw new InvalidOperationException($"{actualLabel} canonical lift-unit count differs from submitted canonical lift-unit count.");

        foreach (var (liftUnitId, expectedUnit) in expectedById)
        {
            if (!actualById.TryGetValue(liftUnitId, out var actualUnit))
                throw new InvalidOperationException($"{actualLabel} is missing canonical LiftUnitId '{liftUnitId}'.");

            if (!string.Equals(expectedUnit.SharedDataHash, actualUnit.SharedDataHash, StringComparison.Ordinal))
                throw new InvalidOperationException($"{actualLabel} shared data hash changed for LiftUnitId '{liftUnitId}'.");
        }
    }

    public static IReadOnlyList<CanonicalLiftUnitSnapshotDto> DeepCloneCanonicalUnits(
        IEnumerable<CanonicalLiftUnitSnapshotDto> units) =>
        units
            .Select(unit => unit with
            {
                AssemblyMemberIds = unit.AssemblyMemberIds.ToArray(),
                ValidationWarnings = unit.ValidationWarnings.ToArray(),
                BlockingReasons = unit.BlockingReasons.ToArray(),
                LiftCogLocal = unit.LiftCogLocal == null
                    ? null
                    : new GaPointSnapshotDto
                    {
                        X = unit.LiftCogLocal.X,
                        Y = unit.LiftCogLocal.Y,
                        Z = unit.LiftCogLocal.Z,
                        Frame = unit.LiftCogLocal.Frame,
                        Unit = unit.LiftCogLocal.Unit
                    },
                LiftCogSurvey = unit.LiftCogSurvey == null
                    ? null
                    : new GaPointSnapshotDto
                    {
                        X = unit.LiftCogSurvey.X,
                        Y = unit.LiftCogSurvey.Y,
                        Z = unit.LiftCogSurvey.Z,
                        Frame = unit.LiftCogSurvey.Frame,
                        Unit = unit.LiftCogSurvey.Unit
                    },
                Cog = CloneCog(unit.Cog)
            })
            .ToArray();

    public static IReadOnlyList<string> ValidateSchemaV2SubmittedUnits(
        CurrentCanonicalModelSnapshotDto snapshot)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(snapshot.CanonicalSnapshotId))
            errors.Add("CanonicalSnapshotId is required.");
        if (string.IsNullOrWhiteSpace(snapshot.CanonicalPayloadHash))
            errors.Add("CanonicalPayloadHash is required.");

        var duplicate = snapshot.CanonicalLiftUnits
            .GroupBy(unit => unit.LiftUnitId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
            errors.Add($"Duplicate LiftUnitId '{duplicate.Key}'.");

        foreach (var unit in snapshot.CanonicalLiftUnits)
        {
            if (string.IsNullOrWhiteSpace(unit.LiftUnitId))
                errors.Add("A canonical lift unit is missing LiftUnitId.");
            if (IsPlaceholder(unit.ElementName))
                errors.Add($"LiftUnit '{unit.LiftUnitId}' has unresolved ElementName.");
            if (IsPlaceholder(unit.ElementType))
                errors.Add($"LiftUnit '{unit.LiftUnitId}' has unresolved ElementType.");
            if (IsPlaceholder(unit.MaterialType))
                errors.Add($"LiftUnit '{unit.LiftUnitId}' has unresolved MaterialType.");
            if (unit.LengthM is not > 0 || unit.WidthM is not > 0 || unit.HeightM is not > 0)
                errors.Add($"LiftUnit '{unit.LiftUnitId}' has missing dimensions.");
            if (unit.VolumeM3 is not > 0)
                errors.Add($"LiftUnit '{unit.LiftUnitId}' has missing volume.");
            if (unit.LiftWeightTon is not > 0)
                errors.Add($"LiftUnit '{unit.LiftUnitId}' has missing trusted weight.");
            var physicalCog = GetPhysicalCogPoint(unit);
            if (physicalCog == null || IsZeroPoint(physicalCog))
                errors.Add($"LiftUnit '{unit.LiftUnitId}' has missing trusted COG.");
            if (!unit.CogTrustedForGa || !unit.Cog.PhysicalCogTrustedForGa)
                errors.Add($"LiftUnit '{unit.LiftUnitId}' has untrusted COG.");
            if (string.IsNullOrWhiteSpace(unit.SharedDataHash))
                errors.Add($"LiftUnit '{unit.LiftUnitId}' is missing SharedDataHash.");
            else
            {
                var expectedHash = ComputeSharedDataHash(unit);
                if (!string.Equals(unit.SharedDataHash, expectedHash, StringComparison.Ordinal))
                    errors.Add($"LiftUnit '{unit.LiftUnitId}' SharedDataHash does not match shared fields.");
            }
        }

        return errors;
    }

    public static CanonicalLiftUnitSnapshotDto ToCanonicalLiftUnit(
        PrecastElement element,
        IReadOnlyList<PrecastElement>? allElements = null,
        IEnumerable<PrecastElement>? materialSourceElements = null)
    {
        var isGroup = element.ChildGlobalIds.Count > 0 || element.AssemblyMemberIds.Count > 0;
        var memberIds = element.AssemblyMemberIds.Count > 0
            ? element.AssemblyMemberIds
            : element.ChildGlobalIds;
        var dimensions = ResolveGaDimensions(element, allElements);
        var volume = ResolveGaVolume(element, dimensions.Length, dimensions.Width, dimensions.Height);
        var cog = ResolveCanonicalCog(element, allElements);
        var material = ResolveCanonicalMaterial(element, allElements, materialSourceElements);
        var weight = element.AssemblyLiftWeight ?? element.ElementWT ?? element.WeightTons;
        var validationWarnings = FilterCanonicalValidationWarnings(element, dimensions, volume, weight, ToDomainPoint(cog.PhysicalCogProjectM), material).ToList();
        var mark = FirstMeaningful(element.ResolvedDisplayMark, element.ElementMark, element.AssemblyMark, element.ElementID) ?? element.ElementID;
        var type = ResolveElementType(element);
        var name = ResolveElementName(element);
        var distinctMemberIds = memberIds.Distinct(StringComparer.Ordinal).ToList();

        var unit = new CanonicalLiftUnitSnapshotDto
        {
            LiftUnitId = element.ElementID,
            SourceElementGlobalId = element.ElementID.StartsWith("ASM:", StringComparison.Ordinal)
                ? element.MainPartGlobalId
                : element.ElementID,
            ElementName = name,
            ElementType = type,
            RawIfcType = element.RawIfcType,
            ResolvedCategory = element.ResolvedCategory,
            Material = element.ElementMaterial,
            MaterialType = material.MaterialType,
            MaterialGrade = material.MaterialGrade,
            RawMaterialValue = material.RawMaterialValue,
            MaterialSource = material.MaterialSource,
            StoreyOrElevation = element.ElementCOG.Z.ToString("0.###", CultureInfo.InvariantCulture),
            ElementMark = mark,
            CanonicalMark = mark,
            AssemblyMark = element.AssemblyMark,
            MarkSource = element.AssemblyMarkSource,
            MarkConfidence = element.AssemblyMarkConfidence.ToString(),
            IsExplicitMark = element.AssemblyMarkIsExplicit || element.ExplicitAssemblyMarkFound,
            LiftUnitKind = isGroup ? "GroupedAssembly" : "IndividualElement",
            RoleAtRunTime = element.Role.ToString(),
            IsAssemblyGroup = isGroup,
            ParentAssemblyGlobalId = element.ParentAssemblyGlobalId,
            MainPartGlobalId = element.MainPartGlobalId,
            AssemblyMemberIds = distinctMemberIds,
            LengthM = dimensions.Length,
            WidthM = dimensions.Width,
            HeightM = dimensions.Height,
            VolumeM3 = volume,
            LiftWeightTon = weight,
            Cog = cog,
            LiftCogLocal = cog.PhysicalCogProjectM,
            AssemblyMemberCount = distinctMemberIds.Count > 0 ? distinctMemberIds.Count : 1,
            DimensionSource = dimensions.Source,
            DimensionMethod = dimensions.Method,
            LiftUnitDefinitionVersion = LiftUnitDefinitionVersion,
            CogMethod = cog.PhysicalCogMethod,
            CogConfidence = cog.PhysicalCogConfidence,
            CogTrustedForGa = cog.PhysicalCogTrustedForGa,
            ImportIncludeInGaFlag = element.IncludeInGa,
            PassedFinalReadiness = true,
            WasSubmittedToGa = true,
            WeightSource = element.WeightSource,
            WeightTrustLevel = element.WeightTrustLevel.ToString(),
            ValidationStatus = validationWarnings.Count == 0 ? "Valid" : element.ValidationStatus.ToString(),
            ValidationWarnings = validationWarnings,
            BlockingReasons = validationWarnings
        };

        return unit with { SharedDataHash = ComputeSharedDataHash(unit) };
    }

    private static IEnumerable<ImportAnomalySnapshotDto> BuildImportAnomalies(
        IReadOnlyList<PrecastElement> allElements,
        IReadOnlyList<CanonicalLiftUnitSnapshotDto> canonicalUnits)
    {
        var canonicalIds = canonicalUnits.Select(unit => unit.LiftUnitId).ToHashSet(StringComparer.Ordinal);
        foreach (var element in allElements)
        {
            if (!string.IsNullOrWhiteSpace(element.ElementID) && canonicalIds.Contains(element.ElementID))
                continue;

            yield return new ImportAnomalySnapshotDto
            {
                SourceObjectId = element.ElementID,
                IfcGlobalId = element.ElementID,
                DisplayName = FirstMeaningful(element.ElementName, element.ElementMark, element.ElementID) ?? element.ElementID,
                RawIfcType = element.RawIfcType ?? string.Empty,
                AnomalyCode = element.Role.ToString(),
                Reason = BuildAnomalyReason(element),
                Details = string.Join("; ", element.ValidationWarnings.Where(warning => !string.IsNullOrWhiteSpace(warning))),
                DetectedAt = DateTimeOffset.UtcNow
            };
        }
    }

    public static CanonicalCogSnapshotDto ResolveCanonicalCog(
        PrecastElement element,
        IReadOnlyList<PrecastElement>? allElements = null)
    {
        var geometry = ElementBindingResolver.ResolveGeometry(element, allElements, preferAssemblyMainPart: true);
        var geometricCenter = geometry.CenterOfGravityM == null ? null : ToPoint(geometry.CenterOfGravityM);
        var members = ResolveAssemblyMembers(element, allElements)
            .Where(member => !IsReinforcement(member))
            .ToList();

        if (members.Count > 0 && TryComputeWeightedCog(members, out var weightedCog))
        {
            return new CanonicalCogSnapshotDto
            {
                PhysicalCogProjectM = ToPoint(weightedCog),
                GeometricCenterProjectM = geometricCenter,
                PhysicalCogMethod = "MemberWeightWeightedCentroid",
                PhysicalCogSource = "Canonical assembly member weights and physical COG",
                PhysicalCogConfidence = "Trusted",
                PhysicalCogTrustedForGa = true
            };
        }

        if (IsValidNonZeroPoint(element.AssemblyLiftCOG))
        {
            return new CanonicalCogSnapshotDto
            {
                PhysicalCogProjectM = ToPoint(element.AssemblyLiftCOG!),
                GeometricCenterProjectM = geometricCenter,
                PhysicalCogMethod = "PersistedAssemblyLiftCOG",
                PhysicalCogSource = "AssemblyLiftCOG",
                PhysicalCogConfidence = element.PhysicalCOGAvailable ? "Trusted" : "Untrusted",
                PhysicalCogTrustedForGa = element.PhysicalCOGAvailable
            };
        }

        var mainPart = ElementBindingResolver.ResolveMainPartOrSelf(element, allElements);
        if (!ReferenceEquals(mainPart, element) && IsValidNonZeroPoint(mainPart.ElementCOG))
        {
            return new CanonicalCogSnapshotDto
            {
                PhysicalCogProjectM = ToPoint(mainPart.ElementCOG),
                GeometricCenterProjectM = geometricCenter,
                PhysicalCogMethod = "MainPartPhysicalCOG",
                PhysicalCogSource = "Main part ElementCOG fallback",
                PhysicalCogConfidence = mainPart.PhysicalCOGAvailable ? "Trusted" : "Untrusted",
                PhysicalCogTrustedForGa = mainPart.PhysicalCOGAvailable
            };
        }

        if (IsValidNonZeroPoint(element.ElementCOG))
        {
            return new CanonicalCogSnapshotDto
            {
                PhysicalCogProjectM = ToPoint(element.ElementCOG),
                GeometricCenterProjectM = geometricCenter,
                PhysicalCogMethod = "ElementPhysicalCOG",
                PhysicalCogSource = "ElementCOG",
                PhysicalCogConfidence = element.PhysicalCOGAvailable ? "Trusted" : "Untrusted",
                PhysicalCogTrustedForGa = element.PhysicalCOGAvailable
            };
        }

        return new CanonicalCogSnapshotDto
        {
            PhysicalCogProjectM = ToPoint(new DomainPoint3D()),
            GeometricCenterProjectM = geometricCenter,
            PhysicalCogMethod = "Unavailable",
            PhysicalCogSource = "No trusted physical COG",
            PhysicalCogConfidence = "Unavailable",
            PhysicalCogTrustedForGa = false
        };
    }

    private static CanonicalMaterialInfo ResolveCanonicalMaterial(
        PrecastElement element,
        IReadOnlyList<PrecastElement>? allElements,
        IEnumerable<PrecastElement>? materialSourceElements = null)
    {
        var candidates = new List<string?>();
        var mainPart = ElementBindingResolver.ResolveMainPartOrSelf(element, allElements);
        candidates.Add(element.ElementMaterial);
        if (!ReferenceEquals(mainPart, element))
            candidates.Add(mainPart.ElementMaterial);

        candidates.AddRange(ResolveAssemblyMembers(element, allElements)
            .Select(member => member.ElementMaterial));

        AddSharedMaterialEvidence(candidates, element, materialSourceElements);

        return candidates
            .Select(raw => CanonicalMaterialResolver.Resolve(FirstMeaningful(raw)))
            .Where(info => !IsPlaceholder(info.MaterialType))
            .OrderByDescending(MaterialResolutionScore)
            .FirstOrDefault()
            ?? CanonicalMaterialResolver.Resolve(element.ElementMaterial);
    }

    private static void AddSharedMaterialEvidence(
        ICollection<string?> candidates,
        PrecastElement element,
        IEnumerable<PrecastElement>? materialSourceElements)
    {
        if (materialSourceElements == null)
            return;

        var byId = materialSourceElements
            .Where(source => source != null && !string.IsNullOrWhiteSpace(source.ElementID))
            .GroupBy(source => source.ElementID, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        AddMaterialCandidate(candidates, byId, element.ElementID);
        AddMaterialCandidate(candidates, byId, element.MainPartGlobalId);
        AddMaterialCandidate(candidates, byId, element.ParentGlobalId);
        AddMaterialCandidate(candidates, byId, element.ParentAssemblyGlobalId);

        foreach (var memberId in element.AssemblyMemberIds.Concat(element.ChildGlobalIds))
            AddMaterialCandidate(candidates, byId, memberId);
    }

    private static void AddMaterialCandidate(
        ICollection<string?> candidates,
        IReadOnlyDictionary<string, PrecastElement> byId,
        string? elementId)
    {
        if (string.IsNullOrWhiteSpace(elementId))
            return;

        if (byId.TryGetValue(elementId, out var source))
            candidates.Add(source.ElementMaterial);
    }

    private static int MaterialResolutionScore(CanonicalMaterialInfo info)
    {
        var score = 0;
        if (!IsPlaceholder(info.MaterialGrade) && info.MaterialGrade != "\u2014")
            score += 100;
        if (!IsGenericMaterialType(info.MaterialType))
            score += 20;
        if (!IsPlaceholder(info.RawMaterialValue))
            score += 5;
        return score;
    }

    private static bool IsGenericMaterialType(string? value)
    {
        if (IsPlaceholder(value))
            return true;

        return value!.Trim().Equals("Miscellaneous", StringComparison.OrdinalIgnoreCase)
            || value.Trim().Equals("MISCELLANEOUS", StringComparison.OrdinalIgnoreCase)
            || value.Trim().Equals("Other", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<PrecastElement> ResolveAssemblyMembers(
        PrecastElement element,
        IReadOnlyList<PrecastElement>? allElements)
    {
        if (allElements == null || allElements.Count == 0)
            return Array.Empty<PrecastElement>();

        var ids = element.AssemblyMemberIds.Count > 0
            ? element.AssemblyMemberIds
            : element.ChildGlobalIds;
        if (ids.Count == 0)
            return Array.Empty<PrecastElement>();

        var idSet = ids.ToHashSet(StringComparer.Ordinal);
        return allElements
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.ElementID)
                && idSet.Contains(candidate.ElementID))
            .ToList();
    }

    private static bool TryComputeWeightedCog(
        IReadOnlyList<PrecastElement> members,
        out DomainPoint3D cog)
    {
        double totalWeight = 0;
        double x = 0;
        double y = 0;
        double z = 0;

        foreach (var member in members)
        {
            var weight = member.WeightTons ?? member.ElementWT;
            if (weight is not > 0 || !member.PhysicalCOGAvailable || !IsValidNonZeroPoint(member.ElementCOG))
            {
                cog = new DomainPoint3D();
                return false;
            }

            totalWeight += weight.Value;
            x += weight.Value * member.ElementCOG.X;
            y += weight.Value * member.ElementCOG.Y;
            z += weight.Value * member.ElementCOG.Z;
        }

        if (totalWeight <= 0)
        {
            cog = new DomainPoint3D();
            return false;
        }

        cog = new DomainPoint3D(x / totalWeight, y / totalWeight, z / totalWeight);
        return true;
    }

    private static bool IsCanonicalLiftUnitCandidate(
        PrecastElement element,
        IReadOnlyDictionary<string, PrecastElement> byId)
    {
        if (IfcReferenceGeometryPolicy.IsGridReference(element) || IsReinforcement(element))
            return false;
        if (!element.IncludeInGa)
            return false;
        if (element.Role is IfcElementRole.NeedsReview or IfcElementRole.GroupingOnly or IfcElementRole.InternalAuxiliary or IfcElementRole.Excluded)
            return false;
        if (!HasTrustedPositiveWeight(element))
            return false;
        if (!IsValidPoint(element.ElementCOG))
            return false;
        if (!string.IsNullOrWhiteSpace(element.ParentGlobalId)
            && byId.TryGetValue(element.ParentGlobalId, out var parent)
            && parent.IncludeInGa
            && parent.Role is IfcElementRole.LiftAssembly or IfcElementRole.LiftableElement
            && HasTrustedPositiveWeight(parent))
        {
            return false;
        }

        return true;
    }

    private static bool HasTrustedPositiveWeight(PrecastElement element) =>
        element.WeightTons is > 0
        && CraneOptimizer.Domain.Policy.IfcWeightTrustPolicy.IsGaApproved(element.WeightTrustLevel);

    private static bool IsReinforcement(PrecastElement element)
    {
        var classification = string.Join(" ",
            element.RawIfcType,
            element.ResolvedCategory,
            element.ElementType,
            element.ElementName,
            element.ElementRevitId,
            element.ElementMark,
            element.AssemblyMark).ToUpperInvariant();

        return classification.Contains("REINFORC", StringComparison.Ordinal)
            || classification.Contains("REBAR", StringComparison.Ordinal)
            || classification.Contains("TENDON", StringComparison.Ordinal)
            || classification.Contains("STIRRUP", StringComparison.Ordinal)
            || classification.Contains("TIE-WIRE", StringComparison.Ordinal);
    }

    private static string ResolveElementName(PrecastElement element) =>
        FirstMeaningful(
            element.ParentAssemblyName,
            element.MainPartName,
            element.ElementName,
            element.AssemblyMark,
            element.ResolvedDisplayMark,
            element.ElementMark,
            element.ElementID) ?? "Unknown";

    private static string ResolveElementType(PrecastElement element)
    {
        var evidence = string.Join(" ", new[]
            {
                element.ResolvedCategory,
                element.MainPartCategory,
                element.ElementType,
                element.RawIfcType,
                element.ElementName,
                element.MainPartName,
                element.ElementMark,
                element.AssemblyMark,
                element.ResolvedDisplayMark
            }
            .Select(value => FirstMeaningful(value))
            .Where(value => value != null))
            .ToUpperInvariant();

        if (evidence.Contains("COLUMN", StringComparison.Ordinal))
            return "Column";
        if (evidence.Contains("BEAM", StringComparison.Ordinal))
            return "Beam";
        if (evidence.Contains("WALL", StringComparison.Ordinal)
            || evidence.Contains("INSIDESHELL", StringComparison.Ordinal)
            || evidence.Contains("OUTSIDESHELL", StringComparison.Ordinal)
            || evidence.Contains("LWP", StringComparison.Ordinal)
            || evidence.Contains("SW-", StringComparison.Ordinal)
            || evidence.Contains("PP-", StringComparison.Ordinal))
        {
            return "Wall";
        }

        if (evidence.Contains("SLAB", StringComparison.Ordinal)
            || evidence.Contains("HOLLOWCORE", StringComparison.Ordinal)
            || evidence.Contains("TEE SLAB", StringComparison.Ordinal)
            || evidence.Contains("DT71", StringComparison.Ordinal)
            || evidence.Contains("BCAN", StringComparison.Ordinal))
        {
            return "Slab";
        }

        if (evidence.Contains("ROOF", StringComparison.Ordinal)
            || evidence.Contains("PAD ROOF", StringComparison.Ordinal))
        {
            return "Roof";
        }

        return FirstMeaningfulEngineeringType(element.ResolvedCategory, element.ElementType, element.RawIfcType) ?? "Unknown";
    }

    private static (double? Length, double? Width, double? Height, string Source, string Method) ResolveGaDimensions(
        PrecastElement element,
        IReadOnlyList<PrecastElement>? allElements)
    {
        var geometry = ElementBindingResolver.ResolveGeometry(element, allElements, preferAssemblyMainPart: true);
        var length = FirstPositive(geometry.LengthM, element.ElementLength, element.BBoxLength, element.BoundingBox_M.X);
        var width = FirstPositive(geometry.WidthM, element.ElementWidth, element.BBoxWidth, element.BoundingBox_M.Y);
        var height = FirstPositive(geometry.HeightM, element.ElementHeight, element.BBoxHeight, element.BoundingBox_M.Z);
        if (length is > 0 && width is > 0 && height is > 0)
        {
            var source = geometry.LengthM is > 0 || geometry.WidthM is > 0 || geometry.HeightM is > 0
                ? "Canonical resolved geometry"
                : element.ElementLength is > 0 || element.ElementWidth is > 0 || element.ElementHeight is > 0
                    ? "Explicit IFC dimensions"
                    : element.BBoxLength is > 0 || element.BBoxWidth is > 0 || element.BBoxHeight is > 0
                        ? "Persisted bounding box"
                        : "Transformed bounding box";
            var method = source switch
            {
                "Canonical resolved geometry" => "ElementBindingResolver main-part geometry",
                "Explicit IFC dimensions" => "ElementLength/ElementWidth/ElementHeight",
                "Persisted bounding box" => "BBoxLength/BBoxWidth/BBoxHeight",
                _ => "BoundingBox_M axes"
            };
            return (length, width, height, source, method);
        }

        return (null, null, null, "Unavailable in this saved run", "Unavailable in this saved run");
    }

    private static PrecastElement MergeSharedFields(
        PrecastElement element,
        IReadOnlyDictionary<string, PrecastElement> sharedFieldSources)
    {
        if (!sharedFieldSources.TryGetValue(element.ElementID, out var source) || ReferenceEquals(element, source))
            return element;

        var merged = element.CreateCopy();
        merged.ElementLength = FirstPositive(merged.ElementLength, source.ElementLength);
        merged.ElementWidth = FirstPositive(merged.ElementWidth, source.ElementWidth);
        merged.ElementHeight = FirstPositive(merged.ElementHeight, source.ElementHeight);
        merged.BBoxLength = FirstPositive(merged.BBoxLength, source.BBoxLength);
        merged.BBoxWidth = FirstPositive(merged.BBoxWidth, source.BBoxWidth);
        merged.BBoxHeight = FirstPositive(merged.BBoxHeight, source.BBoxHeight);
        merged.BoundingBox_M = MergePoint(merged.BoundingBox_M, source.BoundingBox_M);
        merged.VolumeM3 = FirstPositive(merged.VolumeM3, source.VolumeM3);
        merged.ElementVolume = FirstPositive(merged.ElementVolume, source.ElementVolume);
        merged.ElementWT = FirstPositive(merged.ElementWT, source.ElementWT);
        merged.WeightTons = FirstPositive(merged.WeightTons, source.WeightTons);
        merged.ElementName = FirstMeaningful(merged.ElementName, source.ElementName, merged.MainPartName, source.MainPartName, merged.AssemblyMark, source.AssemblyMark, merged.ElementMark, source.ElementMark) ?? merged.ElementName;
        merged.ElementType = FirstMeaningfulEngineeringType(merged.ElementType, source.ElementType, merged.ResolvedCategory, source.ResolvedCategory, merged.RawIfcType, source.RawIfcType, merged.ElementName, source.ElementName) ?? "Unknown";
        merged.ElementMaterial = FirstMeaningfulMaterial(merged.ElementMaterial, source.ElementMaterial) ?? merged.ElementMaterial;
        merged.RawIfcType = FirstMeaningful(merged.RawIfcType, source.RawIfcType) ?? merged.RawIfcType;
        merged.ResolvedCategory = FirstMeaningful(merged.ResolvedCategory, source.ResolvedCategory, merged.ElementType, source.ElementType) ?? merged.ResolvedCategory;
        merged.ResolvedDisplayMark = FirstMeaningful(merged.ResolvedDisplayMark, source.ResolvedDisplayMark, merged.ElementMark, source.ElementMark) ?? merged.ResolvedDisplayMark;
        merged.ElementMark = FirstMeaningful(merged.ElementMark, source.ElementMark, merged.ResolvedDisplayMark, source.ResolvedDisplayMark) ?? merged.ElementMark;
        merged.AssemblyMark = FirstMeaningful(merged.AssemblyMark, source.AssemblyMark) ?? merged.AssemblyMark;
        merged.MainPartGlobalId = FirstMeaningful(merged.MainPartGlobalId, source.MainPartGlobalId) ?? merged.MainPartGlobalId;
        merged.ParentGlobalId = FirstMeaningful(merged.ParentGlobalId, source.ParentGlobalId) ?? merged.ParentGlobalId;
        if (merged.AssemblyMemberIds.Count == 0 && source.AssemblyMemberIds.Count > 0)
            merged.AssemblyMemberIds = source.AssemblyMemberIds.ToList();
        if (merged.ChildGlobalIds.Count == 0 && source.ChildGlobalIds.Count > 0)
            merged.ChildGlobalIds = source.ChildGlobalIds.ToList();

        return merged;
    }

    private static DomainPoint3D MergePoint(DomainPoint3D target, DomainPoint3D source) => new(
        FirstPositive(target.X, source.X) ?? target.X,
        FirstPositive(target.Y, source.Y) ?? target.Y,
        FirstPositive(target.Z, source.Z) ?? target.Z);

    private static double? ResolveGaVolume(PrecastElement element, double? length, double? width, double? height)
    {
        if (element.VolumeM3 is > 0)
            return element.VolumeM3;
        if (element.ElementVolume is > 0)
            return element.ElementVolume;
        if (length is > 0 && width is > 0 && height is > 0)
            return length.Value * width.Value * height.Value;
        return null;
    }

    private static string BuildCanonicalSnapshotId(string projectGuid, string payloadHash)
    {
        var prefix = string.IsNullOrWhiteSpace(projectGuid) ? "canonical" : projectGuid.Trim();
        return $"{prefix}:{payloadHash[..Math.Min(16, payloadHash.Length)]}";
    }

    private static string BuildAnomalyReason(PrecastElement element)
    {
        if (!element.IncludeInGa)
            return "Not included in GA canonical scope.";
        if (!HasTrustedPositiveWeight(element))
            return "Missing or untrusted positive weight.";
        if (!IsValidPoint(element.ElementCOG))
            return "Missing or invalid trusted COG.";
        return element.Role.ToString();
    }

    private static IEnumerable<string> FilterCanonicalValidationWarnings(
        PrecastElement element,
        (double? Length, double? Width, double? Height, string Source, string Method) dimensions,
        double? volume,
        double? weight,
        DomainPoint3D cog,
        CanonicalMaterialInfo material)
    {
        foreach (var warning in element.ValidationWarnings.Where(warning => !string.IsNullOrWhiteSpace(warning)))
        {
            if (IsStaleCanonicalWarning(warning, dimensions, volume, weight, cog, material))
                continue;

            yield return warning;
        }
    }

    private static bool IsStaleCanonicalWarning(
        string warning,
        (double? Length, double? Width, double? Height, string Source, string Method) dimensions,
        double? volume,
        double? weight,
        DomainPoint3D cog,
        CanonicalMaterialInfo material)
    {
        var text = warning.Trim();
        if (text.Contains("MissingTrustedWeight", StringComparison.OrdinalIgnoreCase)
            || text.Contains("MissingWeight", StringComparison.OrdinalIgnoreCase))
        {
            return weight is > 0;
        }

        if (text.Contains("MissingAssemblyCOG", StringComparison.OrdinalIgnoreCase)
            || text.Contains("MissingCOG", StringComparison.OrdinalIgnoreCase))
        {
            return IsValidPoint(cog) && !IsZeroPoint(cog);
        }

        if (text.Contains("MissingAssemblyVolume", StringComparison.OrdinalIgnoreCase)
            || text.Contains("MissingVolume", StringComparison.OrdinalIgnoreCase))
        {
            return volume is > 0;
        }

        if (text.Contains("MissingMaterial", StringComparison.OrdinalIgnoreCase))
            return !IsPlaceholder(material.MaterialType);

        if (text.Contains("MissingDimension", StringComparison.OrdinalIgnoreCase)
            || text.Contains("MissingLength", StringComparison.OrdinalIgnoreCase)
            || text.Contains("MissingWidth", StringComparison.OrdinalIgnoreCase)
            || text.Contains("MissingHeight", StringComparison.OrdinalIgnoreCase))
        {
            return dimensions.Length is > 0 && dimensions.Width is > 0 && dimensions.Height is > 0;
        }

        return false;
    }

    private static GaPointSnapshotDto ToPoint(DomainPoint3D point) => new()
    {
        X = point.X,
        Y = point.Y,
        Z = point.Z,
        Frame = "ProjectLocalMeters",
        Unit = "m"
    };

    private static DomainPoint3D ToDomainPoint(GaPointSnapshotDto point) =>
        new(point.X, point.Y, point.Z);

    private static GaPointSnapshotDto? GetPhysicalCogPoint(CanonicalLiftUnitSnapshotDto unit)
    {
        if (IsValidNonZeroPoint(unit.Cog.PhysicalCogProjectM))
            return unit.Cog.PhysicalCogProjectM;

        return unit.LiftCogLocal;
    }

    private static CanonicalCogSnapshotDto CloneCog(CanonicalCogSnapshotDto cog) => new()
    {
        PhysicalCogProjectM = ClonePoint(cog.PhysicalCogProjectM) ?? new GaPointSnapshotDto(),
        GeometricCenterProjectM = ClonePoint(cog.GeometricCenterProjectM),
        GaCalculationCogM = ClonePoint(cog.GaCalculationCogM),
        PhysicalCogMethod = cog.PhysicalCogMethod,
        PhysicalCogSource = cog.PhysicalCogSource,
        PhysicalCogConfidence = cog.PhysicalCogConfidence,
        PhysicalCogTrustedForGa = cog.PhysicalCogTrustedForGa,
        ProjectCoordinateFrameId = cog.ProjectCoordinateFrameId,
        GaCoordinateFrameId = cog.GaCoordinateFrameId,
        ProjectToGaTransform = cog.ProjectToGaTransform == null
            ? null
            : new CoordinateTransformSnapshotDto
            {
                TransformId = cog.ProjectToGaTransform.TransformId,
                SourceFrameId = cog.ProjectToGaTransform.SourceFrameId,
                TargetFrameId = cog.ProjectToGaTransform.TargetFrameId,
                OffsetXM = cog.ProjectToGaTransform.OffsetXM,
                OffsetYM = cog.ProjectToGaTransform.OffsetYM,
                OffsetZM = cog.ProjectToGaTransform.OffsetZM,
                RotationDegrees = cog.ProjectToGaTransform.RotationDegrees,
                Scale = cog.ProjectToGaTransform.Scale
            }
    };

    private static GaPointSnapshotDto? ClonePoint(GaPointSnapshotDto? point) =>
        point == null
            ? null
            : new GaPointSnapshotDto
            {
                X = point.X,
                Y = point.Y,
                Z = point.Z,
                Frame = point.Frame,
                Unit = point.Unit
            };

    private static bool IsValidPoint(DomainPoint3D? point) =>
        point != null && double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z);

    private static bool IsZeroPoint(DomainPoint3D point) =>
        Math.Abs(point.X) < double.Epsilon
        && Math.Abs(point.Y) < double.Epsilon
        && Math.Abs(point.Z) < double.Epsilon;

    private static bool IsZeroPoint(GaPointSnapshotDto point) =>
        Math.Abs(point.X) < double.Epsilon
        && Math.Abs(point.Y) < double.Epsilon
        && Math.Abs(point.Z) < double.Epsilon;

    private static bool IsValidNonZeroPoint(DomainPoint3D? point) =>
        IsValidPoint(point) && !IsZeroPoint(point!);

    private static bool IsValidNonZeroPoint(GaPointSnapshotDto? point) =>
        point != null
        && double.IsFinite(point.X)
        && double.IsFinite(point.Y)
        && double.IsFinite(point.Z)
        && !IsZeroPoint(point);

    private static double? NullIfUnavailable(double value) =>
        double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) < double.Epsilon ? null : value;

    private static double? FirstPositive(params double?[] values) =>
        values.FirstOrDefault(value => value is > 0);

    private static string Number(double? value) =>
        value?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string HashText(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string? FirstMeaningful(params string?[] values) =>
        values
            .Select(value => value?.Trim())
            .FirstOrDefault(value => !IsPlaceholder(value));

    private static string? FirstMeaningfulEngineeringType(params string?[] values) =>
        values
            .Select(value => value?.Trim())
            .FirstOrDefault(value => !IsPlaceholder(value) && !IsGenericIfcType(value));

    private static string? FirstMeaningfulMaterial(params string?[] values)
    {
        var candidates = values
            .Select(value => value?.Trim())
            .Where(value => !IsPlaceholder(value))
            .ToList();

        return candidates
            .OrderByDescending(value => MaterialResolutionScore(CanonicalMaterialResolver.Resolve(value)))
            .FirstOrDefault();
    }

    private static bool IsGenericIfcType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var text = value.Trim();
        return text.Equals("IfcBuildingElementPart", StringComparison.OrdinalIgnoreCase)
            || text.Equals("IfcBuildingElementProxy", StringComparison.OrdinalIgnoreCase)
            || text.Equals("IfcElementAssembly", StringComparison.OrdinalIgnoreCase)
            || text.Equals("IfcProduct", StringComparison.OrdinalIgnoreCase)
            || text.Equals("IfcElement", StringComparison.OrdinalIgnoreCase)
            || text.Equals("IfcProxy", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var text = value.Trim();
        return text.Equals("-", StringComparison.OrdinalIgnoreCase)
            || text.Equals("—", StringComparison.OrdinalIgnoreCase)
            || text.Equals("N/A", StringComparison.OrdinalIgnoreCase)
            || text.Equals("NA", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Undefined", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Unresolved", StringComparison.OrdinalIgnoreCase)
            || text.Equals("null", StringComparison.OrdinalIgnoreCase);
    }
}
