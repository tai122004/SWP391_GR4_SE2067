using RWPM.Models.Entities;
using RWPM.Models.ViewModels.Performance;

namespace RWPM.Services.Abstraction
{
    /// <summary>Đánh giá hiệu suất: chỉ số chuyên cần theo tháng + điểm/nhận xét của quản lý.</summary>
    public interface IPerformanceService
    {
        /// <summary>Danh sách nhân viên trong phạm vi kèm chỉ số tháng (chưa phân trang).</summary>
        Task<List<PerformanceRowVM>> GetRowsAsync(PerformanceScope scope, int year, int month, int? storeId, string? search);

        PerformanceSummaryVM Summarize(IReadOnlyCollection<PerformanceRowVM> rows);

        Task<byte[]> ExportExcelAsync(IReadOnlyCollection<PerformanceRowVM> rows, int year, int month);

        /// <summary>Chi tiết tháng: chỉ số, danh sách ca và xu hướng 6 tháng; null nếu không có nhân viên.</summary>
        Task<PerformanceDetailVM?> GetDetailAsync(int employeeId, int year, int month);

        /// <summary>Thẻ điểm 12 tháng gần nhất (không trước tháng vào làm) của chính nhân viên.</summary>
        Task<PerformanceMyVM> GetMyAsync(int employeeId);

        /// <summary>Tạo/cập nhật đánh giá (upsert theo EmployeeId+Year+Month). Ném <see cref="ArgumentException"/> khi dữ liệu không hợp lệ.</summary>
        Task SaveReviewAsync(PerformanceReviewInput input, string reviewerUsername);

        Task<Employee?> GetEmployeeAsync(int employeeId);

        Task<Employee?> GetEmployeeByUsernameAsync(string username);
    }
}
