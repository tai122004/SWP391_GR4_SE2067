using RWPM.Common.Enums;
using RWPM.Models.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RWPM.Models.Entities
{
    public class Shift : AuditableEntity, IActivatable
    {
        [Key]
        public int ShiftId { get; set; }

        [Required]
        [MaxLength(20)]
        public string ShiftCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string ShiftName { get; set; } = string.Empty;

        public ShiftType Type { get; set; }

        //Khung giờ chính 
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        //Khung giờ dành cho Ca Gãy
        public TimeSpan? StartTime2 { get; set; }
        public TimeSpan? EndTime2 { get; set; }

        public int BreakMinutes { get; set; }

        [MaxLength(255)]
        public string Description { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }

    public static class ShiftExtensions
    {
        public static string GetLocalizedName(this Shift shift)
        {
            return shift.ShiftCode switch
            {
                "S" => Resources.Models.Shift.ShiftName_S,
                "C" => Resources.Models.Shift.ShiftName_C,
                "G" => Resources.Models.Shift.ShiftName_G,
                "HC" => Resources.Models.Shift.ShiftName_HC,
                _ => shift.ShiftName
            };
        }
    }
}
