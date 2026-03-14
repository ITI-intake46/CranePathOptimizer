using CraneOptimizer.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CraneOptimizer.Domain.Interfaces
{
    public interface ICraneRepository
    {
        Task<List<Crane>> GetAllAsync();
        Task<Crane?> GetByIdWithSpecsAsync(int id);
    }
}
