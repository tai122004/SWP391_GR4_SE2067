using System.ComponentModel.DataAnnotations;
using RWPM.Common.Attributes;

namespace RWPM.Models.ViewModels.Store
{
    public class StoreEditVM
    {
        public int StoreId { get; set; }

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

        public StoreEditVM() { }

        public StoreEditVM(global::Store entity)
        {
            StoreId = entity.StoreId;
            StoreCode = entity.StoreCode;
            StoreName = entity.StoreName;
            Address = entity.Address;
            Phone = entity.Phone;
            IsActive = entity.IsActive;
        }

        public void ApplyToEntity(global::Store entity)
        {
            entity.StoreCode = StoreCode.Trim();
            entity.StoreName = StoreName.Trim();
            entity.Address = Address?.Trim() ?? string.Empty;
            entity.Phone = Phone?.Trim() ?? string.Empty;
            entity.IsActive = IsActive;
        }
    }
}
