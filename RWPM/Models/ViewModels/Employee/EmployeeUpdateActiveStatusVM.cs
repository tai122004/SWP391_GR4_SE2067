using RWPM.Common.Attributes;
using System.ComponentModel.DataAnnotations;

namespace RWPM.Models.ViewModels.Employee
{
    public class EmployeeUpdateActiveStatusVM
    {
        [RequiredLocalization]
        public int? EmployeeId { get; set; }

        [Display(Name = "Active", ResourceType = typeof(Resources.Models.Employee))]
        [RequiredLocalization]
        public bool? IsActive { get; set; }
    }
}
