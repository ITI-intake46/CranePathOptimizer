using CraneOptimizer.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using CraneOptimizer.UI.Features.F15_ObjectSnap.Services;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Models;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Services;
using System.Diagnostics;
using System.Threading;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Shell.ViewModels
{
    public partial class MainViewModel
    {
        private readonly SemaphoreSlim _projectStateSaveLock = new(1, 1);
        private CancellationTokenSource? _projectStateSaveDebounce;

        private void OnProjectStateChanged() => QueueProjectStateSave();

        private void OnProjectStateCraneChanged(SceneCrane _) => QueueProjectStateSave();

        private void QueueProjectStateSave()
        {
            if (_suppressProjectStateSave || _currentProjectSession == null)
                return;

            var pendingSave = new CancellationTokenSource();
            CancellationTokenSource? previousSave =
                Interlocked.Exchange(ref _projectStateSaveDebounce, pendingSave);
            previousSave?.Cancel();
            _ = SaveProjectStateAfterChangesSettleAsync(pendingSave);
        }

        private async Task SaveProjectStateAfterChangesSettleAsync(CancellationTokenSource pendingSave)
        {
            try
            {
                await Task.Delay(150, pendingSave.Token);
                await SaveCurrentProjectStateAsync();
            }
            catch (OperationCanceledException)
            {
                // A newer scene change superseded this save.
            }
            finally
            {
                Interlocked.CompareExchange(ref _projectStateSaveDebounce, null, pendingSave);
                pendingSave.Dispose();
            }
        }

        private async Task SaveCurrentProjectStateAsync()
        {
            var session = _currentProjectSession;
            if (session == null)
                return;

            await _projectStateSaveLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await _projectStateService.SaveProjectStateAsync(session, BuildProjectStateSnapshot())
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Project state save failed: {ex}");
            }
            finally
            {
                _projectStateSaveLock.Release();
            }
        }

        private ProjectStateSnapshot BuildProjectStateSnapshot()
        {
            var ifcOffset = IfcViewer.PendingPlacementPoint ?? new Point3D(0, 0, 0);
            var elements = Scene.GetElements();
            var firstCOG = elements.FirstOrDefault()?.ElementCOG;

            // Log [IfcSave Diagnosis] as strictly requested in safeguards
            Debug.WriteLine($"[IfcSave Diagnosis] Saved InsertionOffset: ({ifcOffset.X:F3}, {ifcOffset.Y:F3}, {ifcOffset.Z:F3})");
            Debug.WriteLine($"[IfcSave Diagnosis] First Element COG: {(firstCOG != null ? $"({firstCOG.X:F3}, {firstCOG.Y:F3}, {firstCOG.Z:F3})" : "null")}");
            Debug.WriteLine($"[IfcSave Diagnosis] Element Count: {elements.Count}");

            var dxfFiles = Dxf.GetSourceStates()
                .Select(source => new ProjectModelFileState
                {
                    FileType = "DXF",
                    Role = "SitePlan",
                    AbsolutePath = source.FilePath,
                    FileName = source.DisplayName ?? System.IO.Path.GetFileName(source.FilePath),
                    TransformValues = source.TransformValues,
                    SourceId = source.Id,
                    LoadedOrder = source.LoadedOrder,
                    IsSelected = source.IsSelected,
                    IsActiveSource = source.IsActive,
                    HasSavedTransform = source.HasSavedTransform,
                    UseDxfColors = source.UseDxfColors
                })
                .ToList();

            return new ProjectStateSnapshot
            {
                IfcFile = string.IsNullOrWhiteSpace(IfcViewer.LoadedIfcFilePath)
                    ? null
                    : new ProjectIfcFileState
                    {
                        AbsolutePath = IfcViewer.LoadedIfcFilePath,
                        FileName = System.IO.Path.GetFileName(IfcViewer.LoadedIfcFilePath),
                        Role = "Primary",
                        InsertionOffset = new DomainPoint3D(ifcOffset.X, ifcOffset.Y, ifcOffset.Z),
                        RotationDegrees = 0,
                        Scale = 1
                    },
                IfcElements = elements,
                DxfFile = dxfFiles.FirstOrDefault(),
                DxfFiles = dxfFiles,
                Zones = Scene.GetZoneSnapshot()
                    .Select(zone => new ProjectSiteZoneState
                    {
                        ZoneId = zone.ZoneId,
                        Name = zone.Name ?? string.Empty,
                        Type = zone.Type,
                        Area = zone.Area,
                        Vertices = zone.Vertices.ToList(),
                        MinimumHeight = zone.MinimumHeight,
                        MaximumHeight = zone.MaximumHeight,
                        SourceDxfId = zone.SourceDxfId,
                        SourceDxfFileName = zone.SourceDxfFileName,
                        SourceDxfFilePath = zone.SourceDxfFilePath,
                        SourceLayerName = zone.SourceLayerName,
                        SourceCandidateId = zone.SourceCandidateId,
                        ImportMethod = zone.ImportMethod,
                        OriginalAssignmentType = zone.OriginalAssignmentType
                    })
                    .ToList(),
                Cranes = Scene.GetCranes()
                    .Select(crane => new ProjectCraneSetupState
                    {
                        SceneCraneId = crane.Id,
                        SourceCraneId = crane.CraneDefinition?.Id,
                        SourceCraneSpecificationId = crane.ActiveSpecification?.Id,
                        CraneName = crane.CraneDefinition?.Name ?? string.Empty,
                        CraneType = crane.CraneDefinition?.CraneType.ToString() ?? string.Empty,
                        Position = crane.Position,
                        Rotation = crane.Rotation,
                        SlewAngle = crane.SlewAngle,
                        PivotOffset = crane.PivotOffset,
                        BoomAngle = crane.BoomAngle,
                        BoomLength = crane.BoomLength,
                        IsSelected = Scene.SelectedCrane == crane,
                        IsValidPlacement = crane.IsValidPlacement,
                        CurrentRadius = crane.CurrentRadius,
                        CurrentCapacity = crane.CurrentCapacity
                    })
                    .ToList(),
                Viewport = new ProjectViewportState
                {
                    Origin = OriginPoint,
                    ShowCraneRadius = ShowCraneRadius,
                    RenderQualityLabel = RenderQualityLabel
                }
            };
        }

        private async Task LoadProjectStateAsync(ProjectSession session)
        {
            _suppressProjectStateSave = true;
            bool requiresIfcCacheRefresh = false;
            try
            {
                var state = await _projectStateService.LoadProjectStateAsync(session).ConfigureAwait(true);
                await RestoreProjectStateAsync(state).ConfigureAwait(true);
                requiresIfcCacheRefresh = state.RequiresIfcCacheRefresh;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Project state load failed: {ex}");
                _status.Set($"Project opened, but saved project state could not be loaded: {ex.Message}");
            }
            finally
            {
                _suppressProjectStateSave = false;
            }

            if (requiresIfcCacheRefresh && Scene.GetElements().Count > 0)
            {
                await SaveCurrentProjectStateAsync().ConfigureAwait(true);
            }
        }

        private async Task RestoreProjectStateAsync(ProjectStateSnapshot state)
        {
            // Always disable Object Snap by default when restoring/opening projects
            var osnap = App.ServiceProvider?.GetService<IOsnapService>();
            osnap?.SetMasterEnabled(false);

            Scene.ClearZones();
            Scene.ClearCranes();

            if (state.Viewport?.ShowCraneRadius is bool showCraneRadius)
                ShowCraneRadius = showCraneRadius;

            if (!string.IsNullOrWhiteSpace(state.Viewport?.RenderQualityLabel))
                RenderQualityLabel = state.Viewport.RenderQualityLabel;

            if (state.Viewport?.Origin != null)
                SetOrigin(state.Viewport.Origin);

            if (state.IfcFile != null && System.IO.File.Exists(state.IfcFile.AbsolutePath))
            {
                var offset = state.IfcFile.InsertionOffset;
                var restored = await IfcViewer.LoadIfcFromProjectAsync(
                    state.IfcFile.AbsolutePath,
                    new Point3D(offset.X, offset.Y, offset.Z),
                    state.IfcElements).ConfigureAwait(true);

                if (restored)
                    CurrentProjectIfcPath = state.IfcFile.AbsolutePath;
            }

            foreach (var zone in state.Zones)
            {
                Scene.AddZone(new GreenAreaZone
                {
                    ZoneId = zone.ZoneId,
                    Name = zone.Name,
                    Type = zone.Type,
                    Area = zone.Area,
                    Vertices = zone.Vertices.ToList(),
                    MinimumHeight = zone.MinimumHeight,
                    MaximumHeight = zone.MaximumHeight,
                    SourceDxfId = zone.SourceDxfId,
                    SourceDxfFileName = zone.SourceDxfFileName,
                    SourceDxfFilePath = zone.SourceDxfFilePath,
                    SourceLayerName = zone.SourceLayerName,
                    SourceCandidateId = zone.SourceCandidateId,
                    ImportMethod = zone.ImportMethod,
                    OriginalAssignmentType = zone.OriginalAssignmentType
                });
            }

            await RestoreCranesAsync(state.Cranes).ConfigureAwait(true);

            var dxfFiles = state.DxfFiles.Count > 0
                ? state.DxfFiles
                : state.DxfFile == null
                    ? new List<ProjectModelFileState>()
                    : new List<ProjectModelFileState> { state.DxfFile };
            Dxf.LoadDxfsFromProject(dxfFiles.Select((file, index) =>
                new CraneOptimizer.UI.Features.F11_DXF.DTOs.DxfSourceState(
                    file.SourceId ?? Guid.NewGuid(),
                    file.AbsolutePath,
                    file.TransformValues ?? Array.Empty<double>(),
                    file.LoadedOrder == 0 ? index : file.LoadedOrder,
                    file.IsActiveSource || dxfFiles.Count == 1,
                    file.IsSelected,
                    file.HasSavedTransform || file.TransformValues?.Length >= 4,
                    file.UseDxfColors,
                    file.FileName)));

            ReRenderElements();
            ReRenderCranes();
            _status.Set($"Project restored: {CurrentProjectName}", "Saved project data loaded.");
        }

        private async Task RestoreCranesAsync(IReadOnlyList<ProjectCraneSetupState> craneStates)
        {
            if (craneStates.Count == 0)
                return;

            var availableCranes = await _craneRepo.GetAllWithSpecsAsync().ConfigureAwait(true);
            SceneCrane? selectedCrane = null;

            foreach (var craneState in craneStates)
            {
                var craneDefinition = craneState.SourceCraneId.HasValue
                    ? availableCranes.FirstOrDefault(crane => crane.Id == craneState.SourceCraneId.Value)
                    : null;

                craneDefinition ??= availableCranes.FirstOrDefault(crane =>
                    string.Equals(crane.Name, craneState.CraneName, StringComparison.OrdinalIgnoreCase));

                if (craneDefinition == null)
                    continue;

                var activeSpec = craneState.SourceCraneSpecificationId.HasValue
                    ? craneDefinition.Specifications.FirstOrDefault(spec => spec.Id == craneState.SourceCraneSpecificationId.Value)
                    : null;

                activeSpec ??= craneDefinition.Specifications
                    .OrderByDescending(spec => spec.MaxRadius)
                    .FirstOrDefault();

                var sceneCrane = new SceneCrane
                {
                    Id = craneState.SceneCraneId,
                    CraneDefinition = craneDefinition,
                    ActiveSpecification = activeSpec,
                    Position = craneState.Position,
                    Rotation = craneState.Rotation,
                    SlewAngle = craneState.SlewAngle,
                    PivotOffset = craneState.PivotOffset,
                    BoomAngle = craneState.BoomAngle,
                    BoomLength = craneState.BoomLength,
                    IsValidPlacement = craneState.IsValidPlacement,
                    CurrentRadius = craneState.CurrentRadius,
                    CurrentCapacity = craneState.CurrentCapacity
                };

                Scene.AddCrane(sceneCrane);

                if (craneState.IsSelected)
                    selectedCrane = sceneCrane;
            }

            if (selectedCrane != null)
                SelectedSceneCrane = selectedCrane;
        }
    }
}
