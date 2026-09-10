using Microsoft.EntityFrameworkCore;
using RWPM.Common.Models;
using RWPM.Models.Common;

namespace RWPM.Common
{
    public static class QueryHelper
    {
        public static IQueryable<TEntity> ApplyQueryOptions<TEntity>(
            IQueryable<TEntity> query,
            QueryOptions<TEntity>? options = null)
            where TEntity : class
        {
            options ??= new QueryOptions<TEntity>();

            if (options.NoTracking)
                query = query.AsNoTracking();

            foreach (var include in options.Includes)
                query = query.Include(include);

            // Handle GetOnlyActiveRecord
            if (options.GetOnlyActiveRecord && typeof(IActivatable).IsAssignableFrom(typeof(TEntity)))
            {
                query = query.Cast<IActivatable>().Where(x => x.IsActive).Cast<TEntity>();
            }

            return query;
        }
    }
}
