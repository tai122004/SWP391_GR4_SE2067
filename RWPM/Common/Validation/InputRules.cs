using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;
using RWPM.Common.Enums;

namespace RWPM.Common.Validation;

// Opt-in rules for personnel, scheduling and payroll. Never registered as a global filter.
public static class InputRules
{
    public const int MinimumEmployeeAge = 18;
    public const string EmailPattern = @"^[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+)*@[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?)+$";
    public static string Text(string vi, string en) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi" ? vi : en;
    public static bool Email(string? value) => value != null && value.Length <= 100
        && new EmailAddressAttribute().IsValid(value)
        && value.IndexOf('@') <= 64
        && Regex.IsMatch(value, EmailPattern, RegexOptions.CultureInvariant);
    public static bool Name(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 100
        && Regex.IsMatch(value, @"^\p{L}[\p{L}\p{M}]*(?:[ '\-’]\p{L}[\p{L}\p{M}]*)*$", RegexOptions.CultureInvariant);
    public static bool Password(string? value) => value != null && value.Length <= 100
        && Regex.IsMatch(value, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9]).{8,100}$");
    public static void Require(bool condition, string field, string vi, string en)
    {
        if (!condition) throw new InputValidationException(field, Text(vi, en));
    }
    public static void ValidatePassword(string? value) => Require(Password(value), "NewPassword",
        "Mật khẩu phải có 8–100 ký tự, gồm chữ hoa, chữ thường và số.",
        "Password must contain 8–100 characters including uppercase, lowercase and a digit.");
    public static void ValidateAccount(string username, string fullName, string email, string? phone,
        DateTime? birthDate, Gender? gender, AccountRole role)
    {
        Require(username.Length is >= 5 and <= 30 && Regex.IsMatch(username, RegexHelper.UsernameRegex), "Username",
            "Tài khoản phải có 5–30 ký tự, bắt đầu bằng chữ, chỉ chứa chữ, số, _ hoặc -.",
            "Username must be 5–30 characters, start with a letter and contain only letters, digits, _ or -.");
        Require(Name(fullName), "FullName", "Họ tên không hợp lệ hoặc vượt quá 100 ký tự.", "Invalid full name or name exceeds 100 characters.");
        Require(Email(email), "Email", "Email không hợp lệ; tên miền phải có hậu tố, ví dụ example.com.", "Invalid email; use a domain with a suffix, such as example.com.");
        Require(string.IsNullOrEmpty(phone) || Regex.IsMatch(phone, @"^0[0-9]{9}$"), "PhoneNumber",
            "Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.", "Phone must contain 10 digits starting with 0.");
        ValidateBirthDate(birthDate);
        Require(!gender.HasValue || Enum.IsDefined(gender.Value), "Gender", "Giới tính không hợp lệ.", "Invalid gender.");
        Require(Enum.IsDefined(role) && role != AccountRole.Admin, "Role", "Vai trò không được phép tạo hoặc cập nhật.", "This role cannot be created or assigned.");
    }
    public static void ValidateBirthDate(DateTime? birthDate) => Require(!birthDate.HasValue ||
        (birthDate.Value.Date > DateTime.MinValue.Date && birthDate.Value.Date <= DateTime.Today.AddYears(-MinimumEmployeeAge)),
        "DateOfBirth", "Người lao động phải đủ 18 tuổi theo chính sách của hệ thống.",
        "Employees must be at least 18 years old under the system policy.");
    public static void ValidateEmployment(string code, DateTime joinDate, DateTime? birthDate,
        DateTime? resignDate, EmploymentType type, EmploymentStatus status, decimal? hourlyRate,
        decimal? baseSalary, int leaveBalance, string? citizenId)
    {
        Require(code.Length is > 0 and <= 20 && Regex.IsMatch(code, RegexHelper.CodeRegex), "EmployeeCode",
            "Mã nhân viên chỉ chứa chữ, số, _ hoặc -, tối đa 20 ký tự.", "Employee code may contain letters, digits, _ or -, up to 20 characters.");
        ValidateBirthDate(birthDate);
        Require(birthDate.HasValue, "DateOfBirth", "Vui lòng nhập ngày sinh để xác minh đủ 18 tuổi.",
            "Enter a birth date to verify the employee is at least 18.");
        Require(joinDate != default && (!birthDate.HasValue || birthDate.Value.Date < joinDate.Date), "JoinDate",
            "Ngày vào làm phải sau ngày sinh.", "Join date must be valid and after birth date.");
        Require(joinDate != default && birthDate.GetValueOrDefault().Date <= joinDate.Date.AddYears(-MinimumEmployeeAge), "JoinDate",
            "Nhân viên phải đủ 18 tuổi vào ngày bắt đầu làm việc.", "Employee must be at least 18 on the join date.");
        Require(!resignDate.HasValue || resignDate.Value.Date >= joinDate.Date, "ResignDate",
            "Ngày nghỉ việc không được trước ngày vào làm.", "Resignation date cannot precede join date.");
        Require(status != EmploymentStatus.Resigned || resignDate.HasValue, "ResignDate",
            "Nhân viên thôi việc phải có ngày nghỉ việc.", "Resigned employees require a resignation date.");
        Require(Enum.IsDefined(type), "EmploymentType", "Hình thức làm việc không hợp lệ.", "Invalid employment type.");
        Require(Enum.IsDefined(status), "Status", "Trạng thái nhân viên không hợp lệ.", "Invalid employment status.");
        Require(!hourlyRate.HasValue || hourlyRate.Value is > 0 and <= 100000000, "HourlyRate",
            "Đơn giá phải lớn hơn 0 và không vượt quá 100.000.000; để trống nếu chưa cấu hình.", "Hourly rate must be positive and at most 100,000,000; leave blank if not configured.");
        Require(!baseSalary.HasValue || baseSalary.Value is >= 0 and <= 1000000000, "BaseSalary",
            "Lương cơ bản phải từ 0 đến 1.000.000.000.", "Base salary must be between 0 and 1,000,000,000.");
        Require(leaveBalance is >= 0 and <= 365, "AnnualLeaveBalance", "Quỹ phép phải từ 0 đến 365.", "Leave balance must be between 0 and 365.");
        ValidateCitizenId(citizenId);
    }
    // CCCD (12 số) hoặc CMND cũ (9 số) — bắt buộc với mọi hồ sơ nhân viên
    public const string CitizenIdPattern = @"^(\d{9}|\d{12})$";
    public static string? NormalizeCitizenId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Regex.Replace(value, @"\s+", "");
    public static void ValidateCitizenId(string? citizenId)
    {
        Require(!string.IsNullOrWhiteSpace(citizenId), "CitizenId",
            "Vui lòng nhập số CCCD/CMND.", "Citizen ID (CCCD) is required.");
        Require(Regex.IsMatch(citizenId!, CitizenIdPattern), "CitizenId",
            "Số CCCD/CMND phải gồm 12 chữ số (CCCD) hoặc 9 chữ số (CMND).", "Citizen ID must contain 12 digits (CCCD) or 9 digits (old ID card).");
    }
    public static string Reason(string? reason, int max = 255)
    {
        var value = reason?.Trim() ?? "";
        Require(value.Length > 0 && value.Length <= max, "Reason",
            $"Lý do phải có từ 1 đến {max} ký tự.", $"Reason must contain 1–{max} characters.");
        return value;
    }
    public static void Decision(RegistrationStatus status) => Require(status is RegistrationStatus.Approved or RegistrationStatus.Rejected,
        "Status", "Chỉ được chọn duyệt hoặc từ chối.", "Only approve or reject is allowed.");
    public static string ImportPhone(string value)
    {
        value = value.Trim();
        Require(Regex.IsMatch(value, @"^[0-9 .()\-]*$"), "PhoneNumber", "Số điện thoại chứa ký tự không hợp lệ.", "Phone contains invalid characters.");
        var digits = Regex.Replace(value, @"[ .()\-]", "");
        if (digits.Length == 9 && !digits.StartsWith('0')) digits = "0" + digits;
        return digits;
    }
    // Thông báo hiển thị cho người dùng từ một exception bất kỳ: chỉ giữ message của các lỗi nghiệp vụ
    // (đã được bản địa hóa khi throw); lỗi kỹ thuật (DB, hệ thống...) thay bằng thông báo chung theo ngôn ngữ hiện tại.
    public static string UserMessage(Exception error) => error switch
    {
        ValidationException or InvalidOperationException or ArgumentException or KeyNotFoundException => error.Message,
        // Giữ lý do cụ thể (vd. ngoài phạm vi chi nhánh) nếu có; chỉ thay message mặc định của .NET bằng câu chung
        UnauthorizedAccessException ua when !string.IsNullOrWhiteSpace(ua.Message) && ua.Message != new UnauthorizedAccessException().Message => ua.Message,
        UnauthorizedAccessException => RWPM.Resources.Shared.Auth.Err_AccessDenied,
        _ => Duplicate(error)?.Message ?? RWPM.Resources.Shared.SharedResource.OperationFailed
    };
    public static InputValidationException? Duplicate(Exception error)
    {
        if (error is not Microsoft.EntityFrameworkCore.DbUpdateException) return null;
        var message = error.InnerException?.Message ?? "";
        if (message.Contains("IX_Acc_Email")) return new("Email", Text("Email đã được sử dụng.", "Email is already in use."));
        if (message.Contains("IX_Acc_PhoneNumber")) return new("PhoneNumber", Text("Số điện thoại đã được sử dụng.", "Phone is already in use."));
        if (message.Contains("IX_Employee_EmployeeCode")) return new("EmployeeCode", Text("Mã nhân viên đã tồn tại.", "Employee code already exists."));
        if (message.Contains("IX_Employee_Username") || message.Contains("PK_Acc")) return new("Username", Text("Tài khoản đã tồn tại hoặc đã có hồ sơ nhân viên.", "Account already exists or has an employee profile."));
        if (message.Contains("IX_Store_StoreCode")) return new("StoreCode", Text("Mã chi nhánh đã tồn tại.", "Store code already exists."));
        return null;
    }
}

public sealed class InputValidationException : ValidationException
{
    public string Field { get; }
    public InputValidationException(string field, string message) : base(message) => Field = field;
}
