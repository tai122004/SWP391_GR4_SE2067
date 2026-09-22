using RWPM.Common.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace RWPM.Models.ViewModels.Store
{
    public class StoreCreateVM
    {
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
        public string? Latitude { get; set; } = "21.025142";

        [Display(Name = "Longitude", ResourceType = typeof(Resources.Models.Store))]
        public string? Longitude { get; set; } = "105.553973";

        [Display(Name = "MinAllowedDistanceMeters", ResourceType = typeof(Resources.Models.Store))]
        [Range(0, 10000)]
        public int MinAllowedDistanceMeters { get; set; } = 0;

        [Display(Name = "AllowedRadiusMeters", ResourceType = typeof(Resources.Models.Store))]
        [Range(10, 10000)]
        public int AllowedRadiusMeters { get; set; } = 100;

        [Display(Name = "IsActive", ResourceType = typeof(Resources.Models.Store))]
        public bool IsActive { get; set; } = true;

        public StoreCreateVM() { }

        public static double? ParseCoord(string? val, double min, double max)
        {
            if (string.IsNullOrWhiteSpace(val)) return null;
            var clean = val.Trim().Replace(',', '.');
            if (double.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            {
                if (result >= min && result <= max) return result;
            }
            return null;
        }

        public global::Store ToEntity()
        {
            var lat = ParseCoord(Latitude, -90.0, 90.0) ?? 21.025142;
            var lng = ParseCoord(Longitude, -180.0, 180.0) ?? 105.553973;

            return new global::Store
            {
                StoreCode = StoreCode.Trim(),
                StoreName = StoreName.Trim(),
                Address = Address?.Trim() ?? string.Empty,
                Phone = Phone?.Trim() ?? string.Empty,
                Latitude = lat,
                Longitude = lng,
                MinAllowedDistanceMeters = MinAllowedDistanceMeters >= 0 ? MinAllowedDistanceMeters : 0,
                AllowedRadiusMeters = AllowedRadiusMeters >= 10 ? AllowedRadiusMeters : 100,
                IsActive = IsActive
            };
        }
    }
}
