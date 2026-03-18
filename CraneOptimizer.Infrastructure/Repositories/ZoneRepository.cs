using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WPF_IFC_test1.Database.Entities;

namespace WPF_IFC_test1.Database.Repositories
{
    public class ZoneRepository : IZoneRepository
    {
        private readonly AppDbContext _db;

        public ZoneRepository(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>Returns all zones for a project with their polygon vertices.</summary>
        public async Task<List<ZoneEntity>> GetByProjectAsync(int projectId)
            => await _db.Zones
                        .Include(z => z.Vertices.OrderBy(v => v.Order))
                        .AsNoTracking()
                        .Where(z => z.ProjectId == projectId)
                        .ToListAsync();

        /// <summary>Returns a single zone with vertices.</summary>
        public async Task<ZoneEntity> GetByIdAsync(int id)
            => await _db.Zones
                        .Include(z => z.Vertices.OrderBy(v => v.Order))
                        .AsNoTracking()
                        .FirstOrDefaultAsync(z => z.Id == id);

        public async Task<int> AddAsync(ZoneEntity zone)
        {
            // Re-index vertices before insert
            ReorderVertices(zone);
            _db.Zones.Add(zone);
            await _db.SaveChangesAsync();
            return zone.Id;
        }

        /// <summary>
        /// Replaces the zone header fields and all vertices atomically.
        /// Old vertices are deleted; new ones are inserted in order.
        /// </summary>
        public async Task UpdateAsync(ZoneEntity zone)
        {
            // Remove old vertices
            var old = await _db.ZoneVertices
                                .Where(v => v.ZoneId == zone.Id)
                                .ToListAsync();
            _db.ZoneVertices.RemoveRange(old);

            // Re-index and re-insert
            ReorderVertices(zone);
            _db.Zones.Update(zone);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var zone = await _db.Zones.FindAsync(id);
            if (zone != null)
            {
                _db.Zones.Remove(zone);   // vertices cascade
                await _db.SaveChangesAsync();
            }
        }

        public async Task DeleteAllForProjectAsync(int projectId)
        {
            var zones = await _db.Zones
                                 .Where(z => z.ProjectId == projectId)
                                 .ToListAsync();
            _db.Zones.RemoveRange(zones);
            await _db.SaveChangesAsync();
        }

        // ── helpers ─────────────────────────────────────────────────────────

        private static void ReorderVertices(ZoneEntity zone)
        {
            int i = 0;
            foreach (var v in zone.Vertices)
                v.Order = i++;
        }
    }
}
