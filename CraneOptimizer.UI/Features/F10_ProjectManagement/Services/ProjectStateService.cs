using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.CreatedProject;
using CraneOptimizer.Domain.CreatedProject.IfcFiles;
using CraneOptimizer.Domain.CreatedProject.ProjectState;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.Infrastructure.Contexts.CreatedProject;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Models;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Text.Json;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Mappers;
using System.Diagnostics;
using CraneOptimizer.Domain.Interfaces;

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.Services
{
    public sealed class ProjectStateService : IProjectStateService
    {
        private const string IfcFolder = "Models/IFC";
        private const string DxfFolder = "Models/DXF";
        private const string IfcNormalizationCacheVersionKey = "ifcNormalizationCacheVersion";
        private const string IfcNormalizationCacheVersion = "ifc-identity-grid-ssot-v3";
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly IProjectCoordinateTransformService _transformService;

        public ProjectStateService() : this(new CraneOptimizer.Application.Services.ProjectCoordinateTransformService())
        {
        }

        public ProjectStateService(IProjectCoordinateTransformService transformService)
        {
            _transformService = transformService ?? throw new ArgumentNullException(nameof(transformService));
        }

        public async Task SaveProjectStateAsync(
            ProjectSession session,
            ProjectStateSnapshot state,
            CancellationToken cancellationToken = default)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            // Save coordinate reference to JSON (as fallback)
            var refFilePath = Path.Combine(session.ProjectFolderPath, "coordinate_reference.json");
            try
            {
                var refData = _transformService.CurrentReference;
                var json = JsonSerializer.Serialize(refData, JsonOptions);
                await File.WriteAllTextAsync(refFilePath, json, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save coordinate reference to JSON fallback: {ex}");
            }

            await using var context = CreatedProjectDbContextFactory.Create(session.ProjectFolderPath);
            var projectInfo = await GetProjectInfoAsync(context, session.ProjectInfoId, cancellationToken)
                .ConfigureAwait(false);

            await SaveIfcAsync(context, session, projectInfo.Id, state.IfcFile, state.IfcElements, cancellationToken)
                .ConfigureAwait(false);
            IReadOnlyList<ProjectModelFileState> dxfFiles = state.DxfFiles.Count > 0
                ? state.DxfFiles
                : state.DxfFile == null
                    ? Array.Empty<ProjectModelFileState>()
                    : new[] { state.DxfFile };
            await SaveModelFilesAsync(context, session, projectInfo.Id, dxfFiles, cancellationToken)
                .ConfigureAwait(false);
            await SaveZonesAsync(context, projectInfo.Id, state.Zones, cancellationToken)
                .ConfigureAwait(false);
            await SaveCranesAsync(context, projectInfo.Id, state.Cranes, cancellationToken)
                .ConfigureAwait(false);
            await SaveViewportAsync(context, projectInfo.Id, state.Viewport, cancellationToken)
                .ConfigureAwait(false);
            await SaveCoordinateReferenceAsync(context, projectInfo.Id, cancellationToken)
                .ConfigureAwait(false);

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<ProjectStateSnapshot> LoadProjectStateAsync(
            ProjectSession session,
            CancellationToken cancellationToken = default)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            await using var context = CreatedProjectDbContextFactory.Create(session.ProjectFolderPath);
            var projectInfo = await GetProjectInfoAsync(context, session.ProjectInfoId, cancellationToken)
                .ConfigureAwait(false);

            await LoadCoordinateReferenceAsync(context, session.ProjectFolderPath, projectInfo.Id, cancellationToken)
                .ConfigureAwait(false);

            var ifc = await context.IfcFiles
                .AsNoTracking()
                .Where(x => x.ProjectInfoId == projectInfo.Id && x.IsActive)
                .OrderBy(x => x.Id)
                .LastOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            var dxfFiles = await context.ModelFiles
                .AsNoTracking()
                .Where(x => x.ProjectInfoId == projectInfo.Id && x.IsActive && x.FileType == "DXF")
                .OrderBy(x => x.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var zones = await context.SiteZones
                .AsNoTracking()
                .Where(x => x.ProjectInfoId == projectInfo.Id)
                .OrderBy(x => x.SortOrder)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var cranes = await context.CraneSetups
                .AsNoTracking()
                .Where(x => x.ProjectInfoId == projectInfo.Id)
                .OrderBy(x => x.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var viewport = await context.ViewportStates
                .AsNoTracking()
                .Where(x => x.ProjectInfoId == projectInfo.Id && x.StateKey == "Main")
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            List<PrecastElement> restoredElements = new();
            bool requiresIfcCacheRefresh = false;
            if (ifc != null)
            {
                if (ValidateIfcCache(session.ProjectFolderPath, ifc, out var validationReason))
                {
                    var entities = await context.IfcElements
                        .AsNoTracking()
                        .Where(x => x.IfcFileId == ifc.Id)
                        .OrderBy(x => x.Id)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);

                    foreach (var entity in entities)
                    {
                        restoredElements.Add(PrecastElementMapper.ToDomain(entity));
                    }
                    CraneOptimizer.Domain.Services.AssemblyRollupService.ReconstructAssemblyClassifications(restoredElements);
                    Debug.WriteLine($"[ProjectState] DB Restore: Valid cache. Loaded {restoredElements.Count} elements from database.");
                }
                else
                {
                    Debug.WriteLine($"[ProjectState] DB Restore Fallback: Cache is invalid/missing. Reason: {validationReason}");
                    requiresIfcCacheRefresh = true;
                }
            }

            return new ProjectStateSnapshot
            {
                IfcFile = ToIfcState(session.ProjectFolderPath, ifc),
                IfcElements = restoredElements,
                RequiresIfcCacheRefresh = requiresIfcCacheRefresh,
                DxfFile = ToModelFileState(session.ProjectFolderPath, dxfFiles.LastOrDefault()),
                DxfFiles = dxfFiles.Select(entity => ToModelFileState(session.ProjectFolderPath, entity)!)
                    .Where(state => state != null)
                    .OrderBy(state => state.LoadedOrder)
                    .ToList(),
                Zones = zones.Select(ToZoneState).ToList(),
                Cranes = cranes.Select(ToCraneState).ToList(),
                Viewport = ToViewportState(viewport)
            };
        }

        private static bool ValidateIfcCache(
            string projectFolderPath,
            ProjectIfcFileEntity? entity,
            out string validationReason)
        {
            if (entity == null)
            {
                validationReason = "No persisted IFC file metadata in database.";
                return false;
            }

            if (!entity.IsCacheValid)
            {
                validationReason = "Cache was explicitly marked invalid in database.";
                return false;
            }

            if (entity.ElementCount <= 0)
            {
                validationReason = "Persisted element count in database is zero or negative.";
                return false;
            }

            if (!HasCurrentIfcNormalizationCacheVersion(entity.MetadataJson))
            {
                validationReason = "IFC normalization cache version is missing or outdated; elements must be rebuilt with current identity and grid rules.";
                return false;
            }

            var absolutePath = ToAbsolutePath(projectFolderPath, entity.RelativePath);
            if (!File.Exists(absolutePath))
            {
                validationReason = $"IFC file not found at project path: {absolutePath}";
                return false;
            }

            var fileInfo = new FileInfo(absolutePath);

            // Compare file size
            if (fileInfo.Length != entity.FileSizeBytes)
            {
                validationReason = $"IFC file size changed. Disk: {fileInfo.Length} bytes, DB: {entity.FileSizeBytes} bytes.";
                return false;
            }

            // Compare last write time using UTC and standard tolerance of 2 seconds
            if (entity.LastWriteUtc.HasValue)
            {
                var dbUtc = DateTime.SpecifyKind(entity.LastWriteUtc.Value, DateTimeKind.Utc);
                var diff = (fileInfo.LastWriteTimeUtc - dbUtc).Duration();
                if (diff > TimeSpan.FromSeconds(2))
                {
                    validationReason = $"IFC last write time changed. Disk: {fileInfo.LastWriteTimeUtc.ToUniversalTime()}, DB: {dbUtc}.";
                    return false;
                }
            }

            validationReason = "Valid";
            return true;
        }

        private static async Task<ProjectInfoEntity> GetProjectInfoAsync(
            CreatedProjectDbContext context,
            int projectInfoId,
            CancellationToken cancellationToken)
        {
            var projectInfo = await context.ProjectInfo
                .FirstOrDefaultAsync(x => x.Id == projectInfoId, cancellationToken)
                .ConfigureAwait(false);

            if (projectInfo == null)
                throw new InvalidOperationException("Project database does not contain ProjectInfo.");

            return projectInfo;
        }

        private static async Task SaveIfcAsync(
            CreatedProjectDbContext context,
            ProjectSession session,
            int projectInfoId,
            ProjectIfcFileState? ifc,
            List<PrecastElement> elements,
            CancellationToken cancellationToken)
        {
            if (ifc == null || string.IsNullOrWhiteSpace(ifc.AbsolutePath) || !File.Exists(ifc.AbsolutePath))
            {
                var existing = await context.IfcFiles
                    .Where(x => x.ProjectInfoId == projectInfoId)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                context.IfcFiles.RemoveRange(existing);

                var existingElements = await context.IfcElements
                    .Where(x => x.ProjectInfoId == projectInfoId)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                context.IfcElements.RemoveRange(existingElements);
                return;
            }

            var asset = CopyAssetIntoProject(session.ProjectFolderPath, ifc.AbsolutePath, IfcFolder);
            var metadata = ReadFileMetadata(asset.AbsolutePath);
            var transform = new ProjectIfcTransformDto
            {
                OffsetX = ifc.InsertionOffset.X,
                OffsetY = ifc.InsertionOffset.Y,
                OffsetZ = ifc.InsertionOffset.Z,
                RotationDegrees = ifc.RotationDegrees,
                Scale = ifc.Scale
            };

            var existingFile = await context.IfcFiles
                .FirstOrDefaultAsync(x => x.ProjectInfoId == projectInfoId && x.IsActive, cancellationToken)
                .ConfigureAwait(false);

            bool canReuseCache = false;
            if (existingFile != null
                && string.Equals(existingFile.RelativePath, asset.RelativePath, StringComparison.OrdinalIgnoreCase)
                && existingFile.FileSizeBytes == metadata.SizeBytes
                && existingFile.IsCacheValid
                && HasCurrentIfcNormalizationCacheVersion(existingFile.MetadataJson))
            {
                if (existingFile.LastWriteUtc.HasValue && metadata.LastWriteUtc.HasValue)
                {
                    var existingUtc = DateTime.SpecifyKind(existingFile.LastWriteUtc.Value, DateTimeKind.Utc);
                    var metadataUtc = DateTime.SpecifyKind(metadata.LastWriteUtc.Value, DateTimeKind.Utc);
                    var diff = (existingUtc - metadataUtc).Duration();
                    if (diff <= TimeSpan.FromSeconds(2))
                    {
                        canReuseCache = true;
                    }
                }
            }

            if (canReuseCache && existingFile != null)
            {
                var persistedIdentities = await context.IfcElements
                    .AsNoTracking()
                    .Where(x => x.IfcFileId == existingFile.Id)
                    .Select(x => new { x.GlobalId, x.DisplayName })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                var runtimeNames = elements
                    .Where(element => !string.IsNullOrWhiteSpace(element.ElementID))
                    .GroupBy(element => element.ElementID, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.First().ElementName, StringComparer.Ordinal);

                canReuseCache = persistedIdentities.Count > 0
                    && persistedIdentities.Count == runtimeNames.Count
                    && persistedIdentities.All(row => runtimeNames.TryGetValue(row.GlobalId, out var runtimeName)
                        && string.Equals(row.DisplayName, runtimeName, StringComparison.Ordinal));
            }

            if (canReuseCache && existingFile != null)
            {
                existingFile.TransformJson = JsonSerializer.Serialize(transform, JsonOptions);
                existingFile.MetadataJson = StampIfcNormalizationCacheVersion(ifc.MetadataJson);
                context.IfcFiles.Update(existingFile);
                Debug.WriteLine("[SaveProject] Fast Save: Reused existing IFC element cache. Only updated transform.");
                return;
            }

            // Otherwise, do full save
            var oldFiles = await context.IfcFiles
                .Where(x => x.ProjectInfoId == projectInfoId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            context.IfcFiles.RemoveRange(oldFiles);

            var oldElements = await context.IfcElements
                .Where(x => x.ProjectInfoId == projectInfoId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            context.IfcElements.RemoveRange(oldElements);

            var newFile = new ProjectIfcFileEntity
            {
                ProjectInfoId = projectInfoId,
                FileName = asset.FileName,
                RelativePath = asset.RelativePath,
                OriginalFullPath = ifc.AbsolutePath,
                Role = ifc.Role,
                FileSizeBytes = metadata.SizeBytes,
                LastWriteUtc = metadata.LastWriteUtc,
                TransformJson = JsonSerializer.Serialize(transform, JsonOptions),
                MetadataJson = StampIfcNormalizationCacheVersion(ifc.MetadataJson),
                IsActive = true,
                IsCacheValid = true,
                ImportedAtUtc = DateTime.UtcNow,
                ElementCount = elements.Count
            };

            await context.IfcFiles.AddAsync(newFile, cancellationToken).ConfigureAwait(false);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            Debug.WriteLine($"[SaveIfcAsync] elements count: {elements.Count}, newFile.Id: {newFile.Id}");

            foreach (var el in elements)
            {
                var entity = PrecastElementMapper.ToEntity(el, projectInfoId, newFile.Id);
                await context.IfcElements.AddAsync(entity, cancellationToken).ConfigureAwait(false);
                Debug.WriteLine($"[SaveIfcAsync] Tracked element entity: {entity.GlobalId} with IfcFileId: {entity.IfcFileId}");
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var verifyCount = await context.IfcElements.CountAsync(x => x.IfcFileId == newFile.Id, cancellationToken).ConfigureAwait(false);
            Debug.WriteLine($"[SaveIfcAsync] Verification count in DB: {verifyCount}");

            Debug.WriteLine($"[SaveProject] Full Save: Persisted new IFC file metadata and {elements.Count} elements to database.");
        }

        private static async Task SaveModelFilesAsync(
            CreatedProjectDbContext context,
            ProjectSession session,
            int projectInfoId,
            IReadOnlyList<ProjectModelFileState> dxfFiles,
            CancellationToken cancellationToken)
        {
            var existing = await context.ModelFiles
                .Where(x => x.ProjectInfoId == projectInfoId && x.FileType == "DXF")
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var existingBySourceId = existing
                .Select(entity => (Entity: entity, Metadata: Deserialize<ProjectDxfMetadataDto>(entity.MetadataJson)))
                .Where(item => item.Metadata?.SourceId != null)
                .GroupBy(item => item.Metadata!.SourceId!.Value)
                .ToDictionary(group => group.Key, group => group.First().Entity);

            context.ModelFiles.RemoveRange(existing);

            foreach (var dxf in dxfFiles.Where(dxf => !string.IsNullOrWhiteSpace(dxf.AbsolutePath)))
            {
                string relativePath;
                string fileName;
                ProjectFileMetadata fileMetadata;
                if (File.Exists(dxf.AbsolutePath))
                {
                    var asset = CopyDxfAssetIntoProject(
                        session.ProjectFolderPath,
                        dxf.AbsolutePath,
                        dxf.SourceId ?? Guid.NewGuid());
                    relativePath = asset.RelativePath;
                    fileName = string.IsNullOrWhiteSpace(dxf.FileName) ? asset.FileName : dxf.FileName;
                    fileMetadata = ReadFileMetadata(asset.AbsolutePath);
                }
                else if (dxf.SourceId is Guid sourceId &&
                         existingBySourceId.TryGetValue(sourceId, out var prior))
                {
                    relativePath = prior.RelativePath;
                    fileName = string.IsNullOrWhiteSpace(dxf.FileName) ? prior.FileName : dxf.FileName;
                    fileMetadata = new ProjectFileMetadata(prior.FileSizeBytes, prior.LastWriteUtc);
                }
                else
                {
                    relativePath = dxf.AbsolutePath;
                    fileName = string.IsNullOrWhiteSpace(dxf.FileName)
                        ? Path.GetFileName(dxf.AbsolutePath)
                        : dxf.FileName;
                    fileMetadata = new ProjectFileMetadata(0, null);
                }

                var sourceMetadata = new ProjectDxfMetadataDto
                {
                    SourceId = dxf.SourceId,
                    LoadedOrder = dxf.LoadedOrder,
                    IsSelected = dxf.IsSelected,
                    IsActive = dxf.IsActiveSource,
                    HasSavedTransform = dxf.HasSavedTransform,
                    UseDxfColors = dxf.UseDxfColors
                };

                await context.ModelFiles.AddAsync(new ProjectModelFileEntity
                {
                    ProjectInfoId = projectInfoId,
                    FileType = "DXF",
                    Role = string.IsNullOrWhiteSpace(dxf.Role) ? "SitePlan" : dxf.Role,
                    FileName = fileName,
                    RelativePath = relativePath,
                    OriginalFullPath = dxf.AbsolutePath,
                    FileSizeBytes = fileMetadata.SizeBytes,
                    LastWriteUtc = fileMetadata.LastWriteUtc,
                    TransformJson = JsonSerializer.Serialize(dxf.TransformValues ?? Array.Empty<double>(), JsonOptions),
                    MetadataJson = JsonSerializer.Serialize(sourceMetadata, JsonOptions),
                    IsActive = true,
                    ImportedAtUtc = DateTime.UtcNow
                }, cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task SaveZonesAsync(
            CreatedProjectDbContext context,
            int projectInfoId,
            IReadOnlyList<ProjectSiteZoneState> zones,
            CancellationToken cancellationToken)
        {
            var existing = await context.SiteZones
                .Where(x => x.ProjectInfoId == projectInfoId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            context.SiteZones.RemoveRange(existing);

            for (var i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                await context.SiteZones.AddAsync(new SiteZoneEntity
                {
                    ProjectInfoId = projectInfoId,
                    ZoneGuid = zone.ZoneId.ToString("N"),
                    Name = zone.Name,
                    ZoneType = zone.Type.ToString(),
                    Area = zone.Area,
                    SortOrder = i,
                    VerticesJson = JsonSerializer.Serialize(new ProjectZoneEnvelope
                    {
                        Vertices = zone.Vertices,
                        MinimumHeight = zone.MinimumHeight,
                        MaximumHeight = zone.MaximumHeight,
                        SourceDxfId = zone.SourceDxfId,
                        SourceDxfFileName = zone.SourceDxfFileName,
                        SourceDxfFilePath = zone.SourceDxfFilePath,
                        SourceLayerName = zone.SourceLayerName,
                        SourceCandidateId = zone.SourceCandidateId,
                        ImportMethod = zone.ImportMethod,
                        OriginalAssignmentType = zone.OriginalAssignmentType
                    }, JsonOptions)
                }, cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task SaveCranesAsync(
            CreatedProjectDbContext context,
            int projectInfoId,
            IReadOnlyList<ProjectCraneSetupState> cranes,
            CancellationToken cancellationToken)
        {
            var existing = await context.CraneSetups
                .Where(x => x.ProjectInfoId == projectInfoId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            context.CraneSetups.RemoveRange(existing);

            foreach (var crane in cranes)
            {
                await context.CraneSetups.AddAsync(new CraneSetupEntity
                {
                    ProjectInfoId = projectInfoId,
                    SceneCraneGuid = crane.SceneCraneId.ToString("N"),
                    SourceCraneId = crane.SourceCraneId?.ToString(),
                    SourceCraneSpecificationId = crane.SourceCraneSpecificationId,
                    CraneName = crane.CraneName,
                    CraneType = crane.CraneType,
                    PositionX = crane.Position.X,
                    PositionY = crane.Position.Y,
                    PositionZ = crane.Position.Z,
                    Rotation = crane.Rotation,
                    SlewAngle = crane.SlewAngle,
                    PivotOffsetX = crane.PivotOffset.X,
                    PivotOffsetY = crane.PivotOffset.Y,
                    PivotOffsetZ = crane.PivotOffset.Z,
                    BoomAngle = crane.BoomAngle,
                    BoomLength = crane.BoomLength,
                    IsSelected = crane.IsSelected,
                    IsValidPlacement = crane.IsValidPlacement,
                    CurrentRadius = crane.CurrentRadius,
                    CurrentCapacity = crane.CurrentCapacity,
                    ConfigurationJson = crane.ConfigurationJson
                }, cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task SaveViewportAsync(
            CreatedProjectDbContext context,
            int projectInfoId,
            ProjectViewportState? viewport,
            CancellationToken cancellationToken)
        {
            var existing = await context.ViewportStates
                .Where(x => x.ProjectInfoId == projectInfoId && x.StateKey == "Main")
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            context.ViewportStates.RemoveRange(existing);

            if (viewport == null)
                return;

            await context.ViewportStates.AddAsync(new ProjectViewportStateEntity
            {
                ProjectInfoId = projectInfoId,
                StateKey = "Main",
                OriginX = viewport.Origin?.X,
                OriginY = viewport.Origin?.Y,
                OriginZ = viewport.Origin?.Z,
                ShowCraneRadius = viewport.ShowCraneRadius,
                RenderQualityLabel = viewport.RenderQualityLabel,
                CameraJson = viewport.CameraJson,
                UpdatedAtUtc = DateTime.UtcNow
            }, cancellationToken).ConfigureAwait(false);
        }

        private static ProjectIfcFileState? ToIfcState(string projectFolderPath, ProjectIfcFileEntity? entity)
        {
            if (entity == null)
                return null;

            var transform = Deserialize<ProjectIfcTransformDto>(entity.TransformJson) ?? new ProjectIfcTransformDto();

            return new ProjectIfcFileState
            {
                AbsolutePath = ToAbsolutePath(projectFolderPath, entity.RelativePath),
                FileName = entity.FileName,
                Role = entity.Role,
                InsertionOffset = new DomainPoint3D(transform.OffsetX, transform.OffsetY, transform.OffsetZ),
                RotationDegrees = transform.RotationDegrees,
                Scale = transform.Scale <= 0 ? 1d : transform.Scale,
                MetadataJson = entity.MetadataJson
            };
        }

        private static bool HasCurrentIfcNormalizationCacheVersion(string? metadataJson)
        {
            if (string.IsNullOrWhiteSpace(metadataJson))
                return false;

            try
            {
                using var document = JsonDocument.Parse(metadataJson);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return false;

                return document.RootElement.TryGetProperty(IfcNormalizationCacheVersionKey, out var version)
                    && string.Equals(version.GetString(), IfcNormalizationCacheVersion, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private static string StampIfcNormalizationCacheVersion(string? metadataJson)
        {
            var metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(metadataJson))
            {
                try
                {
                    var existing = JsonSerializer.Deserialize<Dictionary<string, object?>>(metadataJson, JsonOptions);
                    if (existing != null)
                    {
                        foreach (var pair in existing)
                        {
                            metadata[pair.Key] = pair.Value;
                        }
                    }
                }
                catch
                {
                    metadata["rawMetadata"] = metadataJson;
                }
            }

            metadata[IfcNormalizationCacheVersionKey] = IfcNormalizationCacheVersion;
            return JsonSerializer.Serialize(metadata, JsonOptions);
        }

        private static ProjectModelFileState? ToModelFileState(string projectFolderPath, ProjectModelFileEntity? entity)
        {
            if (entity == null)
                return null;

            var metadata = Deserialize<ProjectDxfMetadataDto>(entity.MetadataJson);
            return new ProjectModelFileState
            {
                FileType = entity.FileType,
                Role = entity.Role,
                AbsolutePath = ToAbsolutePath(projectFolderPath, entity.RelativePath),
                FileName = entity.FileName,
                TransformValues = Deserialize<double[]>(entity.TransformJson),
                MetadataJson = entity.MetadataJson,
                SourceId = metadata?.SourceId,
                LoadedOrder = metadata?.LoadedOrder ?? 0,
                IsSelected = metadata?.IsSelected ?? false,
                IsActiveSource = metadata?.IsActive ?? false,
                HasSavedTransform = metadata?.HasSavedTransform ?? false,
                UseDxfColors = metadata?.UseDxfColors ?? false
            };
        }

        private static ProjectSiteZoneState ToZoneState(SiteZoneEntity entity)
        {
            _ = Guid.TryParse(entity.ZoneGuid, out var zoneId);
            _ = Enum.TryParse<ZoneType>(entity.ZoneType, out var zoneType);

            var envelope = Deserialize<ProjectZoneEnvelope>(entity.VerticesJson);
            var vertices = envelope?.Vertices
                ?? Deserialize<List<DomainPoint3D>>(entity.VerticesJson)
                ?? new List<DomainPoint3D>();
            return new ProjectSiteZoneState
            {
                ZoneId = zoneId == Guid.Empty ? Guid.NewGuid() : zoneId,
                Name = entity.Name,
                Type = zoneType,
                Area = entity.Area,
                Vertices = vertices,
                MinimumHeight = envelope?.MinimumHeight,
                MaximumHeight = envelope?.MaximumHeight,
                SourceDxfId = envelope?.SourceDxfId,
                SourceDxfFileName = envelope?.SourceDxfFileName,
                SourceDxfFilePath = envelope?.SourceDxfFilePath,
                SourceLayerName = envelope?.SourceLayerName,
                SourceCandidateId = envelope?.SourceCandidateId,
                ImportMethod = envelope?.ImportMethod,
                OriginalAssignmentType = envelope?.OriginalAssignmentType
            };
        }

        private static ProjectCraneSetupState ToCraneState(CraneSetupEntity entity)
        {
            _ = Guid.TryParse(entity.SceneCraneGuid, out var sceneCraneId);
            int? sourceCraneId = int.TryParse(entity.SourceCraneId, out var parsedSourceCraneId)
                ? parsedSourceCraneId
                : null;

            return new ProjectCraneSetupState
            {
                SceneCraneId = sceneCraneId == Guid.Empty ? Guid.NewGuid() : sceneCraneId,
                SourceCraneId = sourceCraneId,
                SourceCraneSpecificationId = entity.SourceCraneSpecificationId,
                CraneName = entity.CraneName,
                CraneType = entity.CraneType,
                Position = new DomainPoint3D(entity.PositionX, entity.PositionY, entity.PositionZ),
                Rotation = entity.Rotation,
                SlewAngle = entity.SlewAngle,
                PivotOffset = new DomainPoint3D(entity.PivotOffsetX, entity.PivotOffsetY, entity.PivotOffsetZ),
                BoomAngle = entity.BoomAngle,
                BoomLength = entity.BoomLength,
                IsSelected = entity.IsSelected,
                IsValidPlacement = entity.IsValidPlacement,
                CurrentRadius = entity.CurrentRadius,
                CurrentCapacity = entity.CurrentCapacity,
                ConfigurationJson = entity.ConfigurationJson
            };
        }

        private static ProjectViewportState? ToViewportState(ProjectViewportStateEntity? entity)
        {
            if (entity == null)
                return null;

            DomainPoint3D? origin = entity.OriginX.HasValue && entity.OriginY.HasValue && entity.OriginZ.HasValue
                ? new DomainPoint3D(entity.OriginX.Value, entity.OriginY.Value, entity.OriginZ.Value)
                : null;

            return new ProjectViewportState
            {
                Origin = origin,
                ShowCraneRadius = entity.ShowCraneRadius,
                RenderQualityLabel = entity.RenderQualityLabel,
                CameraJson = entity.CameraJson
            };
        }

        private static T? Deserialize<T>(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return default;

            try
            {
                return JsonSerializer.Deserialize<T>(json, JsonOptions);
            }
            catch
            {
                return default;
            }
        }

        private static ProjectAssetPath CopyAssetIntoProject(
            string projectFolderPath,
            string sourcePath,
            string relativeFolder)
        {
            var normalizedProjectFolder = Path.GetFullPath(projectFolderPath);
            var sourceFullPath = Path.GetFullPath(sourcePath);
            var fileName = Path.GetFileName(sourceFullPath);
            var destinationFolder = Path.Combine(normalizedProjectFolder, relativeFolder.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(destinationFolder);

            var destinationPath = Path.Combine(destinationFolder, fileName);
            if (!PathsEqual(sourceFullPath, destinationPath))
                File.Copy(sourceFullPath, destinationPath, overwrite: true);

            return new ProjectAssetPath(
                Path.GetRelativePath(normalizedProjectFolder, destinationPath).Replace(Path.DirectorySeparatorChar, '/'),
                destinationPath,
                fileName);
        }

        private static ProjectAssetPath CopyDxfAssetIntoProject(
            string projectFolderPath,
            string sourcePath,
            Guid sourceId)
        {
            var normalizedProjectFolder = Path.GetFullPath(projectFolderPath);
            var sourceFullPath = Path.GetFullPath(sourcePath);
            var fileName = Path.GetFileName(sourceFullPath);
            var destinationFolder = Path.Combine(normalizedProjectFolder, DxfFolder.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(destinationFolder);

            var storedFileName = $"{sourceId:N}_{fileName}";
            var destinationPath = Path.Combine(destinationFolder, storedFileName);
            if (!PathsEqual(sourceFullPath, destinationPath))
                File.Copy(sourceFullPath, destinationPath, overwrite: true);

            return new ProjectAssetPath(
                Path.GetRelativePath(normalizedProjectFolder, destinationPath).Replace(Path.DirectorySeparatorChar, '/'),
                destinationPath,
                fileName);
        }

        private async Task SaveCoordinateReferenceAsync(
            CreatedProjectDbContext context,
            int projectInfoId,
            CancellationToken cancellationToken)
        {
            try
            {
                var refData = _transformService.CurrentReference;

                var dbRef = await context.CoordinateReferences
                    .FirstOrDefaultAsync(x => x.ProjectInfoId == projectInfoId, cancellationToken)
                    .ConfigureAwait(false);

                if (dbRef == null)
                {
                    dbRef = new ProjectCoordinateReferenceEntity
                    {
                        ProjectInfoId = projectInfoId,
                        CreatedAtUtc = DateTime.UtcNow
                    };
                    await context.CoordinateReferences.AddAsync(dbRef, cancellationToken).ConfigureAwait(false);
                }

                dbRef.IsEnabled = refData.IsEnabled;
                dbRef.Name = refData.Name ?? "Coordinate Reference";
                dbRef.CoordinateSystemName = refData.CoordinateSystemName;
                dbRef.EPSGCode = refData.EPSGCode;
                dbRef.LocalReferenceX = refData.LocalReferenceX;
                dbRef.LocalReferenceY = refData.LocalReferenceY;
                dbRef.LocalReferenceZ = refData.LocalReferenceZ;
                dbRef.SurveyEasting = refData.SurveyEasting;
                dbRef.SurveyNorthing = refData.SurveyNorthing;
                dbRef.SurveyElevation = refData.SurveyElevation;
                dbRef.RotationToNorthDegrees = refData.RotationToNorthDegrees;
                dbRef.Scale = refData.Scale;
                dbRef.Source = refData.Source ?? "ManualSinglePoint";
                dbRef.Latitude = refData.Latitude;
                dbRef.Longitude = refData.Longitude;
                dbRef.Description = refData.Description;
                dbRef.UpdatedAtUtc = DateTime.UtcNow;

                context.CoordinateReferences.Update(dbRef);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save coordinate reference to DB: {ex}");
            }
        }

        private async Task LoadCoordinateReferenceAsync(
            CreatedProjectDbContext context,
            string projectFolderPath,
            int projectInfoId,
            CancellationToken cancellationToken)
        {
            ProjectCoordinateReferenceEntity? dbRef = null;
            try
            {
                dbRef = await context.CoordinateReferences
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.ProjectInfoId == projectInfoId, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load coordinate reference from DB: {ex}");
            }

            if (dbRef != null)
            {
                var refData = new ProjectCoordinateReference
                {
                    IsEnabled = dbRef.IsEnabled,
                    Name = dbRef.Name,
                    CoordinateSystemName = dbRef.CoordinateSystemName,
                    EPSGCode = dbRef.EPSGCode,
                    LocalReferenceX = dbRef.LocalReferenceX,
                    LocalReferenceY = dbRef.LocalReferenceY,
                    LocalReferenceZ = dbRef.LocalReferenceZ,
                    SurveyEasting = dbRef.SurveyEasting,
                    SurveyNorthing = dbRef.SurveyNorthing,
                    SurveyElevation = dbRef.SurveyElevation,
                    RotationToNorthDegrees = dbRef.RotationToNorthDegrees,
                    Scale = dbRef.Scale,
                    Source = dbRef.Source,
                    Latitude = dbRef.Latitude,
                    Longitude = dbRef.Longitude,
                    Description = dbRef.Description,
                    CreatedAt = dbRef.CreatedAtUtc,
                    UpdatedAt = dbRef.UpdatedAtUtc
                };
                _transformService.ApplyReference(refData, out _);
            }
            else
            {
                // Fallback to JSON
                var refFilePath = Path.Combine(projectFolderPath, "coordinate_reference.json");
                if (File.Exists(refFilePath))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(refFilePath, cancellationToken).ConfigureAwait(false);
                        var refData = JsonSerializer.Deserialize<ProjectCoordinateReference>(json, JsonOptions);
                        if (refData != null)
                        {
                            _transformService.ApplyReference(refData, out _);
                        }
                        else
                        {
                            _transformService.ResetReference();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to load coordinate reference from JSON fallback: {ex}");
                        _transformService.ResetReference();
                    }
                }
                else
                {
                    _transformService.ResetReference();
                }
            }
        }

        private static string ToAbsolutePath(string projectFolderPath, string relativePath)
            => Path.GetFullPath(Path.Combine(projectFolderPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        private static ProjectFileMetadata ReadFileMetadata(string filePath)
        {
            var file = new FileInfo(filePath);
            return new ProjectFileMetadata(file.Length, file.LastWriteTimeUtc);
        }

        private static bool PathsEqual(string left, string right)
            => string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);

        private sealed record ProjectAssetPath(string RelativePath, string AbsolutePath, string FileName);

        private sealed record ProjectFileMetadata(long SizeBytes, DateTime? LastWriteUtc);

        private sealed class ProjectIfcTransformDto
        {
            public double OffsetX { get; set; }
            public double OffsetY { get; set; }
            public double OffsetZ { get; set; }
            public double RotationDegrees { get; set; }
            public double Scale { get; set; } = 1d;
        }

        private sealed class ProjectDxfMetadataDto
        {
            public Guid? SourceId { get; set; }
            public int LoadedOrder { get; set; }
            public bool IsSelected { get; set; }
            public bool IsActive { get; set; }
            public bool HasSavedTransform { get; set; }
            public bool UseDxfColors { get; set; }
        }

        private sealed class ProjectZoneEnvelope
        {
            public List<DomainPoint3D> Vertices { get; set; } = new();
            public double? MinimumHeight { get; set; }
            public double? MaximumHeight { get; set; }
            public Guid? SourceDxfId { get; set; }
            public string? SourceDxfFileName { get; set; }
            public string? SourceDxfFilePath { get; set; }
            public string? SourceLayerName { get; set; }
            public string? SourceCandidateId { get; set; }
            public string? ImportMethod { get; set; }
            public ZoneType? OriginalAssignmentType { get; set; }
        }
    }
}
