using System.ComponentModel.DataAnnotations;

namespace RWPM.Common.Attributes
{
    public class RequiredByteSelectionAttribute : RangeAttribute
    {
        public RequiredByteSelectionAttribute() : base(1, byte.MaxValue)
        {
            ErrorMessageResourceName = "RequiredIntegerSelection";
            ErrorMessageResourceType = typeof(Resources.Shared.SharedResource);
        }
    }
}
