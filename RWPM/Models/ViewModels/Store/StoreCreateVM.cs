using RWPM.Common.Attributes;
using System.ComponentModel.DataAnnotations;

namespace RWPM.Models.ViewModels.Store
{
    public class StoreCreateVM
    {
        [Display(Name = "StoreCode", ResourceType = typeof(Resources.Models.Store))]
        [RequiredLocalization]
        [MaxLengthLocalized(20)]
        public string StoreCode { get; set; } = string.Empty;

        [Display(Name = "StoreName", ResourceType = typeof(Resources.Models.Store))]
        [RequiredLocalization]
        [MaxLengthLocalized(100)]
        public string StoreName { get; set; } = string.Empty;

        [Display(Name = "Address", ResourceType = typeof(Resources.Models.Store))]
        [MaxLengthLocalized(255)]
        public string Address { get; set; } = string.Empty;

        [Display(Name = "Phone", ResourceType = typeof(Resources.Models.Store))]
        [MaxLengthLocalized(20)]
        public string Phone { get; set; } = string.Empty;

        [Display(Name = "IsActive", ResourceType = typeof(Resources.Models.Store))]
        public bool IsActive { get; set; } = true;

        public StoreCreateVM() { }

        public global::Store ToEntity()
        {
            return new global::Store
            {
                StoreCode = StoreCode.Trim(),
                StoreName = StoreName.Trim(),
                Address = Address?.Trim() ?? string.Empty,
                Phone = Phone?.Trim() ?? string.Empty,
                IsActive = IsActive
            };
        }
    }
}
