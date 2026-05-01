using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace CraneOptimizer.Infrastructure.Repositories
{
    public class CraneRepository : ICraneRepository
    {
        private readonly CraneDbContext _context;

        public CraneRepository(CraneDbContext context)
        {
            _context = context;
        }

        public async Task<List<Crane>> GetAllAsync()
        {
            return await GetAllWithSpecsAsync();
        }

        public async Task<List<Crane>> GetAllWithSpecsAsync()
        {
            return await _context.Cranes
                                .Include(c => c.Specifications)
                                .ThenInclude(s => s.LoadChart)
                                .AsNoTracking()
                                .ToListAsync();
        }

        public async Task<Crane?> GetByIdWithSpecsAsync(int id)
        {
            return await _context.Cranes
                .AsNoTracking()
                .Include(c => c.Specifications)
                .ThenInclude(s => s.LoadChart)
                .FirstOrDefaultAsync(c => c.Id == id);
        }
        public async Task AddAsync(Crane crane)
        {
            await _context.Cranes.AddAsync(crane);
            await _context.SaveChangesAsync();
        }

        public async Task AddRangeAsync(List<Crane> cranes)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _context.ChangeTracker.AutoDetectChangesEnabled = false;

                await _context.Cranes.AddRangeAsync(cranes);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
            finally
            {
                _context.ChangeTracker.AutoDetectChangesEnabled = true;
            }
        }

        public async Task UpdateAsync(Crane crane)
        {
            _context.Cranes.Update(crane);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var crane = await _context.Cranes.FindAsync(id);
            if (crane == null) return;

            _context.Cranes.Remove(crane);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> ExistsByNameAsync(string name)
        {
            return await _context.Cranes
                .AnyAsync(c => c.Name == name);
        }
    }
}
