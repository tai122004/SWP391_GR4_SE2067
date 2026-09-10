using RWPM.Resources.Shared;
using System.ComponentModel.DataAnnotations;

namespace RWPM.Common.Attributes
{
    public class MaxLengthLocalizedAttribute : MaxLengthAttribute
    {
        public MaxLengthLocalizedAttribute(int length) : base(length)
        {
            ErrorMessageResourceName = "MaxLength"; // key in your .resx file
            ErrorMessageResourceType = typeof(SharedResource);
        }
    }
}
