using System.ComponentModel.DataAnnotations;
using RWPM.Common.Attributes;
using RWPM.Common.Enums;

namespace RWPM.Models.ViewModels.ShiftRegistration
{
    public class ShiftRegistrationCreateVM
    {
        [Display(Name = "Employee", ResourceType = typeof(RWPM.Resources.Shared.ShiftRegistrationResource))]
        [RequiredLocalization]
        public int EmployeeId { get; set; }

        [Display(Name = "Shift", ResourceType = typeof(RWPM.Resources.Shared.ShiftRegistrationResource))]
        [RequiredLocalization]
        public int ShiftId { get; set; }

        public List<int>? ShiftIds { get; set; }

        [Display(Name = "WorkDate", ResourceType = typeof(RWPM.Resources.Shared.ShiftRegistrationResource))]
        [RequiredLocalization]
        [DataType(DataType.Date)]
        public DateTime WorkDate { get; set; }

        [Display(Name = "Note", ResourceType = typeof(RWPM.Resources.Shared.ShiftRegistrationResource))]
        [MaxLengthLocalized(255)]
        public string Note { get; set; } = string.Empty;

        public RWPM.Models.Entities.ShiftRegistration ToEntity()
        {
            return new Models.Entities.ShiftRegistration
            {
                EmployeeId = EmployeeId,
                ShiftId = ShiftId,
                WorkDate = WorkDate,
                Note = Note,
                Status = RegistrationStatus.Pending
            };
        }
    }
}
