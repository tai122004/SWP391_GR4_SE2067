using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RWPM.Common.Enums;

namespace RWPM.Models.Entities
{
    [Table("PayrollAuditLog")]
    public class PayrollAuditLog
    {
        [Key]
        public int PayrollAuditLogId { get; set; }

        [Required]
        public int PayrollId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Action { get; set; } = string.Empty; // Calculate, Recalculate, Lock, Unlock, Delete

        public PayrollStatus? FromStatus { get; set; }

        public PayrollStatus? ToStatus { get; set; }

        [Required]
        [MaxLength(50)]
        public string OperatorUsername { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [MaxLength(500)]
        public string? Reason { get; set; }

        [MaxLength(1000)]
        public string? Details { get; set; }

        // Navigation
        [ForeignKey("PayrollId")]
        public virtual Payroll Payroll { get; set; } = null!;
    }
}
