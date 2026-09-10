using System.ComponentModel.DataAnnotations;

namespace RWPM.Common.Attributes
{
    public class EnumDataTypeLocalizationAttribute : ValidationAttribute
    {
        private readonly Type _enumType;

        public EnumDataTypeLocalizationAttribute(Type enumType)
        {
            if (!enumType.IsEnum)
                throw new ArgumentException("Type must be an enum", nameof(enumType));

            _enumType = enumType;

            // Set ErrorMessageResourceName and ResourceType here for reuse
            ErrorMessageResourceName = "EnumInvalid";
            ErrorMessageResourceType = typeof(Resources.Shared.SharedResource);
        }

        public override bool IsValid(object? value)
        {
            if (value == null) return true;

            if (!Enum.IsDefined(_enumType, value))
                return false;

            return true;
        }
    }
}
