using System;
using System.ComponentModel.DataAnnotations;
using RWPM.Common.Attributes;
using RWPM.Common.Enums;

namespace RWPM.Models.ViewModels.Employee
{
    public class EmployeeCreateVM
    {
        [Display(Name = "EmployeeID", ResourceType = typeof(Resources.Models.Employee))]
        [RequiredLocalization]
        [MaxLengthLocalized(20)]
        public string EmployeeCode { get; set; } = string.Empty;

        [Display(Name = "EmployeeAcc", ResourceType = typeof(Resources.Models.Employee))]
        [RequiredLocalization]
        [MaxLengthLocalized(30)]
        public string Username { get; set; } = string.Empty;

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
        public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;

        [Display(Name = "EmploymentStatus", ResourceType = typeof(Resources.Models.Employee))]
        public EmploymentStatus Status { get; set; } = EmploymentStatus.Official;

        [Display(Name = "HourlyRate", ResourceType = typeof(Resources.Models.Employee))]
        [Range(0, 100000000)]
        public decimal? HourlyRate { get; set; }

        [Display(Name = "BaseSalary", ResourceType = typeof(Resources.Models.Employee))]
        [Range(0, 1000000000)]
        public decimal? BaseSalary { get; set; }

        [Display(Name = "AnnualLeaveBalance", ResourceType = typeof(Resources.Models.Employee))]
        [Range(0, 365)]
        public int AnnualLeaveBalance { get; set; } = 12;

        [Display(Name = "CitizenId", ResourceType = typeof(Resources.Models.Employee))]
        [MaxLengthLocalized(20)]
        public string? CitizenId { get; set; }

        // Thông tin liên lạc
        [Display(Name = "PhoneNumber", ResourceType = typeof(Resources.Models.Acc))]
        [MaxLengthLocalized(20)]
        public string? PhoneNumber { get; set; }

        [Display(Name = "DateOfBirth", ResourceType = typeof(Resources.Models.Acc))]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [Display(Name = "Gender", ResourceType = typeof(Resources.Models.Acc))]
        public Gender? Gender { get; set; }

        [Display(Name = "Status", ResourceType = typeof(Resources.Models.Employee))]
        public bool IsActive { get; set; } = true;

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
