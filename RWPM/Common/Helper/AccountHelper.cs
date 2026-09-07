using RWPM.Models.Entities;
using System.Linq.Expressions;
using System.Security.Claims;

namespace RWPM.Common.Helper
{
    public static class AccountHelper
    {
        public readonly static string ADMIN_USERNAME = "admin";
        public readonly static string ANONYMOUS_USERNAME = "_anonymous_";
        public readonly static string SEEDER = "_seeder_";

        public static Expression<Func<Acc, bool>> IsNotDefaultAccount => x => x.Username != ADMIN_USERNAME && x.Username != ANONYMOUS_USERNAME;

        public static string GetCurrentUsername(IHttpContextAccessor httpContextAccessor)
        {
            var user = GetCurrentUser(httpContextAccessor);
            return GetCurrentUsername(user);
        }

        public static string GetCurrentUsername(ClaimsPrincipal user)
        {
            if (user.Identity?.Name is null)
                throw new Exception("Current authentiacted user have null name!");
            return user.Identity.Name;
        }

        public static ClaimsPrincipal GetCurrentUser(IHttpContextAccessor httpContextAccessor)
        {
            var user = httpContextAccessor.HttpContext?.User;
            if (user is null || user.Identity is null || !user.Identity.IsAuthenticated)
                throw new Exception("Can't get current authenticated user!");
            return user;
        }

        public static bool IsAdmin(IHttpContextAccessor httpContextAccessor)
        {
            var user = httpContextAccessor.HttpContext?.User;
            if (user is null || user.Identity is null || !user.Identity.IsAuthenticated)
                throw new Exception("Can't get current authenticated user!");

            return user.IsInRole("Admin") || user.IsInRole("SuperAdmin");
        }
    }
}
