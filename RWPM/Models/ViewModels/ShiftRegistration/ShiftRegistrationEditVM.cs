using System.ComponentModel.DataAnnotations;
using RWPM.Common.Enums;
using RWPM.Models.Entities;

namespace RWPM.Models.ViewModels.ShiftRegistration
{
    public class ShiftRegistrationEditVM
    {
        [Required]
        public int ShiftRegistrationId { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [Required]
        public int ShiftId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime WorkDate { get; set; }

        public RegistrationStatus Status { get; set; }

        [MaxLength(255)]
        public string Note { get; set; } = string.Empty;

        public ShiftRegistrationEditVM() { }

        public ShiftRegistrationEditVM(Models.Entities.ShiftRegistration entity)
        {
            ShiftRegistrationId = entity.ShiftRegistrationId;
            EmployeeId = entity.EmployeeId;
            ShiftId = entity.ShiftId;
            WorkDate = entity.WorkDate;
            Status = entity.Status;
            Note = entity.Note;
        }

        public void ApplyToEntity(Models.Entities.ShiftRegistration entity)
        {
            entity.EmployeeId = EmployeeId;
            entity.ShiftId = ShiftId;
            entity.WorkDate = WorkDate;
            entity.Status = Status;
            entity.Note = Note;
        }
    }
}
