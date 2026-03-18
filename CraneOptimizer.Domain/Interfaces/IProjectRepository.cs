using System.Collections.Generic;
using System.Threading.Tasks;
using WPF_IFC_test1.Database.Entities;

namespace WPF_IFC_test1.Database.Repositories
{
    public interface IProjectRepository
    {
        Task<List<ProjectEntity>> GetAllAsync();
        Task<ProjectEntity>       GetByIdAsync(int id);
        Task<ProjectEntity>       GetWithAllAsync(int id);   // includes cranes, zones, elements
        Task<int>                 AddAsync(ProjectEntity project);
        Task                      UpdateAsync(ProjectEntity project);
        Task                      DeleteAsync(int id);
    }
}
