using RWPM.Resources.Shared;
using System.ComponentModel.DataAnnotations;

namespace RWPM.Common.Attributes
{
    public class RegexLocalizedAttribute : RegularExpressionAttribute
    {
        public RegexLocalizedAttribute(string pattern, string resourceKey)
            : base(pattern)
        {
            ErrorMessageResourceName = resourceKey;
            ErrorMessageResourceType = typeof(SharedResource); // or FactoryCategory if model-specific
        }
    }
}
