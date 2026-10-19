using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using RWPM.Resources.Shared;
using System.ComponentModel.DataAnnotations;

namespace RWPM.Common.Localization
{
    /// <summary>
    /// Đa ngôn ngữ cho mọi thông báo validate mặc định của ASP.NET Core (tiếng Anh cứng):
    /// - [Required] ngầm định cho thuộc tính string không nullable ("The Địa chỉ field is required.")
    /// - [Required]/[Range]/[EmailAddress]... khai báo không kèm ErrorMessageResource
    /// Chạy SAU DataAnnotationsMetadataProvider (đăng ký bằng PostConfigure) nên nhìn thấy cả attribute ngầm định.
    /// </summary>
    public class LocalizedValidationMetadataProvider : IValidationMetadataProvider
    {
        public void CreateValidationMetadata(ValidationMetadataProviderContext context)
        {
            foreach (var item in context.ValidationMetadata.ValidatorMetadata)
            {
                if (item is not ValidationAttribute attr) continue;
                if (!string.IsNullOrEmpty(attr.ErrorMessage) || !string.IsNullOrEmpty(attr.ErrorMessageResourceName)) continue;

                var key = attr switch
                {
                    RequiredAttribute => nameof(SharedResource.Required),
                    RangeAttribute => nameof(SharedResource.Range),
                    MaxLengthAttribute => nameof(SharedResource.MaxLength),
                    MinLengthAttribute => nameof(SharedResource.MinLength),
                    StringLengthAttribute s => s.MinimumLength > 0 ? nameof(SharedResource.StringLength) : nameof(SharedResource.StringLengthMax),
                    EmailAddressAttribute => nameof(SharedResource.EmailAddress),
                    PhoneAttribute => nameof(SharedResource.PhoneInvalid),
                    CompareAttribute => nameof(SharedResource.Compare),
                    RegularExpressionAttribute => nameof(SharedResource.RegularExpression),
                    _ => null
                };
                if (key == null) continue;

                attr.ErrorMessageResourceType = typeof(SharedResource);
                attr.ErrorMessageResourceName = key;
            }
        }
    }

    public static class ValidationLocalizationExtensions
    {
        /// <summary>Thông báo lỗi model binding ("The field X must be a number."...) lấy từ SharedResource.</summary>
        public static void UseLocalizedBindingMessages(this MvcOptions options)
        {
            var p = options.ModelBindingMessageProvider;
            p.SetValueMustBeANumberAccessor(name => string.Format(SharedResource.Binding_ValueMustBeANumber, name));
            p.SetNonPropertyValueMustBeANumberAccessor(() => SharedResource.Binding_NonPropertyValueMustBeANumber);
            p.SetAttemptedValueIsInvalidAccessor((value, name) => string.Format(SharedResource.Binding_AttemptedValueIsInvalid, value, name));
            p.SetNonPropertyAttemptedValueIsInvalidAccessor(value => string.Format(SharedResource.Binding_NonPropertyAttemptedValueIsInvalid, value));
            p.SetUnknownValueIsInvalidAccessor(name => string.Format(SharedResource.Binding_UnknownValueIsInvalid, name));
            p.SetNonPropertyUnknownValueIsInvalidAccessor(() => SharedResource.Binding_NonPropertyUnknownValueIsInvalid);
            p.SetValueIsInvalidAccessor(value => string.Format(SharedResource.Binding_ValueIsInvalid, value));
            p.SetValueMustNotBeNullAccessor(_ => SharedResource.Binding_ValueMustNotBeNull);
            p.SetMissingBindRequiredValueAccessor(name => string.Format(SharedResource.Binding_MissingBindRequiredValue, name));
            p.SetMissingKeyOrValueAccessor(() => SharedResource.Binding_MissingKeyOrValue);
            p.SetMissingRequestBodyRequiredValueAccessor(() => SharedResource.Binding_MissingRequestBodyRequiredValue);
        }
    }
}
