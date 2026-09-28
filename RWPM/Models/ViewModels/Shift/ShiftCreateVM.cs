using RWPM.Common.Attributes;
using RWPM.Common.Enums;
using RWPM.Models.Entities;
using System.ComponentModel.DataAnnotations;

using RWPM.Common.Constants;

namespace RWPM.Models.ViewModels.Shift
{
    public class ShiftCreateVM : IValidatableObject
    {
        [Display(Name = "ShiftCode", ResourceType = typeof(Resources.Models.Shift))]
        [MaxLengthLocalized(30)]
        public string ShiftCode { get; set; } = string.Empty;

        [Display(Name = "ShiftName", ResourceType = typeof(Resources.Models.Shift))]
        [RequiredLocalization]
        [MaxLengthLocalized(100)]
        public string ShiftName { get; set; } = string.Empty;

        [Display(Name = "ShiftType", ResourceType = typeof(Resources.Models.Shift))]
        [RequiredByteSelection]
        public ShiftType Type { get; set; }

        [Display(Name = "StartTime", ResourceType = typeof(Resources.Models.Shift))]
        [Required]
        public TimeSpan StartTime { get; set; }

        [Display(Name = "EndTime", ResourceType = typeof(Resources.Models.Shift))]
        [Required]
        public TimeSpan EndTime { get; set; }

        [Display(Name = "BreakMinutes", ResourceType = typeof(Resources.Models.Shift))]
        [Range(0, 60, ErrorMessageResourceType = typeof(Resources.Models.Shift), ErrorMessageResourceName = "Invalid_MaxBreakMinutes")]
        public int BreakMinutes { get; set; }

        public bool IsBreakPaid { get; set; }
        [DataType(DataType.Date)]
        public DateTime EffectiveFrom { get; set; } = DateTime.Today;
        [DataType(DataType.Date)]
        public DateTime? EffectiveTo { get; set; }
        public bool AllowOutsideStoreHours { get; set; }
        [MaxLength(255)]
        public string? OutsideHoursReason { get; set; }
        [Range(1, 1000)]
        public int? DefaultRequiredHeadcount { get; set; }
        [Range(1, 1000)]
        public int? DefaultMaximumHeadcount { get; set; }

        [Display(Name = "Description", ResourceType = typeof(Resources.Models.Shift))]
        [MaxLengthLocalized(255)]
        public string? Description { get; set; }

        public bool UseDefaultAttendancePolicy { get; set; } = true;

        [Display(Name = "GracePeriodMinutes", ResourceType = typeof(Resources.Models.Shift))]
        [Range(0, 60)]
        public int? GracePeriodMinutes { get; set; } = ShiftDefaults.GracePeriodMinutes;

        [Display(Name = "EarlyCheckInMinutes", ResourceType = typeof(Resources.Models.Shift))]
        [Range(0, 120)]
        public int? EarlyCheckInMinutes { get; set; } = ShiftDefaults.EarlyCheckInMinutes;

        [Display(Name = "EarlyCheckOutMinutes", ResourceType = typeof(Resources.Models.Shift))]
        [Range(0, 60)]
        public int? EarlyCheckOutMinutes { get; set; } = ShiftDefaults.EarlyCheckOutMinutes;

        [Display(Name = "LateThresholdMinutes", ResourceType = typeof(Resources.Models.Shift))]
        [Range(0, 240)]
        public int? LateThresholdMinutes { get; set; } = ShiftDefaults.LateThresholdMinutes;

        [Display(Name = "IsTemplate", ResourceType = typeof(Resources.Models.Shift))]
        public bool IsTemplate { get; set; } = true;

        [Display(Name = "StoreId", ResourceType = typeof(Resources.Models.Shift))]
        public int? StoreId { get; set; }

        [Display(Name = "IsActive", ResourceType = typeof(Resources.Models.Shift))]
        public bool IsActive { get; set; } = true;

        public ShiftCreateVM() { }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Type != ShiftType.Flexible && string.IsNullOrWhiteSpace(ShiftCode))
                yield return new ValidationResult("Mã ca là bắt buộc đối với ca thông thường.", new[] { nameof(ShiftCode) });

            if (EndTime == StartTime)
            {
                yield return new ValidationResult("Giờ kết thúc không được trùng giờ bắt đầu.", new[] { nameof(EndTime) });
            }

            double period1Minutes = RWPM.Common.Helper.ShiftTimeHelper.GetDuration(StartTime, EndTime).TotalMinutes;
            double totalMinutes = period1Minutes;

            if (period1Minutes > 12 * 60)
            {
                yield return new ValidationResult("Thời lượng ca không được vượt quá 12 giờ.", new[] { nameof(EndTime) });
            }

            if (period1Minutes > 0 && period1Minutes < 120)
            {
                yield return new ValidationResult(Resources.Models.Shift.Invalid_MinShiftDuration, new[] { nameof(EndTime) });
            }

            if (BreakMinutes > 60)
            {
                yield return new ValidationResult(Resources.Models.Shift.Invalid_MaxBreakMinutes, new[] { nameof(BreakMinutes) });
            }

            if (BreakMinutes > 0 && BreakMinutes >= totalMinutes)
            {
                yield return new ValidationResult(Resources.Models.Shift.Invalid_BreakMinutes, new[] { nameof(BreakMinutes) });
            }
            else if (totalMinutes > 0 && (totalMinutes - BreakMinutes) < 120)
            {
                yield return new ValidationResult(Resources.Models.Shift.Invalid_MinShiftDuration, new[] { nameof(EndTime) });
            }

            if (GracePeriodMinutes.HasValue && LateThresholdMinutes.HasValue && GracePeriodMinutes.Value >= LateThresholdMinutes.Value)
            {
                yield return new ValidationResult(Resources.Models.Shift.Invalid_GracePeriodThreshold, new[] { nameof(GracePeriodMinutes), nameof(LateThresholdMinutes) });
            }

            if (!IsTemplate && !StoreId.HasValue)
            {
                yield return new ValidationResult(Resources.Models.Shift.StoreId_Required, new[] { nameof(StoreId) });
            }
            if (EffectiveTo?.Date < EffectiveFrom.Date)
                yield return new ValidationResult("Ngày hết hiệu lực phải từ ngày bắt đầu hiệu lực trở đi.", new[] { nameof(EffectiveTo) });
            if (DefaultRequiredHeadcount.HasValue != DefaultMaximumHeadcount.HasValue
                || (DefaultRequiredHeadcount.HasValue && DefaultMaximumHeadcount < DefaultRequiredHeadcount))
                yield return new ValidationResult("Số nhân viên tối đa không được nhỏ hơn số nhân viên cần.", new[] { nameof(DefaultMaximumHeadcount) });
        }

        public Entities.Shift ToEntity()
        {
            return new Entities.Shift
            {
                ShiftCode = ShiftCode?.Trim() ?? string.Empty,
                ShiftName = ShiftName.Trim(),
                Type = Type,
                StartTime = StartTime,
                EndTime = EndTime,
                BreakMinutes = BreakMinutes,
                IsBreakPaid = IsBreakPaid,
                EffectiveFrom = EffectiveFrom.Date,
                EffectiveTo = EffectiveTo?.Date,
                AllowOutsideStoreHours = AllowOutsideStoreHours,
                OutsideHoursReason = null,
                DefaultRequiredHeadcount = DefaultRequiredHeadcount,
                DefaultMaximumHeadcount = DefaultMaximumHeadcount,
                GracePeriodMinutes = UseDefaultAttendancePolicy ? null : GracePeriodMinutes,
                EarlyCheckInMinutes = UseDefaultAttendancePolicy ? null : EarlyCheckInMinutes,
                EarlyCheckOutMinutes = UseDefaultAttendancePolicy ? null : EarlyCheckOutMinutes,
                LateThresholdMinutes = UseDefaultAttendancePolicy ? null : LateThresholdMinutes,
                IsTemplate = IsTemplate,
                StoreId = IsTemplate ? null : StoreId,
                Description = Description?.Trim() ?? string.Empty,
                IsActive = IsActive
            };
        }
    }
}
