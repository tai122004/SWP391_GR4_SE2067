using RWPM.Common.Attributes;
using System.ComponentModel.DataAnnotations;

namespace RWPM.Models.ViewModels.Acc
{
    public class AccUpdateActiveStatusVM
    {
        [Display(Name = "Username", ResourceType = typeof(Resources.Models.Acc))]
        [RequiredLocalization]
        public string? Username { get; set; }
        [Display(Name = "Active", ResourceType = typeof(Resources.Models.Employee))]
        [RequiredLocalization]
        public bool? IsActive { get; set; }
    }
}
