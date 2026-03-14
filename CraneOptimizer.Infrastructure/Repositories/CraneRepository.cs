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
            return await _context.Cranes.ToListAsync();
        }

        public async Task<Crane?> GetByIdWithSpecsAsync(int id)
        {
            return await _context.Cranes
                .AsNoTracking()
                .Include(c => c.Specifications)
                .ThenInclude(s => s.LoadChart)
                .FirstOrDefaultAsync(c => c.Id == id);
        }
    }
}
