using RWPM.Common.Enums;
using RWPM.Models.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RWPM.Models.Entities
{
    public class ShiftSwapRequest : AuditableEntity
    {
        [Key]
        public int ShiftSwapRequestId { get; set; }

        [Required]
        public int RequesterEmployeeId { get; set; }

        [Required]
        public int SourceRegistrationId { get; set; }

        public int? TargetEmployeeId { get; set; }

        public int? TargetRegistrationId { get; set; }

        public int? TargetShiftId { get; set; }

        [Column(TypeName = "date")]
        public DateTime? TargetWorkDate { get; set; }

        [Required]
        public int StoreId { get; set; }

        public RegistrationStatus Status { get; set; } = RegistrationStatus.Pending;

        [MaxLength(255)]
        public string Reason { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? RejectionReason { get; set; }

        [MaxLength(50)]
        public string? ApprovedBy { get; set; }

        public DateTime? ApprovedAt { get; set; }

        // Navigation properties
        public Employee RequesterEmployee { get; set; } = null!;
        public Employee? TargetEmployee { get; set; }
        public ShiftRegistration SourceRegistration { get; set; } = null!;
        public ShiftRegistration? TargetRegistration { get; set; }
        public Shift? TargetShift { get; set; }
        public Store Store { get; set; } = null!;
    }
}
