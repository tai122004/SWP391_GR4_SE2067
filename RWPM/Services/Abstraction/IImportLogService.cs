using RWPM.Common.Models;
using RWPM.Models.Entities;

namespace RWPM.Services.Abstraction
{
    public interface IImportLogService
    {
        public Task<ImportLog?> GetByIdAsync(Guid id, QueryOptions<ImportLog>? queryOptions = null);
        public Task<ImportLog> GetRequiredByIdAsync(Guid id, QueryOptions<ImportLog>? queryOptions = null);
        public Task CreateAsync(ImportLog entity);
        public Task UpdateAsync(Guid id, int successfulRow, string? errorMessage = null);
    }
}
