using System.ComponentModel.DataAnnotations;
using RWPM.Common.Helper;

namespace RWPM.Common.Enums
{
    public enum AccountRole : byte
    {
        /// <summary>
        /// Super Admin / HR - Quản lý toàn bộ hệ thống
        /// </summary>
        [Display(ResourceType = typeof(Resources.Models.Acc), Name = "Role_SuperAdmin")]
        SuperAdmin = 1,

        /// <summary>
        /// Area Manager - Quản lý khu vực
        /// </summary>
        [Display(ResourceType = typeof(Resources.Models.Acc), Name = "Role_AreaManager")]
        AreaManager = 2,

        /// <summary>
        /// Store Manager - Quản lý cửa hàng
        /// </summary>
        [Display(ResourceType = typeof(Resources.Models.Acc), Name = "Role_StoreManager")]
        StoreManager = 3,

        /// <summary>
        /// Shift Leader - Trưởng ca
        /// </summary>
        [Display(ResourceType = typeof(Resources.Models.Acc), Name = "Role_ShiftLeader")]
        ShiftLeader = 4,

        /// <summary>
        /// Sales Staff - Nhân viên bán hàng
        /// </summary>
        [Display(ResourceType = typeof(Resources.Models.Acc), Name = "Role_SalesStaff")]
        SalesStaff = 5,

        /// <summary>
        /// Part-time Staff - Nhân viên bán thời gian
        /// </summary>
        [Display(ResourceType = typeof(Resources.Models.Acc), Name = "Role_PartTimeStaff")]
        PartTimeStaff = 6,

        /// <summary>
        /// Quản trị viên (Alias giữ tương thích với cấu hình admin)
        /// </summary>
        [Display(ResourceType = typeof(Resources.Models.Acc), Name = "Role_SuperAdmin")]
        Admin = SuperAdmin
    }

    public static class AccountRoleExtensions
    {
        public static string GetDisplayName(this AccountRole role)
        {
            return UIHelper.GetDisplayName(role);
        }
    }
}