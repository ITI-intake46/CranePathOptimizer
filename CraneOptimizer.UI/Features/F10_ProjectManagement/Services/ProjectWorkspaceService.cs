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
            Path.Combine("Models", "IFC"),
            Path.Combine("Models", "DXF"),
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
            await SqliteMigrationCompatibilityGuard.EnsureCreatedProjectOperatingConditionSchemaAsync(projectContext, cancellationToken).ConfigureAwait(false);
            await SqliteMigrationCompatibilityGuard.EnsureCreatedProjectCompanyNameSchemaAsync(projectContext, cancellationToken).ConfigureAwait(false);
            await projectContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            await SqliteMigrationCompatibilityGuard.EnsureCreatedProjectOperatingConditionSchemaAsync(projectContext, cancellationToken).ConfigureAwait(false);
            await SqliteMigrationCompatibilityGuard.EnsureCreatedProjectCompanyNameSchemaAsync(projectContext, cancellationToken).ConfigureAwait(false);
            var projectInfo = new ProjectInfoEntity
            {
                ProjectGuid = projectId.ToString("N"),
                Name = trimmedName,
                UnitSystem = "Metric",
                LengthUnit = "m",
                AreaUnit = "m²",
                Country = request.Country,
                SiteLocation = request.SiteLocation,
                CompanyName = request.CompanyName,
                ProjectImagePath = request.ProjectImagePath,
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
                request.CompanyName,
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
            await SqliteMigrationCompatibilityGuard.EnsureCreatedProjectOperatingConditionSchemaAsync(projectContext, cancellationToken).ConfigureAwait(false);
            await SqliteMigrationCompatibilityGuard.EnsureCreatedProjectCompanyNameSchemaAsync(projectContext, cancellationToken).ConfigureAwait(false);
            await projectContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            await SqliteMigrationCompatibilityGuard.EnsureCreatedProjectOperatingConditionSchemaAsync(projectContext, cancellationToken).ConfigureAwait(false);
            await SqliteMigrationCompatibilityGuard.EnsureCreatedProjectCompanyNameSchemaAsync(projectContext, cancellationToken).ConfigureAwait(false);
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
            await SqliteMigrationCompatibilityGuard.EnsureCreatedProjectOperatingConditionSchemaAsync(projectContext, cancellationToken).ConfigureAwait(false);
            await SqliteMigrationCompatibilityGuard.EnsureCreatedProjectCompanyNameSchemaAsync(projectContext, cancellationToken).ConfigureAwait(false);
            await projectContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            await SqliteMigrationCompatibilityGuard.EnsureCreatedProjectOperatingConditionSchemaAsync(projectContext, cancellationToken).ConfigureAwait(false);
            await SqliteMigrationCompatibilityGuard.EnsureCreatedProjectCompanyNameSchemaAsync(projectContext, cancellationToken).ConfigureAwait(false);
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
                companyName: null,
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

        public async Task UpdateProjectMetadataAsync(
            Guid projectId,
            string name,
            string? companyName,
            string? country,
            string? siteLocation,
            string? projectImagePath,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Project name is required.", nameof(name));

            // ── 1. Update global catalog ───────────────────────────────────
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var project = await context.Projects
                .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
                .ConfigureAwait(false);

            if (project == null)
                throw new KeyNotFoundException($"Project {projectId} not found.");

            project.Name             = name.Trim();
            project.CompanyName      = string.IsNullOrWhiteSpace(companyName) ? null : companyName.Trim();
            project.LastModifiedDate = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // ── 2. Update per-project DB (Country / SiteLocation / Image) ─
            if (!string.IsNullOrWhiteSpace(project.ProjectFolderPath)
                && Directory.Exists(project.ProjectFolderPath))
            {
                try
                {
                    await using var projectContext = CreatedProjectDbContextFactory.Create(project.ProjectFolderPath);
                    var info = await projectContext.ProjectInfo
                        .OrderBy(x => x.Id)
                        .FirstOrDefaultAsync(cancellationToken)
                        .ConfigureAwait(false);

                    if (info != null)
                    {
                        info.Name             = name.Trim();
                        info.CompanyName      = string.IsNullOrWhiteSpace(companyName)      ? null : companyName.Trim();
                        info.Country          = string.IsNullOrWhiteSpace(country)          ? null : country.Trim();
                        info.SiteLocation     = string.IsNullOrWhiteSpace(siteLocation)     ? null : siteLocation.Trim();
                        info.ProjectImagePath = string.IsNullOrWhiteSpace(projectImagePath) ? info.ProjectImagePath : projectImagePath.Trim();
                        await projectContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
                catch
                {
                    // Per-project DB update is best-effort; don't fail the whole save
                }
            }
        }

        public async Task<string> RenameProjectFolderAsync(
            Guid projectId,
            string newProjectName,
            string? companyName,
            string? country,
            string? siteLocation,
            string? projectImagePath,
            CancellationToken cancellationToken)
        {
            var trimmedName = (newProjectName ?? string.Empty).Trim();
            ValidateFolderName(trimmedName);

            // Load the project record
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var project = await context.Projects
                .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
                .ConfigureAwait(false);

            if (project == null)
                throw new KeyNotFoundException($"Project {projectId} not found in the catalog.");

            var oldPath = project.ProjectFolderPath;
            if (string.IsNullOrWhiteSpace(oldPath))
                throw new InvalidOperationException("The project does not have a folder path stored.");

            if (!Directory.Exists(oldPath))
                throw new DirectoryNotFoundException($"The current project folder was not found on disk: {oldPath}");

            var parentPath = Path.GetDirectoryName(oldPath)
                ?? throw new InvalidOperationException("Cannot determine the parent directory of the project folder.");

            var newPath = Path.Combine(parentPath, trimmedName);

            // No-op: same folder
            if (string.Equals(Path.GetFullPath(oldPath), Path.GetFullPath(newPath), StringComparison.OrdinalIgnoreCase))
            {
                // Still update metadata/display name
                await UpdateMetadataInContextAsync(context, project, trimmedName, companyName, country, siteLocation, projectImagePath, oldPath, cancellationToken).ConfigureAwait(false);
                return oldPath;
            }

            if (Directory.Exists(newPath))
                throw new IOException($"A folder named \"{trimmedName}\" already exists at that location. Choose a different project name.");

            // ── Rename the folder ─────────────────────────────────────────
            Directory.Move(oldPath, newPath);

            // ── Update databases (with rollback on failure) ───────────────
            try
            {
                await UpdateMetadataInContextAsync(context, project, trimmedName, companyName, country, siteLocation, projectImagePath, newPath, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception dbEx)
            {
                // Attempt rollback: move folder back
                try
                {
                    Directory.Move(newPath, oldPath);
                }
                catch (Exception rollbackEx)
                {
                    throw new InvalidOperationException(
                        $"Folder was renamed to \"{newPath}\" but database update failed, AND rollback also failed: {rollbackEx.Message}. " +
                        $"Original error: {dbEx.Message}", dbEx);
                }

                throw new InvalidOperationException(
                    $"Database update failed after folder rename. The folder has been restored to its original location. Error: {dbEx.Message}", dbEx);
            }

            // ── Patch CurrentSession if it refers to this project ─────────
            if (CurrentSession != null && CurrentSession.ProjectId == projectId)
            {
                CurrentSession = new ProjectSession
                {
                    ProjectId           = CurrentSession.ProjectId,
                    ProjectName         = trimmedName,
                    ProjectFolderPath   = newPath,
                    ProjectDatabasePath = CreatedProjectDbContextFactory.GetDatabasePath(newPath),
                    ProjectInfoId       = CurrentSession.ProjectInfoId,
                    ProjectGuid         = CurrentSession.ProjectGuid,
                    LastOpenedAtUtc     = CurrentSession.LastOpenedAtUtc,
                    ProjectCard         = CurrentSession.ProjectCard
                };
            }

            return newPath;
        }

        /// <summary>Shared logic: update names/path in the global catalog and the per-project DB.</summary>
        private static async Task UpdateMetadataInContextAsync(
            CraneDbContext context,
            Project project,
            string name,
            string? companyName,
            string? country,
            string? siteLocation,
            string? projectImagePath,
            string folderPath,
            CancellationToken cancellationToken)
        {
            project.Name             = name;
            project.ProjectFolderPath = folderPath;
            project.CompanyName      = string.IsNullOrWhiteSpace(companyName) ? null : companyName.Trim();
            project.LastModifiedDate = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
            {
                try
                {
                    await using var proj = CreatedProjectDbContextFactory.Create(folderPath);
                    var info = await proj.ProjectInfo.OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
                    if (info != null)
                    {
                        info.Name             = name;
                        info.CompanyName      = string.IsNullOrWhiteSpace(companyName)      ? null : companyName.Trim();
                        info.Country          = string.IsNullOrWhiteSpace(country)          ? null : country.Trim();
                        info.SiteLocation     = string.IsNullOrWhiteSpace(siteLocation)     ? null : siteLocation.Trim();
                        info.ProjectImagePath = string.IsNullOrWhiteSpace(projectImagePath) ? info.ProjectImagePath : projectImagePath.Trim();
                        await proj.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
                catch
                {
                    // Best-effort
                }
            }
        }

        /// <summary>Validates a prospective folder name against Windows rules.</summary>
        private static void ValidateFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Project name is required.");

            var invalid = Path.GetInvalidFileNameChars();
            if (name.IndexOfAny(invalid) >= 0)
                throw new ArgumentException($"The project name contains characters that are not allowed in a folder name: \\ / : * ? \" < > |");

            // Reject Windows reserved names
            var reserved = new[] { "CON","PRN","AUX","NUL","COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9","LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9" };
            var upper = name.ToUpperInvariant();
            if (Array.Exists(reserved, r => string.Equals(upper, r, StringComparison.Ordinal)))
                throw new ArgumentException($"\"{name}\" is a reserved Windows name and cannot be used as a folder name.");
        }

        private static async Task<Project> UpsertGlobalProjectAsync(
            CraneDbContext context,
            Guid projectId,
            string name,
            string projectFolderPath,
            UnitSettings unitSettings,
            string? companyName,
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

            if (companyName != null)
                project.CompanyName = companyName;

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
                Directory.CreateDirectory(trimmedParent);

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
