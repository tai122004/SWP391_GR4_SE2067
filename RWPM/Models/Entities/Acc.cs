using RWPM.Common.Enums;
using RWPM.Models.Common;
using System.ComponentModel.DataAnnotations;

namespace RWPM.Models.Entities
{
    public class Acc : AuditableEntity, IActivatable
    {
        [Key]
        [MaxLength(30)]
        public string Username { get; set; } = string.Empty;
        [Required]
        [MaxLength(255)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EnumDataType(typeof(AccountRole))]
        public AccountRole Role { get; set; }

        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "date")]
        public DateTime? DateOfBirth { get; set; }

        public Gender? Gender { get; set; }

        [MaxLength(255)]
        public string? AvatarUrl { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime? LastLogin { get; set; }

        /// <summary>Bắt buộc đổi mật khẩu ở lần đăng nhập kế tiếp (tài khoản mới, import hoặc được cấp lại mật khẩu).</summary>
        public bool MustChangePassword { get; set; }

        /// <summary>Tăng mỗi khi mật khẩu/vai trò/trạng thái thay đổi để vô hiệu các phiên đăng nhập cũ.</summary>
        [MaxLength(64)]
        public string? SecurityStamp { get; set; }
        //public int? DeptCatId { get; set; }
        //public DeptCat.DeptCat? DeptCat {  get; set; }
    }
}
