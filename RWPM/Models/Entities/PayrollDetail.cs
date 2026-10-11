using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RWPM.Common.Enums;

namespace RWPM.Models.Entities
{
    [Table("PayrollDetail")]
    public class PayrollDetail
    {
        [Key]
        public int PayrollDetailId { get; set; }

        [Required]
        public int PayrollId { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        // Snapshot thông tin nhân viên
        [Required]
        [MaxLength(20)]
        public string EmployeeCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string EmployeeName { get; set; } = string.Empty;

        public int StoreId { get; set; }

        [MaxLength(150)]
        public string StoreName { get; set; } = string.Empty;

        public EmploymentType EmploymentType { get; set; }

        // Snapshot mức lương & giờ làm
        [Column(TypeName = "decimal(18,2)")]
        public decimal HourlyRate { get; set; } = 0; // Đơn giá cố định tại thời điểm tính

        public double TotalStandardHours { get; set; } = 0; // Giờ theo lịch phân ca

        public double TotalWorkHours { get; set; } = 0; // Giờ thực tế tính lương

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalSalary { get; set; } = 0; // TotalWorkHours * HourlyRate

        // Đối chiếu & Cảnh báo
        public int ApprovedLeaveDays { get; set; } = 0; // Số ngày nghỉ phép đã duyệt

        public int MissingAttendanceCount { get; set; } = 0; // Số ca bất thường/thiếu vào ra

        public PayrollDetailStatus Status { get; set; } = PayrollDetailStatus.Valid;

        [MaxLength(500)]
        public string? WarningNotes { get; set; }

        // Navigations
        [ForeignKey("PayrollId")]
        public virtual Payroll Payroll { get; set; } = null!;

        [ForeignKey("EmployeeId")]
        public virtual Employee Employee { get; set; } = null!;

        [ForeignKey("StoreId")]
        public virtual global::Store Store { get; set; } = null!;
    }
}
