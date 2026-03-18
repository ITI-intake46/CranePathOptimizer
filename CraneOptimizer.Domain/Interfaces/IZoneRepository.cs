using System.Collections.Generic;
using System.Threading.Tasks;
using WPF_IFC_test1.Database.Entities;

namespace WPF_IFC_test1.Database.Repositories
{
    public interface IZoneRepository
    {
        Task<List<ZoneEntity>> GetByProjectAsync(int projectId);   // includes Vertices
        Task<ZoneEntity>       GetByIdAsync(int id);               // includes Vertices
        Task<int>              AddAsync(ZoneEntity zone);
        Task                   UpdateAsync(ZoneEntity zone);       // replaces vertices
        Task                   DeleteAsync(int id);
        Task                   DeleteAllForProjectAsync(int projectId);
    }
}
