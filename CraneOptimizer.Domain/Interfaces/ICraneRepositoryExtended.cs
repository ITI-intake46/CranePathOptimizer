using System.Collections.Generic;
using System.Threading.Tasks;
using WPF_IFC_test1.Database.Entities;

namespace WPF_IFC_test1.Database.Repositories
{
    public interface ICraneRepository
    {
        Task<List<CraneEntity>> GetByProjectAsync(int projectId);
        Task<CraneEntity>       GetByIdAsync(int id);
        Task<int>               AddAsync(CraneEntity crane);
        Task                    UpdateAsync(CraneEntity crane);
        Task                    DeleteAsync(int id);
        Task                    DeleteAllForProjectAsync(int projectId);
    }
}
