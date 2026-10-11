# TÀI LIỆU ĐẶC TẢ VÀ THIẾT KẾ TÍNH NĂNG PAYROLL MANAGEMENT (QUẢN LÝ TÍNH LƯƠNG)
**Dự án**: RWPM – Hệ thống Quản trị Nhân sự & Chấm công Chuỗi Bán lẻ Thời trang  
**Nền tảng**: ASP.NET Core (.NET 8), Entity Framework Core 6, SQL Server, ClosedXML  
**Ngày lập**: 11/10/2026  

---

## MỤC LỤC
1. [Tổng quan & Mục tiêu](#1-tổng-quan--mục-tiêu)
2. [Phân tích hiện trạng Source Code & Dữ liệu](#2-phân-tích-hiện-trạng-source-code--dữ-liệu)
3. [Quy tắc nghiệp vụ tính lương chi tiết](#3-quy-tắc-nghiệp-vụ-tính-lương-chi-tiết)
4. [Thiết kế Cơ sở Dữ liệu & Entity Model (Snapshot Pattern)](#4-thiết-kế-cơ-sở-dữ-liệu--entity-model-snapshot-pattern)
5. [Thiết kế Tầng Nghiệp vụ (Service & Logic)](#5-thiết-kế-tầng-nghiệp-vụ-service--logic)
6. [Thiết kế Phân quyền Server-Side](#6-thiết-kế-phân-quyền-server-side)
7. [Thiết kế Giao diện (UI/UX) & Điều hướng](#7-thiết-kế-giao-diện-uiux--điều-hướng)
8. [Đặc tả Xuất Excel bằng ClosedXML](#8-đặc-tả-xuất-excel-bằng-closedxml)
9. [Kịch bản Kiểm thử Nghiệp vụ (Test Scenarios)](#9-kịch-bản-kiểm-thử-nghiệp-vụ-test-scenarios)
10. [Danh mục File Triển khai](#10-danh-mục-file-triển-khai)

---

## 1. TỔNG QUAN & MỤC TIÊU

Hệ thống RWPM hiện đã có phân hệ quản lý nhân viên, đăng ký ca làm, chấm công GPS/hình ảnh, điều chỉnh công và quản lý đơn nghỉ phép. Tính năng **Payroll Management** được xây dựng nhằm:
- Tổng hợp giờ làm việc thực tế được tính lương từ dữ liệu chấm công đã chuẩn hóa.
- Áp dụng đơn giá tính lương theo giờ (`HourlyRate`) cho toàn bộ nhân viên (bao gồm Full-time và Part-time).
- Cung cấp màn hình quản lý bảng lương theo kỳ (tháng/năm), xem chi tiết, phát hiện cảnh báo bất thường và chốt bảng lương.
- Đảm bảo tính bất biến của dữ liệu (Data Immutability / Snapshot): khi bảng lương đã chốt (`Locked`), các biến động về chấm công hay mức lương sau đó không được làm sai lệch số liệu lịch sử.
- Phân quyền chặt chẽ giữa Admin/HR (quản lý toàn quyền), StoreManager (chỉ xem trong chi nhánh) và Employee (chỉ xem phiếu lương cá nhân của chính mình).
- Xuất bảng lương ra Excel chuyên nghiệp bằng thư viện ClosedXML.

---

## 2. PHÂN TÍCH HIỆN TRẠNG SOURCE CODE & DỮ LIỆU

### 2.1. Entity `Employee` (`Employee.cs`)
- **Khóa chính**: `EmployeeId` (int, Identity).
- **Mã nhân viên**: `EmployeeCode` (nvarchar(20), Unique).
- **Tài khoản**: `Username` (nvarchar(30), Unique, FK đến bảng `Acc`).
- **Chi nhánh**: `StoreId` (int, FK đến bảng `Store`).
- **Hình thức làm việc**: `EmploymentType` (enum: `FullTime = 1`, `PartTime = 2`, `Intern = 3`).
- **Mức lương theo giờ**: `HourlyRate` (`decimal(18,2)?`, nullable) – **Đây là trường dữ liệu duy nhất dùng để tính lương**.
- **Lương cơ bản**: `BaseSalary` (`decimal(18,2)?`, nullable) – **Không sử dụng** làm công thức tính lương tháng theo yêu cầu nghiệp vụ.
- **Trạng thái**: `IsActive` (bool), `Status` (`Official = 1`, `Probation = 2`, `Suspended = 3`, `Resigned = 4`).

### 2.2. Entity `AttendanceRecord` (`AttendanceRecord.cs`) & Logic `WorkHours`
- **Khóa chính**: `AttendanceId` (int).
- **Liên kết**: `Username` (nvarchar(30), FK đến `Acc`), `ShiftId` (int?, FK đến `Shift`).
- **Ngày làm việc**: `Date` (DateTime, kiểu SQL `date`).
- **Giờ vào / ra**: `CheckInTime` (TimeSpan?), `CheckOutTime` (TimeSpan?).
- **Cờ điều chỉnh**: `IsAdjusted` (bool, mặc định `false`, chuyển thành `true` khi HR/Admin can thiệp qua `AttendanceService.AdjustAttendanceAsync`).
- **Cách tính `WorkHours` hiện tại**:
  * Là thuộc tính tính toán không lưu DB: `[NotMapped] public double? WorkHours`.
  * Yêu cầu tiên quyết: Phải có `Shift != null`, `CheckInTime.HasValue` và `CheckOutTime.HasValue`. Nếu thiếu bất kỳ điều kiện nào, trả về `null`.
  * Khớp thời gian thực tế vào khung giờ ca làm:
    * `shiftRange = ShiftTimeHelper.GetDateTimeRange(Date, Shift)`
    * `lateMins = (checkInActual - shiftRange.StartAt).TotalMinutes`
    * `earlyMins = (shiftRange.EndAt - checkOutActual).TotalMinutes`
  * Khi `IsAdjusted == false` (áp dụng luật tự động của hệ thống):
    * `lateMins > 60`: Phạt đi muộn quá 1 tiếng $\rightarrow$ `return 0` (mất trắng ca).
    * `0 < lateMins <= 15`: Châm chước 15 phút đầu $\rightarrow$ coi như vào đúng giờ (`checkInActual = shiftRange.StartAt`).
    * `0 < earlyMins <= 15`: Châm chước 15 phút về sớm $\rightarrow$ coi như về đúng giờ (`checkOutActual = shiftRange.EndAt`).
  * Khi `IsAdjusted == true`: Bỏ qua luật phạt tự động vì đã được HR thẩm định và duyệt điều chỉnh.
  * Gọi hàm lõi `ShiftTimeHelper.GetWorkedHours(Date, Shift, checkInActual, checkOutActual)`:
    * Khớp trong khung ca làm việc (`StartAt` đến `EndAt`).
    * Tự động trừ thời gian nghỉ giải lao (`BreakStartTime` đến `BreakEndTime`) nếu ca có cấu hình nghỉ.
    * Trả về số giờ: $\text{Minutes} / 60.0$.

### 2.3. Entity `Shift` (`Shift.cs`)
- Có thuộc tính `[NotMapped] public double ScheduledWorkHours`:
  * Tính số giờ làm việc chuẩn của ca làm việc sau khi đã trừ giờ giải lao.
  * Dùng để tính tổng giờ phân ca theo lịch của nhân viên (thông qua bảng `ShiftRegistration` có trạng thái `Approved`) để đối chiếu với giờ thực tế.

### 2.4. Entity `LeaveRequest` (`LeaveRequest.cs`)
- Quản lý đơn nghỉ phép theo `EmployeeId`, `StoreId`, khoảng ngày `StartDate` đến `EndDate`, và `Status` (`Approved`, `Pending`, `Rejected`).
- Dùng để thống kê số ngày nghỉ phép đã được phê duyệt trong kỳ đối chiếu cho HR.
- **Không tự động cộng giờ nghỉ vào giờ làm thực tế**.

---

## 3. QUY TẮC NGHIỆP VỤ TÍNH LƯƠNG CHI TIẾT

### 3.1. Công thức tính lương theo giờ
Tất cả nhân viên (Full-time, Part-time, Intern) đều được tính theo công thức:
$$\text{Lương thực nhận} = \text{Tổng số giờ làm việc được tính lương} \times \text{Đơn giá theo giờ (HourlyRate)}$$
*Ví dụ*:
- Đơn giá theo giờ (`HourlyRate`): 30.000 VNĐ.
- Tổng giờ làm việc được tính lương trong kỳ: 120,5 giờ.
- Tổng lương kỳ đó: $120,5 \times 30.000 = 3.615.000$ VNĐ.

### 3.2. Quy tắc kiểm tra đơn giá (`HourlyRate`)
- Hệ thống lấy trực tiếp `HourlyRate` đã lưu trong `Employee.cs`.
- Tuyệt đối **không** sử dụng `BaseSalary` làm công thức tính lương.
- Không tự ý thiết lập mức lương mặc định (ví dụ không được gán 0 hay mức tối thiểu cố định).
- Nếu nhân viên có `HourlyRate == null` hoặc `<= 0`:
  * Trạng thái chi tiết: `MissingRate`.
  * Hiển thị cảnh báo màu vàng/đỏ: *"Chưa thiết lập đơn giá theo giờ (HourlyRate)"*.
  * Tạm thời tính tiền $= 0$ và nhắc nhở HR cập nhật `HourlyRate` cho nhân viên tại chức năng Quản lý nhân viên trước khi chốt bảng lương.

### 3.3. Quy tắc tổng hợp giờ làm việc từ `AttendanceRecord`
- **Xác định kỳ lương**: Kỳ lương tháng $M$, năm $Y$:
  * `StartDate`: ngày 01 tháng $M$ năm $Y$.
  * `EndDate`: ngày cuối cùng của tháng $M$ năm $Y$.
- **Truy vấn chấm công**: Lấy các `AttendanceRecord` có `Date >= StartDate && Date <= EndDate` kèm `.Include(a => a.Shift)`.
- **Khớp nối**: `AttendanceRecord.Username == Employee.Username`.
- **Ngăn chặn tính trùng**:
  * Gom nhóm bản ghi theo `AttendanceId` duy nhất hoặc nhóm theo `(Username, Date, ShiftId)`.
- **Xử lý bản ghi hợp lệ**:
  * Có đủ `CheckInTime`, `CheckOutTime` và `Shift != null` $\rightarrow$ `ar.WorkHours.HasValue`.
  * Cộng dồn `ar.WorkHours.Value` vào `TotalWorkHours`.
- **Xử lý bản ghi thiếu dữ liệu / bất thường**:
  * Nếu bản ghi thiếu CheckIn, thiếu CheckOut hoặc không xác định được Ca làm việc: `ar.WorkHours == null`.
  * **Tuyệt đối không tự suy đoán giờ làm**.
  * Tăng biến đếm `MissingAttendanceCount++`.
  * Tạo chuỗi cảnh báo: *"Có X ca làm việc thiếu dữ liệu vào/ra (Ngày: ...)"*.
  * Trạng thái chi tiết được chuyển sang `HasWarnings` để HR phát hiện và dùng tính năng *Điều chỉnh chấm công* xử lý.

---

## 4. THIẾT KẾ CƠ SỞ DỮ LIỆU & ENTITY MODEL (SNAPSHOT PATTERN)

Để đảm bảo tính bất biến của bảng lương sau khi chốt, toàn bộ số liệu tính toán, mức giá và thông tin nhân viên tại thời điểm tính sẽ được lưu thành **Snapshot** trong 2 bảng mới:

### 4.1. Entity `Payroll` (Kỳ lương)
```csharp
namespace RWPM.Models.Entities
{
    [Table("Payroll")]
    public class Payroll : AuditableEntity
    {
        [Key]
        public int PayrollId { get; set; }

        [Required]
        public int PeriodMonth { get; set; } // 1 - 12

        [Required]
        public int PeriodYear { get; set; }  // e.g. 2026

        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty; // "Bảng lương Tháng 10/2026"

        [Column(TypeName = "date")]
        public DateTime StartDate { get; set; }

        [Column(TypeName = "date")]
        public DateTime EndDate { get; set; }

        public PayrollStatus Status { get; set; } = PayrollStatus.Draft; // 1: Draft, 2: Locked

        public int TotalEmployees { get; set; } = 0;

        public double TotalWorkHours { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalGrossSalary { get; set; } = 0;

        [MaxLength(500)]
        public string? Note { get; set; }

        [MaxLength(30)]
        public string? LockedBy { get; set; }

        public DateTime? LockedDate { get; set; }

        // Navigation
        public virtual ICollection<PayrollDetail> Details { get; set; } = new List<PayrollDetail>();
    }
}
```

### 4.2. Entity `PayrollDetail` (Chi tiết lương nhân viên)
```csharp
namespace RWPM.Models.Entities
{
    [Table("PayrollDetail")]
    public class PayrollDetail
    {
        [Key]
        public int PayrollDetailId { get; set; }

        [Required]
        public int PayrollId { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        // Snapshot thông tin nhân viên
        [Required]
        [MaxLength(20)]
        public string EmployeeCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string EmployeeName { get; set; } = string.Empty;

        public int StoreId { get; set; }

        [MaxLength(150)]
        public string StoreName { get; set; } = string.Empty;

        public EmploymentType EmploymentType { get; set; }

        // Snapshot mức lương & giờ làm
        [Column(TypeName = "decimal(18,2)")]
        public decimal HourlyRate { get; set; } = 0; // Đơn giá cố định tại thời điểm tính

        public double TotalStandardHours { get; set; } = 0; // Giờ theo lịch phân ca

        public double TotalWorkHours { get; set; } = 0; // Giờ thực tế tính lương

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalSalary { get; set; } = 0; // TotalWorkHours * HourlyRate

        // Đối chiếu & Cảnh báo
        public int ApprovedLeaveDays { get; set; } = 0; // Số ngày nghỉ phép đã duyệt

        public int MissingAttendanceCount { get; set; } = 0; // Số ca bất thường/thiếu vào ra

        public PayrollDetailStatus Status { get; set; } = PayrollDetailStatus.Valid;

        [MaxLength(500)]
        public string? WarningNotes { get; set; }

        // Navigations
        [ForeignKey("PayrollId")]
        public virtual Payroll Payroll { get; set; } = null!;

        [ForeignKey("EmployeeId")]
        public virtual Employee Employee { get; set; } = null!;

        [ForeignKey("StoreId")]
        public virtual Store Store { get; set; } = null!;
    }
}
```

### 4.3. Các Enum hỗ trợ
```csharp
namespace RWPM.Common.Enums
{
    public enum PayrollStatus : byte
    {
        Draft = 1,   // Bản nháp / Mới tính (Cho phép tính lại)
        Locked = 2   // Đã chốt (Bất biến, không thể sửa trừ khi Unlock)
    }

    public enum PayrollDetailStatus : byte
    {
        Valid = 1,        // Hợp lệ, tính lương thành công
        MissingRate = 2,  // Chưa có đơn giá theo giờ
        HasWarnings = 3   // Có ca chấm công bất thường / thiếu vào ra
    }
}
```

### 4.4. Cấu hình Fluent API trong `DefaultDatabaseContext.cs`
- `Payroll`:
  * Unique Index trên bộ `(PeriodMonth, PeriodYear)`: Ngăn chặn tạo trùng kỳ lương cho cùng 1 tháng/năm.
- `PayrollDetail`:
  * Index trên `(PayrollId, EmployeeId)`: Ngăn chặn nhân viên bị tính trùng 2 lần trong cùng 1 bảng lương.
  * Cascade Delete từ `Payroll` sang `PayrollDetail` khi xóa kỳ lương ở trạng thái `Draft`.

---

## 5. THIẾT KẾ TẦNG NGHIỆP VỤ (SERVICE & LOGIC)

### 5.1. Interface `IPayrollService`
```csharp
namespace RWPM.Services.Abstraction
{
    public interface IPayrollService
    {
        Task<List<Payroll>> GetPayrollsAsync(int? year = null);
        Task<Payroll?> GetPayrollByIdAsync(int id);
        Task<PayrollDetailVM> GetPayrollDetailAsync(int payrollId, int? storeId = null, string? search = null);
        Task<Payroll> CalculatePayrollAsync(int month, int year, string operatorUsername);
        Task LockPayrollAsync(int payrollId, string operatorUsername);
        Task UnlockPayrollAsync(int payrollId, string reason, string operatorUsername);
        Task DeletePayrollAsync(int payrollId, string operatorUsername);
        Task<EmployeePayslipVM?> GetEmployeePayslipAsync(string username, int? month = null, int? year = null);
        Task<List<EmployeePayslipSummaryVM>> GetEmployeePayslipHistoryAsync(string username);
        Task<byte[]> ExportPayrollToExcelAsync(int payrollId, int? storeId = null);
    }
}
```

### 5.2. Quy trình Tính lương (`CalculatePayrollAsync`)
1. **Kiểm tra kỳ lương**:
    * Nếu kỳ $(M, Y)$ đã tồn tại và đang ở trạng thái `Locked` $\rightarrow$ Ném lỗi `InvalidOperationException("Kỳ lương này đã được chốt, không thể tính lại!")`.
    * Nếu kỳ $(M, Y)$ đã tồn tại ở trạng thái `Draft` $\rightarrow$ Xóa các `PayrollDetail` cũ để nạp lại snapshot mới.
2. **Khởi tạo chu kỳ**:
    * `StartDate = new DateTime(year, month, 1);`
    * `EndDate = StartDate.AddMonths(1).AddDays(-1);`
3. **Nạp dữ liệu nguồn**:
    * Nạp danh sách `Employee` (`IsActive == true` hoặc có phát sinh công trong kỳ) kèm `Account` và `Store`.
    * Nạp danh sách `AttendanceRecord` trong khoảng `[StartDate, EndDate]` kèm `Shift`.
    * Nạp danh sách `ShiftRegistration` có `Status == RegistrationStatus.Approved` trong khoảng `[StartDate, EndDate]` kèm `Shift`.
    * Nạp danh sách `LeaveRequest` có `Status == RegistrationStatus.Approved` giao cắt trong kỳ.
4. **Xử lý từng nhân viên**:
    * Lấy các bản ghi chấm công của nhân viên: `empAttendance = allAttendance.Where(a => a.Username == emp.Username).ToList();`
    * Tính giờ làm thực tế:
      * Gom các bản ghi có `ar.WorkHours.HasValue`: `totalWorkHours = empAttendance.Where(a => a.WorkHours.HasValue).Sum(a => a.WorkHours.Value);`
      * Đếm các bản ghi thiếu: `missingCount = empAttendance.Count(a => !a.WorkHours.HasValue);`
    * Tính giờ phân ca chuẩn:
      * `totalStandardHours = empRegistrations.Sum(r => r.Shift.ScheduledWorkHours);`
    * Thống kê nghỉ phép:
      * `approvedLeaveDays = empLeaves.Sum(l => (l.EndDate - l.StartDate).Days + 1);`
    * Kiểm tra đơn giá & tính tiền:
      * Nếu `!emp.HourlyRate.HasValue || emp.HourlyRate.Value <= 0`:
        * `detail.HourlyRate = 0;`
        * `detail.TotalSalary = 0;`
        * `detail.Status = PayrollDetailStatus.MissingRate;`
        * `detail.WarningNotes = "Chưa thiết lập đơn giá theo giờ (HourlyRate).";`
      * Nếu có ca thiếu dữ liệu vào/ra:
        * `detail.Status = PayrollDetailStatus.HasWarnings;`
        * `detail.WarningNotes = $"Có {missingCount} ca chấm công thiếu giờ vào/ra hoặc chưa hợp lệ.";`
      * Thành tiền: `detail.TotalSalary = Math.Round((decimal)detail.TotalWorkHours * detail.HourlyRate, 0);`
5. **Lưu DB và cập nhật tổng hợp**:
    * Cập nhật `TotalEmployees`, `TotalWorkHours`, `TotalGrossSalary` cho `Payroll`.
    * `await _context.SaveChangesAsync();`

---

## 6. THIẾT KẾ PHÂN QUYỀN SERVER-SIDE

Áp dụng cơ chế phân quyền bảo mật 2 lớp: `[Authorize(Roles = ...)]` ở Controller và Scoping logic ở Action:

| Role | Quản lý bảng lương (`/Payroll/Index`, `/Payroll/Detail`) | Chốt/Mở khóa kỳ lương | Xem bảng lương theo chi nhánh | Xem phiếu lương cá nhân (`/Payroll/MyPayslips`) |
| :--- | :---: | :---: | :---: | :---: |
| **Admin** | Có toàn quyền | Có | Xem toàn bộ hệ thống | Có |
| **HR** | Có toàn quyền | Có | Xem toàn bộ hệ thống | Có |
| **StoreManager** | Chỉ xem chi nhánh mình quản lý | Không | Bị giới hạn cố định: `storeId = currentEmp.StoreId` | Có |
| **Employee** | Bị từ chối (`Forbid`) | Không | Bị từ chối (`Forbid`) | Chỉ xem phiếu lương cá nhân của chính mình (chỉ các kỳ `Locked`) |

---

## 7. THIẾT KẾ GIAO DIỆN (UI/UX) & ĐIỀU HƯỚNG

Tuân thủ giao diện Bootstrap 5, tone màu xanh hiện đại, font chữ chuẩn và thẻ tóm tắt (Summary Cards) như các trang `Employee` và `Attendance` hiện có:

### 7.1. Màn hình Danh sách Kỳ lương (`/Payroll/Index`)
- **Header**: Tiêu đề trang, nút "Tính lương kỳ mới" (Modal chọn Tháng/Năm), Bộ lọc theo Năm.
- **Thẻ thống kê (Metric Cards)**: Tổng số kỳ lương, Tổng ngân sách lương năm, Số kỳ đã chốt, Số kỳ nháp.
- **Bảng dữ liệu**:
  * Tên kỳ lương (Tháng/Năm)
  * Trạng thái (Badge: `Draft` - Xanh dương nhạt / `Locked` - Xanh lá)
  * Tổng nhân viên
  * Tổng giờ làm
  * Tổng quỹ lương (Định dạng VNĐ)
  * Người tạo / Ngày tạo, Người chốt / Ngày chốt
  * Thao tác: Xem chi tiết, Chốt lương, Mở khóa, Xuất Excel, Xóa (nếu là Draft).

### 7.2. Màn hình Chi tiết Bảng lương Kỳ (`/Payroll/Detail?id={id}`)
- **Header**: Tên kỳ lương, Badge trạng thái (`Draft` hoặc `Locked`), nút "Chốt bảng lương", nút "Xuất Excel", nút "Tính lại" (nếu là Draft).
- **Bộ lọc nhanh**: Lọc theo Chi nhánh (Store SelectList), Tìm kiếm theo Mã/Tên nhân viên, Lọc theo Trạng thái (Hợp lệ, Thiếu đơn giá, Có cảnh báo).
- **Cảnh báo tổng quan**: Banner thông báo màu vàng/đỏ nếu phát hiện có nhân viên thiếu `HourlyRate` hoặc ca chấm công bất thường.
- **Bảng dữ liệu chi tiết**:
  * Mã NV, Họ tên NV, Chi nhánh, Hình thức (Full-time/Part-time)
  * Giờ theo lịch (Scheduled Hours)
  * Giờ thực tế tính lương (Actual Paid Hours)
  * Số ngày nghỉ phép đã duyệt
  * Đơn giá theo giờ (`HourlyRate` - VNĐ)
  * Tổng tiền lương (VNĐ)
  * Trạng thái & Cảnh báo (Badge có tooltip giải thích chi tiết)
  * Thao tác: Nút xem chi tiết chấm công từng ca của nhân viên trong kỳ.

### 7.3. Màn hình Phiếu lương Cá nhân (`/Payroll/MyPayslips`)
- Dành riêng cho mọi nhân viên.
- Hiển thị danh sách các kỳ lương đã được duyệt/chốt (`Locked`).
- Xem chi tiết: Tổng giờ làm, Mức lương theo giờ, Tổng thu nhập, Số ngày nghỉ phép và chi tiết từng ngày công trong kỳ.

### 7.4. Menu Điều hướng (`_NavigationBarPartial.cshtml`)
- Đối với Admin, HR, StoreManager: Thêm mục **"Bảng lương"** (`Payroll/Index`, icon `bi bi-cash-stack`).
- Đối với Employee: Thêm mục **"Phiếu lương của tôi"** (`Payroll/MyPayslips`, icon `bi bi-receipt`).

---

## 8. ĐẶC TẢ XUẤT EXCEL BẰNG CLOSEDXML

Xuất file Excel `.xlsx` chuyên nghiệp theo chuẩn báo cáo tài chính của chuỗi bán lẻ:
- **Tên Sheet**: `BangLuong_Thang_MM_YYYY`
- **Tiêu đề báo cáo**:
  * Tên công ty: `RWPM - HỆ THỐNG QUẢN LÝ NHÂN SỰ & CHẤM CÔNG CHUỖI BÁN LẺ`
  * Tiêu đề chính: `BẢNG LƯƠNG NHÂN VIÊN THÁNG MM/YYYY`
  * Ngày xuất & Người xuất
- **Bảng cột dữ liệu**:
  1. STT
  2. Mã nhân viên
  3. Họ và tên
  4. Chi nhánh
  5. Hình thức làm việc
  6. Giờ phân ca theo lịch
  7. Giờ thực tế tính lương
  8. Nghỉ phép đã duyệt (ngày)
  9. Đơn giá theo giờ (VNĐ)
  10. Tổng tiền lương (VNĐ)
  11. Trạng thái / Cảnh báo
- **Styling**:
  * Header màu xanh thương hiệu (`#0d6efd`), chữ trắng, in đậm.
  * Cột tiền định dạng: `#,##0`.
  * Cột giờ định dạng: `0.00`.
  * Dòng Tổng cộng (Total Row): In đậm, nền xám nhạt, tính tổng giờ và tổng tiền lương.
  * Tự động căn chỉnh độ rộng cột (`worksheet.Columns().AdjustToContents()`).

---

## 9. KỊCH BẢN KIỂM THỬ NGHIỆP VỤ (TEST SCENARIOS)

| STT | Kịch bản kiểm thử | Hành vi mong đợi |
| :---: | :--- | :--- |
| **TC1** | Tính lương kỳ có nhân viên đầy đủ giờ công và đã có `HourlyRate` (Full-time & Part-time) | Tính chính xác: $\text{Giờ làm} \times \text{HourlyRate}$. Không sử dụng `BaseSalary`. |
| **TC2** | Nhân viên chưa có `HourlyRate` (`HourlyRate == null` hoặc `<= 0`) | Không gán mặc định. Tiền lương = 0, trạng thái `MissingRate`, hiển thị cảnh báo đỏ trên UI và Excel. |
| **TC3** | Nhân viên có ca chấm công thiếu giờ Check-in hoặc Check-out | Không tự suy đoán giờ. Ca đó bị bỏ qua trong tổng giờ, tăng `MissingAttendanceCount`, hiển thị cảnh báo để HR điều chỉnh. |
| **TC4** | Ca chấm công đã được HR điều chỉnh (`IsAdjusted == true`) | Tính đủ giờ làm thực tế theo `ShiftTimeHelper` mà không bị áp luật phạt tự động đi muộn > 60 phút. |
| **TC5** | Tính lương lại nhiều lần khi kỳ đang ở trạng thái `Draft` | Hệ thống cập nhật đè dữ liệu mới nhất, không sinh bản ghi trùng lặp. |
| **TC6** | Chốt bảng lương (`Status = Locked`) và kiểm tra tính bất biến | Bảng lương chuyển sang `Locked`. Sau đó sửa `HourlyRate` của nhân viên hay sửa chấm công trong kỳ thì số liệu trong bảng lương đã chốt **không hề thay đổi**. |
| **TC7** | Cố tình tính lại bảng lương khi đã `Locked` | Server ném lỗi ngăn chặn, thông báo kỳ lương đã bị khóa. |
| **TC8** | StoreManager đăng nhập | Chỉ xem được dữ liệu nhân viên thuộc Store mình quản lý; nút Chốt lương bị ẩn/chặn quyền. |
| **TC9** | Employee đăng nhập | Không thể vào `/Payroll/Index`. Vào `/Payroll/MyPayslips` chỉ thấy các kỳ đã `Locked` của chính mình. |
| **TC10**| Xuất Excel | Tải về file `.xlsx` định dạng chuẩn, số liệu khớp chính xác 100% với giao diện web. |

---

## 10. DANH MỤC FILE TRIỂN KHAI

### Các file thêm mới:
1. `RWPM/Models/Entities/Payroll.cs`
2. `RWPM/Models/Entities/PayrollDetail.cs`
3. `RWPM/Common/Enums/PayrollStatus.cs`
4. `RWPM/Common/Enums/PayrollDetailStatus.cs`
5. `RWPM/Models/ViewModels/Payroll/PayrollListVM.cs`
6. `RWPM/Models/ViewModels/Payroll/PayrollDetailVM.cs`
7. `RWPM/Models/ViewModels/Payroll/EmployeePayslipVM.cs`
8. `RWPM/Services/Abstraction/IPayrollService.cs`
9. `RWPM/Services/Implementation/PayrollService.cs`
10. `RWPM/Controllers/PayrollController.cs`
11. `RWPM/Views/Payroll/Index.cshtml`
12. `RWPM/Views/Payroll/Detail.cshtml`
13. `RWPM/Views/Payroll/EmployeeDetail.cshtml`
14. `RWPM/Views/Payroll/MyPayslips.cshtml`

### Các file cập nhật:
1. `RWPM/Infrastructure/Data/DefaultDatabaseContext.cs` (Khai báo DbSet, quan hệ, chỉ mục unique)
2. `RWPM/Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs` (Đăng ký IPayrollService)
3. `RWPM/Views/Shared/_NavigationBarPartial.cshtml` (Thêm menu Bảng lương & Phiếu lương)
4. EF Core Migration tạo 2 bảng `Payroll` và `PayrollDetail`.
