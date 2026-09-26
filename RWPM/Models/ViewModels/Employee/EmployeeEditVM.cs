using System;
using System.ComponentModel.DataAnnotations;
using RWPM.Common.Attributes;
using RWPM.Common.Enums;

namespace RWPM.Models.ViewModels.Employee
{
    public class EmployeeEditVM
    {
        public int EmployeeId { get; set; }

        [Display(Name = "EmployeeID", ResourceType = typeof(Resources.Models.Employee))]
        [RequiredLocalization]
        [MaxLengthLocalized(20)]
        public string EmployeeCode { get; set; } = string.Empty;

        [Display(Name = "EmployeeAcc", ResourceType = typeof(Resources.Models.Employee))]
        [RequiredLocalization]
        [MaxLengthLocalized(30)]
        public string Username { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

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

        // Thông tin cá nhân liên kết với Account
        [Display(Name = "PhoneNumber", ResourceType = typeof(Resources.Models.Acc))]
        [MaxLengthLocalized(20)]
        public string? PhoneNumber { get; set; }

        [Display(Name = "DateOfBirth", ResourceType = typeof(Resources.Models.Acc))]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [Display(Name = "Gender", ResourceType = typeof(Resources.Models.Acc))]
        public Gender? Gender { get; set; }

        [Display(Name = "ResignDate", ResourceType = typeof(Resources.Models.Employee))]
        [DataType(DataType.Date)]
        public DateTime? ResignDate { get; set; }

        [Display(Name = "ResignReason", ResourceType = typeof(Resources.Models.Employee))]
        [MaxLengthLocalized(255)]
        public string? ResignReason { get; set; }

        [Display(Name = "Status", ResourceType = typeof(Resources.Models.Employee))]
        public bool IsActive { get; set; } = true;

        public EmployeeEditVM() { }

        public EmployeeEditVM(RWPM.Models.Entities.Employee entity)
        {
            EmployeeId = entity.EmployeeId;
            EmployeeCode = entity.EmployeeCode;
            Username = entity.Username;
            FullName = entity.Account?.FullName ?? string.Empty;
            StoreId = entity.StoreId;
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
            }
        }
    }
}
