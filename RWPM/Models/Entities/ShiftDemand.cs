using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RWPM.Models.Common;

namespace RWPM.Models.Entities
{
    /// <summary>
    /// Nhu cầu nhân sự (số người cần) cho một ca tại một chi nhánh.
    /// Hai loại: mẫu theo thứ trong tuần (DayOfWeek có giá trị, Date = null)
    /// hoặc ghi đè cho một ngày cụ thể (Date có giá trị, DayOfWeek = null) — ghi đè được ưu tiên.
    /// </summary>
    [Table("ShiftDemand")]
    public class ShiftDemand : AuditableEntity
    {
        [Key]
        public int ShiftDemandId { get; set; }

        public int StoreId { get; set; }

        public int ShiftId { get; set; }

        /// <summary>Thứ trong tuần cho mẫu lặp lại (0 = Chủ nhật ... 6 = Thứ bảy).</summary>
        public DayOfWeek? DayOfWeek { get; set; }

        /// <summary>Ngày cụ thể cho ghi đè (lễ, khuyến mãi...).</summary>
        [Column(TypeName = "date")]
        public DateTime? Date { get; set; }

        /// <summary>Số người cần (0 = ca này không cần người vào ngày đó).</summary>
        public int RequiredCount { get; set; }

        [MaxLength(255)]
        public string? Note { get; set; }

        [ForeignKey(nameof(StoreId))]
        public virtual Store Store { get; set; } = null!;

        [ForeignKey(nameof(ShiftId))]
        public virtual Shift Shift { get; set; } = null!;
    }
}
