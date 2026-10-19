using RWPM.Resources.Shared;
using System.ComponentModel.DataAnnotations;

namespace RWPM.Common.Attributes
{
    /// <summary>Range có thông báo lỗi đa ngôn ngữ (key "Range" trong SharedResource).</summary>
    public class RangeLocalizedAttribute : RangeAttribute
    {
        public RangeLocalizedAttribute(int minimum, int maximum) : base(minimum, maximum)
        {
            ErrorMessageResourceName = "Range";
            ErrorMessageResourceType = typeof(SharedResource);
        }

        public RangeLocalizedAttribute(double minimum, double maximum) : base(minimum, maximum)
        {
            ErrorMessageResourceName = "Range";
            ErrorMessageResourceType = typeof(SharedResource);
        }
    }
}
