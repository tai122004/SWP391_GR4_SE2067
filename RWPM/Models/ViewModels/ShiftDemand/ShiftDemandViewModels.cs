using RWPM.Services.Abstraction;

namespace RWPM.Models.ViewModels.ShiftDemand
{
    /// <summary>Phạm vi quản lý nhu cầu nhân sự của người dùng hiện tại (đọc từ CSDL theo tài khoản).</summary>
    public sealed class DemandScope
    {
        /// <summary>Được truy cập màn hình nhu cầu / độ phủ ca.</summary>
        public bool CanAccess { get; init; }
        /// <summary>HR/Admin: mọi chi nhánh.</summary>
        public bool AllStores { get; init; }
        /// <summary>Quản lý cửa hàng: chi nhánh của chính mình.</summary>
        public int? OwnStoreId { get; init; }
        public string? OwnStoreName { get; init; }
        /// <summary>Hồ sơ nhân viên của người dùng (nếu có) — dùng để chặn tự phân công.</summary>
        public int? OwnEmployeeId { get; init; }

        public bool CanAccessStore(int storeId) => CanAccess && (AllStores || OwnStoreId == storeId);
    }

    public sealed class DemandStoreOption
    {
        public int StoreId { get; init; }
        public string StoreCode { get; init; } = string.Empty;
        public string StoreName { get; init; } = string.Empty;
    }

    /// <summary>Thông tin ca dùng cho các lưới nhu cầu / độ phủ.</summary>
    public sealed class DemandShiftInfo
    {
        public int ShiftId { get; init; }
        public string ShiftName { get; init; } = string.Empty;
        public TimeSpan StartTime { get; init; }
        public TimeSpan EndTime { get; init; }
        public byte EndDayOffset { get; init; }
        public DateTime EffectiveFrom { get; init; }
        public DateTime? EffectiveTo { get; init; }

        public string TimeRange => FormatRange(StartTime, EndTime, EndDayOffset > 0 || EndTime <= StartTime);

        public static string FormatRange(TimeSpan start, TimeSpan end, bool nextDay) =>
            $"{start:hh\\:mm} – {end:hh\\:mm}{(nextDay ? " (+1)" : "")}";
    }

    // ===================== Mẫu theo tuần =====================

    public sealed class WeeklyTemplateVM
    {
        public int StoreId { get; init; }
        public List<DemandShiftInfo> Shifts { get; init; } = new();
        /// <summary>Khóa "{shiftId}_{(int)DayOfWeek}" -> số người cần.</summary>
        public Dictionary<string, int> Values { get; init; } = new();

        public static string Key(int shiftId, DayOfWeek day) => $"{shiftId}_{(int)day}";
        public int? Get(int shiftId, DayOfWeek day) => Values.TryGetValue(Key(shiftId, day), out var v) ? v : null;
    }

    public sealed class WeeklyTemplateCellInput
    {
        public int ShiftId { get; set; }
        /// <summary>0 = Chủ nhật ... 6 = Thứ bảy.</summary>
        public int DayOfWeek { get; set; }
        /// <summary>null = xóa cấu hình (chưa cấu hình).</summary>
        public int? RequiredCount { get; set; }
    }

    public sealed class SaveWeeklyTemplateInput
    {
        public int StoreId { get; set; }
        public List<WeeklyTemplateCellInput>? Cells { get; set; }
    }

    // ===================== Ghi đè theo ngày =====================

    public sealed class ShiftDemandOverrideItem
    {
        public int ShiftDemandId { get; init; }
        public int StoreId { get; init; }
        public int ShiftId { get; init; }
        public string ShiftName { get; init; } = string.Empty;
        public string TimeRange { get; init; } = string.Empty;
        public DateTime Date { get; init; }
        public int RequiredCount { get; init; }
        public string? Note { get; init; }
        /// <summary>Giá trị theo mẫu tuần của cùng ca/thứ (để so sánh).</summary>
        public int? TemplateValue { get; init; }
        public bool IsPast { get; init; }
        public string? ModifiedBy { get; init; }
        public DateTime? ModifiedAt { get; init; }
    }

    public sealed class ShiftDemandOverrideInput
    {
        /// <summary>null/0 = tạo mới.</summary>
        public int? Id { get; set; }
        public int StoreId { get; set; }
        public int ShiftId { get; set; }
        public DateTime? Date { get; set; }
        public int? RequiredCount { get; set; }
        public string? Note { get; set; }
    }

    public sealed class CopyTemplateInput
    {
        public int FromStoreId { get; set; }
        public int ToStoreId { get; set; }
    }

    public sealed class ShiftDemandIndexVM
    {
        public int? StoreId { get; init; }
        public string? StoreName { get; init; }
        public bool IsStoreManager { get; init; }
        public List<DemandStoreOption> Stores { get; init; } = new();
        public WeeklyTemplateVM Template { get; init; } = new();
        public List<ShiftDemandOverrideItem> Overrides { get; init; } = new();
        public DateTime Today { get; init; } = DateTime.Today;
    }

    // ===================== Độ phủ ca =====================

    public sealed class CoverageSummary
    {
        public int ShortShifts { get; init; }
        public int MissingPeople { get; init; }
        public int OverShifts { get; init; }
        public int NotConfigured { get; init; }
        public int TotalCells { get; init; }

        public static CoverageSummary From(IEnumerable<ShiftCoverageRow> rows)
        {
            var list = rows.ToList();
            return new CoverageSummary
            {
                ShortShifts = list.Count(x => x.Shortage > 0),
                MissingPeople = list.Sum(x => x.Shortage),
                OverShifts = list.Count(x => x.Over > 0),
                NotConfigured = list.Count(x => !x.Required.HasValue),
                TotalCells = list.Count
            };
        }
    }

    public sealed class CoverageWeekVM
    {
        public int? StoreId { get; init; }
        public string? StoreName { get; init; }
        public bool IsStoreManager { get; init; }
        public List<DemandStoreOption> Stores { get; init; } = new();
        public DateTime WeekStart { get; init; }
        public List<DateTime> Days { get; init; } = new();
        public List<DemandShiftInfo> Shifts { get; init; } = new();
        public List<ShiftCoverageRow> Rows { get; init; } = new();
        public CoverageSummary Summary { get; init; } = new();
        public DateTime Today { get; init; } = DateTime.Today;

        private Dictionary<string, ShiftCoverageRow>? _index;
        public ShiftCoverageRow? Get(int shiftId, DateTime date)
        {
            _index ??= Rows.GroupBy(r => $"{r.ShiftId}_{r.Date:yyyyMMdd}").ToDictionary(g => g.Key, g => g.First());
            return _index.TryGetValue($"{shiftId}_{date:yyyyMMdd}", out var row) ? row : null;
        }
    }

    public sealed class CoverageEmployeeItem
    {
        public int ShiftRegistrationId { get; init; }
        public int EmployeeId { get; init; }
        public string EmployeeCode { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public string? Note { get; init; }
        public string? ApprovedBy { get; init; }
        public DateTime? ApprovedAt { get; init; }
        public DateTime CreatedDate { get; init; }
        public string? CreatedBy { get; init; }
    }

    public sealed class CoverageSuggestion
    {
        public int EmployeeId { get; init; }
        public string EmployeeCode { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public bool IsAvailable { get; init; }
        /// <summary>Lý do không phù hợp (đã bản địa hóa) khi IsAvailable = false.</summary>
        public string? Reason { get; init; }
        public bool HasDeclaredAvailability { get; init; }
    }

    public sealed class CoverageCellDetail
    {
        public ShiftCoverageRow Coverage { get; init; } = null!;
        public string TimeRange { get; init; } = string.Empty;
        public bool IsPast { get; init; }
        public List<CoverageEmployeeItem> Approved { get; init; } = new();
        public List<CoverageEmployeeItem> Pending { get; init; } = new();
        public List<CoverageSuggestion> Suggestions { get; init; } = new();
    }

    public sealed class CoverageAssignInput
    {
        public int StoreId { get; set; }
        public int ShiftId { get; set; }
        public DateTime? Date { get; set; }
        public int EmployeeId { get; set; }
        /// <summary>Người dùng đã xác nhận phân công dù vượt nhu cầu.</summary>
        public bool ConfirmOver { get; set; }
    }
}
