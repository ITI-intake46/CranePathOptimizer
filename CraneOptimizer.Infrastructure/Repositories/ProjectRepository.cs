using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WPF_IFC_test1.Database.Entities;

namespace WPF_IFC_test1.Database.Repositories
{
    public class ProjectRepository : IProjectRepository
    {
        private readonly AppDbContext _db;

        public ProjectRepository(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>Returns all projects (no nav props — list view only).</summary>
        public async Task<List<ProjectEntity>> GetAllAsync()
            => await _db.Projects
                        .AsNoTracking()
                        .ToListAsync();

        /// <summary>Returns a single project by ID (no nav props).</summary>
        public async Task<ProjectEntity> GetByIdAsync(int id)
            => await _db.Projects
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Id == id);

        /// <summary>
        /// Returns a project with all related data loaded:
        /// IfcElements, Cranes, Zones → Vertices.
        /// Use this before opening a project in the 3-D view.
        /// </summary>
        public async Task<ProjectEntity> GetWithAllAsync(int id)
            => await _db.Projects
                        .Include(p => p.IfcElements)
                        .Include(p => p.Cranes)
                        .Include(p => p.Zones)
                            .ThenInclude(z => z.Vertices)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Id == id);

        /// <summary>Inserts a new project and returns its generated ID.</summary>
        public async Task<int> AddAsync(ProjectEntity project)
        {
            project.CreatedAt = DateTime.UtcNow;
            project.UpdatedAt = DateTime.UtcNow;
            _db.Projects.Add(project);
            await _db.SaveChangesAsync();
            return project.Id;
        }

        /// <summary>Updates header fields and bumps UpdatedAt.</summary>
        public async Task UpdateAsync(ProjectEntity project)
        {
            project.UpdatedAt = DateTime.UtcNow;
            _db.Projects.Update(project);
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Deletes a project and all cascade-dependent rows
        /// (IfcElements, Cranes, Zones, ZoneVertices).
        /// </summary>
        public async Task DeleteAsync(int id)
        {
            var project = await _db.Projects.FindAsync(id);
            if (project != null)
            {
                _db.Projects.Remove(project);
                await _db.SaveChangesAsync();
            }
        }
    }
}
