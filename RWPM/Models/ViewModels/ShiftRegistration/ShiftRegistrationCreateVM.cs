using System.ComponentModel.DataAnnotations;
using RWPM.Common.Enums;
using RWPM.Models.Entities;

namespace RWPM.Models.ViewModels.ShiftRegistration
{
    public class ShiftRegistrationCreateVM
    {
        [Required(ErrorMessage = "Employee is required.")]
        public int EmployeeId { get; set; }

        [Required(ErrorMessage = "Shift is required.")]
        public int ShiftId { get; set; }

        [Required(ErrorMessage = "Date is required.")]
        [DataType(DataType.Date)]
        public DateTime WorkDate { get; set; }

        [MaxLength(255)]
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
