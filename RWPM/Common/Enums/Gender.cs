using System.ComponentModel.DataAnnotations;

namespace RWPM.Common.Enums
{
    public enum Gender : byte
    {
        [Display(ResourceType = typeof(Resources.Models.Acc), Name = "Gender_Male")]
        Male = 1,

        [Display(ResourceType = typeof(Resources.Models.Acc), Name = "Gender_Female")]
        Female = 2,

        [Display(ResourceType = typeof(Resources.Models.Acc), Name = "Gender_Other")]
        Other = 3
    }
}
