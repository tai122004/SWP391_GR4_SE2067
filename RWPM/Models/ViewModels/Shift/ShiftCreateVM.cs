using RWPM.Common.Attributes;
using RWPM.Common.Enums;
using RWPM.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace RWPM.Models.ViewModels.Shift
{
    public class ShiftCreateVM : IValidatableObject
    {
        [Display(Name = "ShiftCode", ResourceType = typeof(Resources.Models.Shift))]
        [RequiredLocalization]
        [MaxLengthLocalized(20)]
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

        [Display(Name = "StartTime2", ResourceType = typeof(Resources.Models.Shift))]
        public TimeSpan? StartTime2 { get; set; }

        [Display(Name = "EndTime2", ResourceType = typeof(Resources.Models.Shift))]
        public TimeSpan? EndTime2 { get; set; }

        [Display(Name = "BreakMinutes", ResourceType = typeof(Resources.Models.Shift))]
        [Range(0, 60, ErrorMessageResourceType = typeof(Resources.Models.Shift), ErrorMessageResourceName = "Invalid_MaxBreakMinutes")]
        public int BreakMinutes { get; set; }

        [Display(Name = "Description", ResourceType = typeof(Resources.Models.Shift))]
        [MaxLengthLocalized(255)]
        public string? Description { get; set; }

        [Display(Name = "IsActive", ResourceType = typeof(Resources.Models.Shift))]
        public bool IsActive { get; set; } = true;

        public ShiftCreateVM() { }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndTime <= StartTime)
            {
                yield return new ValidationResult(Resources.Models.Shift.Invalid_TimeRange, new[] { nameof(EndTime) });
            }

            double period1Minutes = (EndTime > StartTime) ? (EndTime - StartTime).TotalMinutes : 0;
            double totalMinutes = period1Minutes;

            if (Type == ShiftType.Split)
            {
                if (!StartTime2.HasValue || !EndTime2.HasValue)
                {
                    yield return new ValidationResult(Resources.Models.Shift.Invalid_SplitShift_Required, new[] { nameof(StartTime2), nameof(EndTime2) });
                }
                else
                {
                    if (EndTime2.Value <= StartTime2.Value)
                    {
                        yield return new ValidationResult(Resources.Models.Shift.Invalid_SplitShift_TimeRange, new[] { nameof(EndTime2) });
                    }
                    if (StartTime2.Value < EndTime)
                    {
                        yield return new ValidationResult(Resources.Models.Shift.Invalid_SplitShift_Overlap, new[] { nameof(StartTime2) });
                    }

                    double period2Minutes = (EndTime2.Value > StartTime2.Value) ? (EndTime2.Value - StartTime2.Value).TotalMinutes : 0;
                    totalMinutes += period2Minutes;

                    if (period1Minutes > 0 && period2Minutes > 0 && (period1Minutes < 90 || period2Minutes < 90 || totalMinutes < 240))
                    {
                        yield return new ValidationResult(Resources.Models.Shift.Invalid_MinSplitPeriodDuration, new[] { nameof(StartTime), nameof(EndTime), nameof(StartTime2), nameof(EndTime2) });
                    }
                }
            }
            else
            {
                if (period1Minutes > 0 && period1Minutes < 120)
                {
                    yield return new ValidationResult(Resources.Models.Shift.Invalid_MinShiftDuration, new[] { nameof(EndTime) });
                }
            }

            if (BreakMinutes > 60)
            {
                yield return new ValidationResult(Resources.Models.Shift.Invalid_MaxBreakMinutes, new[] { nameof(BreakMinutes) });
            }

            if (BreakMinutes > 0 && BreakMinutes >= totalMinutes)
            {
                yield return new ValidationResult(Resources.Models.Shift.Invalid_BreakMinutes, new[] { nameof(BreakMinutes) });
            }
            else if (totalMinutes > 0 && (totalMinutes - BreakMinutes) < 120 && Type != ShiftType.Split)
            {
                yield return new ValidationResult(Resources.Models.Shift.Invalid_MinShiftDuration, new[] { nameof(EndTime) });
            }
        }

        public Entities.Shift ToEntity()
        {
            var isSplit = Type == ShiftType.Split;
            return new Entities.Shift
            {
                ShiftCode = ShiftCode.Trim(),
                ShiftName = ShiftName.Trim(),
                Type = Type,
                StartTime = StartTime,
                EndTime = EndTime,
                StartTime2 = isSplit ? StartTime2 : null,
                EndTime2 = isSplit ? EndTime2 : null,
                BreakMinutes = BreakMinutes,
                Description = Description?.Trim() ?? string.Empty,
                IsActive = IsActive
            };
        }
    }
}
