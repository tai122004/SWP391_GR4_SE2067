using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RWPM.Models.Common;

namespace RWPM.Models.Entities
{
    /// <summary>
    /// Đánh giá hiệu suất theo tháng của Quản lý cửa hàng / HR cho một nhân viên.
    /// Các chỉ số chuyên cần (tỉ lệ đi làm, đi muộn, về sớm, vắng, giờ công) được tính trực tiếp từ dữ liệu chấm công;
    /// bản ghi này lưu điểm và nhận xét, kèm ảnh chụp chỉ số tại thời điểm đánh giá để đối chiếu về sau.
    /// </summary>
    [Table("PerformanceReview")]
    public class PerformanceReview : AuditableEntity
    {
        [Key]
        public int PerformanceReviewId { get; set; }

        public int EmployeeId { get; set; }

        /// <summary>Chi nhánh của nhân viên tại thời điểm đánh giá (phục vụ phân quyền Quản lý cửa hàng).</summary>
        public int? StoreId { get; set; }

        public int Year { get; set; }

        public int Month { get; set; }

        /// <summary>Điểm tổng 1–5.</summary>
        public byte Score { get; set; }

        [MaxLength(1000)]
        public string? Comment { get; set; }

        [Required]
        [MaxLength(30)]
        public string ReviewedBy { get; set; } = string.Empty;

        [Precision(0)]
        public DateTime ReviewedAt { get; set; }

        /// <summary>Ảnh chụp chỉ số chuyên cần lúc đánh giá (JSON).</summary>
        public string? MetricsSnapshotJson { get; set; }

        [ForeignKey(nameof(EmployeeId))]
        public virtual Employee Employee { get; set; } = null!;

        [ForeignKey(nameof(StoreId))]
        public virtual Store? Store { get; set; }
    }
}
