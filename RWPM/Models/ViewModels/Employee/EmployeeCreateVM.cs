using System;
using System.ComponentModel.DataAnnotations;
using RWPM.Common.Attributes;

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
                IsActive = IsActive
            };
        }
    }
}
