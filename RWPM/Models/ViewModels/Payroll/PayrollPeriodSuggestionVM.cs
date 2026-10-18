namespace RWPM.Models.ViewModels.Payroll
{
    /// <summary>
    /// Gợi ý kỳ lương tiếp theo + danh sách các khoảng ngày bị "hổng" (chưa thuộc kỳ lương nào).
    /// Quy tắc: các kỳ lương phải nối tiếp nhau liên tục, kỳ mới bắt đầu đúng ngày sau ngày kết thúc của kỳ trước.
    /// </summary>
    public class PayrollPeriodSuggestionVM
    {
        /// <summary>Ngày bắt đầu bắt buộc/gợi ý cho kỳ mới.</summary>
        public DateTime SuggestedStartDate { get; set; }

        /// <summary>Ngày kết thúc gợi ý (tròn 1 tháng, không vượt quá hôm nay).</summary>
        public DateTime SuggestedEndDate { get; set; }

        /// <summary>Ngày kết thúc tối đa được phép chọn.</summary>
        public DateTime MaxEndDate { get; set; }

        /// <summary>true nếu đã có kỳ lương trước đó -> ngày bắt đầu bị khóa, không cho sửa.</summary>
        public bool IsStartLocked { get; set; }

        public DateTime? LastEndDate { get; set; }
        public string? LastTitle { get; set; }

        /// <summary>false nếu kỳ trước đã tính tới hôm nay -> chưa thể tạo kỳ mới.</summary>
        public bool CanCreate => SuggestedStartDate <= MaxEndDate;

        /// <summary>Các khoảng ngày nằm giữa 2 kỳ lương nhưng chưa được tính lương.</summary>
        public List<PayrollGapVM> Gaps { get; set; } = new();
    }

    public class PayrollGapVM
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int Days => (EndDate - StartDate).Days + 1;

        /// <summary>Ngày kết thúc tối đa khi tính bù (khoảng trống dài hơn 1 tháng phải tính bù nhiều lần).</summary>
        public DateTime MaxEndDate { get; set; }
    }
}
