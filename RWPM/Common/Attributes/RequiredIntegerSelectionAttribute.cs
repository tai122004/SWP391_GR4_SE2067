using System.ComponentModel.DataAnnotations;

namespace RWPM.Common.Attributes
{
    public class RequiredIntegerSelectionAttribute : RangeAttribute
    {
        public RequiredIntegerSelectionAttribute() : base(1, int.MaxValue)
        {
            ErrorMessageResourceName = "RequiredIntegerSelection";
            ErrorMessageResourceType = typeof(Resources.Shared.SharedResource);
        }
    }
}
