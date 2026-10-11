using System.ComponentModel.DataAnnotations;

namespace RWPM.Models.ViewModels.ShiftRegistration
{
    public class ShiftSwapCreateVM
    {
        public int RequesterEmployeeId { get; set; }

        [Required]
        public int SourceRegistrationId { get; set; }

        // "peer" = đổi với đồng nghiệp, "direct" = chuyển sang ca khác
        public string SwapType { get; set; } = "peer";

        // Trường hợp đổi với đồng nghiệp (peer)
        public int? TargetEmployeeId { get; set; }
        public int? TargetRegistrationId { get; set; }

        // Trường hợp đổi sang ca khác của mình (direct)
        public int? TargetShiftId { get; set; }
        public DateTime? TargetWorkDate { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập lý do đổi ca.")]
        [MaxLength(255)]
        public string Reason { get; set; } = string.Empty;
    }
}
