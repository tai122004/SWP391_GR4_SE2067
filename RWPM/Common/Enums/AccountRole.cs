using System.ComponentModel.DataAnnotations;

namespace RWPM.Common.Enums
{
    /// <summary>
    /// Vai trò tài khoản. Tên enum chính là tên role trong claim (xem AppRoles).
    /// Giá trị 3, 5 trước đây dành cho AreaManager / ShiftLeader, đã bỏ — không dùng lại để tránh lẫn dữ liệu cũ.
    /// </summary>
    public enum AccountRole : byte
    {
        /// <summary>Quản trị viên hệ thống</summary>
        [Display(ResourceType = typeof(Resources.Models.Acc), Name = "Role_Admin")]
        Admin = 1,

        /// <summary>Nhân sự (HR) - quản lý nhân sự, hợp đồng, tài khoản</summary>
        [Display(ResourceType = typeof(Resources.Models.Acc), Name = "Role_HR")]
        HR = 2,

        /// <summary>Quản lý cửa hàng</summary>
        [Display(ResourceType = typeof(Resources.Models.Acc), Name = "Role_StoreManager")]
        StoreManager = 4,

        /// <summary>Nhân viên</summary>
        [Display(ResourceType = typeof(Resources.Models.Acc), Name = "Role_Employee")]
        Employee = 6,
    }

    public static class AccountRoleExtensions
    {
        public static string GetDisplayName(this AccountRole role) => UIHelper.GetDisplayName(role);
    }
}
