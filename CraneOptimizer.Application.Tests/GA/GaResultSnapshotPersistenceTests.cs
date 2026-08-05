using System.Globalization;
using System.Reflection;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;
using CraneOptimizer.Application.GAAnalysis;
using CraneOptimizer.Application.Reports.Services;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Entities.Enums;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Infrastructure.Optimization.GeneticAlgorithms.Snapshots;

namespace CraneOptimizer.Application.Tests.GA;

public sealed class GaResultSnapshotPersistenceTests
{
    [Fact]
    public async Task Completed_snapshot_round_trips_with_matrix_checksum_manifest_and_report_rows()
    {
        var root = CreateTestRoot();
        var execution = CreateExecution("run-roundtrip", grouped: false, coverage: 0.5);
        var snapshot = await MapAsync(execution, root);
        var validator = new GaResultSnapshotValidator();
        var store = CreateStore(root, validator);

        var validation = validator.Validate(snapshot);
        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
        Assert.Equal(3, snapshot.SchemaVersion);
        Assert.Equal(3, snapshot.Run.SnapshotSchemaVersion);
        Assert.Equal(3, snapshot.Model.OptimizationInputFingerprintVersion);
        Assert.Equal(GaOptimizationInputFingerprint.RequiredComponentNames, snapshot.Model.OptimizationInputFingerprintComponents.Keys);

        await store.SaveAsync(snapshot);
        var snapshotPath = Path.Combine(root, "Reports", "GA", "run-roundtrip", JsonGaResultSnapshotStore.SnapshotFileName);
        var checksumPath = Path.Combine(root, "Reports", "GA", "run-roundtrip", JsonGaResultSnapshotStore.ChecksumFileName);
        var manifestPath = Path.Combine(root, "Reports", "GA", "run-roundtrip", JsonGaResultSnapshotStore.ManifestFileName);

        Assert.True(File.Exists(snapshotPath));
        Assert.True(File.Exists(checksumPath));
        Assert.True(File.Exists(manifestPath));
        Assert.DoesNotContain($"{Path.DirectorySeparatorChar}32{Path.DirectorySeparatorChar}", snapshotPath);

        var loaded = await store.LoadAsync(snapshotPath);
        Assert.NotNull(loaded);
        Assert.Equal("run-roundtrip", loaded.Run.OptimizationRunId);
        Assert.Equal(3, loaded.SchemaVersion);
        Assert.Equal(2, loaded.LiftUnits.Count);
        Assert.Equal(2, loaded.Positions.Count);
        Assert.All(loaded.Positions, position => Assert.Equal(2, position.Evaluations.Count));
        Assert.Equal(1, loaded.Positions.Sum(position => position.FinalAssignedCount));
        Assert.Single(loaded.FinalOutcomes.Where(outcome => outcome.IsLifted));
        Assert.Single(loaded.Positions.SelectMany(position => position.Evaluations)
            .Where(evaluation => evaluation.LiftUnitId == "IFC-A" && evaluation.IsFinalAssignedToPoint));

        var failed = Assert.Single(loaded.FinalOutcomes.Where(outcome => !outcome.IsLifted));
        Assert.Equal("IFC-B", failed.LiftUnitId);
        Assert.False(string.IsNullOrWhiteSpace(failed.BestFailedEvaluationId));

        var adapter = new GaSnapshotReportAdapter();
        var rows = adapter.BuildRows(loaded, ReportTemplateCatalog.GaResultReport, "All Rows");
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal("run-roundtrip", row.Values["OptimizationRunId"]));
        Assert.Contains(rows, row => Equals("50.00 %", row.Values["CoverageRatio"]));

        var setups = adapter.BuildCraneSetups(loaded);
        Assert.Equal(1, setups.Sum(setup => setup.LiftedItemsCount));
        Assert.Equal(100, Assert.Single(setups.Where(setup => setup.LiftedItemsCount == 1)).LiftedItemsPercentage);

        var liftedUnit = Assert.Single(loaded.LiftUnits.Where(unit => unit.LiftUnitId == "IFC-A"));
        Assert.Equal(2.4, liftedUnit.LengthM);
        Assert.Equal(0.8, liftedUnit.WidthM);
        Assert.Equal(1.2, liftedUnit.HeightM);
        Assert.Equal(2.304, liftedUnit.VolumeM3!.Value, precision: 3);
        Assert.Equal("Canonical resolved geometry", liftedUnit.DimensionSource);
        Assert.True(liftedUnit.CogTrustedForGa);
        var liftedRow = Assert.Single(rows.Where(row => row.ElementId == "IFC-A"));
        Assert.Equal(2.4, liftedRow.Values["Length"]);
        Assert.Equal(0.8, liftedRow.Values["Width"]);
        Assert.Equal(1.2, liftedRow.Values["Height"]);
        Assert.Equal(2.304, Assert.IsType<double>(liftedRow.Values["VolumeM3"]), precision: 3);
    }

    [Fact]
    public async Task Diagnostics_off_and_non_english_culture_still_write_invariant_production_json()
    {
        var root = CreateTestRoot();
        var execution = CreateExecution("run-diagnostics-off", grouped: false, coverage: 1.0);
        execution.RequestedSettings.DiagnosticsLevel = DiagnosticsLevel.Off;
        execution.RequestedSettings.EnableProfiling = false;
        execution.EffectiveFinalSettings.DiagnosticsLevel = DiagnosticsLevel.Off;
        execution.EffectiveFinalSettings.EnableProfiling = false;
        var snapshot = await MapAsync(execution, root);
        var store = CreateStore(root, new GaResultSnapshotValidator());

        var oldCulture = CultureInfo.CurrentCulture;
        var oldUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-EG");
            CultureInfo.CurrentUICulture = new CultureInfo("ar-EG");
            await store.SaveAsync(snapshot);
        }
        finally
        {
            CultureInfo.CurrentCulture = oldCulture;
            CultureInfo.CurrentUICulture = oldUiCulture;
        }

        var json = await File.ReadAllTextAsync(Path.Combine(root, "Reports", "GA", "run-diagnostics-off", JsonGaResultSnapshotStore.SnapshotFileName));
        Assert.Contains("\"coverageRatio\": 1", json);
        Assert.DoesNotContain("\"mutationRate\": 0,1", json);

        var loaded = await store.LoadAsync(Path.Combine(root, "Reports", "GA", "run-diagnostics-off", JsonGaResultSnapshotStore.SnapshotFileName));
        Assert.NotNull(loaded);
        Assert.Equal("Off", loaded.RequestedSettings.DiagnosticsLevel);
        Assert.False(loaded.RequestedSettings.EnableProfiling);

        var rows = new GaSnapshotReportAdapter().BuildRows(loaded, ReportTemplateCatalog.GaResultReport, "All Rows");
        Assert.Contains(rows, row => Equals("100.00 %", row.Values["CoverageRatio"]));
    }

    [Fact]
    public async Task Multi_stage_snapshot_preserves_shared_run_stage_ids_and_selected_stage_not_last()
    {
        var root = CreateTestRoot();
        var execution = CreateExecution("run-multistage", grouped: false, coverage: 0.5, multiStage: true);
        var snapshot = await MapAsync(execution, root);

        Assert.Equal("run-multistage", snapshot.Run.OptimizationRunId);
        Assert.Equal("stage-2", snapshot.Run.FinalStageRunId);
        Assert.Equal(2, snapshot.Run.FinalStageIndex);
        Assert.Equal(3, snapshot.Stages.Count);
        Assert.All(snapshot.Stages, stage => Assert.Equal("run-multistage", stage.OptimizationRunId));
        Assert.Equal(["stage-1", "stage-2", "stage-3"], snapshot.Stages.Select(stage => stage.StageRunId).ToArray());
        Assert.Single(snapshot.Stages.Where(stage => stage.WasSelectedAsFinal));
        Assert.True(snapshot.Stages[1].WasSelectedAsFinal);
        Assert.Equal(2, snapshot.EffectiveFinalSettings.MaxCranePositions);
        Assert.Equal(2, snapshot.Stages[1].MaxCranePositions);
        Assert.Equal(3, snapshot.Stages[2].MaxCranePositions);
    }

    [Fact]
    public async Task Grouped_snapshot_preserves_synthetic_identity_and_does_not_join_by_duplicate_marks()
    {
        var root = CreateTestRoot();
        var execution = CreateExecution("run-grouped", grouped: true, coverage: 0.5);
        var snapshot = await MapAsync(execution, root);

        var groupedUnit = Assert.Single(snapshot.LiftUnits.Where(unit => unit.LiftUnitId == "ASM:DUP-MARK"));
        Assert.Equal("GroupedAssembly", groupedUnit.LiftUnitKind);
        Assert.Equal("IFC-MAIN", groupedUnit.SourceElementGlobalId);
        Assert.Equal("IFC-PARENT", groupedUnit.ParentAssemblyGlobalId);
        Assert.Equal("IFC-MAIN", groupedUnit.MainPartGlobalId);
        Assert.Equal(["IFC-MAIN", "IFC-CHILD"], groupedUnit.AssemblyMemberIds);
        Assert.Equal(12.5, groupedUnit.LiftWeightTon);
        Assert.Equal("DUP-MARK", groupedUnit.ElementMark);
        Assert.NotEqual(groupedUnit.ElementMark, groupedUnit.SourceElementGlobalId);

        var finalOutcomeIds = snapshot.FinalOutcomes.Select(outcome => outcome.LiftUnitId).ToArray();
        Assert.Contains("ASM:DUP-MARK", finalOutcomeIds);
        Assert.Contains("IFC-B", finalOutcomeIds);
        Assert.DoesNotContain("DUP-MARK", finalOutcomeIds);
    }

    [Fact]
    public async Task Provider_classifies_foreign_historic_checksum_mismatch_and_missing_checksum()
    {
        var root = CreateTestRoot();
        var validator = new GaResultSnapshotValidator();
        var store = CreateStore(root, validator);
        var currentStore = new GaCurrentResultStore();
        var provider = new GaResultSnapshotProvider(currentStore, store, new LegacyCsvGaSnapshotAdapter(), new GaSnapshotClassificationService());

        var older = await MapAsync(CreateExecution("run-old", grouped: false, coverage: 0.5), root);
        var newer = await MapAsync(CreateExecution("run-new", grouped: false, coverage: 0.5), root);
        newer = WithModelFingerprint(newer, older.Model.ModelFingerprint);
        await store.SaveAsync(older);
        await Task.Delay(20);
        await store.SaveAsync(newer);

        var newestChecksum = Path.Combine(root, "Reports", "GA", "run-new", JsonGaResultSnapshotStore.ChecksumFileName);
        await File.WriteAllTextAsync(newestChecksum, "0000000000000000000000000000000000000000000000000000000000000000  ga_result_snapshot.json");

        var resolved = await provider.ResolveAsync(root, "project-guid-1", older.Model.ModelFingerprint);
        Assert.NotNull(resolved.Snapshot);
        Assert.Equal("run-old", resolved.Snapshot.Run.OptimizationRunId);

        var descriptors = await store.ListRunsAsync(root, "other-project", older.Model.ModelFingerprint);
        Assert.Contains(descriptors, descriptor => descriptor.OptimizationRunId == "run-old" && descriptor.Classification == "Foreign");

        var historicDescriptors = await store.ListRunsAsync(root, "project-guid-1", "different-model");
        Assert.Contains(historicDescriptors, descriptor => descriptor.OptimizationRunId == "run-old" && descriptor.Classification == "Historic");

        File.Delete(Path.Combine(root, "Reports", "GA", "run-old", JsonGaResultSnapshotStore.ChecksumFileName));
        var loadedWithoutChecksum = await store.LoadAsync(Path.Combine(root, "Reports", "GA", "run-old", JsonGaResultSnapshotStore.SnapshotFileName));
        Assert.NotNull(loadedWithoutChecksum);
    }

    [Fact]
    public void Classification_null_current_fingerprint_is_unverified_not_historic_and_ifc_hash_can_match_current()
    {
        var classifier = new GaSnapshotClassificationService();
        var saved = new GaRunDescriptorDto
        {
            OptimizationRunId = "run-1",
            Status = "Completed",
            ProjectGuid = "project-guid-1",
            ModelFingerprint = "saved-model",
            IfcSha256 = "abc123",
            SourceKind = "ProductionJson"
        };

        var unknown = classifier.Classify(
            new GaProjectActivationContext("C:\\tmp", "project-guid-1", "Project", null, null, null, null),
            saved);
        Assert.Equal("CurrentProjectUnverified", unknown.Classification);

        var ifcHashMatch = classifier.Classify(
            new GaProjectActivationContext("C:\\tmp", "project-guid-1", "Project", null, "different-model", "ABC123", null),
            saved);
        Assert.Equal("CurrentByIfcHash", ifcHashMatch.Classification);

        var historic = classifier.Classify(
            new GaProjectActivationContext("C:\\tmp", "project-guid-1", "Project", null, "different-model", "different-hash", null),
            saved);
        Assert.Equal("Historic", historic.Classification);

        var foreign = classifier.Classify(
            new GaProjectActivationContext("C:\\tmp", "other-project", "Project", null, "saved-model", null, null),
            saved);
        Assert.Equal("Foreign", foreign.Classification);
    }

    [Fact]
    public async Task Legacy_adapter_recursively_discovers_ga_audit_files_and_builds_basic_rows_without_full_matrix()
    {
        var root = CreateTestRoot();
        var runDir = Path.Combine(root, "Reports", "GA", "cranes", "nested", "legacy-run-1");
        Directory.CreateDirectory(runDir);
        await File.WriteAllTextAsync(
            Path.Combine(runDir, "ga_audit_run_summary.csv"),
            "OptimizationRunId,CompletedAtUtc,CraneName,TotalTargetElements,LiftedElements,CoverageRatio,EffectiveLiftMode\nlegacy-run-1,2026-07-11T12:00:00Z,\"100Tons, solved\",2,1,0.5,GroupedAssemblies\n");
        await File.WriteAllTextAsync(
            Path.Combine(runDir, "ga_audit_positions.csv"),
            "SetupIndex,CraneX,CraneY,CraneZ,FinalAssignedCount\n1,10.5,20.25,0,1\n");
        await File.WriteAllTextAsync(
            Path.Combine(runDir, "ga_audit_element_assignments.csv"),
            "OptimizationRunId,LiftUnitId,SourceElementGlobalId,ElementMark,SetupIndex,LiftStatus,Radius,RequiredBoomLength,BoomLength,RequiredCapacity,AvailableCapacity,SafetyMargin,FailureReason\nlegacy-run-1,ASM:A-1,IFC-MAIN,\"Mark, A\",1,Liftable,15,31,29,11,20,9,\nlegacy-run-1,IFC-B,IFC-B,B-1,1,Overweight,25,42,40,30,20,-10,Too heavy\n");

        var adapter = new LegacyCsvGaSnapshotAdapter();
        var context = new GaProjectActivationContext(root, "project-guid-1", "Legacy Project", null, null, null, null);
        var descriptors = await adapter.ListRunsAsync(context);

        var descriptor = Assert.Single(descriptors);
        Assert.Equal("legacy-run-1", descriptor.OptimizationRunId);
        Assert.Equal("LegacyCsv", descriptor.SourceKind);
        Assert.Equal("CurrentProjectUnverified", descriptor.Classification);
        Assert.Equal(2, descriptor.TotalTargetLiftUnitCount);
        Assert.Equal(1, descriptor.LiftedLiftUnitCount);
        Assert.Equal(1, descriptor.SetupCount);

        var resolved = await adapter.LoadAsync(context, descriptor);
        Assert.NotNull(resolved.Snapshot);
        Assert.False(resolved.Capabilities.HasPositionEvaluationMatrix);
        Assert.True(resolved.Capabilities.HasFinalAssignments);
        Assert.False(resolved.Capabilities.SupportsSelectedPointFeasibility);
        Assert.Equal(2, resolved.Snapshot.FinalOutcomes.Count);
        Assert.Single(resolved.Snapshot.FinalOutcomes.Where(outcome => outcome.IsLifted));

        var rows = new GaSnapshotReportAdapter().BuildRows(
            resolved.Snapshot,
            ReportTemplateCatalog.GaResultReport,
            "All Rows");
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, row => Equals("ASM:A-1", row.Values["ElementId"]));
    }

    [Fact]
    public void Snapshot_report_adapter_maps_required_boom_length_from_required_field()
    {
        var liftUnit = new CanonicalLiftUnitSnapshotDto
        {
            LiftUnitId = "IFC-1",
            LiftUnitKind = "IndividualElement",
            RoleAtRunTime = "Target",
            ElementMark = "M1",
            ElementName = "COLUMN",
            ElementType = "Column",
            MaterialType = "Concrete",
            MaterialGrade = "C30",
            LengthM = 1,
            WidthM = 1,
            HeightM = 1,
            VolumeM3 = 1,
            LiftWeightTon = 1,
            LiftCogLocal = new GaPointSnapshotDto { X = 1, Y = 2, Z = 3 },
            AssemblyMemberIds = ["IFC-1"],
            AssemblyMemberCount = 1
        };
        liftUnit = liftUnit with { SharedDataHash = CanonicalLiftUnitSnapshotBuilder.ComputeSharedDataHash(liftUnit) };

        var snapshot = new GaResultSnapshotDto
        {
            Run = new GaRunIdentitySnapshotDto { OptimizationRunId = "run-boom", Status = "Completed", CompletedAtUtc = DateTimeOffset.UtcNow },
            Project = new GaProjectSnapshotDto { ProjectGuid = "project-guid-1" },
            Crane = new GaCraneSnapshotDto { CraneName = "Crane" },
            Summary = new GaRunSummarySnapshotDto { TotalTargetLiftUnitCount = 1, LiftedLiftUnitCount = 1, PositionCount = 1, CoverageRatio = 1 },
            Capabilities = GaSnapshotCapabilitiesDto.Production(false, false, false),
            LiftUnits =
            [
                liftUnit
            ],
            Positions =
            [
                new GaPositionSnapshotDto
                {
                    PositionId = "P1",
                    SetupIndex = 1,
                    LocalPosition = new GaPointSnapshotDto(),
                    FinalAssignedCount = 1,
                    FinalAssignedLiftUnitIds = ["IFC-1"],
                    Evaluations =
                    [
                        new GaElementEvaluationSnapshotDto
                        {
                            EvaluationId = "E1",
                            LiftUnitId = "IFC-1",
                            CandidatePointId = "P1",
                            LiftStatus = "Liftable",
                            BoomLengthM = 29,
                            RequiredBoomLengthM = 31,
                            SelectedChartBoomLengthM = 35,
                            IsFinalAssignedToPoint = true,
                            IsFinalAssigned = true,
                            FinalOwnerPointId = "P1"
                        }
                    ]
                }
            ],
            FinalOutcomes =
            [
                new GaFinalOutcomeSnapshotDto { LiftUnitId = "IFC-1", IsLifted = true, FinalEvaluationId = "E1", FinalOwnerPositionId = "P1", FinalLiftStatus = "Liftable" }
            ]
        };

        var row = Assert.Single(new GaSnapshotReportAdapter().BuildRows(
            snapshot,
            ReportTemplateCatalog.GaResultReport,
            "All Rows"));

        Assert.Equal(29d, row.BoomLength);
        Assert.Equal(31d, row.Values["RequiredBoomLength"]);
        Assert.Equal(35d, row.Values["SelectedChartBoomLength"]);
    }

    [Fact]
    public void Snapshot_report_adapter_fails_when_final_outcome_integrity_is_broken()
    {
        var snapshot = new GaResultSnapshotDto
        {
            Run = new GaRunIdentitySnapshotDto { OptimizationRunId = "run-broken", Status = "Completed", CompletedAtUtc = DateTimeOffset.UtcNow },
            Project = new GaProjectSnapshotDto { ProjectGuid = "project-guid-1" },
            Crane = new GaCraneSnapshotDto { CraneName = "Crane" },
            Summary = new GaRunSummarySnapshotDto { TotalTargetLiftUnitCount = 2, LiftedLiftUnitCount = 1, PositionCount = 0, CoverageRatio = 0.5 },
            Capabilities = GaSnapshotCapabilitiesDto.Production(false, false, false),
            LiftUnits =
            [
                new CanonicalLiftUnitSnapshotDto { LiftUnitId = "IFC-1", LiftUnitKind = "IndividualElement", RoleAtRunTime = "Target" },
                new CanonicalLiftUnitSnapshotDto { LiftUnitId = "IFC-2", LiftUnitKind = "IndividualElement", RoleAtRunTime = "Target" }
            ],
            FinalOutcomes =
            [
                new GaFinalOutcomeSnapshotDto { LiftUnitId = "IFC-1", IsLifted = true, FinalLiftStatus = "Liftable" }
            ]
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new GaSnapshotReportAdapter().BuildRows(snapshot, ReportTemplateCatalog.GaResultReport, "All Submitted Lift Units"));

        Assert.Contains("final outcomes", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ga_settings_snapshot_has_mapping_decision_for_every_public_readable_property()
    {
        var mapped = typeof(GaSettingsSnapshotDto)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        var ignored = new HashSet<string>(StringComparer.Ordinal)
        {
            "RiggingLength", // Obsolete alias for WireLength.
            "RunId", // Obsolete alias for StageRunId.
            "ProfilingLogDirectory" // Project-relative diagnostics reference, not an engineering setting.
        };

        var missing = typeof(GASettings)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanRead)
            .Select(property => property.Name)
            .Where(name => !mapped.Contains(name) && !ignored.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public async Task SnapshotMapper_CopiesPreRunSubmittedCanonicalUnitsWithoutRebuilding()
    {
        var root = CreateTestRoot();
        var execution = CreateExecution("dimension-scope-run", grouped: false, coverage: 1.0);
        var expected = execution.OriginalInput.SubmittedCanonicalLiftUnits.Single(unit => unit.LiftUnitId == "IFC-A");
        execution.OriginalInput.Elements = execution.OriginalInput.Elements
            .Select(element => element.CreateCopy())
            .ToList();

        var submitted = execution.OriginalInput.Elements[0];
        submitted.ElementType = "Unknown";
        submitted.ElementName = "Unknown";
        submitted.ElementMaterial = "Unknown";
        submitted.ElementLength = null;
        submitted.ElementWidth = null;
        submitted.ElementHeight = null;
        submitted.BBoxLength = null;
        submitted.BBoxWidth = null;
        submitted.BBoxHeight = null;
        submitted.BoundingBox_M = new DomainPoint3D();

        var snapshot = await MapAsync(execution, root);
        var unit = snapshot.LiftUnits.Single(unit => unit.LiftUnitId == submitted.ElementID);

        Assert.Equal(expected.ElementName, unit.ElementName);
        Assert.Equal(expected.ElementType, unit.ElementType);
        Assert.Equal(expected.MaterialType, unit.MaterialType);
        Assert.Equal(expected.LengthM, unit.LengthM);
        Assert.Equal(expected.WidthM, unit.WidthM);
        Assert.Equal(expected.HeightM, unit.HeightM);
        Assert.Equal(expected.SharedDataHash, unit.SharedDataHash);
    }

    [Fact]
    public async Task SnapshotMapper_PreservesConcreteGradeFromAssemblyMemberSourceThroughSavedReportRows()
    {
        var root = CreateTestRoot();
        var execution = CreateExecution("material-grade-run", grouped: true, coverage: 1.0);
        var group = execution.OriginalInput.Elements.Single(element => element.ElementID == "ASM:DUP-MARK");
        group.ElementMaterial = "Concrete";
        group.MainPartGlobalId = "IFC-MAIN";
        group.AssemblyMemberIds = ["IFC-MAIN", "IFC-CHILD"];
        group.ChildGlobalIds = ["IFC-MAIN", "IFC-CHILD"];

        var mainPart = CreateElement("IFC-MAIN", "DUP-MARK", 8.0, new DomainPoint3D(4, 5, 6), isAssembly: false);
        mainPart.ElementMaterial = "Concrete C30";
        mainPart.ParentGlobalId = group.ElementID;
        var child = CreateElement("IFC-CHILD", "DUP-MARK", 4.5, new DomainPoint3D(4, 5, 6), isAssembly: false);
        child.ElementMaterial = "Concrete C30";
        child.ParentGlobalId = group.ElementID;
        var sharedSources = execution.OriginalInput.Elements.Concat([mainPart, child]).ToList();

        var canonicalSnapshot = CanonicalLiftUnitSnapshotBuilder.BuildCurrentSnapshot(
            execution.OriginalInput.Elements,
            execution.OriginalInput.ProjectId ?? string.Empty,
            null,
            "GroupedAssemblies",
            sharedSources);
        execution.OriginalInput.FeatureScopeElements = sharedSources;
        execution.OriginalInput.CanonicalSnapshot = canonicalSnapshot;
        execution.OriginalInput.SubmittedCanonicalLiftUnits =
            CanonicalLiftUnitSnapshotBuilder.DeepCloneCanonicalUnits(canonicalSnapshot.CanonicalLiftUnits);
        execution.OriginalInput.CanonicalSnapshotId = canonicalSnapshot.CanonicalSnapshotId;
        execution.OriginalInput.CanonicalPayloadHash = canonicalSnapshot.CanonicalPayloadHash;

        var snapshot = await MapAsync(execution, root);
        var savedUnit = Assert.Single(snapshot.LiftUnits.Where(unit => unit.LiftUnitId == group.ElementID));
        Assert.Equal("Concrete", savedUnit.MaterialType);
        Assert.Equal("C30", savedUnit.MaterialGrade);

        var store = CreateStore(root, new GaResultSnapshotValidator());
        await store.SaveAsync(snapshot);
        var loaded = await store.LoadAsync(Path.Combine(root, "Reports", "GA", "material-grade-run", JsonGaResultSnapshotStore.SnapshotFileName));
        Assert.NotNull(loaded);
        Assert.Equal("C30", loaded.LiftUnits.Single(unit => unit.LiftUnitId == group.ElementID).MaterialGrade);

        var sourceRow = Assert.Single(new GaSnapshotReportAdapter().BuildRows(
            loaded,
            ReportTemplateCatalog.GaResultReport,
            "All Submitted Lift Units").Where(row => row.ElementId == group.ElementID));
        Assert.Equal("C30", sourceRow.Values["MaterialGrade"]);

        var reportRow = Assert.Single(new LiftingResultReportDataProvider().BuildRows(
            ReportTemplateCatalog.GaResultReport,
            "All Submitted Lift Units",
            [sourceRow]));
        Assert.Equal("C30", reportRow.Values["MaterialGrade"]);
    }

    [Fact]
    public async Task SnapshotMapper_PersistsRiggingWireSeparatelyFromCraneHoistWireAndAccessoryWeight()
    {
        var root = CreateTestRoot();
        var execution = CreateExecution("wire-accessory-run", grouped: false, coverage: 1.0);
        execution.RequestedSettings.WireLength = 2.0;
        execution.RequestedSettings.AdditionalWt = 0.0;
        execution.EffectiveFinalSettings.WireLength = 2.0;
        execution.EffectiveFinalSettings.AdditionalWt = 0.0;
        execution.OriginalInput.Settings.WireLength = 2.0;
        execution.OriginalInput.Settings.AdditionalWt = 0.0;
        foreach (var stage in execution.Stages)
        {
            stage.EffectiveSettings.WireLength = 2.0;
            stage.EffectiveSettings.AdditionalWt = 0.0;
        }

        var snapshot = await MapAsync(execution, root);

        Assert.Null(snapshot.Crane.WireLengthM);
        Assert.Equal(0.0, snapshot.Crane.HoistWireLengthM);
        Assert.Equal(2.0, snapshot.LiftingAccessories.WireRopeLengthM);
        Assert.Equal(2.0, snapshot.LiftingAccessories.RiggingWireRopeLengthM);
        Assert.Equal(0.0, snapshot.LiftingAccessories.HookRiggingWeightTon);
        Assert.Equal(0.0, snapshot.LiftingAccessories.TotalBelowHookWeightTon);
        Assert.Equal(0.0, snapshot.LiftingAccessories.TotalLiftingAccessoriesWeightTon);
        Assert.All(
            snapshot.Positions.SelectMany(position => position.Evaluations),
            evaluation => Assert.Equal(0.0, evaluation.HookRiggingWeightTon));
    }

    [Fact]
    public void Version_two_input_fingerprint_is_componentized_and_sensitive_to_engineering_changes()
    {
        var settings = CreateSettings("fingerprint-run", "stage-1", 1, grouped: false);
        var input = CreateInput(settings, grouped: false);
        var baseline = GaOptimizationInputFingerprint.Build(input, settings);

        settings.SafetyFactor -= 0.05;
        var safetyChanged = GaOptimizationInputFingerprint.Build(input, settings);

        Assert.NotEqual(baseline.Aggregate, safetyChanged.Aggregate);
        Assert.NotEqual(
            baseline.Components["CapacityFactors"],
            safetyChanged.Components["CapacityFactors"]);
        Assert.All(
            baseline.Components.Keys.Where(key => key != "CapacityFactors"),
            key => Assert.Equal(baseline.Components[key], safetyChanged.Components[key]));

        input.Zones.Add(new GreenAreaZone
        {
            ZoneId = Guid.Parse("659bd7ee-35e7-4b75-9686-4a60e1b5142d"),
            Name = "Engineering zone",
            Vertices =
            [
                new DomainPoint3D(0, 0, 0),
                new DomainPoint3D(10, 0, 0),
                new DomainPoint3D(10, 10, 0)
            ]
        });
        var zoneChanged = GaOptimizationInputFingerprint.Build(input, settings);

        Assert.NotEqual(safetyChanged.Aggregate, zoneChanged.Aggregate);
        Assert.NotEqual(safetyChanged.Components["Zones"], zoneChanged.Components["Zones"]);
        Assert.All(
            safetyChanged.Components.Keys.Where(key => key != "Zones"),
            key => Assert.Equal(safetyChanged.Components[key], zoneChanged.Components[key]));
    }

    [Fact]
    public async Task Lift_analysis_keeps_adequacy_right_sizing_and_screening_as_independent_dimensions()
    {
        var root = CreateTestRoot();
        var snapshot = await MapAsync(
            CreateExecution("analysis-inadequate", grouped: false, coverage: 0.5),
            root);
        var service = new GaLiftAnalysisService(new StubAlternativeScreeningService());

        var result = await service.AnalyzeAsync(new GaLiftAnalysisRequest
        {
            Snapshot = snapshot,
            RunAlternativeScreening = false
        });

        Assert.Equal(CurrentCraneAdequacy.Inadequate, result.CurrentCraneAdequacy);
        Assert.Equal(RightSizingAssessment.NotEvaluated, result.RightSizingAssessment);
        Assert.Equal(AlternativeScreeningOutcome.NotRun, result.AlternativeScreeningOutcome);
        Assert.Equal(1, result.Metrics.ReliableCraneLimitFailureCount);
        Assert.StartsWith("rev_", result.RevisionId);
        Assert.Equal("RunSettingsSharedOffsets", result.GeometrySource);
    }

    private sealed class StubAlternativeScreeningService : IGaAlternativeScreeningService
    {
        public Task<GaAlternativeScreeningResult> ScreenAsync(
            GaResultSnapshotDto snapshot,
            CurrentCraneAdequacy adequacy,
            IReadOnlyList<GaCraneCandidate> candidates,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new GaAlternativeScreeningResult
            {
                Outcome = AlternativeScreeningOutcome.NoValidatedAlternative
            });
    }

    private static async Task<GaResultSnapshotDto> MapAsync(GaExecutionResult execution, string root)
    {
        var mapper = new GaResultSnapshotMapper();
        return await mapper.MapAsync(execution, new GaSnapshotMappingContext
        {
            ProjectGuid = "project-guid-1",
            ProjectName = "Snapshot Test Project",
            ProjectRoot = root,
            ApplicationVersion = "test-version",
            GitCommitHash = "test-commit"
        });
    }

    private static IGaResultSnapshotStore CreateStore(string root, IGaResultSnapshotValidator validator) =>
        new JsonGaResultSnapshotStore(new TestPathResolver(root), validator);

    private static string CreateTestRoot()
    {
        var root = Path.Combine("C:\\tmp", "ga-result-snapshot-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static GaExecutionResult CreateExecution(
        string runId,
        bool grouped,
        double coverage,
        bool multiStage = false)
    {
        var started = DateTimeOffset.UtcNow.AddMinutes(-2);
        var completed = DateTimeOffset.UtcNow;
        var requested = CreateSettings(runId, "stage-requested", 1, grouped);
        var effective = CreateSettings(runId, multiStage ? "stage-2" : "stage-1", multiStage ? 2 : 1, grouped);
        var input = CreateInput(requested, grouped);
        var result = CreateResult(grouped, coverage);
        var stages = multiStage
            ? new[]
            {
                CreateStage(runId, "stage-1", 1, requested, CreateResult(grouped, 0.25), selected: false),
                CreateStage(runId, "stage-2", 2, effective, result, selected: true),
                CreateStage(runId, "stage-3", 3, CreateSettings(runId, "stage-3", 3, grouped), CreateResult(grouped, 0.40), selected: false)
            }
            : new[] { CreateStage(runId, "stage-1", 1, effective, result, selected: true) };

        return new GaExecutionResult
        {
            OriginalInput = input,
            BestResult = result,
            OptimizationRunId = runId,
            FinalStageRunId = effective.StageRunId,
            FinalStageIndex = effective.StageIndex,
            StartedAtUtc = started,
            CompletedAtUtc = completed,
            DurationMilliseconds = (long)(completed - started).TotalMilliseconds,
            TerminationReason = "TestComplete",
            RequestedSettings = requested,
            EffectiveFinalSettings = effective,
            Stages = stages
        };
    }

    private static GaStageExecutionResult CreateStage(
        string runId,
        string stageRunId,
        int stageIndex,
        GASettings settings,
        PlacementResult result,
        bool selected)
    {
        var effective = settings.Clone();
        effective.OptimizationRunId = runId;
        effective.StageRunId = stageRunId;
        effective.StageIndex = stageIndex;
        effective.MaxCranePositions = stageIndex;

        return new GaStageExecutionResult
        {
            OptimizationRunId = runId,
            StageRunId = stageRunId,
            StageIndex = stageIndex,
            EffectiveSettings = effective,
            Result = result,
            StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-stageIndex),
            CompletedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-stageIndex).AddSeconds(5),
            DurationMilliseconds = 5000,
            TerminationReason = "TestStageComplete",
            WasSelectedAsFinal = selected
        };
    }

    private static GASettings CreateSettings(string runId, string stageRunId, int stageIndex, bool grouped) => new()
    {
        PopulationSize = 12,
        MaxGenerations = 7,
        MutationRate = 0.10,
        MutationSigma = 1.25,
        CrossoverRate = 0.75,
        WireLength = 3.5,
        AdditionalWt = 1.0,
        DeratingRatio = 0.90,
        SafetyFactor = 0.95,
        MaxCranePositions = stageIndex,
        RandomSeed = 1234 + stageIndex,
        GridStep = 1.0,
        CraneFootprintRadius = 4.0,
        RenderedOperatingEnvelopeRadius = 60.0,
        MinimumRelocationDistance = 6.0,
        DuplicatePositionDistance = 2.0,
        MutationExplorationMultiplier = 1.5,
        PositionSamplingAttempts = 10,
        CoverageWeight = 10,
        CoverageDominanceMultiplier = 100,
        RelocationWeight = 3 + stageIndex,
        RadiusPenaltyWeight = 2,
        RadiusQualityPenaltyWeight = 1,
        OverweightPenaltyWeight = 8,
        HeightPenaltyWeight = 4,
        RadiusStdDevPenaltyWeight = 2,
        BalancePenaltyWeight = 2,
        SimilarPositionPenaltyWeight = 3,
        RelocationDistancePenaltyWeight = 1,
        MaxRadiusForNormalisation = 60,
        EarlyStoppingGenerations = 3,
        ConvergenceThresholdPercent = 0.1,
        EnableAdaptiveSigma = true,
        SigmaCoolingExponent = 1.2,
        MinimumMutationSigma = 0.25,
        EnableConcurrentOptimization = true,
        EnableReachabilitySeeding = false,
        HotSpotSeedingRatio = 0.2,
        EnableMultiStageDiscovery = true,
        MultiStageCoverageTarget = 0.95,
        AdaptiveRelocationScaleFactor = 0.4,
        UseHungarianAssignment = true,
        EnableGroupedLifts = grouped,
        TargetElementTypes = ["IfcBeam", "IfcColumn"],
        LoadChartNumberOfLinesConstraint = 4,
        LoadChartOperatingConditionConstraint = "Main boom",
        SlewOffsetX = 0.1,
        SlewOffsetY = 0.2,
        SlewOffsetZ = 0.3,
        BoomHingeOffsetX = 0.4,
        BoomHingeOffsetY = 0.5,
        BoomHingeOffsetZ = 0.6,
        OptimizationRunId = runId,
        StageRunId = stageRunId,
        StageIndex = stageIndex,
        BatchModeEnabled = true,
        ExperimentBatchId = "batch-1",
        ExperimentName = "Snapshot Regression",
        ParameterSetId = "P1",
        ParameterSetName = "Baseline",
        RunIndex = 2,
        DiagnosticsLevel = DiagnosticsLevel.Off,
        EnableProfiling = false
    };

    private static PlacementInput CreateInput(GASettings settings, bool grouped)
    {
        var crane = new Crane
        {
            Id = 10,
            Name = "Test Crane",
            Manufacturer = "OpenAI Lifting",
            CraneType = CraneType.Mobile,
            VehicleLength = 12,
            VehicleWidth = 3,
            VehicleHeight = 4,
            OutriggerLength = 8,
            OutriggerWidth = 7
        };
        var spec = new CraneSpecification
        {
            Id = 20,
            CraneId = crane.Id,
            Crane = crane,
            MaxCapacity = 100,
            MinRadius = 3,
            MaxRadius = 60,
            MinBoomLength = 10,
            MaxBoomLength = 80,
            CounterWeight = 20,
            OutriggerConfig = "Full",
            LoadChartSourceDescription = "Test chart",
            LoadChartCapacityPolicy = "ConservativeMinimumAcrossDuplicateVisualColumns",
            LoadChartStorageMode = "ExactPdfVisual",
            LoadChart =
            [
                new TargetPoint
                {
                    Radius = 10,
                    BoomLength = 30,
                    Capacity = 40,
                    VisualColumnIndex = 2,
                    VisualColumnHeader = "30 m",
                    NumberOfLines = 4,
                    OperatingConditionLabel = "Main boom",
                    CraneSpecificationId = 20
                }
            ]
        };
        crane.Specifications.Add(spec);

        var elements = grouped
            ? new List<PrecastElement>
            {
                CreateElement(
                    "ASM:DUP-MARK",
                    "DUP-MARK",
                    12.5,
                    new DomainPoint3D(4, 5, 6),
                    isAssembly: true),
                CreateElement("IFC-B", "DUP-MARK", 20.0, new DomainPoint3D(8, 5, 6), isAssembly: false)
            }
            : new List<PrecastElement>
            {
                CreateElement("IFC-A", "DUP-MARK", 10.0, new DomainPoint3D(4, 5, 6), isAssembly: false),
                CreateElement("IFC-B", "DUP-MARK", 20.0, new DomainPoint3D(8, 5, 6), isAssembly: false)
            };

        var canonicalSnapshot = CanonicalLiftUnitSnapshotBuilder.BuildCurrentSnapshot(
            elements,
            "project-guid-1",
            null,
            grouped ? "GroupedAssemblies" : "Individual",
            elements);

        return new PlacementInput
        {
            Zones = [],
            ForbiddenZones = [],
            Elements = elements,
            Assignment = [],
            CraneSpec = spec,
            Settings = settings,
            ProjectName = "Snapshot Test Project",
            ProjectId = "project-guid-1",
            ProjectFolderPath = "C:\\tmp",
            IfcFileName = "snapshot.ifc",
            RawElementCount = elements.Count,
            OptimizationScopeElementCount = elements.Count,
            RequestedMaxCranePositions = settings.MaxCranePositions,
            RawGaEligibleElementCount = elements.Count,
            GroupedAssemblyCount = grouped ? 1 : 0,
            UngroupedElementCount = grouped ? 1 : 2,
            TotalElementsCount = elements.Count,
            FeatureScopeElements = elements,
            CanonicalSnapshot = canonicalSnapshot,
            SubmittedCanonicalLiftUnits = CanonicalLiftUnitSnapshotBuilder.DeepCloneCanonicalUnits(canonicalSnapshot.CanonicalLiftUnits),
            CanonicalSnapshotId = canonicalSnapshot.CanonicalSnapshotId,
            CanonicalPayloadHash = canonicalSnapshot.CanonicalPayloadHash,
            ProjectFeatureSnapshotId = canonicalSnapshot.CanonicalSnapshotId,
            ScopeDefinitionHash = canonicalSnapshot.CanonicalPayloadHash
        };
    }

    private static PrecastElement CreateElement(
        string id,
        string mark,
        double weight,
        DomainPoint3D cog,
        bool isAssembly)
    {
        var element = new PrecastElement
        {
            ElementID = id,
            ElementName = $"{mark} Name",
            ElementMark = mark,
            ResolvedDisplayMark = mark,
            ElementType = "IfcBeam",
            RawIfcType = "IfcBeam",
            ResolvedCategory = "Beam",
            ElementMaterial = "Concrete",
            ElementLength = 2.4,
            ElementWidth = 0.8,
            ElementHeight = 1.2,
            VolumeM3 = 2.304,
            ElementWT = weight,
            WeightTons = weight,
            WeightSource = "IfcQuantityWeight",
            WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
            ElementCOG = cog,
            COG_M = cog,
            IncludeInGa = true,
            Role = IfcElementRole.LiftableElement,
            ValidationStatus = ValidationStatus.Valid
        };

        if (isAssembly)
        {
            element.ParentGlobalId = "IFC-PARENT";
            element.MainPartGlobalId = "IFC-MAIN";
            element.AssemblyMark = mark;
            element.AssemblyLiftWeight = weight;
            element.AssemblyLiftCOG = cog;
            element.AssemblyMemberIds = ["IFC-MAIN", "IFC-CHILD"];
            element.ChildGlobalIds = ["IFC-MAIN", "IFC-CHILD"];
            element.AssemblyMarkConfidence = AssemblyMarkConfidence.High;
            element.AssemblyMarkIsExplicit = true;
        }

        return element;
    }

    private static PlacementResult CreateResult(bool grouped, double coverage)
    {
        var liftedId = grouped ? "ASM:DUP-MARK" : "IFC-A";
        var failedId = "IFC-B";
        var liftedCount = coverage >= 1.0 ? 2 : 1;

        var position0 = new CranePositionResult
        {
            X = 10,
            Y = 20,
            Z = 0,
            NewLiftableCount = 1,
            ElementResults =
            [
                CreateEvaluation(liftedId, "Point_0", 0, true, true, weight: grouped ? 12.5 : 10.0, required: (grouped ? 12.5 : 10.0) + 1.0),
                CreateEvaluation(failedId, "Point_0", 0, coverage >= 1.0, coverage >= 1.0, weight: 20.0, required: 21.0)
            ]
        };
        var position1 = new CranePositionResult
        {
            X = 40,
            Y = 20,
            Z = 0,
            NewLiftableCount = 1,
            ElementResults =
            [
                CreateEvaluation(liftedId, "Point_1", 1, true, false, weight: grouped ? 12.5 : 10.0, required: (grouped ? 12.5 : 10.0) + 1.0),
                CreateEvaluation(failedId, "Point_1", 1, false, false, weight: 20.0, required: 21.0)
            ]
        };

        return new PlacementResult
        {
            Positions = [position0, position1],
            FitnessScore = 123.45,
            CoverageRatio = coverage,
            LiftedElementCount = liftedCount,
            TotalElementCount = 2,
            RawGaEligibleElementCount = 2,
            GroupedAssemblyCount = grouped ? 1 : 0,
            UngroupedElementCount = grouped ? 1 : 2,
            Statistics = new LiftStatistics
            {
                Liftable = liftedCount,
                OutOfRadius = coverage >= 1.0 ? 0 : 1
            }
        };
    }

    private static ElementLiftResult CreateEvaluation(
        string liftUnitId,
        string pointId,
        int pointIndex,
        bool feasible,
        bool finalAssigned,
        double weight,
        double required)
    {
        var available = feasible ? 40.0 : 12.0;
        return new ElementLiftResult
        {
            ElementId = liftUnitId,
            ElementMark = "DUP-MARK",
            CanonicalMark = "DUP-MARK",
            ElementType = "IfcBeam",
            Weight = weight,
            CogX = 4,
            CogY = 5,
            CogZ = 6,
            Status = feasible ? LiftStatus.Liftable : LiftStatus.OutOfRadius,
            ReachStatus = feasible ? ReachStatus.Reachable : ReachStatus.OutOfRadius,
            ChartStatus = feasible ? ChartStatus.ListedCapacityFound : ChartStatus.NoListedCapacity,
            Radius = 10,
            RequiredRadius = 10,
            SelectedChartRadius = 10,
            BoomLength = 30,
            SelectedChartBoomLength = 30,
            RawChartCapacity = 44.44,
            AvailableCapacity = available,
            RequiredCapacity = required,
            SafetyMargin = available - required,
            LookupMode = "ConservativeStep",
            IsExactMatch = true,
            SelectedVisualColumnIndex = 2,
            SelectedVisualColumnHeader = "30 m",
            SelectedNumberOfLines = 4,
            SelectedOperatingConditionLabel = "Main boom",
            CandidatePointId = pointId,
            CandidatePointIndex = pointIndex,
            IsFeasibleFromPoint = feasible,
            IsFinalAssignedToPoint = finalAssigned,
            IsFinalAssigned = finalAssigned || feasible,
            AssignedPointId = finalAssigned ? pointId : string.Empty,
            AssignedPointIndex = finalAssigned ? pointIndex : -1,
            FinalOwnerPointId = finalAssigned ? pointId : string.Empty,
            AssignmentReason = finalAssigned ? "HungarianAssignment" : string.Empty,
            AssignmentScore = feasible ? 100 - pointIndex : -pointIndex,
            FeasiblePointIds = feasible ? "Point_0;Point_1" : string.Empty,
            HasBestFailedCandidate = !feasible,
            BestFailedPointId = !feasible ? pointId : string.Empty,
            BestFailedLiftStatus = LiftStatus.OutOfRadius,
            BestFailedReachStatus = ReachStatus.OutOfRadius,
            BestFailedChartStatus = ChartStatus.NoListedCapacity,
            BestFailedSafetyMargin = available - required,
            BestFailedRequiredRadius = 10,
            BestFailedRequiredBoomLength = 30,
            BestFailureReason = !feasible ? "Out of radius" : string.Empty,
            CraneBaseX = 10,
            CraneBaseY = 20,
            CraneBaseZ = 0,
            SlewCenterX = 10.1,
            SlewCenterY = 20.2,
            SlewCenterZ = 0.3,
            BoomHingeX = 10.4,
            BoomHingeY = 20.5,
            BoomHingeZ = 0.6,
            LoadPointX = 4,
            LoadPointY = 5,
            LoadPointZ = 6
        };
    }

    private static GaResultSnapshotDto WithModelFingerprint(GaResultSnapshotDto snapshot, string modelFingerprint) => new()
    {
        ApplicationVersion = snapshot.ApplicationVersion,
        SchemaVersion = snapshot.SchemaVersion,
        SnapshotType = snapshot.SnapshotType,
        CanonicalSnapshotId = snapshot.CanonicalSnapshotId,
        CanonicalPayloadHash = snapshot.CanonicalPayloadHash,
        GitCommitHash = snapshot.GitCommitHash,
        Run = snapshot.Run,
        Project = snapshot.Project,
        Model = new GaModelSnapshotDto
        {
            CanonicalSnapshotId = snapshot.Model.CanonicalSnapshotId,
            CanonicalPayloadHash = snapshot.Model.CanonicalPayloadHash,
            ModelFingerprint = modelFingerprint,
            OptimizationInputFingerprint = snapshot.Model.OptimizationInputFingerprint,
            OptimizationInputFingerprintVersion = snapshot.Model.OptimizationInputFingerprintVersion,
            OptimizationInputFingerprintComponents = snapshot.Model.OptimizationInputFingerprintComponents,
            IfcFileName = snapshot.Model.IfcFileName,
            IfcProjectRelativePath = snapshot.Model.IfcProjectRelativePath,
            IfcSha256 = snapshot.Model.IfcSha256,
            IfcFileSizeBytes = snapshot.Model.IfcFileSizeBytes,
            IfcLastWriteUtc = snapshot.Model.IfcLastWriteUtc,
            ScenarioId = snapshot.Model.ScenarioId,
            ProjectFeatureSnapshotId = snapshot.Model.ProjectFeatureSnapshotId,
            ScopeDefinitionHash = snapshot.Model.ScopeDefinitionHash
        },
        Coordinates = snapshot.Coordinates,
        RequestedSettings = snapshot.RequestedSettings,
        EffectiveFinalSettings = snapshot.EffectiveFinalSettings,
        Stages = snapshot.Stages,
        Crane = snapshot.Crane,
        LiftingAccessories = snapshot.LiftingAccessories,
        LoadChart = snapshot.LoadChart,
        InputScope = snapshot.InputScope,
        Summary = snapshot.Summary,
        Performance = snapshot.Performance,
        Reachability = snapshot.Reachability,
        LiftUnits = snapshot.LiftUnits,
        Positions = snapshot.Positions,
        FinalOutcomes = snapshot.FinalOutcomes,
        Capabilities = snapshot.Capabilities,
        Diagnostics = snapshot.Diagnostics,
        Integrity = snapshot.Integrity
    };

    private sealed class TestPathResolver : IGaResultSnapshotPathResolver
    {
        private readonly string _root;

        public TestPathResolver(string root)
        {
            _root = root;
        }

        public string ResolveRunDirectory(string optimizationRunId) =>
            Path.Combine(_root, "Reports", "GA", optimizationRunId);

        public string ResolveSnapshotPath(string optimizationRunId) =>
            Path.Combine(ResolveRunDirectory(optimizationRunId), JsonGaResultSnapshotStore.SnapshotFileName);
    }
}
