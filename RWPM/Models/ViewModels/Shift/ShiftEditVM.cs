using RWPM.Common.Attributes;
using RWPM.Common.Enums;
using System.ComponentModel.DataAnnotations;

using RWPM.Common.Constants;

namespace RWPM.Models.ViewModels.Shift
{
    public class ShiftEditVM : IValidatableObject
    {
        public int ShiftId { get; set; }

        [Display(Name = "ShiftCode", ResourceType = typeof(Resources.Models.Shift))]
        [RequiredLocalization]
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
        public int? GracePeriodMinutes { get; set; }

        [Display(Name = "EarlyCheckInMinutes", ResourceType = typeof(Resources.Models.Shift))]
        [Range(0, 120)]
        public int? EarlyCheckInMinutes { get; set; }

        [Display(Name = "EarlyCheckOutMinutes", ResourceType = typeof(Resources.Models.Shift))]
        [Range(0, 60)]
        public int? EarlyCheckOutMinutes { get; set; }

        [Display(Name = "LateThresholdMinutes", ResourceType = typeof(Resources.Models.Shift))]
        [Range(0, 240)]
        public int? LateThresholdMinutes { get; set; }

        [Display(Name = "IsTemplate", ResourceType = typeof(Resources.Models.Shift))]
        public bool IsTemplate { get; set; } = true;

        [Display(Name = "StoreId", ResourceType = typeof(Resources.Models.Shift))]
        public int? StoreId { get; set; }

        [Display(Name = "IsActive", ResourceType = typeof(Resources.Models.Shift))]
        public bool IsActive { get; set; } = true;

        public ShiftEditVM() { }

        public ShiftEditVM(Entities.Shift entity)
        {
            ShiftId = entity.ShiftId;
            ShiftCode = entity.ShiftCode;
            ShiftName = entity.ShiftName;
            Type = entity.Type;
            StartTime = entity.StartTime;
            EndTime = entity.EndTime;
            BreakMinutes = entity.BreakMinutes;
            IsBreakPaid = entity.IsBreakPaid;
            EffectiveFrom = entity.EffectiveFrom;
            EffectiveTo = entity.EffectiveTo;
            AllowOutsideStoreHours = entity.AllowOutsideStoreHours;
            OutsideHoursReason = entity.OutsideHoursReason;
            DefaultRequiredHeadcount = entity.DefaultRequiredHeadcount;
            DefaultMaximumHeadcount = entity.DefaultMaximumHeadcount;
            
            UseDefaultAttendancePolicy = !entity.GracePeriodMinutes.HasValue 
                                      && !entity.EarlyCheckInMinutes.HasValue 
                                      && !entity.EarlyCheckOutMinutes.HasValue
                                      && !entity.LateThresholdMinutes.HasValue;

            GracePeriodMinutes = entity.GracePeriodMinutes ?? ShiftDefaults.GracePeriodMinutes;
            EarlyCheckInMinutes = entity.EarlyCheckInMinutes ?? ShiftDefaults.EarlyCheckInMinutes;
            EarlyCheckOutMinutes = entity.EarlyCheckOutMinutes ?? ShiftDefaults.EarlyCheckOutMinutes;
            LateThresholdMinutes = entity.LateThresholdMinutes ?? ShiftDefaults.LateThresholdMinutes;

            IsTemplate = entity.IsTemplate;
            StoreId = entity.StoreId;
            Description = entity.Description;
            IsActive = entity.IsActive;
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndTime == StartTime)
            {
                yield return new ValidationResult("Giờ kết thúc không được trùng giờ bắt đầu.", new[] { nameof(EndTime) });
            }

            double period1Minutes = RWPM.Common.Helper.ShiftTimeHelper.GetDuration(StartTime, EndTime).TotalMinutes;
            double totalMinutes = period1Minutes;

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

        public void ApplyToEntity(Entities.Shift entity)
        {
            entity.ShiftCode = ShiftCode.Trim();
            entity.ShiftName = ShiftName.Trim();
            entity.Type = Type;
            entity.StartTime = StartTime;
            entity.EndTime = EndTime;
            entity.BreakMinutes = BreakMinutes;
            entity.IsBreakPaid = IsBreakPaid;
            entity.EffectiveFrom = EffectiveFrom.Date;
            entity.EffectiveTo = EffectiveTo?.Date;
            entity.AllowOutsideStoreHours = AllowOutsideStoreHours;
            entity.OutsideHoursReason = null;
            entity.DefaultRequiredHeadcount = DefaultRequiredHeadcount;
            entity.DefaultMaximumHeadcount = DefaultMaximumHeadcount;
            entity.GracePeriodMinutes = UseDefaultAttendancePolicy ? null : GracePeriodMinutes;
            entity.EarlyCheckInMinutes = UseDefaultAttendancePolicy ? null : EarlyCheckInMinutes;
            entity.EarlyCheckOutMinutes = UseDefaultAttendancePolicy ? null : EarlyCheckOutMinutes;
            entity.LateThresholdMinutes = UseDefaultAttendancePolicy ? null : LateThresholdMinutes;
            entity.IsTemplate = IsTemplate;
            entity.StoreId = IsTemplate ? null : StoreId;
            entity.Description = Description?.Trim() ?? string.Empty;
            entity.IsActive = IsActive;
        }
    }
}
