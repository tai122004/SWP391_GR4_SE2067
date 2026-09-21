using RWPM.Common.Models;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.Shift;

namespace RWPM.Services.Abstraction
{
    public interface IShiftService
    {
        Task<Shift?> GetByIdAsync(int shiftId, QueryOptions<Shift>? options = null);
        Task<Shift> GetRequiredByIdAsync(int shiftId, QueryOptions<Shift>? options = null);
        Task<List<Shift>> GetAllAsync(QueryOptions<Shift>? options = null);
        Task<PaginationRes<Shift>> SearchAsync(ShiftSearch searchObject, QueryOptions<Shift>? options = null);
        Task<Shift> CreateAsync(Shift entity);
        Task UpdateAsync(Shift entity);
        Task<bool> ExistsByCodeAsync(string shiftCode, int? excludeShiftId = null);
        Task UpdateActiveStatusAsync(int shiftId, bool active);
    }
}
