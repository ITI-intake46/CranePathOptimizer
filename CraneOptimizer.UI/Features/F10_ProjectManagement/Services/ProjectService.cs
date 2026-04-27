using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Infrastructure.Contexts;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Models;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.Services
{
    public sealed class ProjectService : IProjectService
    {
        private readonly IDbContextFactory<CraneDbContext> _contextFactory;

        public ProjectService(IDbContextFactory<CraneDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<ProjectCardModel> CreateProjectAsync(
            string name,
            string ifcFilePath,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Project name is required.", nameof(name));

            if (string.IsNullOrWhiteSpace(ifcFilePath))
                throw new ArgumentException("IFC file path is required.", nameof(ifcFilePath));

            var now = DateTime.UtcNow;
            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = name.Trim(),
                CreatedDate = now,
                LastOpenedDate = now,
                LastModifiedDate = now,
                Notes = string.Empty,
                IsFavorite = false,
                IsDeleted = false,
                IfcFiles =
                {
                    new ProjectIfcFile
                    {
                        Id = Guid.NewGuid(),
                        FilePath = ifcFilePath,
                        FileName = Path.GetFileName(ifcFilePath),
                        Exists = File.Exists(ifcFilePath)
                    }
                }
            };

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            await context.Projects.AddAsync(project, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return ToCard(project, project.IfcFiles.FirstOrDefault());
        }

        public async Task<IReadOnlyList<ProjectCardModel>> GetRecentProjectsAsync(CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await QueryProjects(context)
                .OrderByDescending(project => project.LastOpenedDate)
                .Select(project => new ProjectCardModel
                {
                    Id = project.Id,
                    Name = project.Name,
                    IfcFilePath = project.IfcFiles
                        .OrderBy(file => file.FileName)
                        .Select(file => file.FilePath)
                        .FirstOrDefault() ?? string.Empty,
                    CreatedDate = project.CreatedDate,
                    LastOpenedDate = project.LastOpenedDate,
                    LastModifiedDate = project.LastModifiedDate,
                    IsFavorite = project.IsFavorite,
                    Notes = project.Notes
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<ProjectCardModel?> OpenProjectAsync(Guid projectId, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var project = await context.Projects
                .Include(project => project.IfcFiles)
                .FirstOrDefaultAsync(project => project.Id == projectId, cancellationToken);

            if (project == null)
                return null;

            var now = DateTime.UtcNow;
            project.LastOpenedDate = now;
            project.LastModifiedDate = now;
            await context.SaveChangesAsync(cancellationToken);

            return ToCard(project, project.IfcFiles.FirstOrDefault());
        }

        public async Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var project = await context.Projects
                .FirstOrDefaultAsync(project => project.Id == projectId, cancellationToken);

            if (project == null)
                return;

            project.IsDeleted = true;
            project.LastModifiedDate = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }

        public async Task ToggleFavoriteAsync(Guid projectId, bool isFavorite, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var project = await context.Projects
                .FirstOrDefaultAsync(project => project.Id == projectId, cancellationToken);

            if (project == null)
                return;

            project.IsFavorite = isFavorite;
            project.LastModifiedDate = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<ProjectCardModel>> SearchProjectsAsync(
            string searchText,
            bool favoritesOnly,
            CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var query = QueryProjects(context);

            if (favoritesOnly)
                query = query.Where(project => project.IsFavorite);

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var term = searchText.Trim();
                query = query.Where(project =>
                    project.Name.Contains(term) ||
                    project.IfcFiles.Any(file => file.FileName.Contains(term) || file.FilePath.Contains(term)));
            }

            return await query
                .OrderByDescending(project => project.LastOpenedDate)
                .Select(project => new ProjectCardModel
                {
                    Id = project.Id,
                    Name = project.Name,
                    IfcFilePath = project.IfcFiles
                        .OrderBy(file => file.FileName)
                        .Select(file => file.FilePath)
                        .FirstOrDefault() ?? string.Empty,
                    CreatedDate = project.CreatedDate,
                    LastOpenedDate = project.LastOpenedDate,
                    LastModifiedDate = project.LastModifiedDate,
                    IsFavorite = project.IsFavorite,
                    Notes = project.Notes
                })
                .ToListAsync(cancellationToken);
        }

        private IQueryable<Project> QueryProjects(CraneDbContext context)
        {
            return context.Projects
                .AsNoTracking()
                .Include(project => project.IfcFiles);
        }

        private static ProjectCardModel ToCard(Project project, ProjectIfcFile? ifcFile)
        {
            return new ProjectCardModel
            {
                Id = project.Id,
                Name = project.Name,
                IfcFilePath = ifcFile?.FilePath ?? string.Empty,
                CreatedDate = project.CreatedDate,
                LastOpenedDate = project.LastOpenedDate,
                LastModifiedDate = project.LastModifiedDate,
                IsFavorite = project.IsFavorite,
                Notes = project.Notes
            };
        }
    }
}
