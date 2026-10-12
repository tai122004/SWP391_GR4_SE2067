using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RWPM.Common.Enums;
using RWPM.Models.Common;

namespace RWPM.Models.Entities
{
    [Table("Payroll")]
    public class Payroll : AuditableEntity
    {
        [Key]
        public int PayrollId { get; set; }

        [Required]
        public int PeriodMonth { get; set; } // 1 - 12

        [Required]
        public int PeriodYear { get; set; }  // e.g. 2026

        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty; // "Bảng lương Tháng 10/2026"

        [Column(TypeName = "date")]
        public DateTime StartDate { get; set; }

        [Column(TypeName = "date")]
        public DateTime EndDate { get; set; }

        public PayrollStatus Status { get; set; } = PayrollStatus.Draft; // 1: Draft, 2: Locked

        public int TotalEmployees { get; set; } = 0;

        public double TotalWorkHours { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalGrossSalary { get; set; } = 0;

        [MaxLength(500)]
        public string? Note { get; set; }

        [MaxLength(30)]
        public string? LockedBy { get; set; }

        public DateTime? LockedDate { get; set; }

        // Navigation
        public virtual ICollection<PayrollDetail> Details { get; set; } = new List<PayrollDetail>();
        public virtual ICollection<PayrollAuditLog> AuditLogs { get; set; } = new List<PayrollAuditLog>();
    }
}
