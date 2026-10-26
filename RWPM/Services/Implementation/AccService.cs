using RWPM.Common.Validation;
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
            await using var accountTransaction = _ctx.Database.CurrentTransaction == null
                ? await _ctx.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable) : null;

            //I. Pre-validation 
            entity.Username = entity.Username?.Trim() ?? "";
            entity.FullName = entity.FullName?.Trim() ?? "";
            entity.Email = entity.Email?.Trim() ?? "";
            entity.PhoneNumber = entity.PhoneNumber?.Trim();
            InputRules.ValidateAccount(entity.Username, entity.FullName, entity.Email, entity.PhoneNumber, entity.DateOfBirth, entity.Gender, entity.Role);

            InputRules.ValidatePassword(entity.Password);

            //II. Validation
            if (entity.Role == AccountRole.Admin)
                throw new InvalidOperationException(Resources.Shared.Acc.Err_CannotCreateAdmin);

            if (await ExistsAsync(entity.Username))
                throw new InputValidationException("Username", InputRules.Text("Tài khoản đã tồn tại.", "Username already exists."));

            if (!string.IsNullOrWhiteSpace(entity.Email))
            {
                var trimmedEmail = entity.Email.Trim().ToLower();
                if (await _ctx.Acc.AnyAsync(a => a.Email.Trim().ToLower() == trimmedEmail))
                {
                    throw new InputValidationException("Email", string.Format(Resources.Shared.Acc.Err_EmailInUse, entity.Email));
                }
            }

            if (!string.IsNullOrWhiteSpace(entity.PhoneNumber))
            {
                var trimmedPhone = entity.PhoneNumber.Trim();
                if (await _ctx.Acc.AnyAsync(a => a.PhoneNumber != null && a.PhoneNumber.Trim() == trimmedPhone))
                {
                    throw new InputValidationException("PhoneNumber", string.Format(Resources.Shared.Acc.Err_PhoneInUse, entity.PhoneNumber));
                }
            }

            var actor = await WorkflowGuard.ActorAsync(_ctx, AccountHelper.GetCurrentUsername(_httpContextAccessor));
            InputRules.Require(actor.Role is AccountRole.Admin or AccountRole.HR, "Role", "Không có quyền quản lý tài khoản.", "Not authorized to manage accounts.");

            //III. Insert
            var createEntity = new Acc()
            {
                Username = entity.Username,
                Password = _hasher.HashPassword(null!, entity.Password),
                FullName = entity.FullName.Trim(),
                Role = entity.Role,
                Email = entity.Email,
                PhoneNumber = entity.PhoneNumber,
                DateOfBirth = entity.DateOfBirth,
                Gender = entity.Gender,
                AvatarUrl = entity.AvatarUrl,
                // Mật khẩu do HR/Admin đặt -> người dùng phải đổi ở lần đăng nhập đầu tiên
                MustChangePassword = true,
                SecurityStamp = RWPM.Common.Security.SessionSecurity.NewStamp(),

                CreatedDate = DateTime.Now,
                CreatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor),
            };
            await _ctx.Acc.AddAsync(createEntity);
            await _ctx.SaveChangesAsync();
            if (accountTransaction != null) await accountTransaction.CommitAsync();
            return createEntity;
        }

        public async Task DeleteAsync(Acc entity)
        {
            await EnsureCanChangeStatusAsync(entity, InputRules.Text("xóa", "delete"));
            if (await _ctx.Employee.AnyAsync(e => e.Username == entity.Username))
                throw new InputValidationException("Username", InputRules.Text(
                    $"Không thể xóa tài khoản {entity.Username} vì đã có hồ sơ nhân viên. Hãy xóa hoặc cho thôi việc hồ sơ nhân viên, hoặc khóa tài khoản thay vì xóa.",
                    $"Cannot delete account {entity.Username} because it has an employee profile. Delete or resign the employee profile, or deactivate the account instead."));
            _ctx.Acc.Remove(entity);
            await _ctx.SaveChangesAsync();
        }

        /// <summary>
        /// Chặn HR khóa/xóa tài khoản Admin và chặn người dùng tự khóa/xóa chính mình.
        /// </summary>
        private async Task EnsureCanChangeStatusAsync(Acc target, string action)
        {
            var caller = AccountHelper.GetCurrentUsername(_httpContextAccessor);
            var actor = await WorkflowGuard.ActorAsync(_ctx, caller);
            if (target.Role == AccountRole.Admin && actor.Role != AccountRole.Admin)
                throw new UnauthorizedAccessException(InputRules.Text($"Không được {action} tài khoản Admin.", $"Cannot {action} an Admin account."));
            if (string.Equals(target.Username, caller, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException(InputRules.Text($"Không được {action} tài khoản của chính mình.", $"You cannot {action} your own account."));
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
            searchObject.PageNumber = Math.Clamp(searchObject.PageNumber, 1, 1000000);
            searchObject.PageSize = Math.Clamp(searchObject.PageSize, 1, 50);

            // I. Prepare queryable
            var query = _ctx.Acc.AsQueryable();

            // II. Filter data
            if (!string.IsNullOrWhiteSpace(searchObject.Search))
            {
                query = query.Where(c => c.Username.StartsWith(searchObject.Search) ||
                                         c.Email.StartsWith(searchObject.Search));
            }

            if (searchObject.IsActiveBool.HasValue)
            {
                query = query.Where(c => c.IsActive == searchObject.IsActiveBool.Value);
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
            await using var accountTransaction = _ctx.Database.CurrentTransaction == null
                ? await _ctx.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable) : null;

            // I. Pre-validation
            entity.Username = entity.Username?.Trim() ?? "";
            entity.FullName = entity.FullName?.Trim() ?? "";
            entity.Email = entity.Email?.Trim() ?? "";
            entity.PhoneNumber = entity.PhoneNumber?.Trim();
            InputRules.ValidateAccount(entity.Username, entity.FullName, entity.Email, entity.PhoneNumber, entity.DateOfBirth, entity.Gender, entity.Role);

            // II. Validation
            if (entity.Role == AccountRole.Admin)
                throw new InvalidOperationException(Resources.Shared.Acc.Err_CannotChangeRoleToAdmin);

            var updateEntity = await GetRequiredByIdAsync(entity.Username, new QueryOptions<Acc>()
            {
                NoTracking = false,
            });

            if (!string.IsNullOrWhiteSpace(entity.Email))
            {
                var trimmedEmail = entity.Email.Trim().ToLower();
                if (await _ctx.Acc.AnyAsync(a => a.Username != entity.Username && a.Email.Trim().ToLower() == trimmedEmail))
                {
                    throw new InputValidationException("Email", string.Format(Resources.Shared.Acc.Err_EmailInUse, entity.Email));
                }
            }

            if (!string.IsNullOrWhiteSpace(entity.PhoneNumber))
            {
                var trimmedPhone = entity.PhoneNumber.Trim();
                if (await _ctx.Acc.AnyAsync(a => a.Username != entity.Username && a.PhoneNumber != null && a.PhoneNumber.Trim() == trimmedPhone))
                {
                    throw new InputValidationException("PhoneNumber", string.Format(Resources.Shared.Acc.Err_PhoneInUse, entity.PhoneNumber));
                }
            }

            var actor = await WorkflowGuard.ActorAsync(_ctx, AccountHelper.GetCurrentUsername(_httpContextAccessor));
            InputRules.Require(actor.Role is AccountRole.Admin or AccountRole.HR, "Role", "Không có quyền quản lý tài khoản.", "Not authorized to manage accounts.");

            if (updateEntity.Role == AccountRole.Admin && actor.Role != AccountRole.Admin)
                throw new UnauthorizedAccessException(InputRules.Text("Không được sửa tài khoản Admin.", "Cannot edit an Admin account."));
            // III. Update
            updateEntity.FullName = entity.FullName.Trim();
            updateEntity.Role = entity.Role;
            updateEntity.Email = entity.Email;
            updateEntity.PhoneNumber = entity.PhoneNumber;
            updateEntity.DateOfBirth = entity.DateOfBirth;
            updateEntity.Gender = entity.Gender;
            if (!string.IsNullOrEmpty(entity.AvatarUrl))
            {
                updateEntity.AvatarUrl = entity.AvatarUrl;
            }
            updateEntity.UpdatedDate = DateTime.Now;
            updateEntity.UpdatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor);

            // IV. Save
            await _ctx.SaveChangesAsync();
            if (accountTransaction != null) await accountTransaction.CommitAsync();
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

            InputRules.ValidatePassword(newPass);

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
            currentUser.MustChangePassword = false;
            // Đổi stamp -> các phiên đăng nhập khác của tài khoản này bị đăng xuất (phiên hiện tại được cấp lại ở controller)
            currentUser.SecurityStamp = RWPM.Common.Security.SessionSecurity.NewStamp();
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
            var caller = AccountHelper.GetCurrentUsername(_httpContextAccessor);
            var actor = await WorkflowGuard.ActorAsync(_ctx, caller);
            if (actor.Role is not (AccountRole.Admin or AccountRole.HR))
                throw new UnauthorizedAccessException(InputRules.Text("Không có quyền đổi mật khẩu tài khoản khác.", "Not allowed to reset another account password."));
            var target = await GetRequiredByIdAsync(changePasswordDto.Username);
            if (target.Role == AccountRole.Admin && actor.Role != AccountRole.Admin)
                throw new UnauthorizedAccessException(InputRules.Text("Chỉ Admin được đổi mật khẩu Admin.", "Only Admin may reset an Admin password."));
            InputRules.ValidatePassword(changePasswordDto.NewPassword);
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
            // Tự đặt lại mật khẩu của chính mình phải qua màn Đổi mật khẩu (cần mật khẩu cũ), tránh tự bắt buộc đổi và bị đăng xuất
            if (string.Equals(changePasswordDto.Username, caller, StringComparison.OrdinalIgnoreCase))
                throw new InputValidationException("NewPassword", InputRules.Text("Hãy dùng chức năng Đổi mật khẩu để đổi mật khẩu của chính bạn.", "Use Change Password to change your own password."));

            var currentUser = await GetRequiredByIdAsync(changePasswordDto.Username, new QueryOptions<Acc>()
            {
                NoTracking = false
            });

            currentUser.Password = _hasher.HashPassword(null!, changePasswordDto.NewPassword);
            // Mật khẩu do người khác cấp lại -> bắt đổi ở lần đăng nhập kế tiếp, đăng xuất mọi phiên đang mở
            currentUser.MustChangePassword = true;
            currentUser.SecurityStamp = RWPM.Common.Security.SessionSecurity.NewStamp();
            currentUser.UpdatedBy = caller;
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
            await EnsureCanChangeStatusAsync(updateEntity, InputRules.Text("khóa/mở khóa", "lock/unlock"));
            if (active && await _ctx.Employee.AnyAsync(e => e.Username == username && e.Status == EmploymentStatus.Resigned))
                throw new InputValidationException("IsActive", InputRules.Text(
                    "Nhân viên của tài khoản này đã thôi việc. Hãy sửa trạng thái làm việc trong hồ sơ nhân viên trước khi kích hoạt lại.",
                    "This account's employee has resigned. Change the employment status in the employee profile before reactivating."));

            // III. Update
            updateEntity.IsActive = active;
            updateEntity.UpdatedDate = DateTime.Now;
            updateEntity.UpdatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor);

            // Đồng bộ trạng thái cho các bản ghi Nhân viên (Employee) tương ứng với tài khoản này
            var linkedEmployees = await _ctx.Employee.Where(e => e.Username == username).ToListAsync();
            foreach (var emp in linkedEmployees)
            {
                emp.IsActive = active;
                emp.UpdatedDate = DateTime.Now;
                emp.UpdatedBy = updateEntity.UpdatedBy;
            }

            // IV. Save
            await _ctx.SaveChangesAsync();
        }

        public SelectList GetAccountRoleSelectListAsync()
        {
            var data = Enum.GetValues(typeof(AccountRole))
                .Cast<AccountRole>()
                .Where(x => x != AccountRole.Admin)
                .Distinct()
                .Select(e => new SelectListItem
                {
                    Text = UIHelper.GetDisplayName(e),
                    Value = ((byte)e).ToString()
                }).ToArray();

            return new SelectList(data, "Value", "Text");
        }
    }
}
