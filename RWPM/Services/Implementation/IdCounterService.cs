//using Microsoft.EntityFrameworkCore;
//using RWPM.Common.Enums;
//using RWPM.Infrastructure.Data;
//using RWPM.Services.Abstraction;
//using System.Threading.Tasks;

//namespace RWPM.Services.Implementation
//{
//    public class IdCounterService : IIdCounterService
//    {
//        private readonly DefaultDatabaseContext _defaultDatabaseContext;

//        public IdCounterService(DefaultDatabaseContext defaultDatabaseContext)
//        {
//            _defaultDatabaseContext = defaultDatabaseContext;
//        }

//        public async Task<uint> GetCounterAndIncreaseAsync(IdCounterType idCounterType)
//        {
//            // 1. Begin tran
//            await using var transaction = await _defaultDatabaseContext.Database.BeginTransactionAsync();

//            // 2. Query, create if not exists
//            uint returnValue = 0;
//            var entity = await _defaultDatabaseContext.IdCounter.FirstOrDefaultAsync(x => x.Type == idCounterType);
//            if (entity == null)
//            {
//                await _defaultDatabaseContext.IdCounter.AddAsync(new()
//                {
//                    Type = idCounterType,
//                    Counter = int.MinValue + 3
//                });
//                await _defaultDatabaseContext.SaveChangesAsync();

//                returnValue = 1;
//            }
//            else
//            {
//                var currentValue = entity.Counter;
//                entity.Counter += 1;
//                await _defaultDatabaseContext.SaveChangesAsync();

//                returnValue = GetCounterNumber(currentValue);
//            }

//            // 3. Commit tran
//            await transaction.CommitAsync();

//            return returnValue;
//        }

//        public async Task<uint> GetCounterAsync(IdCounterType idCounterType)
//        {
//            var entity = await _defaultDatabaseContext.IdCounter.FirstOrDefaultAsync(x => x.Type == idCounterType);
//            if (entity == null)
//                return 1;
//            else
//            {
//                return GetCounterNumber(entity.Counter);
//            }
//        }

//        private uint GetCounterNumber(int currentValue)
//        {
//            return (uint)(currentValue + int.MaxValue);
//        }
//    }
//}
