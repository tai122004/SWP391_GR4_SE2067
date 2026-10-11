using RWPM.Common.Enums;
using System.ComponentModel.DataAnnotations;

namespace RWPM.Models.ViewModels.ShiftRegistration
{
    public class LeaveRequestCreateVM
    {
        public int EmployeeId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu.")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày kết thúc.")]
        public DateTime EndDate { get; set; }

        [Required]
        public LeaveType LeaveType { get; set; } = LeaveType.Paid;

        [Required(ErrorMessage = "Vui lòng nhập lý do xin nghỉ.")]
        [MaxLength(500, ErrorMessage = "Lý do không được vượt quá 500 ký tự.")]
        public string Reason { get; set; } = string.Empty;
    }
}
