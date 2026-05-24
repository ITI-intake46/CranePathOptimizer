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

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.Services
{
    public sealed class ProjectStateService : IProjectStateService
    {
        private const string IfcFolder = "Models/IFC";
        private const string DxfFolder = "Models/DXF";
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public async Task SaveProjectStateAsync(
            ProjectSession session,
            ProjectStateSnapshot state,
            CancellationToken cancellationToken = default)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            await using var context = CreatedProjectDbContextFactory.Create(session.ProjectFolderPath);
            var projectInfo = await GetProjectInfoAsync(context, session.ProjectInfoId, cancellationToken)
                .ConfigureAwait(false);

            await SaveIfcAsync(context, session, projectInfo.Id, state.IfcFile, cancellationToken)
                .ConfigureAwait(false);
            await SaveModelFilesAsync(context, session, projectInfo.Id, state.DxfFile, cancellationToken)
                .ConfigureAwait(false);
            await SaveZonesAsync(context, projectInfo.Id, state.Zones, cancellationToken)
                .ConfigureAwait(false);
            await SaveCranesAsync(context, projectInfo.Id, state.Cranes, cancellationToken)
                .ConfigureAwait(false);
            await SaveViewportAsync(context, projectInfo.Id, state.Viewport, cancellationToken)
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

            var ifc = await context.IfcFiles
                .AsNoTracking()
                .Where(x => x.ProjectInfoId == projectInfo.Id && x.IsActive)
                .OrderBy(x => x.Id)
                .LastOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            var dxf = await context.ModelFiles
                .AsNoTracking()
                .Where(x => x.ProjectInfoId == projectInfo.Id && x.IsActive && x.FileType == "DXF")
                .OrderBy(x => x.Id)
                .LastOrDefaultAsync(cancellationToken)
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

            return new ProjectStateSnapshot
            {
                IfcFile = ToIfcState(session.ProjectFolderPath, ifc),
                DxfFile = ToModelFileState(session.ProjectFolderPath, dxf),
                Zones = zones.Select(ToZoneState).ToList(),
                Cranes = cranes.Select(ToCraneState).ToList(),
                Viewport = ToViewportState(viewport)
            };
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
            CancellationToken cancellationToken)
        {
            var existing = await context.IfcFiles
                .Where(x => x.ProjectInfoId == projectInfoId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            context.IfcFiles.RemoveRange(existing);

            if (ifc == null || string.IsNullOrWhiteSpace(ifc.AbsolutePath) || !File.Exists(ifc.AbsolutePath))
                return;

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

            await context.IfcFiles.AddAsync(new ProjectIfcFileEntity
            {
                ProjectInfoId = projectInfoId,
                FileName = asset.FileName,
                RelativePath = asset.RelativePath,
                OriginalFullPath = ifc.AbsolutePath,
                Role = ifc.Role,
                FileSizeBytes = metadata.SizeBytes,
                LastWriteUtc = metadata.LastWriteUtc,
                TransformJson = JsonSerializer.Serialize(transform, JsonOptions),
                MetadataJson = ifc.MetadataJson,
                IsActive = true,
                IsCacheValid = false,
                ImportedAtUtc = DateTime.UtcNow
            }, cancellationToken).ConfigureAwait(false);
        }

        private static async Task SaveModelFilesAsync(
            CreatedProjectDbContext context,
            ProjectSession session,
            int projectInfoId,
            ProjectModelFileState? dxf,
            CancellationToken cancellationToken)
        {
            var existing = await context.ModelFiles
                .Where(x => x.ProjectInfoId == projectInfoId && x.FileType == "DXF")
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            context.ModelFiles.RemoveRange(existing);

            if (dxf == null || string.IsNullOrWhiteSpace(dxf.AbsolutePath) || !File.Exists(dxf.AbsolutePath))
                return;

            var asset = CopyAssetIntoProject(session.ProjectFolderPath, dxf.AbsolutePath, DxfFolder);
            var metadata = ReadFileMetadata(asset.AbsolutePath);

            await context.ModelFiles.AddAsync(new ProjectModelFileEntity
            {
                ProjectInfoId = projectInfoId,
                FileType = "DXF",
                Role = string.IsNullOrWhiteSpace(dxf.Role) ? "SitePlan" : dxf.Role,
                FileName = asset.FileName,
                RelativePath = asset.RelativePath,
                OriginalFullPath = dxf.AbsolutePath,
                FileSizeBytes = metadata.SizeBytes,
                LastWriteUtc = metadata.LastWriteUtc,
                TransformJson = JsonSerializer.Serialize(dxf.TransformValues ?? Array.Empty<double>(), JsonOptions),
                MetadataJson = dxf.MetadataJson,
                IsActive = true,
                ImportedAtUtc = DateTime.UtcNow
            }, cancellationToken).ConfigureAwait(false);
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
                    VerticesJson = JsonSerializer.Serialize(zone.Vertices, JsonOptions)
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

        private static ProjectModelFileState? ToModelFileState(string projectFolderPath, ProjectModelFileEntity? entity)
        {
            if (entity == null)
                return null;

            return new ProjectModelFileState
            {
                FileType = entity.FileType,
                Role = entity.Role,
                AbsolutePath = ToAbsolutePath(projectFolderPath, entity.RelativePath),
                FileName = entity.FileName,
                TransformValues = Deserialize<double[]>(entity.TransformJson),
                MetadataJson = entity.MetadataJson
            };
        }

        private static ProjectSiteZoneState ToZoneState(SiteZoneEntity entity)
        {
            _ = Guid.TryParse(entity.ZoneGuid, out var zoneId);
            _ = Enum.TryParse<ZoneType>(entity.ZoneType, out var zoneType);

            return new ProjectSiteZoneState
            {
                ZoneId = zoneId == Guid.Empty ? Guid.NewGuid() : zoneId,
                Name = entity.Name,
                Type = zoneType,
                Area = entity.Area,
                Vertices = Deserialize<List<DomainPoint3D>>(entity.VerticesJson) ?? new List<DomainPoint3D>()
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
    }
}
