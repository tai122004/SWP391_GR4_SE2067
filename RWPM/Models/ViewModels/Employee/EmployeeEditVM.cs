using System;
using System.ComponentModel.DataAnnotations;
using RWPM.Common.Attributes;

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

        [Display(Name = "Status", ResourceType = typeof(Resources.Models.Employee))]
        public bool IsActive { get; set; } = true;

        public EmployeeEditVM() { }

        public EmployeeEditVM(RWPM.Models.Entities.Employee entity)
        {
            EmployeeId = entity.EmployeeId;
            EmployeeCode = entity.EmployeeCode;
            Username = entity.Username;
            StoreId = entity.StoreId;
            JoinDate = entity.JoinDate;
            IsActive = entity.IsActive;
        }

        public void ApplyToEntity(RWPM.Models.Entities.Employee entity)
        {
            entity.EmployeeCode = EmployeeCode.Trim();
            entity.Username = Username.Trim();
            entity.StoreId = StoreId;
            entity.Store = null!;
            entity.JoinDate = JoinDate.Date;
            entity.IsActive = IsActive;
        }
    }
}
