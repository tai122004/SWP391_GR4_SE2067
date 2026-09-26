using RWPM.Models.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RWPM.Models.Entities
{
    public class Employee : AuditableEntity, IActivatable
    {
        [Key]
        public int EmployeeId { get; set; }

        [Required]
        [MaxLength(20)]
        public string EmployeeCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)] 
        public string Username { get; set; } = string.Empty;

        public int StoreId { get; set; }

        [Column(TypeName = "date")] 
        public DateTime JoinDate { get; set; }

        public RWPM.Common.Enums.EmploymentType EmploymentType { get; set; } = RWPM.Common.Enums.EmploymentType.FullTime;

        public RWPM.Common.Enums.EmploymentStatus Status { get; set; } = RWPM.Common.Enums.EmploymentStatus.Official;

        [Column(TypeName = "decimal(18,2)")]
        public decimal? HourlyRate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? BaseSalary { get; set; }

        public int AnnualLeaveBalance { get; set; } = 12;

        [MaxLength(20)]
        public string? CitizenId { get; set; }

        [Column(TypeName = "date")]
        public DateTime? ResignDate { get; set; }

        [MaxLength(255)]
        public string? ResignReason { get; set; }

        public bool IsActive { get; set; } = true;

        public Acc Account { get; set; } = null!;
        public Store Store { get; set; } = null!;
    }
}