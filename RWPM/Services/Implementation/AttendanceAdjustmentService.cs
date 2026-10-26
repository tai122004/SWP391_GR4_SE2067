using Microsoft.EntityFrameworkCore;
using RWPM.Common.Enums;
using RWPM.Common.Validation;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.ShiftRegistration;
using RWPM.Resources.Shared;
using RWPM.Services.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RWPM.Services.Implementation
{
    public class AttendanceAdjustmentService : IAttendanceAdjustmentService
    {
        // Persisted to the DB as-is (Vietnamese); translated at display time in RequestController.
        public const string DefaultRejectionReason = "Quản lý từ chối yêu cầu.";

        // Persisted to AttendanceAdjustmentHistory.Reason as-is (Vietnamese); translated at display time by LocalizeAuditReason.
        public const string StoredApprovePrefix = "[Duyệt điều chỉnh] Lý do NV: ";
        public const string StoredManagerNoteSeparator = " | Quản lý ghi chú: ";

        public static string LocalizeAuditReason(string? reason)
        {
            if (string.IsNullOrEmpty(reason) || !reason.StartsWith(StoredApprovePrefix, StringComparison.Ordinal)) return reason ?? string.Empty;
            return InputRules.Text(StoredApprovePrefix, "[Adjustment approved] Employee reason: ")
                + reason.Substring(StoredApprovePrefix.Length)
                    .Replace(StoredManagerNoteSeparator, InputRules.Text(StoredManagerNoteSeparator, " | Manager note: "));
        }

        private readonly DefaultDatabaseContext _context;

        public AttendanceAdjustmentService(DefaultDatabaseContext context)
        {
            _context = context;
        }

        public async Task<AttendanceAdjustmentRequest> CreateRequestAsync(AttendanceAdjustmentCreateVM model, string username)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model), RequestResource.Adj_ErrInvalidData);
            }

            // Yêu cầu điều chỉnh chỉ được tạo cho chính nhân viên đang đăng nhập (không tin EmployeeId từ client)
            var employee = await _context.Employee
                .Include(e => e.Store)
                .FirstOrDefaultAsync(e => e.Username == username);

            if (employee == null)
            {
                throw new Exception(RequestResource.Adj_ErrEmployeeNotFound);
            }
            await WorkflowGuard.EmployeeAsync(_context, username, employee.EmployeeId);
            var reason = InputRules.Reason(model.Reason, 500);
            var requestedIn = AttendanceInputRules.ParseClockTime(model.RequestedCheckInTime, "RequestedCheckInTime");
            var requestedOut = AttendanceInputRules.ParseClockTime(model.RequestedCheckOutTime, "RequestedCheckOutTime");
            InputRules.Require(requestedIn.HasValue || requestedOut.HasValue, "RequestedCheckInTime",
                "Cần nhập ít nhất một giờ cần điều chỉnh.", "Enter at least one time to adjust.");
            InputRules.Require(!model.AttendanceRecordId.HasValue || model.AttendanceRecordId.Value > 0, "AttendanceRecordId",
                "Mã bản ghi chấm công không hợp lệ.", "Invalid attendance record ID.");

            var targetWorkDate = model.WorkDate?.Date ?? DateTime.Today;

            InputRules.Require(!string.IsNullOrWhiteSpace(model.Reason), "Reason",
                "Vui lòng nhập lý do điều chỉnh.", "Please enter the adjustment reason.");
            InputRules.Require(targetWorkDate.Date <= DateTime.Today, "WorkDate",
                "Không thể yêu cầu điều chỉnh chấm công cho ngày trong tương lai.", "Cannot request an attendance adjustment for a future date.");
            InputRules.Require(string.IsNullOrWhiteSpace(model.RequestedCheckInTime) || model.ParsedRequestedCheckInTime.HasValue,
                "RequestedCheckInTime", "Giờ vào không đúng định dạng.", "Check-in time has an invalid format.");
            InputRules.Require(string.IsNullOrWhiteSpace(model.RequestedCheckOutTime) || model.ParsedRequestedCheckOutTime.HasValue,
                "RequestedCheckOutTime", "Giờ ra không đúng định dạng.", "Check-out time has an invalid format.");
            InputRules.Require(model.ParsedRequestedCheckInTime.HasValue || model.ParsedRequestedCheckOutTime.HasValue,
                "RequestedCheckInTime", "Vui lòng nhập giờ vào hoặc giờ ra cần điều chỉnh.", "Please enter the check-in or check-out time to adjust.");

            AttendanceRecord? record = null;
            if (model.AttendanceRecordId.HasValue)
            {
                record = await _context.AttendanceRecord
                    .Include(r => r.Shift)
                    .FirstOrDefaultAsync(r => r.AttendanceId == model.AttendanceRecordId.Value);

                InputRules.Require(record != null, "AttendanceRecordId", "Không tìm thấy bản ghi chấm công.", "Attendance record not found.");
                InputRules.Require(record!.Username == employee.Username, "AttendanceRecordId",
                    "Bản ghi công không thuộc nhân viên đã chọn.", "Attendance record does not belong to the selected employee.");
                InputRules.Require(!model.WorkDate.HasValue || model.WorkDate.Value.Date == record.Date.Date,
                    "WorkDate", "Ngày làm việc không khớp bản ghi công.", "Work date does not match the attendance record.");
                InputRules.Require(!model.ShiftId.HasValue || model.ShiftId == record.ShiftId,
                    "ShiftId", "Ca làm không khớp bản ghi công.", "Shift does not match the attendance record.");
                targetWorkDate = record.Date.Date;
            }
            else
            {
                InputRules.Require(model.WorkDate.HasValue && model.ShiftId is > 0, "WorkDate",
                    "Cần chọn ngày và ca đã được duyệt.", "Select a date and an approved shift.");
                // Tìm kiếm bản ghi chấm công tương ứng theo ngày và ca nếu có
                var query = _context.AttendanceRecord
                    .Include(r => r.Shift)
                    .Where(r => r.Username == employee.Username && r.Date == targetWorkDate.Date);

                if (model.ShiftId.HasValue)
                {
                    query = query.Where(r => r.ShiftId == model.ShiftId.Value);
                }

                record = await query.OrderByDescending(r => r.AttendanceId).FirstOrDefaultAsync();
            }

            InputRules.Require(targetWorkDate <= DateTime.Today, "WorkDate", "Không thể điều chỉnh công của ngày tương lai.",
                "Cannot adjust attendance for a future date.");
            await AttendanceInputRules.EnsureUnlockedAsync(_context, targetWorkDate);
            var shiftId = record?.ShiftId ?? model.ShiftId;
            var registration = await _context.ShiftRegistration.Include(x => x.Shift)
                .FirstOrDefaultAsync(x => x.EmployeeId == employee.EmployeeId && x.WorkDate == targetWorkDate
                    && x.ShiftId == shiftId && x.Status == RegistrationStatus.Approved);
            InputRules.Require(record != null || registration != null, "ShiftId",
                "Không tìm thấy ca đã duyệt cho ngày làm việc.", "No approved shift exists for this work date.");
            var effectiveIn = requestedIn ?? record?.CheckInTime;
            var effectiveOut = requestedOut ?? record?.CheckOutTime;
            AttendanceInputRules.ValidateTimeOrder(targetWorkDate, effectiveIn, effectiveOut,
                record?.Shift?.EndDayOffset ?? registration?.Shift?.EndDayOffset ?? 0);
            InputRules.Require(record == null || effectiveIn != record.CheckInTime || effectiveOut != record.CheckOutTime,
                "RequestedCheckInTime", "Giờ yêu cầu phải khác giờ hiện tại.", "Requested time must differ from the current time.");

            // Kiểm tra xem đã có yêu cầu nào đang chờ duyệt cho bản ghi / ca này chưa
            if (record != null)
            {
                var hasPending = await _context.AttendanceAdjustmentRequest
                    .AnyAsync(r => r.AttendanceRecordId == record.AttendanceId && r.Status == RegistrationStatus.Pending);

                if (hasPending)
                {
                    throw new Exception(RequestResource.Adj_ErrPendingForShift);
                }
            }
            else
            {
                var hasPending = await _context.AttendanceAdjustmentRequest
                    .AnyAsync(r => r.EmployeeId == employee.EmployeeId && r.WorkDate == targetWorkDate.Date && r.ShiftId == model.ShiftId && r.Status == RegistrationStatus.Pending);

                if (hasPending)
                {
                    throw new Exception(RequestResource.Adj_ErrPendingForShiftDate);
                }
            }

            var requestWorkDate = record != null ? record.Date.Date : targetWorkDate.Date;
            var requestShiftId = record != null ? record.ShiftId : model.ShiftId;

            await EnsurePeriodUnlockedAsync(_context, requestWorkDate);

            var shift = record?.Shift;
            if (shift == null && requestShiftId.HasValue)
            {
                shift = await _context.Shift.FindAsync(requestShiftId.Value);
            }
            ValidateTimeOrder(model.ParsedRequestedCheckInTime ?? record?.CheckInTime,
                model.ParsedRequestedCheckOutTime ?? record?.CheckOutTime, shift);

            // Gắn yêu cầu vào cửa hàng nơi làm ca (đăng ký ca đã duyệt), nếu không có thì dùng cửa hàng gốc của nhân viên
            int? registrationStoreId = null;
            if (requestShiftId.HasValue)
            {
                registrationStoreId = await _context.ShiftRegistration
                    .Where(sr => sr.EmployeeId == employee.EmployeeId && sr.WorkDate == requestWorkDate
                        && sr.ShiftId == requestShiftId.Value && sr.Status == RegistrationStatus.Approved)
                    .Select(sr => (int?)sr.StoreId)
                    .FirstOrDefaultAsync();
            }
            var storeId = registrationStoreId ?? employee.StoreId;
            InputRules.Require(storeId.HasValue && storeId.Value > 0, "StoreId",
                "Không xác định được cửa hàng của ca làm việc. Vui lòng liên hệ quản lý.",
                "Cannot determine the store for this shift. Please contact your manager.");

            var request = new AttendanceAdjustmentRequest
            {
                AttendanceRecordId = record?.AttendanceId,
                EmployeeId = employee.EmployeeId,
                StoreId = storeId!.Value,
                WorkDate = requestWorkDate,
                ShiftId = requestShiftId,
                CurrentCheckInTime = record?.CheckInTime,
                CurrentCheckOutTime = record?.CheckOutTime,
                RequestedCheckInTime = requestedIn,
                RequestedCheckOutTime = requestedOut,
                Reason = reason,
                Status = RegistrationStatus.Pending,
                CreatedBy = username,
                CreatedDate = DateTime.Now
            };

            _context.AttendanceAdjustmentRequest.Add(request);
            await _context.SaveChangesAsync();

            return request;
        }

        public async Task<List<AttendanceAdjustmentRequest>> GetRequestsAsync(DateTime? startDate, DateTime? endDate, int? storeId, RegistrationStatus? status)
        {
            var query = _context.AttendanceAdjustmentRequest
                .Include(r => r.Employee)
                    .ThenInclude(e => e.Account)
                .Include(r => r.Store)
                .Include(r => r.Shift)
                .Include(r => r.AttendanceRecord)
                .AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(r => r.WorkDate >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                query = query.Where(r => r.WorkDate <= endDate.Value.Date);
            }

            if (storeId.HasValue && storeId.Value > 0)
            {
                query = query.Where(r => r.StoreId == storeId.Value);
            }

            if (status.HasValue)
            {
                query = query.Where(r => r.Status == status.Value);
            }

            return await query.OrderByDescending(r => r.CreatedDate).ToListAsync();
        }

        public async Task<AttendanceAdjustmentRequest?> GetByIdAsync(int id)
        {
            return await _context.AttendanceAdjustmentRequest
                .Include(r => r.Employee)
                    .ThenInclude(e => e.Account)
                .Include(r => r.Store)
                .Include(r => r.Shift)
                .Include(r => r.AttendanceRecord)
                .FirstOrDefaultAsync(r => r.AttendanceAdjustmentRequestId == id);
        }

        public async Task ApproveAsync(int id, string approverUsername, TimeSpan? finalCheckInTime = null, TimeSpan? finalCheckOutTime = null, string? approverNote = null)
        {
            await using var transaction = _context.Database.CurrentTransaction == null
                ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable) : null;
            var request = await _context.AttendanceAdjustmentRequest
                .Include(r => r.Employee)
                .Include(r => r.AttendanceRecord)
                .FirstOrDefaultAsync(r => r.AttendanceAdjustmentRequestId == id);

            if (request == null)
            {
                throw new Exception(RequestResource.Adj_ErrNotFound);
            }

            if (request.Status != RegistrationStatus.Pending)
            {
                throw new Exception(RequestResource.Adj_ErrAlreadyProcessed);
            }
            TimeSpan? approvedIn = finalCheckInTime ?? request.RequestedCheckInTime ?? request.AttendanceRecord?.CheckInTime;
            TimeSpan? approvedOut = finalCheckOutTime ?? request.RequestedCheckOutTime ?? request.AttendanceRecord?.CheckOutTime;
            await AttendanceInputRules.EnsureUnlockedAsync(_context, request.WorkDate);
            AttendanceInputRules.ValidateClockTime(finalCheckInTime, "ApprovedCheckInTime");
            AttendanceInputRules.ValidateClockTime(finalCheckOutTime, "ApprovedCheckOutTime");

            await EnsurePeriodUnlockedAsync(_context, request.WorkDate.Date);

            AttendanceRecord? record = request.AttendanceRecord;

            if (record == null && request.AttendanceRecordId.HasValue)
            {
                record = await _context.AttendanceRecord
                    .FirstOrDefaultAsync(r => r.AttendanceId == request.AttendanceRecordId.Value);
                InputRules.Require(record != null, "AttendanceRecordId", "Bản ghi công của đơn không còn tồn tại.",
                    "Attendance record for this request no longer exists.");
            }
            if (record == null)
                record = await _context.AttendanceRecord.FirstOrDefaultAsync(r => r.Username == request.Employee.Username
                    && r.Date == request.WorkDate && r.ShiftId == request.ShiftId);
            if (record == null)
            {
                // Trong lúc chờ duyệt, nhân viên có thể đã chấm công cho cùng ngày/ca -> dùng lại bản ghi đó thay vì tạo trùng
                record = await _context.AttendanceRecord
                    .Where(r => r.Username == request.Employee.Username && r.Date == request.WorkDate.Date && r.ShiftId == request.ShiftId)
                    .OrderByDescending(r => r.AttendanceId)
                    .FirstOrDefaultAsync();
                if (record != null)
                {
                    request.AttendanceRecordId = record.AttendanceId;
                }
            }

            approvedIn ??= record?.CheckInTime;
            approvedOut ??= record?.CheckOutTime;
            InputRules.Require(approvedIn.HasValue || approvedOut.HasValue, "ApprovedCheckInTime",
                "Cần ít nhất một giờ được duyệt.", "At least one approved time is required.");
            var shift = request.ShiftId.HasValue ? await _context.Shift.FindAsync(request.ShiftId.Value) : null;
            AttendanceInputRules.ValidateTimeOrder(request.WorkDate, approvedIn, approvedOut, shift?.EndDayOffset ?? 0);
            InputRules.Require(record == null || approvedIn != record.CheckInTime || approvedOut != record.CheckOutTime,
                "ApprovedCheckInTime", "Giờ được duyệt phải khác giờ công hiện tại.",
                "Approved time must differ from current attendance.");

            // Lấy giờ thực tế do người phê duyệt quyết định (nếu không truyền thì lấy giờ nhân viên yêu cầu,
            // trường nào nhân viên không yêu cầu thì giữ nguyên giờ hiện tại của bản ghi)
            InputRules.Require(approvedIn.HasValue || approvedOut.HasValue, "ApprovedCheckInTime",
                "Vui lòng nhập giờ vào hoặc giờ ra được duyệt.", "Please enter the approved check-in or check-out time.");
            ValidateTimeOrder(approvedIn, approvedOut, shift);

            string auditReason = !string.IsNullOrWhiteSpace(approverNote)
                ? $"{StoredApprovePrefix}{request.Reason}{StoredManagerNoteSeparator}{approverNote.Trim()}"
                : $"{StoredApprovePrefix}{request.Reason}";

            if (record != null)
            {
                request.AttendanceRecordId = record.AttendanceId;
                // Ghi nhận lịch sử điều chỉnh
                var history = new AttendanceAdjustmentHistory
                {
                    AttendanceRecordId = record.AttendanceId,
                    OldCheckInTime = record.CheckInTime,
                    OldCheckOutTime = record.CheckOutTime,
                    NewCheckInTime = approvedIn,
                    NewCheckOutTime = approvedOut,
                    Reason = auditReason,
                    ModifiedBy = approverUsername,
                    ModifiedAt = DateTime.Now
                };
                _context.AttendanceAdjustmentHistory.Add(history);

                // Cập nhật giờ công
                record.CheckInTime = approvedIn;
                record.CheckOutTime = approvedOut;
                record.IsAdjusted = true;
            }
            else
            {
                // Trường hợp trước đó nhân viên quên bấm giờ hoàn toàn
                var newRecord = new AttendanceRecord
                {
                    Username = request.Employee.Username,
                    Date = request.WorkDate,
                    ShiftId = request.ShiftId,
                    CheckInTime = approvedIn,
                    CheckOutTime = approvedOut,
                    IsAdjusted = true
                };
                _context.AttendanceRecord.Add(newRecord);
                await _context.SaveChangesAsync();

                request.AttendanceRecordId = newRecord.AttendanceId;

                var history = new AttendanceAdjustmentHistory
                {
                    AttendanceRecordId = newRecord.AttendanceId,
                    OldCheckInTime = null,
                    OldCheckOutTime = null,
                    NewCheckInTime = approvedIn,
                    NewCheckOutTime = approvedOut,
                    Reason = auditReason,
                    ModifiedBy = approverUsername,
                    ModifiedAt = DateTime.Now
                };
                _context.AttendanceAdjustmentHistory.Add(history);
            }

            // Đánh dấu yêu cầu là Đã duyệt
            request.Status = RegistrationStatus.Approved;
            request.ApprovedBy = approverUsername;
            request.ApprovedAt = DateTime.Now;
            request.UpdatedBy = approverUsername;
            request.UpdatedDate = DateTime.Now;

            await _context.SaveChangesAsync();
            if (transaction != null) await transaction.CommitAsync();
        }

        // Chặn thay đổi chấm công thuộc kỳ lương đã chốt
        internal static async Task EnsurePeriodUnlockedAsync(DefaultDatabaseContext context, DateTime date)
        {
            var day = date.Date;
            InputRules.Require(!await context.Payroll.AnyAsync(p => p.Status == PayrollStatus.Locked && p.StartDate <= day && p.EndDate >= day),
                "WorkDate", "Ngày này thuộc kỳ lương đã chốt. Cần mở khóa kỳ lương trước khi thay đổi chấm công.",
                "This date belongs to a locked payroll. Unlock the payroll before changing attendance.");
        }

        // Giờ ra phải sau giờ vào, trừ ca qua đêm (giờ ra có thể sang ngày hôm sau)
        private static void ValidateTimeOrder(TimeSpan? checkIn, TimeSpan? checkOut, RWPM.Models.Entities.Shift? shift)
        {
            if (!checkIn.HasValue || !checkOut.HasValue) return;
            bool overnight = shift != null && shift.EndDayOffset > 0;
            InputRules.Require(overnight || checkOut.Value > checkIn.Value, "RequestedCheckOutTime",
                "Giờ ra phải sau giờ vào.", "Check-out time must be after check-in time.");
        }

        public async Task RejectAsync(int id, string reason, string rejectorUsername)
        {
            var request = await _context.AttendanceAdjustmentRequest
                .FirstOrDefaultAsync(r => r.AttendanceAdjustmentRequestId == id);

            if (request == null)
            {
                throw new Exception(RequestResource.Adj_ErrNotFound);
            }

            if (request.Status != RegistrationStatus.Pending)
            {
                throw new Exception(RequestResource.Adj_ErrAlreadyProcessed);
            }

            request.Status = RegistrationStatus.Rejected;
            request.RejectionReason = string.IsNullOrWhiteSpace(reason) ? DefaultRejectionReason : reason.Trim();
            request.ApprovedBy = rejectorUsername;
            request.ApprovedAt = DateTime.Now;
            request.UpdatedBy = rejectorUsername;
            request.UpdatedDate = DateTime.Now;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id, string username)
        {
            var request = await _context.AttendanceAdjustmentRequest
                .Include(r => r.Employee)
                .FirstOrDefaultAsync(r => r.AttendanceAdjustmentRequestId == id);

            if (request == null)
            {
                throw new Exception(RequestResource.Adj_ErrNotFound);
            }

            if (request.Status != RegistrationStatus.Pending)
            {
                throw new Exception(RequestResource.Adj_ErrDeleteOnlyPending);
            }

            if (request.Employee.Username != username && request.CreatedBy != username)
            {
                throw new Exception(RequestResource.Adj_ErrNoPermissionDelete);
            }

            _context.AttendanceAdjustmentRequest.Remove(request);
            await _context.SaveChangesAsync();
        }
    }
}
