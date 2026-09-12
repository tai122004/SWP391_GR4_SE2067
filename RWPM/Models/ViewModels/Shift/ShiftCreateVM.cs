using RWPM.Common.Attributes;
using RWPM.Common.Enums;
using RWPM.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace RWPM.Models.ViewModels.Shift
{
    public class ShiftCreateVM
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
        [Range(0, 120)]
        public int BreakMinutes { get; set; }

        [Display(Name = "Description", ResourceType = typeof(Resources.Models.Shift))]
        [MaxLengthLocalized(255)]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "IsActive", ResourceType = typeof(Resources.Models.Shift))]
        public bool IsActive { get; set; } = true;

        public ShiftCreateVM() { }

        public Entities.Shift ToEntity()
        {
            return new Entities.Shift
            {
                ShiftCode = ShiftCode.Trim(),
                ShiftName = ShiftName.Trim(),
                Type = Type,
                StartTime = StartTime,
                EndTime = EndTime,
                StartTime2 = StartTime2,
                EndTime2 = EndTime2,
                BreakMinutes = BreakMinutes,
                Description = Description?.Trim() ?? string.Empty,
                IsActive = IsActive
            };
        }
    }
}
