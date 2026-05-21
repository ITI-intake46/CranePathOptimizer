using CraneOptimizer.Domain.CreatedProject;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Units;
using CraneOptimizer.Infrastructure.Contexts;
using CraneOptimizer.Infrastructure.Contexts.CreatedProject;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Mappers;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Models;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Reflection;

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.Services
{
    public sealed class ProjectWorkspaceService : IProjectWorkspaceService
    {
        private static readonly string[] ProjectSubfolders =
        {
            "Models",
            "Reports",
            "Exports",
            "Logs"
        };

        private readonly IDbContextFactory<CraneDbContext> _contextFactory;

        public ProjectWorkspaceService(IDbContextFactory<CraneDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public ProjectSession? CurrentSession { get; private set; }

        public async Task<ProjectSession> CreateProjectAsync(
            CreateProjectRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.ProjectName))
                throw new ArgumentException("Project name is required.", nameof(request));

            if (string.IsNullOrWhiteSpace(request.ParentFolderPath))
                throw new ArgumentException("Project location is required.", nameof(request));

            var projectId = Guid.NewGuid();
            var trimmedName = request.ProjectName.Trim();
            var now = DateTime.UtcNow;
            var projectFolderPath = BuildProjectFolderPath(request.ParentFolderPath, trimmedName);

            CreateProjectFolders(projectFolderPath);

            await using var projectContext = CreatedProjectDbContextFactory.Create(projectFolderPath);
            await projectContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

            var projectInfo = new ProjectInfoEntity
            {
                ProjectGuid = projectId.ToString("N"),
                Name = trimmedName,
                UnitSystem = "Metric",
                LengthUnit = "m",
                AreaUnit = "m²",
                AppVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(),
                SchemaVersion = 1,
                CacheVersion = 1,
                CreatedAtUtc = now,
                LastOpenedAtUtc = now
            };

            await projectContext.ProjectInfo.AddAsync(projectInfo, cancellationToken).ConfigureAwait(false);
            await projectContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await using var globalContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var project = await UpsertGlobalProjectAsync(
                globalContext,
                projectId,
                trimmedName,
                projectFolderPath,
                request.UnitSettings,
                now,
                updateUnitSettings: true,
                cancellationToken).ConfigureAwait(false);

            CurrentSession = CreateSession(project, projectInfo, projectFolderPath);
            return CurrentSession;
        }

        public async Task<ProjectSession?> OpenProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken)
        {
            await using var globalContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var project = await globalContext.Projects
                .Include(x => x.IfcFiles)
                .FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken)
                .ConfigureAwait(false);

            if (project == null)
                return null;

            if (string.IsNullOrWhiteSpace(project.ProjectFolderPath))
                throw new InvalidOperationException($"Project '{project.Name}' does not have a project folder path.");

            if (!Directory.Exists(project.ProjectFolderPath))
                throw new DirectoryNotFoundException($"Project folder was not found: {project.ProjectFolderPath}");

            var now = DateTime.UtcNow;

            await using var projectContext = CreatedProjectDbContextFactory.Create(project.ProjectFolderPath);
            await projectContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

            var projectInfo = await projectContext.ProjectInfo
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (projectInfo == null)
                throw new InvalidOperationException($"Project database does not contain ProjectInfo: {CreatedProjectDbContextFactory.GetDatabasePath(project.ProjectFolderPath)}");

            projectInfo.LastOpenedAtUtc = now;
            project.LastOpenedDate = now;
            project.LastModifiedDate = now;

            await projectContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await globalContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            CurrentSession = CreateSession(project, projectInfo, project.ProjectFolderPath);
            return CurrentSession;
        }

        public async Task<ProjectSession> OpenProjectFolderAsync(
            string projectFolderPath,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(projectFolderPath))
                throw new ArgumentException("Project folder path is required.", nameof(projectFolderPath));

            var normalizedProjectFolderPath = projectFolderPath.Trim();
            if (!Directory.Exists(normalizedProjectFolderPath))
                throw new DirectoryNotFoundException($"Project folder was not found: {normalizedProjectFolderPath}");

            var projectDatabasePath = CreatedProjectDbContextFactory.GetDatabasePath(normalizedProjectFolderPath);
            if (!File.Exists(projectDatabasePath))
                throw new FileNotFoundException("The selected folder does not contain project.db.", projectDatabasePath);

            var now = DateTime.UtcNow;

            await using var projectContext = CreatedProjectDbContextFactory.Create(normalizedProjectFolderPath);
            await projectContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

            var projectInfo = await projectContext.ProjectInfo
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (projectInfo == null)
                throw new InvalidOperationException($"Project database does not contain ProjectInfo: {projectDatabasePath}");

            var projectId = ResolveProjectId(projectInfo);
            var projectName = string.IsNullOrWhiteSpace(projectInfo.Name)
                ? Path.GetFileName(normalizedProjectFolderPath)
                : projectInfo.Name.Trim();

            projectInfo.LastOpenedAtUtc = now;
            await projectContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await using var globalContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var project = await UpsertGlobalProjectAsync(
                globalContext,
                projectId,
                projectName,
                normalizedProjectFolderPath,
                UnitSettings.Default.Clone(),
                now,
                updateUnitSettings: false,
                cancellationToken).ConfigureAwait(false);

            CurrentSession = CreateSession(project, projectInfo, normalizedProjectFolderPath);
            return CurrentSession;
        }

        public async Task<IReadOnlyList<ProjectCardModel>> GetRecentProjectsAsync(CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var projects = await QueryProjects(context)
                .OrderByDescending(project => project.LastOpenedDate)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return projects
                .Select(project => project.ToProjectCardDto().ToModel())
                .ToList();
        }

        public async Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var project = await context.Projects
                .FirstOrDefaultAsync(project => project.Id == projectId, cancellationToken)
                .ConfigureAwait(false);

            if (project == null)
                return;

            project.IsDeleted = true;
            project.LastModifiedDate = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task ToggleFavoriteAsync(Guid projectId, bool isFavorite, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var project = await context.Projects
                .FirstOrDefaultAsync(project => project.Id == projectId, cancellationToken)
                .ConfigureAwait(false);

            if (project == null)
                return;

            project.IsFavorite = isFavorite;
            project.LastModifiedDate = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<ProjectCardModel>> SearchProjectsAsync(
            string searchText,
            bool favoritesOnly,
            CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var query = QueryProjects(context);

            if (favoritesOnly)
                query = query.Where(project => project.IsFavorite);

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var term = searchText.Trim();
                query = query.Where(project =>
                    project.Name.Contains(term) ||
                    project.ProjectFolderPath.Contains(term) ||
                    project.IfcFiles.Any(file => file.FileName.Contains(term) || file.FilePath.Contains(term)));
            }

            var projects = await query
                .OrderByDescending(project => project.LastOpenedDate)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return projects
                .Select(project => project.ToProjectCardDto().ToModel())
                .ToList();
        }

        private static async Task<Project> UpsertGlobalProjectAsync(
            CraneDbContext context,
            Guid projectId,
            string name,
            string projectFolderPath,
            UnitSettings unitSettings,
            DateTime now,
            bool updateUnitSettings,
            CancellationToken cancellationToken)
        {
            var project = await context.Projects
                .IgnoreQueryFilters()
                .Include(x => x.IfcFiles)
                .FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken)
                .ConfigureAwait(false);

            if (project == null)
            {
                project = new Project
                {
                    Id = projectId,
                    CreatedDate = now,
                    Notes = string.Empty,
                    IsFavorite = false,
                    IsDeleted = false,
                    DisplayLengthUnit = (int)unitSettings.DisplayLengthUnit,
                    DisplayMassUnit = (int)unitSettings.DisplayMassUnit,
                    DisplayAreaUnit = (int)unitSettings.DisplayAreaUnit,
                    UnitDecimalPlaces = unitSettings.DecimalPlaces
                };

                await context.Projects.AddAsync(project, cancellationToken).ConfigureAwait(false);
            }

            project.Name = name;
            project.ProjectFolderPath = projectFolderPath;
            project.IsDeleted = false;
            project.LastOpenedDate = now;
            project.LastModifiedDate = now;

            if (updateUnitSettings)
            {
                project.DisplayLengthUnit = (int)unitSettings.DisplayLengthUnit;
                project.DisplayMassUnit = (int)unitSettings.DisplayMassUnit;
                project.DisplayAreaUnit = (int)unitSettings.DisplayAreaUnit;
                project.UnitDecimalPlaces = unitSettings.DecimalPlaces;
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return project;
        }

        private static Guid ResolveProjectId(ProjectInfoEntity projectInfo)
        {
            if (Guid.TryParseExact(projectInfo.ProjectGuid, "N", out var compactGuid))
                return compactGuid;

            if (Guid.TryParse(projectInfo.ProjectGuid, out var guid))
                return guid;

            throw new InvalidOperationException($"ProjectInfo.ProjectGuid is not a valid Guid: {projectInfo.ProjectGuid}");
        }

        private static IQueryable<Project> QueryProjects(CraneDbContext context)
        {
            return context.Projects
                .AsNoTracking()
                .Include(project => project.IfcFiles);
        }

        private static ProjectSession CreateSession(
            Project project,
            ProjectInfoEntity projectInfo,
            string projectFolderPath)
        {
            return new ProjectSession
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                ProjectFolderPath = projectFolderPath,
                ProjectDatabasePath = CreatedProjectDbContextFactory.GetDatabasePath(projectFolderPath),
                ProjectInfoId = projectInfo.Id,
                ProjectGuid = projectInfo.ProjectGuid,
                LastOpenedAtUtc = projectInfo.LastOpenedAtUtc,
                ProjectCard = project.ToProjectCardDto().ToModel()
            };
        }

        private static void CreateProjectFolders(string projectFolderPath)
        {
            Directory.CreateDirectory(projectFolderPath);

            foreach (var subfolder in ProjectSubfolders)
                Directory.CreateDirectory(Path.Combine(projectFolderPath, subfolder));
        }

        private static string BuildProjectFolderPath(string parentFolderPath, string name)
        {
            var trimmedParent = parentFolderPath.Trim();

            if (!Directory.Exists(trimmedParent))
                throw new DirectoryNotFoundException($"Project location was not found: {trimmedParent}");

            var invalidCharacters = Path.GetInvalidFileNameChars();
            if (name.IndexOfAny(invalidCharacters) >= 0)
                throw new ArgumentException("Project name contains characters that cannot be used in a folder name.", nameof(name));

            var projectFolderPath = Path.Combine(trimmedParent, name);
            if (Directory.Exists(projectFolderPath) && Directory.EnumerateFileSystemEntries(projectFolderPath).Any())
                throw new IOException($"Project folder already exists and is not empty: {projectFolderPath}");

            return projectFolderPath;
        }
    }
}
