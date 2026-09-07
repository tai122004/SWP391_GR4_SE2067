using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RWPM.Common;
using RWPM.Common.Enums;
using RWPM.Common.Exceptions;
using RWPM.Common.Helper;
using RWPM.Common.Models;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.Acc;
using RWPM.Models.ViewModels.Acc.ChangePasswordAcc;
using RWPM.Services.Abstraction;

namespace RWPM.Services.Implementation
{
    public class AccService : IAccService
    {
        private readonly DefaultDatabaseContext _ctx;
        private readonly PasswordHasher<Acc> _hasher;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IAuthService _authService;

        public AccService(DefaultDatabaseContext ctx,
            IHttpContextAccessor httpContextAccessor,
            IAuthService authService)
        {
            _ctx = ctx;
            _hasher = new PasswordHasher<Acc>();
            _httpContextAccessor = httpContextAccessor;
            _authService = authService;
        }

        public async Task<Acc> CreateAsync(Acc entity)
        {
            //I. Pre-validation 
            entity.Username = entity.Username.Trim();

            //II. Validation
            if (entity.Role == AccountRole.Admin)
                throw new Exception("Can't create admin account!");

            if (await ExistsAsync(entity.Username))
                throw new ModelValidationException("Acc_UsernameExists", entity.Username);

            await NavigationValidationAsync(entity);

            //III. Insert
            var createEntity = new Acc()
            {
                Username = entity.Username,
                Password = _hasher.HashPassword(null!, entity.Password),
                FullName = entity.FullName.Trim(),
                Role = entity.Role,
                Email = entity.Email,

                CreatedDate = DateTime.Now,
                CreatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor),
            };
            await _ctx.Acc.AddAsync(createEntity);
            await _ctx.SaveChangesAsync();
            return createEntity;
        }

        public async Task DeleteAsync(Acc entity)
        {
            _ctx.Acc.Remove(entity);
            await _ctx.SaveChangesAsync();
        }

        private Task NavigationValidationAsync(Acc entity)
        {
            return Task.CompletedTask;
        }

        public Task<List<Acc>> GetAllAsync(QueryOptions<Acc>? options = null)
        {
            var query = _ctx.Acc.AsQueryable();
            query = QueryHelper.ApplyQueryOptions(query, options);
            query = RemoveDefaultAccount(query);
            return query.ToListAsync();
        }

        public async Task<Acc?> GetByIdAsync(string username, QueryOptions<Acc>? options = null)
        {
            if (string.IsNullOrWhiteSpace(username))
                return null;

            var query = _ctx.Acc.AsQueryable();
            query = QueryHelper.ApplyQueryOptions(query, options);
            return await query.FirstOrDefaultAsync(x => x.Username == username);
        }

        public async Task<SelectList> GetSelectListAsync()
        {
            var data = new List<object>()
            {
                new
                {
                    Id = string.Empty,
                    Display = Resources.Shared.SharedResource.Dropdown_SelectAccount
                }
            };

            var accountData = _ctx.Acc.AsNoTracking().Where(x => x.IsActive).AsQueryable();
            accountData = RemoveDefaultAccount(accountData);

            data.AddRange(await accountData.Select(f => new {
                Id = f.Username,
                Display = f.FullName + $" ({f.Username})"
            }).ToListAsync());

            return new SelectList(data, "Id", "Display");
        }

        public async Task<PaginationRes<Acc>> SearchAsync(AccSearch searchObject, QueryOptions<Acc>? options = null)
        {
            // I. Prepare queryable
            var query = _ctx.Acc.AsQueryable();

            // II. Filter data
            if (!string.IsNullOrWhiteSpace(searchObject.Search))
            {
                query = query.Where(c => c.Username.StartsWith(searchObject.Search) ||
                                         c.Email.StartsWith(searchObject.Search));
            }

            //if(searchObject.DeptCatId.HasValue && searchObject.DeptCatId > 0)
            //{
            //    query = query.Where(c => c.DeptCatId == searchObject.DeptCatId.Value);
            //}

            if (searchObject.IsActive.HasValue)
            {
                query = query.Where(c => c.IsActive == searchObject.IsActive.Value);
            }

            // III. Load navigation properties if required 
            query = QueryHelper.ApplyQueryOptions(query, options);

            query = RemoveDefaultAccount(query);

            // IV. Execute search 
            var resultCount = await query.CountAsync();
            var resultData = await query
                .OrderBy(x => x.Username)
                .Skip((searchObject.PageNumber - 1) * searchObject.PageSize)
                .Take(searchObject.PageSize)
                .ToListAsync();

            return new PaginationRes<Acc>(
                data: resultData,
                searchObject.PageNumber,
                pageSize: searchObject.PageSize,
                totalRecords: resultCount);
        }

        private IQueryable<Acc> RemoveDefaultAccount(IQueryable<Acc> data)
        {
            return data.Where(AccountHelper.IsNotDefaultAccount);
        }

        public async Task UpdateAsync(Acc entity)
        {
            // I. Pre-validation
            entity.Username = entity.Username.Trim();

            // II. Validation
            if (entity.Role == AccountRole.Admin)
                throw new Exception("Can't change role to admin!");

            var updateEntity = await GetRequiredByIdAsync(entity.Username, new QueryOptions<Acc>()
            {
                NoTracking = false,
            });
            await NavigationValidationAsync(entity);

            // III. Update
            updateEntity.FullName = entity.FullName.Trim();
            updateEntity.Role = entity.Role;
            updateEntity.Email = entity.Email;
            updateEntity.UpdatedDate = DateTime.Now;
            updateEntity.UpdatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor);

            // IV. Save
            await _ctx.SaveChangesAsync();
        }

        public Task<bool> ExistsAsync(string username)
            => _ctx.Acc.AnyAsync(c => c.Username == username);

        public async Task ChangePasswordAsync(string oldPass, string newPass, string confirmNewPass)
        {
            // I. Pre-validation
            if (string.IsNullOrWhiteSpace(oldPass))
                throw new ArgumentNullException(nameof(oldPass));
            if (string.IsNullOrWhiteSpace(newPass))
                throw new ArgumentNullException(nameof(newPass));
            if (string.IsNullOrWhiteSpace(confirmNewPass))
                throw new ArgumentNullException(nameof(confirmNewPass));

            if (!string.Equals(newPass, confirmNewPass))
                throw new ModelValidationException("Acc_NewPassAndConfirmNewPassAreNotSame");

            if (string.Equals(oldPass, newPass))
                throw new ModelValidationException("Acc_NewPassAndOldPassAreSame");

            // II. Check username and password
            var currentUsername = AccountHelper.GetCurrentUsername(_httpContextAccessor);
            if(!await _authService.UserAuthenticatedAsync(currentUsername, oldPass))
                throw new ModelValidationException("Acc_CurrentPasswordIsIncorrect");

            // III. Update password
            var currentUser = await GetRequiredByIdAsync(currentUsername, new QueryOptions<Acc>()
            {
                NoTracking = false
            });

            currentUser.Password = _hasher.HashPassword(null!, newPass);
            currentUser.UpdatedBy = currentUsername;
            currentUser.UpdatedDate = DateTime.Now;

            // IV: Save
            await _ctx.SaveChangesAsync();
        }

        /// <summary>
        /// Only use for admin - to update password of another account
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ModelValidationException"></exception>
        /// <exception cref="Exception"></exception>
        public async Task ChangePasswordForAccountAsync(ChangePasswordAccDto changePasswordDto)
        {
            // I. Pre-validation
            if (string.IsNullOrWhiteSpace(changePasswordDto.Username))
                throw new ArgumentNullException(nameof(changePasswordDto.Username));
            if (string.IsNullOrWhiteSpace(changePasswordDto.NewPassword))
                throw new ArgumentNullException(nameof(changePasswordDto.NewPassword));
            if (string.IsNullOrWhiteSpace(changePasswordDto.ConfirmPassword))
                throw new ArgumentNullException(nameof(changePasswordDto.ConfirmPassword));

            if (!string.Equals(changePasswordDto.NewPassword, changePasswordDto.ConfirmPassword))
                throw new ModelValidationException("Acc_NewPassAndConfirmNewPassAreNotSame");

            // II. Update password
            var currentUser = await GetRequiredByIdAsync(changePasswordDto.Username, new QueryOptions<Acc>()
            {
                NoTracking = false
            });

            currentUser.Password = _hasher.HashPassword(null!, changePasswordDto.NewPassword);
            currentUser.UpdatedBy = changePasswordDto.Username;
            currentUser.UpdatedDate = DateTime.Now;

            // IV: Save
            await _ctx.SaveChangesAsync();
        }

        public async Task<Acc> GetRequiredByIdAsync(string username, QueryOptions<Acc>? options = null)
        {
            var acc = await GetByIdAsync(username, options)
                                ?? throw new ModelValidationException($"Acc_UsernameNotExists", username);
            return acc;
        }

        public async Task UpdateActiveStatusAsync(string username, bool active)
        {
            // I. Pre-validation

            // II. Validation
            var updateEntity = await GetRequiredByIdAsync(username, new QueryOptions<Acc>()
            {
                NoTracking = false,
            });

            // III. Update
            updateEntity.IsActive = active;
            updateEntity.UpdatedDate = DateTime.Now;
            updateEntity.UpdatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor);

            // IV. Save
            await _ctx.SaveChangesAsync();
        }


        public async Task EnsureExistsAsync(string username)
        {
            var exists = await ExistsAsync(username);
            if (!exists)
                throw new ModelValidationException("Acc_UsernameNotExists", username);
        }

        public async Task<SelectList> GetSelectListWithExceptsAsync(string[] exceptUsernames)
        {
            var data = new List<object>()
            {
                new
                {
                    Id = string.Empty,
                    Display = Resources.Shared.SharedResource.Dropdown_SelectAccount
                }
            };

            var accountData = _ctx.Acc.AsNoTracking().Where(x => x.IsActive).AsQueryable();
            accountData = RemoveDefaultAccount(accountData);

            accountData = accountData.Where(x => !exceptUsernames.Contains(x.Username));

            data.AddRange(await accountData.Select(f => new {
                Id = f.Username,
                Display = f.FullName + $" ({f.Username})"
            }).ToListAsync());

            return new SelectList(data, "Id", "Display");
        }

        public SelectList GetAccountRoleSelectListAsync()
        {
            var data = Enum.GetValues(typeof(AccountRole))
                .Cast<AccountRole>()
                .Where(x => x != AccountRole.Admin)
                .Select(e => new SelectListItem
                {
                    Text = UIHelper.GetDisplayName(e),
                    Value = ((byte)e).ToString()
                }).ToArray();

            return new SelectList(data, "Value", "Text");
        }
    }
}
