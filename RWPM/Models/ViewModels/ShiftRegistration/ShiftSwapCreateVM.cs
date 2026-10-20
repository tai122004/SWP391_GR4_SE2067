using RWPM.Common.Attributes;
using System.ComponentModel.DataAnnotations;

namespace RWPM.Models.ViewModels.ShiftRegistration
{
    public class ShiftSwapCreateVM
    {
        private string _swapType = "peer";
        private string _reason = string.Empty;

        public int RequesterEmployeeId { get; set; }

        [RequiredLocalization]
        [Display(Name = "Swap_SourceShift", ResourceType = typeof(RWPM.Resources.Shared.RequestResource))]
        public int SourceRegistrationId { get; set; }

        // "peer" = đổi với đồng nghiệp, "direct" = chuyển sang ca khác
        public string SwapType { get => _swapType; set => _swapType = value?.Trim() ?? string.Empty; }

        // Trường hợp đổi với đồng nghiệp (peer)
        public int? TargetEmployeeId { get; set; }
        public int? TargetRegistrationId { get; set; }

        // Trường hợp đổi sang ca khác của mình (direct)
        public int? TargetShiftId { get; set; }
        public DateTime? TargetWorkDate { get; set; }

        [RequiredLocalization]
        [MaxLengthLocalized(255)]
        [Display(Name = "Swap_Reason", ResourceType = typeof(RWPM.Resources.Shared.RequestResource))]
        public string Reason { get => _reason; set => _reason = value?.Trim() ?? string.Empty; }
    }
}
