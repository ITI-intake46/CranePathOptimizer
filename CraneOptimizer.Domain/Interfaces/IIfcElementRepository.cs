using System.Collections.Generic;
using System.Threading.Tasks;
using WPF_IFC_test1.Database.Entities;

namespace WPF_IFC_test1.Database.Repositories
{
    public interface IIfcElementRepository
    {
        Task<List<IfcElementEntity>> GetByProjectAsync(int projectId);
        Task<IfcElementEntity>       GetByGlobalIdAsync(int projectId, string globalId);
        Task                         BulkInsertAsync(IEnumerable<IfcElementEntity> elements);
        Task                         UpdateAsync(IfcElementEntity element);
        Task                         DeleteAllForProjectAsync(int projectId);
    }
}
