using System.ComponentModel.DataAnnotations;

namespace RWPM.Common.Enums
{
    public enum EmploymentStatus : byte
    {
        [Display(ResourceType = typeof(Resources.Models.Employee), Name = "EmploymentStatus_Official")]
        Official = 1,

        [Display(ResourceType = typeof(Resources.Models.Employee), Name = "EmploymentStatus_Probation")]
        Probation = 2,

        [Display(ResourceType = typeof(Resources.Models.Employee), Name = "EmploymentStatus_OnLeave")]
        OnLeave = 3,

        [Display(ResourceType = typeof(Resources.Models.Employee), Name = "EmploymentStatus_Resigned")]
        Resigned = 4
    }
}
