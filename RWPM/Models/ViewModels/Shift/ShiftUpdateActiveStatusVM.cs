using RWPM.Common.Attributes;

namespace RWPM.Models.ViewModels.Shift
{
    public class ShiftUpdateActiveStatusVM
    {
        [RequiredLocalization]
        public int? ShiftId { get; set; }

        [RequiredLocalization]
        public bool? IsActive { get; set; }
    }
}
