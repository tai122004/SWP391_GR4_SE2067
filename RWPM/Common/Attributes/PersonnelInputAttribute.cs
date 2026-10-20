using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using RWPM.Common.Validation;

namespace RWPM.Common.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public sealed class PersonnelInputAttribute : ValidationAttribute, IClientModelValidator
{
    private readonly string _kind;
    public PersonnelInputAttribute(string kind) => _kind = kind;
    public override bool IsValid(object? value) => value == null || string.IsNullOrEmpty(value.ToString()) || _kind switch
    {
        "Email" => InputRules.Email(value.ToString()),
        "Name" => InputRules.Name(value.ToString()),
        "Password" => InputRules.Password(value.ToString()),
        _ => false
    };
    public override string FormatErrorMessage(string name) => _kind switch
    {
        "Email" => InputRules.Text("Email không hợp lệ; tên miền phải có hậu tố, tối đa 100 ký tự.", "Invalid email; use a domain with a suffix, up to 100 characters."),
        "Name" => InputRules.Text("Họ tên chỉ gồm chữ, khoảng trắng, dấu nháy hoặc gạch nối, tối đa 100 ký tự.", "Name may contain letters, spaces, apostrophes or hyphens, up to 100 characters."),
        _ => InputRules.Text("Mật khẩu phải có 8–100 ký tự, gồm chữ hoa, chữ thường và số.", "Password must contain 8–100 characters including uppercase, lowercase and a digit.")
    };
    public void AddValidation(ClientModelValidationContext context)
    {
        if (_kind == "Name") return; // Unicode name validation is authoritative on the server.
        context.Attributes.TryAdd("data-val", "true");
        context.Attributes.TryAdd("data-val-regex", FormatErrorMessage(context.ModelMetadata.GetDisplayName()));
        context.Attributes.TryAdd("data-val-regex-pattern", _kind == "Password"
            ? @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,100}$"
            : InputRules.EmailPattern);
    }
}
