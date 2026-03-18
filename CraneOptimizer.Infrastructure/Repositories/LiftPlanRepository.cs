using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WPF_IFC_test1.Database.Entities;

namespace WPF_IFC_test1.Database.Repositories
{
    public interface ILiftPlanRepository
    {
        Task<List<LiftPlanEntity>> GetByProjectAsync(int projectId);
        Task<LiftPlanEntity?> GetByIdAsync(int id);
        Task<LiftPlanEntity> AddAsync(LiftPlanEntity plan);
        Task UpdateAsync(LiftPlanEntity plan);
        Task DeleteAsync(int id);
        Task DeleteAllForProjectAsync(int projectId);
    }

    public class LiftPlanRepository : ILiftPlanRepository
    {
        private readonly AppDbContext _db;
        public LiftPlanRepository(AppDbContext db) => _db = db;

        public async Task<List<LiftPlanEntity>> GetByProjectAsync(int projectId)
            => await _db.LiftPlans
                .Include(l => l.ChecklistItems)
                .Where(l => l.ProjectId == projectId)
                .OrderBy(l => l.Phase).ThenBy(l => l.Sequence)
                .ToListAsync();

        public async Task<LiftPlanEntity?> GetByIdAsync(int id)
            => await _db.LiftPlans
                .Include(l => l.ChecklistItems)
                .FirstOrDefaultAsync(l => l.Id == id);

        public async Task<LiftPlanEntity> AddAsync(LiftPlanEntity plan)
        {
            _db.LiftPlans.Add(plan);
            await _db.SaveChangesAsync();
            return plan;
        }

        public async Task UpdateAsync(LiftPlanEntity plan)
        {
            _db.LiftPlans.Update(plan);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var plan = await _db.LiftPlans.FindAsync(id);
            if (plan != null) { _db.LiftPlans.Remove(plan); await _db.SaveChangesAsync(); }
        }

        public async Task DeleteAllForProjectAsync(int projectId)
        {
            var plans = await _db.LiftPlans.Where(l => l.ProjectId == projectId).ToListAsync();
            _db.LiftPlans.RemoveRange(plans);
            await _db.SaveChangesAsync();
        }
    }
}
