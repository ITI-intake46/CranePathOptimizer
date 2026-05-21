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
using System.Text.RegularExpressions;

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
            string name,
            UnitSettings unitSettings,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Project name is required.", nameof(name));

            var projectId = Guid.NewGuid();
            var trimmedName = name.Trim();
            var now = DateTime.UtcNow;
            var projectFolderPath = BuildProjectFolderPath(projectId, trimmedName);

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
                unitSettings,
                now,
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

        private static async Task<Project> UpsertGlobalProjectAsync(
            CraneDbContext context,
            Guid projectId,
            string name,
            string projectFolderPath,
            UnitSettings unitSettings,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var project = await context.Projects
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
                    IsDeleted = false
                };

                await context.Projects.AddAsync(project, cancellationToken).ConfigureAwait(false);
            }

            project.Name = name;
            project.ProjectFolderPath = projectFolderPath;
            project.LastOpenedDate = now;
            project.LastModifiedDate = now;
            project.DisplayLengthUnit = (int)unitSettings.DisplayLengthUnit;
            project.DisplayMassUnit = (int)unitSettings.DisplayMassUnit;
            project.DisplayAreaUnit = (int)unitSettings.DisplayAreaUnit;
            project.UnitDecimalPlaces = unitSettings.DecimalPlaces;

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return project;
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

        private static string BuildProjectFolderPath(Guid projectId, string name)
        {
            var root = Path.Combine(AppContext.BaseDirectory, "Data", "Projects");
            Directory.CreateDirectory(root);

            var folderName = $"{SanitizeFolderName(name)}_{projectId:N}";
            return Path.Combine(root, folderName);
        }

        private static string SanitizeFolderName(string value)
        {
            var invalid = new string(Path.GetInvalidFileNameChars());
            var pattern = $"[{Regex.Escape(invalid)}]+";
            var sanitized = Regex.Replace(value, pattern, "_").Trim();
            sanitized = Regex.Replace(sanitized, "\\s+", "_");

            if (sanitized.Length == 0)
                return "Project";

            const int maxNameLength = 80;
            return sanitized.Length <= maxNameLength
                ? sanitized
                : sanitized[..maxNameLength];
        }
    }
}
