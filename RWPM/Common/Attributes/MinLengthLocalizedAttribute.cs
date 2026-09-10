using RWPM.Resources.Shared;
using System.ComponentModel.DataAnnotations;

namespace RWPM.Common.Attributes
{
    public class MinLengthLocalizedAttribute : MinLengthAttribute
    {
        public MinLengthLocalizedAttribute(int length) : base(length)
        {
            ErrorMessageResourceName = "MinLength"; // key in your .resx file
            ErrorMessageResourceType = typeof(SharedResource);
        }
    }
}
