using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RWPM.Models.Entities
{
    /// <summary>
    /// Thông báo trong ứng dụng. Nội dung lưu dạng mã (Type) + tham số (ArgsJson) để hiển thị đúng ngôn ngữ người nhận.
    /// </summary>
    [Table("Notification")]
    public class Notification
    {
        [Key]
        public long NotificationId { get; set; }

        /// <summary>Tài khoản nhận thông báo.</summary>
        [Required]
        [MaxLength(30)]
        public string Username { get; set; } = string.Empty;

        /// <summary>Mã loại thông báo, ví dụ: RequestApproved, RequestRejected, RequestSubmitted.</summary>
        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = string.Empty;

        /// <summary>Tham số hiển thị (JSON mảng chuỗi).</summary>
        [MaxLength(1000)]
        public string? ArgsJson { get; set; }

        /// <summary>Đường dẫn khi bấm vào thông báo.</summary>
        [MaxLength(300)]
        public string? Url { get; set; }

        public bool IsRead { get; set; }

        [Precision(0)]
        public DateTime CreatedDate { get; set; }

        [Precision(0)]
        public DateTime? ReadAt { get; set; }

        [ForeignKey(nameof(Username))]
        public virtual Acc Account { get; set; } = null!;
    }
}
