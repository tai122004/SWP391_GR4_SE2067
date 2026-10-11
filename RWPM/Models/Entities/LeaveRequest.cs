using RWPM.Common.Enums;
using RWPM.Models.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RWPM.Models.Entities
{
    public class LeaveRequest : AuditableEntity
    {
        [Key]
        public int LeaveRequestId { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [Required]
        public int StoreId { get; set; }

        [Required]
        [Column(TypeName = "date")]
        public DateTime StartDate { get; set; }

        [Required]
        [Column(TypeName = "date")]
        public DateTime EndDate { get; set; }

        [Required]
        public LeaveType LeaveType { get; set; } = LeaveType.Paid;

        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        public RegistrationStatus Status { get; set; } = RegistrationStatus.Pending;

        [MaxLength(255)]
        public string? RejectionReason { get; set; }

        [MaxLength(50)]
        public string? ApprovedBy { get; set; }

        public DateTime? ApprovedAt { get; set; }

        // Navigation properties
        public Employee Employee { get; set; } = null!;
        public Store Store { get; set; } = null!;
    }
}
