using System.ComponentModel.DataAnnotations;
using System.Globalization;
using RWPM.Common.Attributes;
using RWPM.Common.Validation;

namespace RWPM.Models.ViewModels.Store
{
    public class StoreEditVM : IValidatableObject
    {
        private string _storeCode = string.Empty;
        private string _storeName = string.Empty;
        private string _address = string.Empty;
        private string _phone = string.Empty;

        public int StoreId { get; set; }

        [Display(Name = "StoreCode", ResourceType = typeof(Resources.Models.Store))]
        [RequiredLocalization]
        [MaxLengthLocalized(20)]
        [RegexLocalized(@"^[a-zA-Z0-9_-]+$", "RegularExpression_CodeRegex")]
        public string StoreCode { get => _storeCode; set => _storeCode = value?.Trim() ?? string.Empty; }

        [Display(Name = "StoreName", ResourceType = typeof(Resources.Models.Store))]
        [RequiredLocalization]
        [MaxLengthLocalized(100)]
        public string StoreName { get => _storeName; set => _storeName = value?.Trim() ?? string.Empty; }

        [Display(Name = "Address", ResourceType = typeof(Resources.Models.Store))]
        [MaxLengthLocalized(255)]
        public string Address { get => _address; set => _address = value?.Trim() ?? string.Empty; }

        [Display(Name = "Phone", ResourceType = typeof(Resources.Models.Store))]
        [MaxLengthLocalized(20)]
        [RegexLocalized(@"^[0-9]*$", "RegularExpression_DigitsOnly")]
        public string Phone { get => _phone; set => _phone = value?.Trim() ?? string.Empty; }

        [Display(Name = "Latitude", ResourceType = typeof(Resources.Models.Store))]
        public string? Latitude { get; set; }

        [Display(Name = "Longitude", ResourceType = typeof(Resources.Models.Store))]
        public string? Longitude { get; set; }

        [Display(Name = "MinAllowedDistanceMeters", ResourceType = typeof(Resources.Models.Store))]
        [RangeLocalized(0, 10000)]
        public int MinAllowedDistanceMeters { get; set; } = 0;

        [Display(Name = "AllowedRadiusMeters", ResourceType = typeof(Resources.Models.Store))]
        [RangeLocalized(10, 10000)]
        public int AllowedRadiusMeters { get; set; } = 100;

        [Display(Name = "IsActive", ResourceType = typeof(Resources.Models.Store))]
        public bool IsActive { get; set; } = true;

        [Display(Name = "ManagerId", ResourceType = typeof(Resources.Models.Store))]
        public int? ManagerId { get; set; }

        [Display(Name = "OpeningTime", ResourceType = typeof(Resources.Models.Store))]
        [DataType(DataType.Time)]
        [DisplayFormat(DataFormatString = @"{0:hh\:mm}", ApplyFormatInEditMode = true)]
        public TimeSpan? OpeningTime { get; set; }

        [Display(Name = "ClosingTime", ResourceType = typeof(Resources.Models.Store))]
        [DataType(DataType.Time)]
        [DisplayFormat(DataFormatString = @"{0:hh\:mm}", ApplyFormatInEditMode = true)]
        public TimeSpan? ClosingTime { get; set; }

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
            ManagerId = entity.ManagerId;
            OpeningTime = entity.OpeningTime;
            ClosingTime = entity.ClosingTime;
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
            entity.ManagerId = ManagerId;
            entity.OpeningTime = OpeningTime;
            entity.ClosingTime = ClosingTime;
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            foreach (var (value, field, min, max) in new[] {
                (Latitude, nameof(Latitude), -90.0, 90.0),
                (Longitude, nameof(Longitude), -180.0, 180.0) })
            {
                if (!string.IsNullOrWhiteSpace(value) && !StoreCreateVM.ParseCoord(value, min, max).HasValue)
                    yield return new ValidationResult(InputRules.Text("Tọa độ không hợp lệ hoặc ngoài phạm vi cho phép.",
                        "Coordinate is invalid or outside the allowed range."), new[] { field });
            }
            if (string.IsNullOrWhiteSpace(Latitude) != string.IsNullOrWhiteSpace(Longitude))
                yield return new ValidationResult(InputRules.Text("Cần nhập đủ vĩ độ và kinh độ.",
                    "Provide both latitude and longitude."), new[] { nameof(Latitude), nameof(Longitude) });
            if (IsActive && string.IsNullOrWhiteSpace(Latitude) && string.IsNullOrWhiteSpace(Longitude))
                yield return new ValidationResult(InputRules.Text("Vui lòng chọn vị trí chi nhánh trên bản đồ trước khi kích hoạt.",
                    "Select the store location on the map before activating it."), new[] { nameof(Latitude) });
            if (MinAllowedDistanceMeters >= AllowedRadiusMeters)
                yield return new ValidationResult(InputRules.Text("Khoảng cách tối thiểu phải nhỏ hơn bán kính cho phép.",
                    "Minimum distance must be smaller than the allowed radius."), new[] { nameof(MinAllowedDistanceMeters) });
        }
    }
}
