using RWPM.Models.ViewModels.ShiftDemand;

namespace RWPM.Services.Abstraction
{
    /// <summary>Độ phủ của một ca tại một chi nhánh trong một ngày.</summary>
    public sealed class ShiftCoverageRow
    {
        public int StoreId { get; init; }
        public string StoreName { get; init; } = string.Empty;
        public int ShiftId { get; init; }
        public string ShiftName { get; init; } = string.Empty;
        public TimeSpan StartTime { get; init; }
        public TimeSpan EndTime { get; init; }
        public DateTime Date { get; init; }
        /// <summary>Số người cần; null = chưa cấu hình nhu cầu.</summary>
        public int? Required { get; init; }
        /// <summary>true nếu số cần lấy từ ghi đè theo ngày.</summary>
        public bool IsOverride { get; init; }
        public int Approved { get; init; }
        public int Pending { get; init; }
        /// <summary>Required - Approved (tối thiểu 0); 0 nếu chưa cấu hình.</summary>
        public int Shortage => Required.HasValue ? Math.Max(0, Required.Value - Approved) : 0;
        /// <summary>Approved - Required khi vượt; 0 nếu không vượt hoặc chưa cấu hình.</summary>
        public int Over => Required.HasValue ? Math.Max(0, Approved - Required.Value) : 0;
    }

    public interface IShiftDemandService
    {
        /// <summary>
        /// Độ phủ cho khoảng ngày [from, to] (tính theo WorkDate), chỉ các ca đang áp dụng (StoreShift active, Shift active, trong hiệu lực).
        /// storeId null = mọi chi nhánh đang hoạt động. Approved/Pending đếm ShiftRegistration theo StoreId + ShiftId + WorkDate.
        /// </summary>
        Task<List<ShiftCoverageRow>> GetCoverageAsync(int? storeId, DateTime from, DateTime to);
            // ===================== Bổ sung (ShiftDemand) =====================

        /// <summary>Phạm vi của người dùng: Admin/HR mọi chi nhánh; Quản lý cửa hàng chỉ chi nhánh của mình; còn lại không truy cập.</summary>
        Task<DemandScope> GetScopeAsync(string username);

        /// <summary>Chi nhánh đang hoạt động trong phạm vi.</summary>
        Task<List<DemandStoreOption>> GetStoresAsync(DemandScope scope);

        /// <summary>Ca đang gắn (StoreShift active, Shift active) với chi nhánh. onDate có giá trị: chỉ ca trong hiệu lực ngày đó; null: ca chưa hết hiệu lực (EffectiveTo &gt;= hôm nay).</summary>
        Task<List<DemandShiftInfo>> GetStoreShiftsAsync(int storeId, DateTime? onDate = null);

        /// <summary>Mẫu nhu cầu theo thứ của chi nhánh (hàng = ca chưa hết hiệu lực).</summary>
        Task<WeeklyTemplateVM> GetWeeklyTemplateAsync(int storeId);

        /// <summary>Lưu mẫu tuần: RequiredCount null = xóa ô; 0–100 = thêm/cập nhật. Kiểm tra phạm vi theo actor.</summary>
        Task SaveWeeklyTemplateAsync(int storeId, IEnumerable<WeeklyTemplateCellInput> cells, string actor);

        /// <summary>Ghi đè theo ngày: sắp tới (tăng dần) trước, sau đó các ngày đã qua trong pastDays ngày gần nhất (giảm dần).</summary>
        Task<List<ShiftDemandOverrideItem>> GetOverridesAsync(int storeId, int pastDays = 30);

        /// <summary>Tạo (Id null/0) hoặc sửa ghi đè. Ngày &gt;= hôm nay, duy nhất theo chi nhánh+ca+ngày, ghi chú &lt;= 255. Trả về Id.</summary>
        Task<int> SaveOverrideAsync(ShiftDemandOverrideInput input, string actor);

        /// <summary>Xóa ghi đè (chỉ ngày hôm nay trở đi).</summary>
        Task DeleteOverrideAsync(int shiftDemandId, string actor);

        /// <summary>Sao chép mẫu tuần từ chi nhánh khác (chỉ HR/Admin) cho các ca áp dụng ở chi nhánh đích. Trả về số ô đã chép.</summary>
        Task<int> CopyTemplateAsync(int fromStoreId, int toStoreId, string actor);

        /// <summary>Độ phủ của một ca/chi nhánh/ngày; null nếu ca không áp dụng tại chi nhánh vào ngày đó.</summary>
        Task<ShiftCoverageRow?> GetCoverageForAsync(int storeId, int shiftId, DateTime date);

        /// <summary>
        /// Cảnh báo vượt nhu cầu nếu duyệt thêm "adding" người (gộp theo chi nhánh+ca+ngày).
        /// Mỗi dòng dạng "Ca Tối 26/10 tại ABC: cần 4, sẽ duyệt 5 (vượt 1)". Ca chưa cấu hình không cảnh báo. Không chặn.
        /// </summary>
        Task<List<string>> GetOverDemandWarningsAsync(IEnumerable<(int storeId, int shiftId, DateTime date, int adding)> items);

        /// <summary>Chi tiết ô độ phủ: đã duyệt, chờ duyệt, gợi ý nhân viên (kiểm tra phạm vi theo actor).</summary>
        Task<CoverageCellDetail> GetCellDetailAsync(int storeId, int shiftId, DateTime date, string actor);
    }
}
