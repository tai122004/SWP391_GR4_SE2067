using RWPM.Common.Validation;
using System;
using System.ComponentModel.DataAnnotations;
using RWPM.Common.Attributes;
using RWPM.Common.Enums;

namespace RWPM.Models.ViewModels.Employee
{
    public class EmployeeCreateVM : IValidatableObject
    {
        private string _employeeCode = string.Empty;
        private string _username = string.Empty;
        private string? _citizenId = null;
        private string? _phoneNumber = null;

        [Display(Name = "EmployeeID", ResourceType = typeof(Resources.Models.Employee))]
        [RequiredLocalization]
        [MaxLengthLocalized(20)]
        public string EmployeeCode { get => _employeeCode; set => _employeeCode = value?.Trim() ?? string.Empty; }

        [Display(Name = "EmployeeAcc", ResourceType = typeof(Resources.Models.Employee))]
        [RequiredLocalization]
        [MaxLengthLocalized(30)]
        public string Username { get => _username; set => _username = value?.Trim() ?? string.Empty; }

        [Display(Name = "Store", ResourceType = typeof(Resources.Models.Employee))]
        [RequiredLocalization]
        [Range(1, int.MaxValue,
            ErrorMessageResourceType = typeof(Resources.Models.Employee),
            ErrorMessageResourceName = "StoreId_RangeError")]
        public int StoreId { get; set; }

        [Display(Name = "StartDate", ResourceType = typeof(Resources.Models.Employee))]
        [RequiredLocalization]
        [DataType(DataType.Date)]
        public DateTime JoinDate { get; set; } = DateTime.Today;

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

        // Thông tin liên lạc
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

        [Display(Name = "Status", ResourceType = typeof(Resources.Models.Employee))]
        public bool IsActive { get; set; } = true;

        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            InputValidationException? error = null;
            try
            {
                InputRules.ValidateEmployment(EmployeeCode, JoinDate, DateOfBirth, null, EmploymentType, Status,
                    HourlyRate, BaseSalary, AnnualLeaveBalance, CitizenId);
            }
            catch (InputValidationException ex) { error = ex; }
            if (error != null) yield return new ValidationResult(error.Message, new[] { error.Field });
        }

        public EmployeeCreateVM() { }

        public RWPM.Models.Entities.Employee ToEntity()
        {
            return new RWPM.Models.Entities.Employee
            {
                EmployeeCode = EmployeeCode.Trim(),
                Username = Username.Trim(),
                StoreId = StoreId,
                JoinDate = JoinDate.Date,
                EmploymentType = EmploymentType,
                Status = Status,
                HourlyRate = HourlyRate,
                BaseSalary = BaseSalary,
                AnnualLeaveBalance = AnnualLeaveBalance,
                CitizenId = CitizenId?.Trim(),
                IsActive = IsActive
            };
        }
    }
}
