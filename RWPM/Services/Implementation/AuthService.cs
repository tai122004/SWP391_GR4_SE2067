using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RWPM.Common;
using RWPM.Common.Enums;
using RWPM.Common.Exceptions;
using RWPM.Common.Models;

using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.Auth;
using RWPM.Services.Abstraction;
using System.Security.Claims;

namespace RWPM.Services.Implementation
{
    public class AuthService : IAuthService
    {
        private readonly DefaultDatabaseContext _ctx;
        private readonly PasswordHasher<Acc> _hasher = new PasswordHasher<Acc>();
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthService(DefaultDatabaseContext ctx,
            IHttpContextAccessor httpContextAccessor)
        {
            _ctx = ctx;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LoginAsync(LoginVM viewModel)
        {
            var account = await GetByIdAsync(viewModel.Username, new Common.Models.QueryOptions<Acc>()
            {
               NoTracking = false, 
            }) ?? throw new ModelValidationException("Auth_InvalidCredentials");

            if (!account.IsActive)
                throw new ModelValidationException("Acc_IsLock", account.Username);

            var result = _hasher.VerifyHashedPassword(account, account.Password, viewModel.Password);
            if (result != PasswordVerificationResult.Success)
                throw new ModelValidationException("Auth_InvalidCredentials");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, account.Username),
                new Claim(ClaimTypes.Role, account.Role.ToString())
            };

            if (account.Role == AccountRole.Admin)
            {
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(claimsIdentity);

            var httpContext = _httpContextAccessor.HttpContext ?? throw new InvalidOperationException("HttpContext is not available.");
            await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            account.LastLogin = DateTime.Now;
            await _ctx.SaveChangesAsync();
        }

        public async Task<bool> UserAuthenticatedAsync(string username, string password)
        {
            var account = await GetByIdAsync(username);
            if (account == null)
                return false;

            var result = _hasher.VerifyHashedPassword(account, account.Password, password);
            return result == PasswordVerificationResult.Success;
        }

        private async Task<Acc?> GetByIdAsync(string username, QueryOptions<Acc>? options = null)
        {
            if (string.IsNullOrWhiteSpace(username))
                return null;

            var query = _ctx.Acc.AsQueryable();
            query = QueryHelper.ApplyQueryOptions(query, options);
            return await query.FirstOrDefaultAsync(x => x.Username == username);
        }
    }
}
