using System.Text.RegularExpressions;
using RWPM.Common.Enums;
using R = RWPM.Resources.Shared.PayrollResource;

namespace RWPM.Models.ViewModels.Payroll
{
    /// <summary>
    /// Some payroll texts are generated in Vietnamese at calculation time and persisted to the DB
    /// (Payroll.Title, PayrollItemSnapshot.Status/Note/ShiftName, PayrollDetail.WarningNotes/StoreName,
    /// PayrollAuditLog.Details/Reason). The stored values are kept unchanged; this helper translates
    /// the known stored values into the current UI culture at display time. Unknown values are returned as-is.
    /// </summary>
    public static class PayrollDisplayText
    {
        // ---- Values persisted to the DB (do not change: existing rows contain these exact strings) ----
        public const string StoredTitlePrefix = "Bảng lương ";
        public const string SnapValid = "Hợp lệ";
        public const string SnapMissingInOut = "Thiếu giờ vào/ra";
        public const string SnapLateAndEarly = "Đi muộn & Về sớm";
        public const string SnapLate = "Đi muộn";
        public const string SnapEarly = "Về sớm";
        public const string SnapPaidLeave = "Nghỉ phép hưởng lương";
        public const string SnapUnpaidLeave = "Nghỉ không lương";
        public const string SnapAbsentMissing = "Vắng / Thiếu chấm công";
        public const string NoteAttendanceIncomplete = "Chưa hoàn tất chấm công";
        public const string NoteUnpaidLeave = "Nghỉ không hưởng lương";
        public const string NoteUnpaidLeaveNonEligible = "Part-time/Thử việc nghỉ không hưởng lương";
        public const string ShiftUnknown = "Không xác định";
        public const string ShiftFallback = "Ca";
        public const string MissingInOutTag = "(Thiếu vào/ra)";
        public const string WarnMissingRate = "Chưa thiết lập đơn giá theo giờ (HourlyRate).";
        public const string StoreOfficeHR = "Văn phòng / HR";
        public const string DefaultUnlockReason = "HR mở khóa bảng lương";

        public static string StoredTitle(DateTime start, DateTime end) => $"{StoredTitlePrefix}{start:dd/MM/yyyy} - {end:dd/MM/yyyy}";
        public static string StoredLeaveApprovedNote(int leaveRequestId) => $"Đơn nghỉ phép #{leaveRequestId} đã duyệt";
        public static string StoredShiftNoAttendanceNote(string? shiftName) => $"Ca {shiftName ?? ""} đã duyệt nhưng không có chấm công";
        public static string StoredAbnormalWarning(int count, string dates) => $"Có {count} ca bất thường/thiếu chấm công ({dates}).";
        public static string StoredAuditCalculate(bool isRecalculate, int employees, double hours, decimal amount)
            => $"{(isRecalculate ? "Tính lại" : "Khởi tạo")} bảng lương: {employees} nhân viên, tổng giờ công: {hours:N2}h, tổng tiền: {amount:N0} VNĐ";
        public static string StoredAuditLock(int employees, decimal amount)
            => $"Chốt bảng lương thành công với {employees} nhân viên, tổng quỹ lương: {amount:N0} VNĐ";
        public static string StoredAuditUnlock(string reason) => $"Mở khóa bảng lương về trạng thái Nháp. Lý do: {reason}";
        public static string StoredUnlockNote(string operatorUsername, DateTime at, string reason)
            => $"[Mở khóa bởi {operatorUsername} lúc {at:dd/MM/yyyy HH:mm}]: {reason}";

        private static readonly Regex TitleRx = new(@"^Bảng lương (\d{2}/\d{2}/\d{4}) - (\d{2}/\d{2}/\d{4})$", RegexOptions.Compiled);
        private static readonly Regex LeaveNoteRx = new(@"^Đơn nghỉ phép #(\d+) đã duyệt$", RegexOptions.Compiled);
        private static readonly Regex ShiftNoteRx = new(@"^Ca (.*) đã duyệt nhưng không có chấm công$", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex AbnormalRx = new(@"^Có (\d+) ca bất thường/thiếu chấm công \((.*)\)\.$", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex AuditCalcRx = new(@"^(Tính lại|Khởi tạo) bảng lương: (\d+) nhân viên, tổng giờ công: (.+)h, tổng tiền: (.+) VNĐ$", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex AuditLockRx = new(@"^Chốt bảng lương thành công với (\d+) nhân viên, tổng quỹ lương: (.+) VNĐ$", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex AuditUnlockRx = new(@"^Mở khóa bảng lương về trạng thái Nháp\. Lý do: (.*)$", RegexOptions.Compiled | RegexOptions.Singleline);

        /// <summary>Payroll title (auto-generated "Bảng lương dd/MM/yyyy - dd/MM/yyyy").</summary>
        public static string Title(string? title)
        {
            if (string.IsNullOrEmpty(title)) return title ?? string.Empty;
            var m = TitleRx.Match(title);
            return m.Success ? string.Format(R.Payroll_AutoTitle, m.Groups[1].Value, m.Groups[2].Value) : title;
        }

        public static string PayrollStatusText(PayrollStatus status) => status switch
        {
            PayrollStatus.Locked => R.Status_Locked,
            PayrollStatus.Draft => R.Status_Draft,
            _ => status.ToString()
        };

        public static string PayrollStatusText(PayrollStatus? status) => status.HasValue ? PayrollStatusText(status.Value) : string.Empty;

        public static string DetailStatusText(PayrollDetailStatus status) => status switch
        {
            PayrollDetailStatus.Valid => R.DetailStatus_Valid,
            PayrollDetailStatus.MissingRate => R.DetailStatus_MissingRate,
            PayrollDetailStatus.HasWarnings => R.DetailStatus_HasWarnings,
            _ => string.Empty
        };

        public static string SnapshotStatus(string? status) => status switch
        {
            null => string.Empty,
            SnapValid => R.DetailStatus_Valid,
            SnapMissingInOut => R.Snap_MissingInOut,
            SnapLateAndEarly => R.Snap_LateAndEarly,
            SnapLate => R.Snap_Late,
            SnapEarly => R.Snap_Early,
            SnapPaidLeave => R.Card_PaidLeave,
            SnapUnpaidLeave => R.Snap_UnpaidLeave,
            SnapAbsentMissing => R.Snap_AbsentMissing,
            _ => status
        };

        public static string SnapshotNote(string? note)
        {
            if (string.IsNullOrEmpty(note)) return note ?? string.Empty;
            switch (note)
            {
                case NoteAttendanceIncomplete: return R.Note_AttendanceIncomplete;
                case NoteUnpaidLeave: return R.Note_UnpaidLeave;
                case NoteUnpaidLeaveNonEligible: return R.Note_UnpaidLeaveNonEligible;
            }
            var m = LeaveNoteRx.Match(note);
            if (m.Success) return string.Format(R.Note_LeaveApproved, m.Groups[1].Value);
            m = ShiftNoteRx.Match(note);
            if (m.Success) return string.Format(R.Note_ShiftNoAttendance, m.Groups[1].Value);
            return note;
        }

        public static string ShiftName(string? shiftName) => shiftName == ShiftUnknown ? R.Shift_Unknown : shiftName ?? string.Empty;

        public static string StoreName(string? storeName) => storeName == StoreOfficeHR ? R.Store_OfficeHR : storeName ?? string.Empty;

        public static string WarningNotes(string? notes)
        {
            if (string.IsNullOrEmpty(notes)) return notes ?? string.Empty;
            if (notes == WarnMissingRate) return R.Warn_MissingRate;
            var m = AbnormalRx.Match(notes);
            if (!m.Success) return notes;
            var dates = m.Groups[2].Value
                .Replace(MissingInOutTag, "(" + R.Warn_MissingInOutTag + ")")
                .Replace("(" + ShiftFallback + ")", "(" + R.Warn_ShiftFallback + ")");
            return string.Format(R.Warn_Abnormal, m.Groups[1].Value, dates);
        }

        public static string AuditAction(string? action) => action switch
        {
            "Calculate" => R.AuditAction_Calculate,
            "Recalculate" => R.AuditAction_Recalculate,
            "Lock" => R.AuditAction_Lock,
            "Unlock" => R.AuditAction_Unlock,
            _ => action ?? string.Empty
        };

        public static string AuditReason(string? reason) => reason == DefaultUnlockReason ? R.Unlock_DefaultReason : reason ?? string.Empty;

        public static string AuditDetails(string? details)
        {
            if (string.IsNullOrEmpty(details)) return details ?? string.Empty;
            var m = AuditCalcRx.Match(details);
            if (m.Success)
                return string.Format(m.Groups[1].Value == "Tính lại" ? R.AuditDetail_Recalculate : R.AuditDetail_Calculate,
                    m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value);
            m = AuditLockRx.Match(details);
            if (m.Success) return string.Format(R.AuditDetail_Lock, m.Groups[1].Value, m.Groups[2].Value);
            m = AuditUnlockRx.Match(details);
            if (m.Success) return string.Format(R.AuditDetail_Unlock, AuditReason(m.Groups[1].Value));
            return details;
        }
    }
}
