using System.Security.Claims;
using RWPM.Common.Enums;

namespace RWPM.Common.Security
{
    /// <summary>
    /// Tên role dùng trong [Authorize] và User.IsInRole. Lấy đúng tên enum AccountRole
    /// (claim role được tạo từ account.Role.ToString() trong SessionSecurity), nên không thể có role ngoài enum.
    /// </summary>
    public static class AppRoles
    {
        public const string Admin = nameof(AccountRole.Admin);
        public const string HR = nameof(AccountRole.HR);
        public const string StoreManager = nameof(AccountRole.StoreManager);
        public const string Employee = nameof(AccountRole.Employee);

        /// <summary>Quản lý toàn chuỗi: Admin, HR.</summary>
        public const string ChainManagers = Admin + "," + HR;
        /// <summary>Mọi cấp quản lý: Admin, HR, StoreManager.</summary>
        public const string Managers = Admin + "," + HR + "," + StoreManager;
    }

    public static class RoleExtensions
    {
        public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(AppRoles.Admin);
        public static bool IsHR(this ClaimsPrincipal user) => user.IsInRole(AppRoles.HR);
        public static bool IsStoreManager(this ClaimsPrincipal user) => user.IsInRole(AppRoles.StoreManager);
        public static bool IsEmployeeRole(this ClaimsPrincipal user) => user.IsInRole(AppRoles.Employee);

        /// <summary>Admin hoặc HR — xem/quản lý toàn chuỗi.</summary>
        public static bool IsChainManager(this ClaimsPrincipal user) => user.IsAdmin() || user.IsHR();
        /// <summary>Admin, HR hoặc StoreManager.</summary>
        public static bool IsManager(this ClaimsPrincipal user) => user.IsChainManager() || user.IsStoreManager();
        /// <summary>StoreManager không kiêm Admin/HR — bị giới hạn trong chi nhánh của mình.</summary>
        public static bool IsStoreScoped(this ClaimsPrincipal user) => user.IsStoreManager() && !user.IsChainManager();
    }
}
