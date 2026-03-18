using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WPF_IFC_test1.Database.Entities;

namespace WPF_IFC_test1.Database.Repositories
{
    public class IfcElementRepository : IIfcElementRepository
    {
        private readonly AppDbContext _db;

        public IfcElementRepository(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<IfcElementEntity>> GetByProjectAsync(int projectId)
            => await _db.IfcElements
                        .AsNoTracking()
                        .Where(e => e.ProjectId == projectId)
                        .ToListAsync();

        public async Task<IfcElementEntity> GetByGlobalIdAsync(int projectId, string globalId)
            => await _db.IfcElements
                        .AsNoTracking()
                        .FirstOrDefaultAsync(e => e.ProjectId == projectId && e.GlobalId == globalId);

        /// <summary>
        /// Efficient bulk insert — uses AddRange + single SaveChanges.
        /// For very large IFC models (>10 000 elements) consider batching.
        /// </summary>
        public async Task BulkInsertAsync(IEnumerable<IfcElementEntity> elements)
        {
            _db.IfcElements.AddRange(elements);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(IfcElementEntity element)
        {
            _db.IfcElements.Update(element);
            await _db.SaveChangesAsync();
        }

        /// <summary>Wipes all elements for a project (used before re-importing an IFC file).</summary>
        public async Task DeleteAllForProjectAsync(int projectId)
        {
            var rows = await _db.IfcElements
                                .Where(e => e.ProjectId == projectId)
                                .ToListAsync();
            _db.IfcElements.RemoveRange(rows);
            await _db.SaveChangesAsync();
        }
    }
}
