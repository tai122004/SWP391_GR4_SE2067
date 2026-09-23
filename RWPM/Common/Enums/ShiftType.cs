using System.ComponentModel.DataAnnotations;
using RWPM.Common.Helper;

namespace RWPM.Common.Enums
{
    public enum ShiftType : byte
    {
        /// <summary>
        /// Ca Sáng: 8:30 - 16:00
        /// </summary>
        [Display(ResourceType = typeof(Resources.Models.Shift), Name = "ShiftType_Morning")]
        Morning = 1,

        /// <summary>
        /// Ca Chiều/Tối: 15:00 - 22:30
        /// </summary>
        [Display(ResourceType = typeof(Resources.Models.Shift), Name = "ShiftType_Afternoon")]
        Afternoon = 2,

        /// <summary>
        /// Ca Gãy: 9:00 - 13:00 + 17:00 - 22:00
        /// </summary>
        [Display(ResourceType = typeof(Resources.Models.Shift), Name = "ShiftType_Split")]
        Split = 3,

        /// <summary>
        /// Ca Hành Chính (Full-time): 8:30 - 17:30
        /// </summary>
        [Display(ResourceType = typeof(Resources.Models.Shift), Name = "ShiftType_FullDay")]
        FullDay = 4,

        /// <summary>
        /// Ca Tăng ca (Overtime)
        /// </summary>
        [Display(ResourceType = typeof(Resources.Models.Shift), Name = "ShiftType_Overtime")]
        Overtime = 5,

        /// <summary>
        /// Ca Linh động (Flexible) - chỉ cần đủ số giờ
        /// </summary>
        [Display(ResourceType = typeof(Resources.Models.Shift), Name = "ShiftType_Flexible")]
        Flexible = 6,
    }

    public static class ShiftTypeExtensions
    {
        public static string GetDisplayName(this ShiftType type)
        {
            return UIHelper.GetDisplayName(type);
        }
    }
}
