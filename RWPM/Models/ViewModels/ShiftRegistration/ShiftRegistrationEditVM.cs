using System.ComponentModel.DataAnnotations;
using RWPM.Common.Attributes;
using RWPM.Common.Enums;

namespace RWPM.Models.ViewModels.ShiftRegistration
{
    public class ShiftRegistrationEditVM
    {
        [RequiredLocalization]
        public int ShiftRegistrationId { get; set; }

        [Display(Name = "Employee", ResourceType = typeof(RWPM.Resources.Shared.ShiftRegistrationResource))]
        [RequiredLocalization]
        public int EmployeeId { get; set; }

        [Display(Name = "Shift", ResourceType = typeof(RWPM.Resources.Shared.ShiftRegistrationResource))]
        [RequiredLocalization]
        public int ShiftId { get; set; }

        [Display(Name = "WorkDate", ResourceType = typeof(RWPM.Resources.Shared.ShiftRegistrationResource))]
        [RequiredLocalization]
        [DataType(DataType.Date)]
        public DateTime WorkDate { get; set; }

        [Display(Name = "Status", ResourceType = typeof(RWPM.Resources.Shared.ShiftRegistrationResource))]
        public RegistrationStatus Status { get; set; }

        [Display(Name = "Note", ResourceType = typeof(RWPM.Resources.Shared.ShiftRegistrationResource))]
        [MaxLengthLocalized(255)]
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
