using RWPM.Common.Validation;
using System;
using System.ComponentModel.DataAnnotations;
using RWPM.Common.Attributes;
using RWPM.Common.Enums;

namespace RWPM.Models.ViewModels.Employee
{
    public class EmployeeEditVM : IValidatableObject
    {
        private string _employeeCode = string.Empty;
        private string _username = string.Empty;
        private string _fullName = string.Empty;
        private string? _citizenId = null;
        private string? _phoneNumber = null;
        private string? _resignReason = null;

        public int EmployeeId { get; set; }

        [Display(Name = "EmployeeID", ResourceType = typeof(Resources.Models.Employee))]
        [RequiredLocalization]
        [MaxLengthLocalized(20)]
        public string EmployeeCode { get => _employeeCode; set => _employeeCode = value?.Trim() ?? string.Empty; }

        [Display(Name = "EmployeeAcc", ResourceType = typeof(Resources.Models.Employee))]
        [RequiredLocalization]
        [MaxLengthLocalized(30)]
        public string Username { get => _username; set => _username = value?.Trim() ?? string.Empty; }

        public string FullName { get => _fullName; set => _fullName = value?.Trim() ?? string.Empty; }

        [Display(Name = "Store", ResourceType = typeof(Resources.Models.Employee))]
        [RequiredLocalization]
        [Range(1, int.MaxValue,
            ErrorMessageResourceType = typeof(Resources.Models.Employee),
            ErrorMessageResourceName = "StoreId_RangeError")]
        public int StoreId { get; set; }

        [Display(Name = "StartDate", ResourceType = typeof(Resources.Models.Employee))]
        [RequiredLocalization]
        [DataType(DataType.Date)]
        public DateTime JoinDate { get; set; }

        [Display(Name = "EmploymentType", ResourceType = typeof(Resources.Models.Employee))]
        [EnumDataType(typeof(EmploymentType))]
        public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;

        [Display(Name = "EmploymentStatus", ResourceType = typeof(Resources.Models.Employee))]
        [EnumDataType(typeof(EmploymentStatus))]
        public EmploymentStatus Status { get; set; } = EmploymentStatus.Official;

        [Display(Name = "HourlyRate", ResourceType = typeof(Resources.Models.Employee))]
        [RangeLocalized(0.01, 100000000)]
        public decimal? HourlyRate { get; set; }

        [Display(Name = "BaseSalary", ResourceType = typeof(Resources.Models.Employee))]
        [RangeLocalized(0, 1000000000)]
        public decimal? BaseSalary { get; set; }

        [Display(Name = "AnnualLeaveBalance", ResourceType = typeof(Resources.Models.Employee))]
        [RangeLocalized(0, 365)]
        public int AnnualLeaveBalance { get; set; } = 12;

        [Display(Name = "CitizenId", ResourceType = typeof(Resources.Models.Employee))]
        [RequiredLocalization]
        [MaxLengthLocalized(12)]
        public string? CitizenId { get => _citizenId; set => _citizenId = InputRules.NormalizeCitizenId(value); }

        // Thông tin cá nhân liên kết với Account
        [Display(Name = "PhoneNumber", ResourceType = typeof(Resources.Models.Acc))]
        [MaxLengthLocalized(10)]
        [RegexLocalized(RWPM.Common.Helper.PhoneHelper.VnPhonePattern, "RegularExpression_VnPhone")]
        public string? PhoneNumber { get => _phoneNumber; set => _phoneNumber = value?.Trim(); }

        [Display(Name = "DateOfBirth", ResourceType = typeof(Resources.Models.Acc))]
        [RequiredLocalization]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [Display(Name = "Gender", ResourceType = typeof(Resources.Models.Acc))]
        [EnumDataType(typeof(Gender))]
        public Gender? Gender { get; set; }

        [Display(Name = "ResignDate", ResourceType = typeof(Resources.Models.Employee))]
        [DataType(DataType.Date)]
        public DateTime? ResignDate { get; set; }

        [Display(Name = "ResignReason", ResourceType = typeof(Resources.Models.Employee))]
        [MaxLengthLocalized(255)]
        public string? ResignReason { get => _resignReason; set => _resignReason = value?.Trim(); }

        [Display(Name = "Status", ResourceType = typeof(Resources.Models.Employee))]
        public bool IsActive { get; set; } = true;

        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            InputValidationException? error = null;
            try
            {
                InputRules.ValidateEmployment(EmployeeCode, JoinDate, DateOfBirth, ResignDate, EmploymentType, Status,
                    HourlyRate, BaseSalary, AnnualLeaveBalance, CitizenId);
            }
            catch (InputValidationException ex) { error = ex; }
            if (error != null) yield return new ValidationResult(error.Message, new[] { error.Field });
        }

        public EmployeeEditVM() { }

        public EmployeeEditVM(RWPM.Models.Entities.Employee entity)
        {
            EmployeeId = entity.EmployeeId;
            EmployeeCode = entity.EmployeeCode;
            Username = entity.Username;
            FullName = entity.Account?.FullName ?? string.Empty;
            StoreId = entity.StoreId ?? 0;
            JoinDate = entity.JoinDate;
            EmploymentType = entity.EmploymentType;
            Status = entity.Status;
            HourlyRate = entity.HourlyRate;
            BaseSalary = entity.BaseSalary;
            AnnualLeaveBalance = entity.AnnualLeaveBalance;
            CitizenId = entity.CitizenId;
            PhoneNumber = entity.Account?.PhoneNumber;
            DateOfBirth = entity.Account?.DateOfBirth;
            Gender = entity.Account?.Gender;
            ResignDate = entity.ResignDate;
            ResignReason = entity.ResignReason;
            IsActive = entity.IsActive;
        }

        public void ApplyToEntity(RWPM.Models.Entities.Employee entity)
        {
            entity.EmployeeCode = EmployeeCode.Trim();
            entity.Username = Username.Trim();
            entity.StoreId = StoreId;
            entity.Store = null!;
            entity.JoinDate = JoinDate.Date;
            entity.EmploymentType = EmploymentType;
            entity.Status = Status;
            entity.HourlyRate = HourlyRate;
            entity.BaseSalary = BaseSalary;
            entity.AnnualLeaveBalance = AnnualLeaveBalance;
            entity.CitizenId = CitizenId?.Trim();
            entity.ResignDate = ResignDate;
            entity.ResignReason = ResignReason?.Trim();
            entity.IsActive = IsActive;

            if (entity.Account != null)
            {
                entity.Account.PhoneNumber = PhoneNumber?.Trim();
                entity.Account.DateOfBirth = DateOfBirth;
                entity.Account.Gender = Gender;
                // Nếu nhân viên thôi việc, khóa luôn tài khoản
                if (Status == EmploymentStatus.Resigned)
                {
                    entity.Account.IsActive = false;
                    entity.IsActive = false;
                }
                else
                {
                    // Đồng bộ trạng thái tài khoản theo hồ sơ (vd: sửa từ Thôi việc về Chính thức thì mở khóa)
                    entity.Account.IsActive = IsActive;
                }
            }
        }
    }
}
