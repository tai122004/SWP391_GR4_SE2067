using System.ComponentModel.DataAnnotations;
using RWPM.Common.Helper;

namespace RWPM.Common.Enums
{
    public enum AccountRole : byte
    {
        /// <summary>
        /// Quản trị viên hệ thống (Admin)
        /// </summary>
        [Display(ResourceType = typeof(Resources.Models.Acc), Name = "Role_Admin")]
        Admin = 1,

        /// <summary>
        /// Nhân sự (HR) - Quản lý nhân sự, hợp đồng, tài khoản...
        /// </summary>
        [Display(ResourceType = typeof(Resources.Models.Acc), Name = "Role_HR")]
        HR = 2,

        /// <summary>
        /// Area Manager - Quản lý khu vực
        /// </summary>
        [Display(ResourceType = typeof(Resources.Models.Acc), Name = "Role_AreaManager")]
        AreaManager = 3,

        /// <summary>
        /// Store Manager - Quản lý cửa hàng
        /// </summary>
        [Display(ResourceType = typeof(Resources.Models.Acc), Name = "Role_StoreManager")]
        StoreManager = 4,

        /// <summary>
        /// Shift Leader - Trưởng ca
        /// </summary>
        [Display(ResourceType = typeof(Resources.Models.Acc), Name = "Role_ShiftLeader")]
        ShiftLeader = 5,

        /// <summary>
        /// Sales Staff - Nhân viên bán hàng
        /// </summary>
        [Display(ResourceType = typeof(Resources.Models.Acc), Name = "Role_SalesStaff")]
        SalesStaff = 6,

        /// <summary>
        /// Part-time Staff - Nhân viên bán thời gian
        /// </summary>
        [Display(ResourceType = typeof(Resources.Models.Acc), Name = "Role_PartTimeStaff")]
        PartTimeStaff = 7,

    }

    public static class AccountRoleExtensions
    {
        public static string GetDisplayName(this AccountRole role)
        {
            return UIHelper.GetDisplayName(role);
        }
    }
}