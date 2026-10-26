using System.ComponentModel.DataAnnotations;

namespace RWPM.Common.Enums
{
    public enum RegistrationStatus
    {
        [Display(ResourceType = typeof(Resources.Shared.SharedResource), Name = "Req_Pending")]
        Pending = 0,

        [Display(ResourceType = typeof(Resources.Shared.SharedResource), Name = "Req_Approved")]
        Approved = 1,

        [Display(ResourceType = typeof(Resources.Shared.SharedResource), Name = "Req_Rejected")]
        Rejected = 2
    }
}
