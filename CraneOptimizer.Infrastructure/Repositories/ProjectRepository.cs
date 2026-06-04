using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace CraneOptimizer.Infrastructure.Repositories
{
    public class ProjectRepository : IProjectRepository
    {
        private readonly IDbContextFactory<CraneDbContext> _dbFactory;

        public ProjectRepository(IDbContextFactory<CraneDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public async Task CreateAsync(Project project, CancellationToken ct)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            await db.Projects.AddAsync(project, ct);
            await db.SaveChangesAsync(ct);
        }

        public async Task<List<Project>> GetAllAsync(CancellationToken ct)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var projects = await db.Projects
                .AsNoTracking()
                .Include(x => x.IfcFiles)
                .OrderByDescending(x => x.LastOpenedDate)
                .ThenBy(x => x.Name)
                .ToListAsync(ct);

            return projects;
        }

        public async Task<List<Project>> GetRecentAsync(int count, CancellationToken ct)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var projects = await db.Projects
                .AsNoTracking()
                .Include(x => x.IfcFiles)
                .OrderByDescending(x => x.LastOpenedDate)
                .Take(count)
                .ToListAsync(ct);

            return projects;
        }

        public async Task<Project?> GetByIdAsync(Guid id, CancellationToken ct)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var project = await db.Projects
                .AsNoTracking()
                .Include(x => x.IfcFiles)
                .FirstOrDefaultAsync(x => x.Id == id, ct);

            return project;
        }

        public Task<Project?> GetProjectByIdAsync(Guid id, CancellationToken ct = default)
        {
            return GetByIdAsync(id, ct);
        }

        public async Task UpdateConcreteDensityAsync(Guid projectId, double density, CancellationToken ct = default)
        {
            if (density <= 0)
                throw new ArgumentOutOfRangeException(nameof(density), "Density must be > 0");

            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var project = await db.Projects.FirstOrDefaultAsync(x => x.Id == projectId, ct);
            if (project == null)
                throw new KeyNotFoundException($"Project {projectId} not found");

            project.ConcreteDensityKgPerM3 = density;
            project.LastModifiedDate = DateTime.UtcNow;

            db.Entry(project).Property(x => x.ConcreteDensityKgPerM3).IsModified = true;
            db.Entry(project).Property(x => x.LastModifiedDate).IsModified = true;

            await db.SaveChangesAsync(ct);
        }

        public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken ct)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var project = await db.Projects.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (project == null)
            {
                return false;
            }

            project.IsDeleted = true;
            project.LastModifiedDate = DateTime.UtcNow;

            db.Entry(project).Property(x => x.IsDeleted).IsModified = true;
            db.Entry(project).Property(x => x.LastModifiedDate).IsModified = true;

            await db.SaveChangesAsync(ct);
            return true;
        }

        public async Task OpenAsync(Guid id, DateTime openedAtUtc, CancellationToken ct)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var project = await db.Projects.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (project == null)
            {
                return;
            }

            project.LastOpenedDate = openedAtUtc;
            project.LastModifiedDate = openedAtUtc;

            db.Entry(project).Property(x => x.LastOpenedDate).IsModified = true;
            db.Entry(project).Property(x => x.LastModifiedDate).IsModified = true;

            await db.SaveChangesAsync(ct);
        }

        public async Task<bool> ToggleFavoriteAsync(Guid id, bool isFavorite, DateTime modifiedAtUtc, CancellationToken ct)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var project = await db.Projects.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (project == null)
            {
                return false;
            }

            project.IsFavorite = isFavorite;
            project.LastModifiedDate = modifiedAtUtc;

            db.Entry(project).Property(x => x.IsFavorite).IsModified = true;
            db.Entry(project).Property(x => x.LastModifiedDate).IsModified = true;

            await db.SaveChangesAsync(ct);
            return true;
        }

    }
}
