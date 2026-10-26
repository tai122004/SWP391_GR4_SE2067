namespace RWPM.Models.ViewModels.Performance
{
    /// <summary>
    /// Chỉ số chuyên cần của một nhân viên trong một tháng dương lịch, tính trực tiếp từ
    /// ca đăng ký đã duyệt, bản ghi chấm công và đơn nghỉ phép đã duyệt.
    /// Được serialize vào PerformanceReview.MetricsSnapshotJson lúc đánh giá.
    /// </summary>
    public class PerformanceMetrics
    {
        /// <summary>Số ca đăng ký đã duyệt trong tháng.</summary>
        public int ApprovedShifts { get; set; }

        /// <summary>Số ca đã duyệt có chấm công vào (check-in).</summary>
        public int AttendedShifts { get; set; }

        /// <summary>Số ca đã đến hạn (đã đi làm hoặc vắng) — mẫu số của tỉ lệ đi làm.
        /// Không tính ca chưa diễn ra/chưa kết thúc và ca thuộc ngày nghỉ phép đã duyệt.</summary>
        public int DueShifts { get; set; }

        /// <summary>Tỉ lệ đi làm (%) = AttendedShifts / DueShifts; null khi chưa có ca đến hạn.</summary>
        public double? AttendanceRate { get; set; }

        /// <summary>Ca đã duyệt, đã kết thúc, không có check-in và không có nghỉ phép đã duyệt.</summary>
        public int Absences { get; set; }

        public int LateCount { get; set; }
        public int LateMinutes { get; set; }
        public int EarlyLeaveCount { get; set; }
        public int EarlyLeaveMinutes { get; set; }
        public int MissingCheckOutCount { get; set; }

        /// <summary>Số ca trùng ngày nghỉ phép đã duyệt (kể cả ca đã bị hủy khi duyệt đơn nghỉ).</summary>
        public int LeaveShifts { get; set; }

        public double WorkedHours { get; set; }
        public int PaidLeaveDays { get; set; }
        public int UnpaidLeaveDays { get; set; }

        /// <summary>Điểm chuyên cần gợi ý 1–5 (xem <see cref="PerformanceScoring"/>); null khi chưa có dữ liệu.</summary>
        public double? SuggestedScore { get; set; }
    }

    /// <summary>
    /// Công thức điểm chuyên cần gợi ý:
    ///   điểm = 5 − 1 × số buổi vắng − 0,5 × số lần đi muộn − 0,5 × số lần về sớm − 0,25 × số lần thiếu check-out,
    ///   làm tròn 1 chữ số thập phân, tối thiểu 1, tối đa 5.
    /// Không có ca đến hạn và không có chấm công trong tháng → không gợi ý (null).
    /// </summary>
    public static class PerformanceScoring
    {
        public const double MaxScore = 5;
        public const double MinScore = 1;
        public const double AbsencePenalty = 1.0;
        public const double LatePenalty = 0.5;
        public const double EarlyLeavePenalty = 0.5;
        public const double MissingCheckOutPenalty = 0.25;

        public static double? Suggest(PerformanceMetrics m, bool hasAnyAttendance)
        {
            if (m.DueShifts == 0 && !hasAnyAttendance) return null;
            var score = MaxScore
                - AbsencePenalty * m.Absences
                - LatePenalty * m.LateCount
                - EarlyLeavePenalty * m.EarlyLeaveCount
                - MissingCheckOutPenalty * m.MissingCheckOutCount;
            return Math.Round(Math.Clamp(score, MinScore, MaxScore), 1);
        }
    }
}
