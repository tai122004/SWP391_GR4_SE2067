using System.ComponentModel.DataAnnotations;

namespace RWPM.Models.ViewModels.Employee
{
    public class EmployeeUpdateActiveStatusVM
    {
        [Required]
        public int? EmployeeId { get; set; }

        [Required]
        public bool? IsActive { get; set; }
    }
}
