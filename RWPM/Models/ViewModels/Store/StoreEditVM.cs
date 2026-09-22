using System.ComponentModel.DataAnnotations;
using System.Globalization;
using RWPM.Common.Attributes;

namespace RWPM.Models.ViewModels.Store
{
    public class StoreEditVM
    {
        public int StoreId { get; set; }

        [Display(Name = "StoreCode", ResourceType = typeof(Resources.Models.Store))]
        [RequiredLocalization]
        [MaxLengthLocalized(20)]
        [RegexLocalized(@"^[a-zA-Z0-9_-]+$", "RegularExpression_CodeRegex")]
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
        [RegexLocalized(@"^[0-9]*$", "RegularExpression_DigitsOnly")]
        public string Phone { get; set; } = string.Empty;

        [Display(Name = "Latitude", ResourceType = typeof(Resources.Models.Store))]
        public string? Latitude { get; set; }

        [Display(Name = "Longitude", ResourceType = typeof(Resources.Models.Store))]
        public string? Longitude { get; set; }

        [Display(Name = "MinAllowedDistanceMeters", ResourceType = typeof(Resources.Models.Store))]
        [Range(0, 10000)]
        public int MinAllowedDistanceMeters { get; set; } = 0;

        [Display(Name = "AllowedRadiusMeters", ResourceType = typeof(Resources.Models.Store))]
        [Range(10, 10000)]
        public int AllowedRadiusMeters { get; set; } = 100;

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
            Latitude = entity.Latitude?.ToString(CultureInfo.InvariantCulture);
            Longitude = entity.Longitude?.ToString(CultureInfo.InvariantCulture);
            MinAllowedDistanceMeters = entity.MinAllowedDistanceMeters >= 0 ? entity.MinAllowedDistanceMeters : 0;
            AllowedRadiusMeters = entity.AllowedRadiusMeters >= 10 ? entity.AllowedRadiusMeters : 100;
            IsActive = entity.IsActive;
        }

        public void ApplyToEntity(global::Store entity)
        {
            entity.StoreCode = StoreCode.Trim();
            entity.StoreName = StoreName.Trim();
            entity.Address = Address?.Trim() ?? string.Empty;
            entity.Phone = Phone?.Trim() ?? string.Empty;
            entity.Latitude = StoreCreateVM.ParseCoord(Latitude, -90.0, 90.0);
            entity.Longitude = StoreCreateVM.ParseCoord(Longitude, -180.0, 180.0);
            entity.MinAllowedDistanceMeters = MinAllowedDistanceMeters >= 0 ? MinAllowedDistanceMeters : 0;
            entity.AllowedRadiusMeters = AllowedRadiusMeters >= 10 ? AllowedRadiusMeters : 100;
            entity.IsActive = IsActive;
        }
    }
}
