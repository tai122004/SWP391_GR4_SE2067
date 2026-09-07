using Microsoft.EntityFrameworkCore;
using RWPM.Common;
using RWPM.Common.Helper;
using RWPM.Common.Models;
using RWPM.Common.Enums;

using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;
using RWPM.Services.Abstraction;
using RWPM.Common.Exceptions;

namespace RWPM.Services.Implementation
{
    public class ImportLogService : IImportLogService
    {
        private readonly DefaultDatabaseContext _ctx;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ImportLogService(DefaultDatabaseContext databaseContext,
            IHttpContextAccessor httpContextAccessor)
        {
            _ctx = databaseContext;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task CreateAsync(ImportLog entity)
        {
            entity.ImportStartDate = DateTime.Now;
            entity.UserId = AccountHelper.GetCurrentUsername(_httpContextAccessor);

            await _ctx.ImportLog.AddAsync(entity);
            await _ctx.SaveChangesAsync();
        }

        public Task<ImportLog?> GetByIdAsync(Guid id, QueryOptions<ImportLog>? queryOptions = null)
        {
            var query = _ctx.ImportLog.AsQueryable();
            query = QueryHelper.ApplyQueryOptions(query, queryOptions);
            return query.FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<ImportLog> GetRequiredByIdAsync(Guid id, QueryOptions<ImportLog>? queryOptions = null)
        {
            var data = await GetByIdAsync(id, queryOptions)
                        ?? throw new ModelValidationException("ImportLog_NotExists");
            return data;
        }

        public async Task UpdateAsync(Guid id, int successfulRow, string? errorMessage = null)
        {
            var updateEntity = await GetRequiredByIdAsync(id, new QueryOptions<ImportLog>()
            {
                NoTracking = false
            });

            updateEntity.SuccessfulRows = successfulRow;
            updateEntity.ImportDoneDate = DateTime.Now;

            if (!string.IsNullOrEmpty(errorMessage))
            {
                updateEntity.ErrorMessages = errorMessage;
            }

            await _ctx.SaveChangesAsync();
        }

    }
}
