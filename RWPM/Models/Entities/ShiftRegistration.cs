using RWPM.Common.Enums;
using RWPM.Models.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RWPM.Models.Entities
{
    public class ShiftRegistration : AuditableEntity
    {
        [Key]
        public int ShiftRegistrationId { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [Required]
        public int ShiftId { get; set; }

        [Required]
        [Column(TypeName = "date")]
        public DateTime WorkDate { get; set; }

        public RegistrationStatus Status { get; set; } = RegistrationStatus.Pending;

        [MaxLength(255)]
        public string Note { get; set; } = string.Empty;

        // Navigation properties
        public Employee Employee { get; set; } = null!;
        public Shift Shift { get; set; } = null!;
    }
}
