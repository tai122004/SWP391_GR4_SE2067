using RWPM.Common.Attributes;
using RWPM.Common.Enums;
using System.ComponentModel.DataAnnotations;

namespace RWPM.Models.ViewModels.Shift
{
    public class ShiftEditVM
    {
        public int ShiftId { get; set; }

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

        public ShiftEditVM() { }

        public ShiftEditVM(Entities.Shift entity)
        {
            ShiftId = entity.ShiftId;
            ShiftCode = entity.ShiftCode;
            ShiftName = entity.ShiftName;
            Type = entity.Type;
            StartTime = entity.StartTime;
            EndTime = entity.EndTime;
            StartTime2 = entity.StartTime2;
            EndTime2 = entity.EndTime2;
            BreakMinutes = entity.BreakMinutes;
            Description = entity.Description;
            IsActive = entity.IsActive;
        }

        public void ApplyToEntity(Entities.Shift entity)
        {
            entity.ShiftCode = ShiftCode.Trim();
            entity.ShiftName = ShiftName.Trim();
            entity.Type = Type;
            entity.StartTime = StartTime;
            entity.EndTime = EndTime;
            entity.StartTime2 = StartTime2;
            entity.EndTime2 = EndTime2;
            entity.BreakMinutes = BreakMinutes;
            entity.Description = Description?.Trim() ?? string.Empty;
            entity.IsActive = IsActive;
        }
    }
}
