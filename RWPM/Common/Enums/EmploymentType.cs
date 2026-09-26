using System.ComponentModel.DataAnnotations;

namespace RWPM.Common.Enums
{
    public enum EmploymentType : byte
    {
        [Display(ResourceType = typeof(Resources.Models.Employee), Name = "EmploymentType_FullTime")]
        FullTime = 1,

        [Display(ResourceType = typeof(Resources.Models.Employee), Name = "EmploymentType_PartTime")]
        PartTime = 2,

        [Display(ResourceType = typeof(Resources.Models.Employee), Name = "EmploymentType_Intern")]
        Intern = 3
    }
}
