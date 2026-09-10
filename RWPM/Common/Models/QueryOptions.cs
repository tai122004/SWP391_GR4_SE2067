using System.Linq.Expressions;

namespace RWPM.Common.Models
{
    public class QueryOptions<T>
    {
        public bool GetOnlyActiveRecord = false;
        public bool NoTracking { get; set; } = true;
        public List<Expression<Func<T, object?>>> Includes { get; set; } = new();
    }

}
