using RWPM.Resources.Shared;
using System.ComponentModel.DataAnnotations;

namespace RWPM.Common.Attributes
{
    public class RequiredLocalizationAttribute : RequiredAttribute
    {
        public RequiredLocalizationAttribute()
        {
            ErrorMessageResourceName = "Required";
            ErrorMessageResourceType = typeof(SharedResource);
        }
    }
}
