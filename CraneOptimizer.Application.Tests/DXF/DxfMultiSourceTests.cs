using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.UI.Core.Services;
using CraneOptimizer.UI.Features.F11_DXF.DTOs;
using CraneOptimizer.UI.Features.F11_DXF.Models;
using CraneOptimizer.UI.Features.F11_DXF.Services;
using CraneOptimizer.UI.Features.F11_DXF.ViewModels;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.Application.Tests.DXF
{
    public sealed class DxfMultiSourceTests
    {
        [Fact]
        public void SourceSessions_KeepIndependentStableState()
        {
            var firstId = Guid.NewGuid();
            var secondId = Guid.NewGuid();
            var first = new DxfSourceSession(CreateState(firstId, "first.dxf", new[] { 10d, 20d, 1d, 0d }, 0, true));
            var second = new DxfSourceSession(CreateState(secondId, "second.dxf", new[] { 30d, 40d, 2d, 15d }, 1, false));

            Assert.Equal(firstId, first.Id);
            Assert.Equal(secondId, second.Id);
            Assert.Equal(10d, first.SavedTransformArray[0]);
            Assert.Equal(30d, second.SavedTransformArray[0]);
            Assert.NotSame(first.SelectionTarget, second.SelectionTarget);
        }

        [Fact]
        public void LoadProjectState_RestoresAllSourcesActiveAndSelection()
        {
            var firstId = Guid.NewGuid();
            var secondId = Guid.NewGuid();
            var viewModel = CreateViewModel();

            viewModel.LoadDxfsFromProject(new[]
            {
                CreateState(firstId, "first.dxf", new[] { 10d, 20d, 1d, 0d }, 0, false, true),
                CreateState(secondId, "second.dxf", new[] { 30d, 40d, 2d, 15d }, 1, true, true)
            });

            Assert.Equal(2, viewModel.LoadedDxfCount);
            Assert.True(viewModel.HasMultipleDxfs);
            Assert.Equal(secondId, viewModel.ActiveDxf?.Id);
            Assert.Equal(2, viewModel.SelectedDxfCount);
            Assert.Equal(10d, viewModel.LoadedDxfs[0].SavedTransformArray[0]);
            Assert.Equal(30d, viewModel.LoadedDxfs[1].SavedTransformArray[0]);
        }

        [Fact]
        public void LegacySingleSourceRestore_PreservesTransformAndCreatesOneSource()
        {
            var viewModel = CreateViewModel();

            viewModel.LoadDxfFromProject("legacy.dxf", new[] { 12d, 34d, 2d, 45d });

            var source = Assert.Single(viewModel.LoadedDxfs);
            Assert.Equal("legacy.dxf", source.DisplayName);
            Assert.Equal(new[] { 12d, 34d, 2d, 45d }, source.SavedTransformArray.Take(4));
            Assert.True(source.HasSavedTransform);
            Assert.Same(source, viewModel.ActiveDxf);
        }

        [Fact]
        public void AppendingSource_PreservesExistingSourceAndUsesIndependentDefaultTransform()
        {
            var viewModel = CreateViewModel();
            viewModel.LoadDxfFromProject("first.dxf", new[] { 10d, 20d, 2d, 15d });
            var first = viewModel.LoadedDxfs[0];

            viewModel.LoadDxfFromProject("second.dxf", null);

            Assert.Equal(2, viewModel.LoadedDxfCount);
            Assert.Same(first, viewModel.LoadedDxfs[0]);
            Assert.Equal(new[] { 10d, 20d, 2d, 15d }, first.SavedTransformArray.Take(4));
            Assert.Equal(new[] { 0d, 0d, 1d, 0d }, viewModel.LoadedDxfs[1].SavedTransformArray.Take(4));
            Assert.Same(viewModel.LoadedDxfs[1], viewModel.ActiveDxf);
        }

        [Fact]
        public void UpdatingSecondTransform_DoesNotModifyFirstTransform()
        {
            var viewModel = CreateViewModel();
            viewModel.LoadDxfsFromProject(new[]
            {
                CreateState(Guid.NewGuid(), "first.dxf", new[] { 10d, 20d, 1d, 0d }, 0, true),
                CreateState(Guid.NewGuid(), "second.dxf", new[] { 30d, 40d, 2d, 15d }, 1, false)
            });

            viewModel.LoadedDxfs[1].SavedTransformArray = new[] { 90d, 80d, 3d, 25d };

            Assert.Equal(new[] { 10d, 20d, 1d, 0d }, viewModel.LoadedDxfs[0].SavedTransformArray.Take(4));
            Assert.Equal(new[] { 90d, 80d, 3d, 25d }, viewModel.LoadedDxfs[1].SavedTransformArray);
        }

        [Fact]
        public void DxfCommands_TrackLoadedAndSelectedState()
        {
            var viewModel = CreateViewModel();
            Assert.False(viewModel.AlignCommand.CanExecute(null));
            Assert.False(viewModel.DrawZonesByLayerCommand.CanExecute(null));
            Assert.False(viewModel.SelectCommand.CanExecute(null));
            Assert.False(viewModel.SelectAllCommand.CanExecute(null));
            Assert.False(viewModel.DeleteCommand.CanExecute(null));

            viewModel.LoadDxfFromProject("first.dxf", null);
            Assert.True(viewModel.AlignCommand.CanExecute(null));
            Assert.True(viewModel.DrawZonesByLayerCommand.CanExecute(null));
            Assert.True(viewModel.SelectCommand.CanExecute(null));
            Assert.True(viewModel.SelectAllCommand.CanExecute(null));
            Assert.False(viewModel.DeleteCommand.CanExecute(null));

            viewModel.SelectAllCommand.Execute(null);
            Assert.True(viewModel.DeleteCommand.CanExecute(null));
        }

        [Fact]
        public void LoadedStatus_SwitchesFromFilenameToMultiSourceSummary()
        {
            var viewModel = CreateViewModel();
            viewModel.LoadDxfFromProject("first.dxf", null);
            Assert.Equal("Loaded: first.dxf", viewModel.StatusMessage);

            viewModel.LoadDxfFromProject("second.dxf", null);
            Assert.Contains("2 DXFs loaded", viewModel.StatusMessage);
            Assert.Contains("Active: second.dxf", viewModel.StatusMessage);
        }

        [Fact]
        public void SelectAll_SelectsEveryDxfWithoutCreatingOtherSelections()
        {
            var viewModel = CreateViewModel();
            viewModel.LoadDxfsFromProject(new[]
            {
                CreateState(Guid.NewGuid(), "first.dxf", new[] { 0d, 0d, 1d, 0d }, 0, true),
                CreateState(Guid.NewGuid(), "second.dxf", new[] { 0d, 0d, 1d, 0d }, 1, false)
            });

            viewModel.SelectAllCommand.Execute(null);

            Assert.Equal(2, viewModel.SelectedDxfCount);
            Assert.All(viewModel.LoadedDxfs, source => Assert.True(source.IsSelected));
        }

        [Fact]
        public void SceneSelection_ReplacesTogglesAndClearsOnlyDxfSelection()
        {
            var scene = new SceneService();
            var selection = new ViewportSelectionService(scene);
            var first = new CraneOptimizer.UI.Core.Models.SceneDxf { Id = Guid.NewGuid() };
            var second = new CraneOptimizer.UI.Core.Models.SceneDxf { Id = Guid.NewGuid() };
            scene.AddDxfEntity(first);
            scene.AddDxfEntity(second);

            selection.SelectDxf(first, false);
            selection.SelectDxf(second, false);
            Assert.Equal(second.Id, Assert.Single(scene.GetSelectedDxfEntities()).Id);

            selection.SelectDxf(first, true);
            Assert.Equal(2, scene.GetSelectedDxfEntities().Count);
            selection.SelectDxf(first, true);
            Assert.Equal(second.Id, Assert.Single(scene.GetSelectedDxfEntities()).Id);

            selection.SelectDxf(null, false);
            Assert.Empty(scene.GetSelectedDxfEntities());
        }

        [Fact]
        public void DeleteSources_RemovesOnlyTargetsAndGlobalUndoRestoresExactSessionState()
        {
            var (viewModel, scene, history, _) = CreateFixture();
            var firstId = Guid.NewGuid();
            var secondId = Guid.NewGuid();
            viewModel.LoadDxfsFromProject(new[]
            {
                CreateState(firstId, "first.dxf", new[] { 10d, 20d, 1d, 0d }, 0, true),
                CreateState(secondId, "second.dxf", new[] { 30d, 40d, 2d, 15d }, 1, false, true)
            });
            var second = viewModel.LoadedDxfs[1];
            var retainedZone = new GreenAreaZone
            {
                ZoneId = Guid.NewGuid(),
                SourceDxfId = secondId,
                SourceDxfFileName = "second.dxf",
                SourceLayerName = "L-ZONES"
            };
            scene.AddZone(retainedZone);

            viewModel.DeleteSources(new[] { second });

            Assert.Equal(firstId, Assert.Single(viewModel.LoadedDxfs).Id);
            Assert.Same(retainedZone, Assert.Single(scene.GetZones()));
            Assert.True(history.CanUndo);

            history.Undo();
            Assert.Equal(new[] { firstId, secondId }, viewModel.LoadedDxfs.Select(source => source.Id));
            Assert.Equal(new[] { 30d, 40d, 2d, 15d }, viewModel.LoadedDxfs[1].SavedTransformArray.Take(4));
            Assert.True(viewModel.LoadedDxfs[1].IsSelected);

            history.Redo();
            Assert.Equal(firstId, Assert.Single(viewModel.LoadedDxfs).Id);
            Assert.Same(retainedZone, Assert.Single(scene.GetZones()));
        }

        [Theory]
        [InlineData("Allowed", ZoneType.Allowed)]
        [InlineData("Forbidden", ZoneType.Forbidden)]
        public async Task LayerImport_IsOneAtomicHistoryActionWithSourceMetadata(
            string assignment,
            ZoneType expectedType)
        {
            var (viewModel, scene, history, importer) = CreateFixture();
            var sourceId = Guid.NewGuid();
            viewModel.LoadDxfsFromProject(new[]
            {
                CreateState(sourceId, "site.dxf", new[] { 0d, 0d, 1d, 0d }, 0, true)
            });
            var candidates = Enumerable.Range(0, 3).Select(CreateCandidate).ToList();
            importer.Candidates = candidates;

            DxfLayerZoneOperationResult result = await viewModel.ApplyLayerAssignmentAsync(
                viewModel.LoadedDxfs[0],
                new netDxf.DxfDocument(),
                Matrix3D.Identity,
                "L-ZONES",
                DxfLayerZoneAssignment.None,
                assignment == "Allowed"
                    ? DxfLayerZoneAssignment.Allowed
                    : DxfLayerZoneAssignment.Forbidden,
                CancellationToken.None);
            Guid[] ids = scene.GetZones().Select(zone => zone.ZoneId).ToArray();

            Assert.Equal(3, ids.Length);
            Assert.Equal(3, result.AddedCount);
            Assert.All(scene.GetZones(), zone =>
            {
                Assert.Equal(expectedType, zone.Type);
                Assert.Equal(sourceId, zone.SourceDxfId);
                Assert.Equal("site.dxf", zone.SourceDxfFileName);
                Assert.Equal("L-ZONES", zone.SourceLayerName);
                Assert.NotNull(zone.SourceCandidateId);
                Assert.Equal(DxfLayerZoneAssignmentService.ImportMethod, zone.ImportMethod);
            });

            history.Undo();
            Assert.Empty(scene.GetZones());
            history.Redo();
            Assert.Equal(ids, scene.GetZones().Select(zone => zone.ZoneId));
        }

        [Fact]
        public async Task ReplacingAllowedWithForbidden_IsAtomicAndRestoresExactPreviousZones()
        {
            var (viewModel, scene, history, importer) = CreateFixture();
            viewModel.LoadDxfFromProject("site.dxf", null);
            var source = viewModel.LoadedDxfs[0];
            var document = new netDxf.DxfDocument();
            importer.Candidates = new[] { CreateCandidate(0), CreateCandidate(1) };
            await viewModel.ApplyLayerAssignmentAsync(
                source, document, Matrix3D.Identity, "L-ZONES",
                DxfLayerZoneAssignment.None, DxfLayerZoneAssignment.Allowed, CancellationToken.None);
            Guid[] allowedIds = scene.GetZones().Select(zone => zone.ZoneId).ToArray();

            importer.Candidates = new[] { CreateCandidate(2) };
            await viewModel.ApplyLayerAssignmentAsync(
                source, document, Matrix3D.Identity, "L-ZONES",
                DxfLayerZoneAssignment.Allowed, DxfLayerZoneAssignment.Forbidden, CancellationToken.None);
            Guid[] forbiddenIds = scene.GetZones().Select(zone => zone.ZoneId).ToArray();

            Assert.Equal(forbiddenIds, scene.GetZones().Select(zone => zone.ZoneId));
            history.Undo();
            Assert.Equal(allowedIds, scene.GetZones().Select(zone => zone.ZoneId));
            Assert.All(scene.GetZones(), zone => Assert.Equal(ZoneType.Allowed, zone.Type));

            history.Redo();
            Assert.Equal(forbiddenIds, scene.GetZones().Select(zone => zone.ZoneId));
        }

        [Fact]
        public async Task RemoveLayerZones_IsAtomicAndUndoRestoresExactZones()
        {
            var (viewModel, scene, history, importer) = CreateFixture();
            viewModel.LoadDxfFromProject("site.dxf", null);
            var source = viewModel.LoadedDxfs[0];
            var document = new netDxf.DxfDocument();
            importer.Candidates = new[] { CreateCandidate(0), CreateCandidate(1) };
            await viewModel.ApplyLayerAssignmentAsync(
                source, document, Matrix3D.Identity, "L-ZONES",
                DxfLayerZoneAssignment.None, DxfLayerZoneAssignment.Allowed, CancellationToken.None);
            GreenAreaZone[] exactZones = scene.GetZones().ToArray();

            await viewModel.ApplyLayerAssignmentAsync(
                source, document, Matrix3D.Identity, "L-ZONES",
                DxfLayerZoneAssignment.Allowed, DxfLayerZoneAssignment.None, CancellationToken.None);
            Assert.Empty(scene.GetZones());

            history.Undo();
            Assert.Equal(exactZones.Select(zone => zone.ZoneId), scene.GetZones().Select(zone => zone.ZoneId));
            Assert.Equal(
                exactZones.Select(zone => zone.SourceCandidateId),
                scene.GetZones().Select(zone => zone.SourceCandidateId));
            Assert.All(scene.GetZones(), zone =>
            {
                Assert.Equal(DxfLayerZoneAssignmentService.ImportMethod, zone.ImportMethod);
                Assert.Equal("site.dxf", zone.SourceDxfFilePath);
                Assert.Equal(ZoneType.Allowed, zone.OriginalAssignmentType);
                Assert.Equal(0, zone.MinimumHeight);
                Assert.Equal(0, zone.MaximumHeight);
            });
            history.Redo();
            Assert.Empty(scene.GetZones());
        }

        [Fact]
        public async Task RemoveAllLayerZones_IsOneHistoryActionAndUndoRestoresEveryLayer()
        {
            var (viewModel, scene, history, importer) = CreateFixture();
            viewModel.LoadDxfFromProject("site.dxf", null);
            DxfSourceSession source = viewModel.LoadedDxfs[0];
            var document = new netDxf.DxfDocument();

            importer.Candidates = new[] { CreateCandidate(0), CreateCandidate(1) };
            await viewModel.ApplyLayerAssignmentAsync(
                source, document, Matrix3D.Identity, "A-FLOR",
                DxfLayerZoneAssignment.None, DxfLayerZoneAssignment.Allowed, CancellationToken.None);
            importer.Candidates = new[] { CreateCandidate(2), CreateCandidate(3), CreateCandidate(4) };
            await viewModel.ApplyLayerAssignmentAsync(
                source, document, Matrix3D.Identity, "MINE",
                DxfLayerZoneAssignment.None, DxfLayerZoneAssignment.Forbidden, CancellationToken.None);
            GreenAreaZone[] exactZones = scene.GetZones().ToArray();

            await viewModel.ClearAllLayerAssignmentsAsync(
                source,
                new[] { "A-FLOR", "MINE" },
                CancellationToken.None);

            Assert.Empty(scene.GetZones());
            history.Undo();
            Assert.Equal(
                exactZones.Select(zone => zone.ZoneId),
                scene.GetZones().Select(zone => zone.ZoneId));
            history.Redo();
            Assert.Empty(scene.GetZones());
        }

        [Fact]
        public void SourceStateRoundTrip_PreservesOrderingSelectionDisplayAndColors()
        {
            var viewModel = CreateViewModel();
            var sourceId = Guid.NewGuid();
            viewModel.LoadDxfsFromProject(new[]
            {
                new DxfSourceState(
                    sourceId, "stored-name.dxf", new[] { 5d, 6d, 1.5d, 7d },
                    4, true, true, true, true, "OriginalName.dxf")
            });

            var saved = Assert.Single(viewModel.GetSourceStates());
            Assert.Equal(sourceId, saved.Id);
            Assert.Equal("OriginalName.dxf", saved.DisplayName);
            Assert.True(saved.IsActive);
            Assert.True(saved.IsSelected);
            Assert.True(saved.HasSavedTransform);
            Assert.True(saved.UseDxfColors);
            Assert.Equal(new[] { 5d, 6d, 1.5d, 7d }, saved.TransformValues.Take(4));
        }

        [Fact]
        public void ProjectHistory_UndoesAndRedoesAtomicZoneSetWithExactIds()
        {
            var scene = new SceneService();
            var history = new ProjectHistoryService();
            var zones = Enumerable.Range(0, 7).Select(index => new GreenAreaZone
            {
                ZoneId = Guid.NewGuid(),
                Name = $"DXF Zone {index + 1}",
                Type = ZoneType.Allowed,
                Area = index + 1,
                SourceDxfId = Guid.NewGuid(),
                SourceLayerName = "L-ZONES"
            }).ToList();

            history.Execute(new DelegateProjectHistoryAction(
                "import seven DXF layer zones",
                () =>
                {
                    foreach (var zone in zones)
                        scene.AddZone(zone);
                },
                () =>
                {
                    foreach (var zone in zones)
                        scene.RemoveZone(zone);
                }));

            var originalIds = scene.GetZones().Select(zone => zone.ZoneId).ToArray();
            history.Undo();
            Assert.Empty(scene.GetZones());

            history.Redo();
            Assert.Equal(originalIds, scene.GetZones().Select(zone => zone.ZoneId));
            Assert.All(scene.GetZones(), zone => Assert.Equal("L-ZONES", zone.SourceLayerName));
        }

        [Fact]
        public void SceneSelection_SupportsMultipleDxfTargetsAndToggle()
        {
            var scene = new SceneService();
            var selection = new ViewportSelectionService(scene);
            var first = new CraneOptimizer.UI.Core.Models.SceneDxf { Id = Guid.NewGuid(), Layer = "first.dxf" };
            var second = new CraneOptimizer.UI.Core.Models.SceneDxf { Id = Guid.NewGuid(), Layer = "second.dxf" };
            scene.AddDxfEntity(first);
            scene.AddDxfEntity(second);

            selection.Select(first, false);
            selection.Select(second, true);
            Assert.Equal(2, scene.GetSelectedDxfEntities().Count);

            selection.Select(first, true);
            Assert.Single(scene.GetSelectedDxfEntities());
            Assert.Equal(second.Id, scene.SelectedDxfEntity?.Id);
        }

        private static (
            DxfTabViewModel ViewModel,
            SceneService Scene,
            ProjectHistoryService History,
            StubDxfZoneImportService Importer) CreateFixture()
        {
            var scene = new SceneService();
            var history = new ProjectHistoryService();
            var selection = new ViewportSelectionService(scene);
            var importer = new StubDxfZoneImportService();
            var viewModel = new DxfTabViewModel(
                importer,
                scene,
                new ViewportInteractionService(),
                selection,
                history,
                new DxfLayerZoneAssignmentService(scene));
            return (viewModel, scene, history, importer);
        }

        private static DxfTabViewModel CreateViewModel()
        {
            return CreateFixture().ViewModel;
        }

        private static DxfZoneCandidate CreateCandidate(int index)
        {
            var vertices = new[]
            {
                new DomainPoint3D(index, 0, 0),
                new DomainPoint3D(index + 1, 0, 0),
                new DomainPoint3D(index + 1, 1, 0),
                new DomainPoint3D(index, 1, 0)
            };
            return new DxfZoneCandidate(
                Guid.NewGuid(),
                "L-ZONES",
                vertices,
                1d,
                "Polyline2D",
                true,
                Array.Empty<string>());
        }

        private static DxfSourceState CreateState(
            Guid id,
            string path,
            double[] transform,
            int order,
            bool isActive,
            bool isSelected = false) =>
            new(
                id,
                path,
                transform,
                order,
                isActive,
                isSelected,
                true,
                false);

        private sealed class StubDxfZoneImportService : IDxfZoneImportService
        {
            public IReadOnlyList<DxfZoneCandidate> Candidates { get; set; } = Array.Empty<DxfZoneCandidate>();

            public IReadOnlyList<DxfImportLayerSummary> GetLayerSummaries(
                netDxf.DxfDocument document,
                Matrix3D transform,
                CancellationToken cancellationToken = default) =>
                Array.Empty<DxfImportLayerSummary>();

            public IReadOnlyList<DxfZoneCandidate> GetCandidates(
                netDxf.DxfDocument document,
                string layerName,
                Matrix3D transform,
                CancellationToken cancellationToken = default) =>
                Candidates;
        }
    }
}
