using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.UI.Core.Services;
using CraneOptimizer.UI.Features.F11_DXF.DTOs;
using CraneOptimizer.UI.Features.F11_DXF.Models;
using CraneOptimizer.UI.Features.F11_DXF.Services;
using CraneOptimizer.UI.Features.F11_DXF.ViewModels;

namespace CraneOptimizer.Application.Tests.DXF;

public sealed class DxfLayerZoneAssignmentTests
{
    [Fact]
    public async Task PreparationRunsWithoutCallerSynchronizationContext()
    {
        var scene = new TrackingSceneService();
        var service = new DxfLayerZoneAssignmentService(scene);
        var callerContext = new SynchronizationContext();
        SynchronizationContext? candidateContext = callerContext;
        SynchronizationContext? originalContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(callerContext);
        try
        {
            await service.AssignAsync(
                CreateSource(),
                "A-FLOR",
                ZoneType.Allowed,
                _ =>
                {
                    candidateContext = SynchronizationContext.Current;
                    return new[] { CreateCandidate(0) };
                },
                CancellationToken.None);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(originalContext);
        }

        Assert.Null(scene.SnapshotSynchronizationContext);
        Assert.Null(candidateContext);
    }

    [Fact]
    public async Task CancellationBeforeCommitProducesNoTransactionOrSceneChange()
    {
        var scene = new SceneService();
        var service = new DxfLayerZoneAssignmentService(scene);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var providerCalled = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.AssignAsync(
                CreateSource(),
                "A-FLOR",
                ZoneType.Allowed,
                _ =>
                {
                    providerCalled = true;
                    return new[] { CreateCandidate(0) };
                },
                cancellation.Token));

        Assert.False(providerCalled);
        Assert.Empty(scene.GetZones());
    }

    [Theory]
    [InlineData(ZoneType.Allowed, DxfLayerZoneAssignment.Allowed)]
    [InlineData(ZoneType.Forbidden, DxfLayerZoneAssignment.Forbidden)]
    public async Task AssignCreatesPreparedZonesWithStableMetadata(
        ZoneType zoneType,
        DxfLayerZoneAssignment expectedAssignment)
    {
        var scene = new SceneService();
        var service = new DxfLayerZoneAssignmentService(scene);
        DxfLayerZoneSource source = CreateSource();

        DxfLayerZoneTransaction transaction = await service.AssignAsync(
            source,
            "A-FLOR",
            zoneType,
            _ => new[] { CreateCandidate(0), CreateCandidate(1) },
            CancellationToken.None);

        Assert.Equal(DxfLayerZoneAssignment.None, transaction.PreviousAssignment);
        Assert.Equal(expectedAssignment, transaction.NewAssignment);
        Assert.Equal(2, transaction.NextZones.Count);
        Assert.All(transaction.NextZones, zone =>
        {
            Assert.Equal(source.SourceDxfId, zone.SourceDxfId);
            Assert.Equal(source.FilePath, zone.SourceDxfFilePath);
            Assert.Equal("A-FLOR", zone.SourceLayerName);
            Assert.Equal(DxfLayerZoneAssignmentService.ImportMethod, zone.ImportMethod);
            Assert.Equal(zoneType, zone.OriginalAssignmentType);
        });
    }

    [Fact]
    public async Task ClearMatchesOnlySourceLayerAndDxfByLayerZones()
    {
        var scene = new SceneService();
        var service = new DxfLayerZoneAssignmentService(scene);
        DxfLayerZoneSource source = CreateSource();
        Guid otherSourceId = Guid.NewGuid();
        GreenAreaZone targetOne = CreateImportedZone(source.SourceDxfId, "A-FLOR", ZoneType.Allowed);
        GreenAreaZone targetTwo = CreateImportedZone(source.SourceDxfId, "a-flor", ZoneType.Allowed);
        GreenAreaZone otherDxf = CreateImportedZone(otherSourceId, "A-FLOR", ZoneType.Allowed);
        GreenAreaZone otherLayer = CreateImportedZone(source.SourceDxfId, "B-FLOR", ZoneType.Allowed);
        var manual = new GreenAreaZone
        {
            ZoneId = Guid.NewGuid(),
            SourceDxfId = source.SourceDxfId,
            SourceLayerName = "A-FLOR",
            Type = ZoneType.Allowed,
            Name = "Manual zone"
        };
        foreach (GreenAreaZone zone in new[] { targetOne, targetTwo, otherDxf, otherLayer, manual })
            scene.AddZone(zone);

        DxfLayerZoneTransaction transaction = await service.ClearAsync(
            source,
            "A-FLOR",
            CancellationToken.None);
        Apply(scene, transaction, forward: true);

        Assert.Equal(new[] { targetOne.ZoneId, targetTwo.ZoneId }, transaction.PreviousZones.Select(zone => zone.ZoneId));
        Assert.DoesNotContain(scene.GetZones(), zone => zone.ZoneId == targetOne.ZoneId);
        Assert.DoesNotContain(scene.GetZones(), zone => zone.ZoneId == targetTwo.ZoneId);
        Assert.Contains(scene.GetZones(), zone => zone.ZoneId == otherDxf.ZoneId);
        Assert.Contains(scene.GetZones(), zone => zone.ZoneId == otherLayer.ZoneId);
        Assert.Contains(scene.GetZones(), zone => zone.ZoneId == manual.ZoneId);
    }

    [Fact]
    public void SceneBatchRemovalUsesZoneIdWhenCollectionInstanceDiffers()
    {
        var scene = new SceneService();
        GreenAreaZone indexedZone = CreateImportedZone(Guid.NewGuid(), "A-FLOR", ZoneType.Allowed);
        scene.AddZone(indexedZone);
        scene.Zones[0] = CreateImportedZone(
            indexedZone.SourceDxfId!.Value,
            indexedZone.SourceLayerName!,
            indexedZone.Type);
        scene.Zones[0].ZoneId = indexedZone.ZoneId;

        scene.ApplyZoneChanges(
            new[] { indexedZone.ZoneId },
            Array.Empty<GreenAreaZone>());

        Assert.Empty(scene.GetZones());
        Assert.Empty(scene.GetZoneSnapshot());
    }

    [Fact]
    public void SceneBatchRaisesOneAggregateZoneChange()
    {
        var scene = new SceneService();
        int aggregateChanges = 0;
        int itemChanges = 0;
        scene.ZonesChanged += () => aggregateChanges++;
        scene.ZoneAdded += _ => itemChanges++;

        scene.ApplyZoneChanges(
            Array.Empty<Guid>(),
            new[]
            {
                CreateImportedZone(Guid.NewGuid(), "A-FLOR", ZoneType.Allowed),
                CreateImportedZone(Guid.NewGuid(), "B-FLOR", ZoneType.Forbidden)
            });

        Assert.Equal(2, itemChanges);
        Assert.Equal(1, aggregateChanges);
    }

    [Fact]
    public async Task LegacyZoneWithUniqueMatchingFileNameUsesCurrentSource()
    {
        var scene = new SceneService();
        var service = new DxfLayerZoneAssignmentService(scene);
        DxfLayerZoneSource source = CreateSource() with { AllowLegacyFileNameMatch = true };
        GreenAreaZone legacy = CreateImportedZone(Guid.NewGuid(), "A-FLOR", ZoneType.Allowed);
        legacy.SourceDxfFileName = source.DisplayName;
        legacy.SourceDxfFilePath = @"D:\old-location\site.dxf";
        legacy.ImportMethod = null;
        scene.AddZone(legacy);

        IReadOnlyDictionary<string, DxfLayerZoneState> states =
            await service.GetAssignmentStatesAsync(
                source,
                new[] { "A-FLOR" },
                CancellationToken.None);
        DxfLayerZoneTransaction transaction = await service.ClearAsync(
            source,
            "A-FLOR",
            CancellationToken.None);

        Assert.Equal(DxfLayerZoneAssignment.Allowed, states["A-FLOR"].Assignment);
        Assert.Equal(legacy.ZoneId, Assert.Single(transaction.PreviousZones).ZoneId);
    }

    [Fact]
    public async Task AssignmentStateIsDerivedFromActualZonesAndDetectsMixedData()
    {
        var scene = new SceneService();
        var service = new DxfLayerZoneAssignmentService(scene);
        DxfLayerZoneSource source = CreateSource();
        scene.AddZone(CreateImportedZone(source.SourceDxfId, "A-FLOR", ZoneType.Allowed));
        scene.AddZone(CreateImportedZone(source.SourceDxfId, "A-FLOR", ZoneType.Forbidden));

        IReadOnlyDictionary<string, DxfLayerZoneState> states =
            await service.GetAssignmentStatesAsync(
                source,
                new[] { "A-FLOR", "EMPTY" },
                CancellationToken.None);

        Assert.Equal(DxfLayerZoneAssignment.Inconsistent, states["A-FLOR"].Assignment);
        Assert.Equal(2, states["A-FLOR"].ZoneCount);
        Assert.Equal(DxfLayerZoneAssignment.None, states["EMPTY"].Assignment);
    }

    [Fact]
    public async Task DialogAllowedAndForbiddenButtonsAreTrueThreeStateToggles()
    {
        var calls = new List<(DxfLayerZoneAssignment Current, DxfLayerZoneAssignment Target)>();
        var viewModel = CreateDialogViewModel((_, current, target, _) =>
        {
            calls.Add((current, target));
            int count = target == DxfLayerZoneAssignment.None ? 0 : 2;
            return Task.FromResult(new DxfLayerZoneOperationResult(
                target,
                count,
                count,
                current == DxfLayerZoneAssignment.None ? 0 : 2,
                "done"));
        });
        DxfZoneImportLayerOption layer = viewModel.Layers[0];

        await viewModel.ToggleAssignmentAsync(layer, DxfLayerZoneAssignment.Allowed);
        await viewModel.ToggleAssignmentAsync(layer, DxfLayerZoneAssignment.Allowed);
        await viewModel.ToggleAssignmentAsync(layer, DxfLayerZoneAssignment.Forbidden);
        await viewModel.ToggleAssignmentAsync(layer, DxfLayerZoneAssignment.Allowed);

        Assert.Equal(
            new[]
            {
                (DxfLayerZoneAssignment.None, DxfLayerZoneAssignment.Allowed),
                (DxfLayerZoneAssignment.Allowed, DxfLayerZoneAssignment.None),
                (DxfLayerZoneAssignment.None, DxfLayerZoneAssignment.Forbidden),
                (DxfLayerZoneAssignment.Forbidden, DxfLayerZoneAssignment.Allowed)
            },
            calls);
        Assert.Equal(DxfLayerZoneAssignment.Allowed, layer.Assignment);
    }

    [Fact]
    public async Task RemoveAllEnablementTracksActualImportedZoneCount()
    {
        var viewModel = CreateDialogViewModel((_, _, target, _) =>
            Task.FromResult(new DxfLayerZoneOperationResult(
                target,
                target == DxfLayerZoneAssignment.None ? 0 : 3,
                0,
                target == DxfLayerZoneAssignment.None ? 3 : 0,
                "done")));
        DxfZoneImportLayerOption layer = viewModel.Layers[0];
        viewModel.SynchronizeLayerState(
            layer.Name,
            new DxfLayerZoneState(
                DxfLayerZoneAssignment.Allowed,
                3,
                Array.Empty<Guid>()));

        Assert.True(viewModel.RemoveImportedZonesCommand.CanExecute(null));
        await viewModel.RemoveAllLayerZonesAsync();
        Assert.False(viewModel.RemoveImportedZonesCommand.CanExecute(null));

        viewModel.SynchronizeLayerState(
            layer.Name,
            new DxfLayerZoneState(
                DxfLayerZoneAssignment.Allowed,
                3,
                Array.Empty<Guid>()));
        Assert.True(viewModel.RemoveImportedZonesCommand.CanExecute(null));
    }

    [Fact]
    public async Task RemoveAllClearsEveryImportedLayerInOneOperation()
    {
        IReadOnlyList<string>? clearedLayers = null;
        var layers = new[]
        {
            new DxfImportLayerSummary("A-FLOR", "#00FF00", 4, 2, 0),
            new DxfImportLayerSummary("MINE", "#FF0000", 8, 3, 0)
        };
        var states = new Dictionary<string, DxfLayerZoneState>(StringComparer.OrdinalIgnoreCase)
        {
            ["A-FLOR"] = new(DxfLayerZoneAssignment.Allowed, 2, Array.Empty<Guid>()),
            ["MINE"] = new(DxfLayerZoneAssignment.Forbidden, 3, Array.Empty<Guid>())
        };
        var viewModel = new DxfZoneImportDialogViewModel(
            layers,
            states,
            (_, _, target, _) => Task.FromResult(
                new DxfLayerZoneOperationResult(target, 0, 0, 0, "done")),
            (layerNames, _) =>
            {
                clearedLayers = layerNames;
                return Task.FromResult(new DxfLayerZoneOperationResult(
                    DxfLayerZoneAssignment.None,
                    0,
                    0,
                    5,
                    "done"));
            });

        await viewModel.RemoveAllLayerZonesAsync();

        Assert.Equal(new[] { "A-FLOR", "MINE" }, clearedLayers);
        Assert.All(viewModel.Layers, layer =>
        {
            Assert.Equal(DxfLayerZoneAssignment.None, layer.Assignment);
            Assert.Equal(0, layer.ImportedZoneCount);
        });
    }

    [Fact]
    public async Task RapidRepeatedClicksDoNotStartDuplicateLayerOperations()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operationCount = 0;
        var viewModel = CreateDialogViewModel(async (_, _, target, _) =>
        {
            Interlocked.Increment(ref operationCount);
            await gate.Task;
            return new DxfLayerZoneOperationResult(target, 1, 1, 0, "done");
        });
        DxfZoneImportLayerOption layer = viewModel.Layers[0];

        Task first = viewModel.ToggleAssignmentAsync(layer, DxfLayerZoneAssignment.Allowed);
        Task second = viewModel.ToggleAssignmentAsync(layer, DxfLayerZoneAssignment.Allowed);
        gate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, operationCount);
        Assert.Equal(DxfLayerZoneAssignment.Allowed, layer.Assignment);
    }

    private static DxfZoneImportDialogViewModel CreateDialogViewModel(
        Func<
            string,
            DxfLayerZoneAssignment,
            DxfLayerZoneAssignment,
            CancellationToken,
            Task<DxfLayerZoneOperationResult>> operation,
        Func<
            IReadOnlyList<string>,
            CancellationToken,
            Task<DxfLayerZoneOperationResult>>? clearAllOperation = null) =>
        new(
            new[] { new DxfImportLayerSummary("A-FLOR", "#FFFFFF", 4, 2, 0) },
            new Dictionary<string, DxfLayerZoneState>(StringComparer.OrdinalIgnoreCase)
            {
                ["A-FLOR"] = new(
                    DxfLayerZoneAssignment.None,
                    0,
                    Array.Empty<Guid>())
            },
            operation,
            clearAllOperation ?? ((_, _) => Task.FromResult(
                new DxfLayerZoneOperationResult(
                    DxfLayerZoneAssignment.None,
                    0,
                    0,
                    0,
                    "done"))));

    private static DxfLayerZoneSource CreateSource() =>
        new(Guid.NewGuid(), @"C:\models\site.dxf", "site.dxf");

    private static DxfZoneCandidate CreateCandidate(int index) =>
        new(
            Guid.NewGuid(),
            "A-FLOR",
            new[]
            {
                new DomainPoint3D(index, 0, 0),
                new DomainPoint3D(index + 1, 0, 0),
                new DomainPoint3D(index + 1, 1, 0),
                new DomainPoint3D(index, 1, 0)
            },
            1,
            "Polyline2D",
            true,
            Array.Empty<string>());

    private static GreenAreaZone CreateImportedZone(Guid sourceId, string layer, ZoneType type) =>
        new()
        {
            ZoneId = Guid.NewGuid(),
            Name = "Imported",
            Type = type,
            SourceDxfId = sourceId,
            SourceLayerName = layer,
            SourceCandidateId = Guid.NewGuid().ToString(),
            ImportMethod = DxfLayerZoneAssignmentService.ImportMethod
        };

    private static void Apply(
        SceneService scene,
        DxfLayerZoneTransaction transaction,
        bool forward)
    {
        IReadOnlyList<GreenAreaZone> remove = forward
            ? transaction.PreviousZones
            : transaction.NextZones;
        IReadOnlyList<GreenAreaZone> add = forward
            ? transaction.NextZones
            : transaction.PreviousZones;
        scene.ApplyZoneChanges(remove.Select(zone => zone.ZoneId).ToArray(), add);
    }

    private sealed class TrackingSceneService : SceneService
    {
        public SynchronizationContext? SnapshotSynchronizationContext { get; private set; }

        public override IReadOnlyList<GreenAreaZone> GetZoneSnapshot()
        {
            SnapshotSynchronizationContext = SynchronizationContext.Current;
            return base.GetZoneSnapshot();
        }
    }
}
